using System;
using System.Linq;

namespace DesktopPet;

// The favourite toy (docs/LIVING-CORNER.md → M4). C# counts the rounds the user plays with the yarn ball,
// the feather wand and the wind-up mouse (toy_plays); Python keeps the scores and sends the favourite. C#
// then opens three things and owns their timing and walking: a visit to the favourite every 10–15 minutes
// (a sniff or a paw, then sitting by it), an invitation with it on the shared 8–15 minute timer (the ball as
// before; by the wand or the mouse it sits, looks at the user and pats it for 20 seconds), and, when two toys
// lie within 250 DIPs, a stop between them to look at each before going to the favourite (a random one
// without a favourite). The toys never move. All are the cat's own doing: Quiet, Focus, a fullscreen app,
// the battle, Pause, being busy or another feature stop them, and the user's own play wins at once. Without
// Python nothing changes (the invitation brings the ball, no choice).
public sealed partial class PetWindow
{
    private enum Favorite { None, WeighWalk, Weigh, Walk, Greet, Sit, Invite }
    private enum FavoriteGoal { Visit, Invite, Sniff }
    private Favorite favorite;
    private FavoriteGoal favoriteGoal;
    private string? favoriteToy;
    private DesktopThing? favoriteTarget, favoriteOther;
    private double favoriteTime, favoriteVisitDue = -1, favoriteWeighX, favoritePatAt;
    private int favoriteFacing = 1;
    private bool favoritePaw, favoritePawed;
    private long playsBall, playsWand, playsMouse;

    public string? FavoriteToy => favoriteToy;
    public string FavoritePhase => favorite.ToString();
    public string? FavoriteTargetId => favoriteTarget?.Id;
    public AI.ToyPlayReport ToyPlays => new(playsBall, playsWand, playsMouse);

    // Python is answering (it always sends the bond milestone): the choice between two toys is open.
    private bool AiKnowsToys => bondLevel > 0;
    // The cat is only visiting or inviting: the user's own play takes over (TryStartChase, TryStartPass, BallThrown).
    private bool FavoriteYields => favorite != Favorite.None;

    // A round the user started with this toy (a fetch or passing streak, a chase).
    private void CountToyPlay(string id)
    {
        if (id == DesktopToyRules.Ball) playsBall++;
        else if (id == DesktopToyRules.Wand) playsWand++;
        else if (id == DesktopToyRules.Mouse) playsMouse++;
    }

    // From each decision: the favourite, or none (Python forgot it, or has not chosen yet).
    private void ApplyFavoriteDecision(AI.AiDecision decision)
    {
        favoriteToy = FavoriteToyRules.IsPlayToy(decision.FavoriteToy) ? decision.FavoriteToy : null;
        if (favoriteToy == null && favoriteGoal == FavoriteGoal.Visit) StopFavorite();
    }

    private string PlayToyName(string id) => id switch
    {
        DesktopToyRules.Wand => L("Cần câu lông vũ", "Feather wand"),
        DesktopToyRules.Mouse => L("Chuột dây cót", "Wind-up mouse"),
        _ => L("Bóng len", "Yarn ball")
    };

    // Python off, failing or just stopped (from ForgetBond).
    private void ForgetFavorite()
    {
        favoriteToy = null;
        StopFavorite();
    }

    // Lying out, still, on the cat's monitor, nobody holding it: a toy the cat may go to.
    private bool PlayToyFree(DesktopThing toy) => FavoriteToyRules.IsPlayToy(toy.Id) && desktopToys.Contains(toy) && ToysMayShow
        && toy.Window?.IsVisible == true && !toy.Held && !toy.Press && !toy.Carried && !toy.InGame && toy.Motion.AtRest && toy.Glide == null
        && toy.Monitor == CurrentMonitor().DeviceName && !(toy.Id == DesktopToyRules.Ball && toyWindow != null);

    // The nearest other free toy within reach of this one (for the sniff, only toys never sniffed).
    private DesktopThing? FavoriteNeighbour(DesktopThing toy, bool unsniffed) => desktopToys
        .Where(t => t != toy && PlayToyFree(t) && (!unsniffed || !Sniffed(t.Id)) && Math.Abs(t.Motion.X - toy.Motion.X) <= FavoriteToyRules.ChoiceReach)
        .OrderBy(t => Math.Abs(t.Motion.X - toy.Motion.X)).FirstOrDefault();

    // Where the cat's left edge stands beside a toy (the side it comes from, else the other).
    private double? FavoriteStand(DesktopThing toy)
    {
        double half = settings.Width / 2, off = toy.HalfWidth + FavoriteToyRules.Reach; // As by the plant pot.
        int side = CatCenter < toy.Motion.X ? -1 : 1;
        foreach (int s in new[] { side, -side })
            if (CatCanStand(toy.Motion.X + s * off - half) is double left) return left;
        return null;
    }

    private bool FavoriteCatFree => CatOnFloor && pet.Action is PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking;

    private void StartFavorite(FavoriteGoal goal, DesktopThing target, DesktopThing? other)
    {
        favoriteGoal = goal;
        favoriteTarget = target; favoriteOther = other;
        favoriteTime = 0;
        favoritePawed = false;
        if (other != null)
        {
            double middle = (target.Motion.X + other.Motion.X) / 2 - settings.Width / 2;
            favoriteWeighX = Math.Clamp(middle, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            favorite = Favorite.WeighWalk;
        }
        else favorite = Favorite.Walk;
        System.Diagnostics.Trace.WriteLine($"Favourite toy: the cat goes to the {target.Id} ({goal}{(other != null ? ", choosing" : "")}).");
    }

    // For checks (and the timer): visit the favourite toy now. False when it may not.
    public bool GoVisitFavorite()
    {
        if (favorite != Favorite.None || favoriteToy == null || !MayAct(AutoKind.FavoriteVisit) || !FavoriteCatFree
            || DesktopToy(favoriteToy) is not { } toy || !PlayToyFree(toy) || FavoriteStand(toy) == null) return false;
        StartFavorite(FavoriteGoal.Visit, toy, FavoriteNeighbour(toy, unsniffed: false));
        return true;
    }

    // The shared invitation timer (PetWindow.ToyFetch.cs): with Python, the favourite toy if it lies out,
    // and a choice when another toy lies close to it; without, the yarn ball exactly as before.
    public bool InviteToPlay()
    {
        if (!AiKnowsToys) return InviteToFetch();
        if (favorite != Favorite.None || fetch != Fetch.None || !MayAct(AutoKind.ToyInvite) || !FavoriteCatFree) return false;
        var target = favoriteToy != null && DesktopToy(favoriteToy) is { } liked && PlayToyFree(liked) ? liked
            : FetchBall is { } ball && PlayToyFree(ball) && FetchBallWithCat ? ball : null;
        if (target == null) return false;
        var other = FavoriteNeighbour(target, unsniffed: false);
        if (target.Id == DesktopToyRules.Ball && other == null) return InviteToFetch(); // As in M3.
        if (other == null && FavoriteStand(target) == null) return false;
        StartFavorite(FavoriteGoal.Invite, target, other);
        return true;
    }

    // From SniffNewToy (PetWindow.ToyReact.cs): two new toys close together, it weighs them first. False: no choice.
    private bool WeighNewToys(DesktopThing toy)
    {
        if (!AiKnowsToys || favorite != Favorite.None || !PlayToyFree(toy) || FavoriteNeighbour(toy, unsniffed: true) is not { } other) return false;
        StartFavorite(FavoriteGoal.Sniff, toy, other);
        return true;
    }

    // Every frame from TickDesktopToys: a battle or a hidden basket ends a trip whatever the cat is doing.
    private void GuardFavorite()
    {
        if (paused || favorite == Favorite.None) return;
        if (BattleActive || !ToysMayShow) StopFavorite();
    }

    private AutoKind FavoriteKind => favoriteGoal switch
    {
        FavoriteGoal.Visit => AutoKind.FavoriteVisit,
        FavoriteGoal.Invite => AutoKind.ToyInvite,
        _ => AutoKind.ToySniff
    };

    // From TickFeatures, next to the bond milestones. True while it owns the cat.
    private bool TickFavorite(double dt)
    {
        if (favorite == Favorite.None)
        {
            if (favoriteVisitDue < 0) favoriteVisitDue = FavoriteToyRules.NextVisit(featureRandom.NextDouble());
            if ((favoriteVisitDue -= dt) > 0) return false;
            favoriteVisitDue = FavoriteToyRules.NextVisit(featureRandom.NextDouble());
            if (!GoVisitFavorite()) return false;
        }
        // The cat's own idea: anything that would stop it from starting stops it now (not its own trip).
        if (!CanPlay || Blocking(PetAutonomy.BlockedBy(FavoriteKind) & ~AutoBlock.Feature) != AutoBlock.None
            || favoriteTarget is not { } target || !PlayToyFree(target) || (favoriteOther is { } o && favorite is Favorite.WeighWalk or Favorite.Weigh && !PlayToyFree(o)))
        { StopFavorite(); return false; }
        favoriteTime += dt;
        double t = favoriteTime;
        switch (favorite)
        {
            case Favorite.WeighWalk: // To the middle between the two toys.
                if (Math.Abs(favoriteWeighX - pet.X) >= 6 && t < FavoriteToyRules.WalkSeconds) { WalkToward(favoriteWeighX); return true; }
                favorite = Favorite.Weigh; favoriteTime = 0;
                HoldStill(PetAction.Looking, target.Motion.X < CatCenter ? -1 : 1);
                return true;
            case Favorite.Weigh: // A look at one, then at the other, then the pick.
                var other = favoriteOther!;
                var looked = t < FavoriteToyRules.LookSeconds ? target : other;
                HoldStill(PetAction.Looking, looked.Motion.X < CatCenter ? -1 : 1);
                pet.ReactTilt = 6 * pet.Facing * Math.Sin(Math.PI * Math.Clamp(t % FavoriteToyRules.LookSeconds / FavoriteToyRules.LookSeconds, 0, 1));
                if (t < 2 * FavoriteToyRules.LookSeconds) return true;
                pet.ReactTilt = 0;
                return ChooseBetween(target, other);
            case Favorite.Walk:
                if (FavoriteStand(target) is not double stand || t > FavoriteToyRules.WalkSeconds) { StopFavorite(); return false; }
                if (Math.Abs(stand - pet.X) >= 4) { WalkToward(stand); return true; }
                pet.X = stand;
                favoriteFacing = target.Motion.X < CatCenter ? -1 : 1;
                favoriteTime = 0; favoritePawed = false;
                if (favoriteGoal == FavoriteGoal.Visit)
                {
                    favorite = Favorite.Greet;
                    favoritePaw = featureRandom.NextDouble() < 0.5;
                }
                else { favorite = Favorite.Invite; favoritePatAt = FavoriteToyRules.PatAt; }
                HoldStill(PetAction.Sitting, favoriteFacing);
                System.Diagnostics.Trace.WriteLine($"Favourite toy: by the {target.Id} ({favorite}).");
                return true;
            case Favorite.Greet: // A sniff, nose down at it, or one paw at it (the toy does not move).
                HoldStill(PetAction.Sitting, favoriteFacing);
                double length = FavoriteToyRules.GreetSeconds;
                if (favoritePaw) { if (!favoritePawed && t >= 0.3) { favoritePawed = true; pet.BatRemaining = 0.3; } }
                else
                {
                    pet.PlayCrouch = Math.Min(1, t / 0.2) * Math.Min(1, (length - t) / 0.15 + 0.2);
                    pet.ReactDip = 9 * pet.PlayCrouch; pet.ReactTilt = 8 * favoriteFacing * pet.PlayCrouch;
                }
                if (t < length) return true;
                pet.PlayCrouch = pet.ReactDip = pet.ReactTilt = 0;
                favorite = Favorite.Sit; favoriteTime = 0;
                return true;
            case Favorite.Sit: // Sitting by it a while, looking at it now and then.
                HoldStill(PetAction.Sitting, favoriteFacing);
                pet.ReactTilt = 5 * favoriteFacing * Math.Max(0, Math.Sin(t * 1.6));
                if (t < FavoriteToyRules.SitSeconds) return true;
                StopFavorite();
                return false;
            case Favorite.Invite: // Looking at the user, patting the toy now and then: come and play.
                if (t >= favoritePatAt && t < favoritePatAt + 0.6)
                {
                    HoldStill(PetAction.Sitting, favoriteFacing);
                    if (!favoritePawed) { favoritePawed = true; pet.BatRemaining = 0.3; }
                }
                else
                {
                    if (t >= favoritePatAt + 0.6) { favoritePatAt += FavoriteToyRules.PatEvery; favoritePawed = false; }
                    HoldStill(PetAction.Looking, CursorPosition().X < CatCenter ? -1 : 1);
                }
                if (t < FavoriteToyRules.InviteSeconds) return true;
                System.Diagnostics.Trace.WriteLine($"Favourite toy: nobody came to play with the {target.Id}.");
                StopFavorite();
                return false;
        }
        return false;
    }

    // After looking at both: the favourite if it is one of them, else either.
    private bool ChooseBetween(DesktopThing first, DesktopThing second)
    {
        var pick = first.Id == favoriteToy ? first : second.Id == favoriteToy ? second : featureRandom.NextDouble() < 0.5 ? first : second;
        var goal = favoriteGoal;
        System.Diagnostics.Trace.WriteLine($"Favourite toy: between the {first.Id} and the {second.Id}, the cat picks the {pick.Id}.");
        switch (goal)
        {
            case FavoriteGoal.Sniff:
                StopFavorite();
                StartSniffWalk(pick); // PetWindow.ToyReact.cs
                return true;
            case FavoriteGoal.Invite when pick.Id == DesktopToyRules.Ball:
                StopFavorite();
                return InviteToFetch(); // The ball is brought to the user as in M3.
            default:
                favoriteTarget = pick; favoriteOther = null;
                favorite = Favorite.Walk; favoriteTime = 0;
                return true;
        }
    }

    private void StopFavorite()
    {
        if (favorite == Favorite.None) return;
        var was = favorite;
        favorite = Favorite.None;
        favoriteTarget = favoriteOther = null;
        favoriteTime = 0;
        pet.PlayCrouch = pet.ReactDip = pet.ReactTilt = 0;
        bool own = was is Favorite.WeighWalk or Favorite.Walk ? pet.Action == PetAction.Walking
            : pet.Action is PetAction.Sitting or PetAction.Looking;
        if (own && !dragging) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }
}
