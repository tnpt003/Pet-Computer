using System;

namespace DesktopPet;

// The scratching post (docs/LIVING-CORNER.md → M1). The cat walks to it on its own every 6–10 minutes
// (blocked like a nap in the cardboard box), and a scratch it picks anyway (PetBehavior's own or Python's
// SCRATCH) or the user's "Do now → Scratch" goes to the post when it is close enough. There the cat
// scratches with the gesture it already has, then stretches. Nothing new reaches the cat: the post never
// touches it, and needs, bond and learning do not change. The walk owns the cat's movement (like the box
// nap); the scratch and the stretch are PetBehavior's own manual poses, so a click, a drag or an emotion
// ends them as before.
public sealed partial class PetWindow
{
    private enum PostTrip { None, Walk, Scratch, Stretch }
    private PostTrip postTrip;
    private double postVisit = PostScratchRules.VisitMin, postTime;
    private int postSide = -1;

    public string PostTripPhase => postTrip.ToString();
    private DesktopThing? DesktopPost => DesktopToy(DesktopToyRules.Post);

    // The post is out, shown, still on the floor of the cat's monitor, nobody holds it, and the cat
    // stands on that floor with room beside the post.
    private bool PostReady => DesktopPost is { } post && ToysMayShow && post.Window?.IsVisible == true && !post.Held && !post.Press
        && post.Glide == null && post.Motion.AtRest && post.Monitor == CurrentMonitor().DeviceName
        && pet.Grounded && pet.LedgeId == 0 && PostStand(post) != null;
    // For "Do now": the post is out, and close enough to go to.
    public bool PostOut => DesktopPost != null && ToysMayShow;
    public bool PostNear => PostReady && Math.Abs(DesktopPost!.Motion.X - CatCenter) <= PostScratchRules.NearDistance;

    // Where the cat's left edge stands to scratch (from the side it comes from, else the other).
    private double? PostStand(DesktopThing post)
    {
        double half = settings.Width / 2;
        if (PostScratchRules.StandAt(post.Motion.X, CatCenter, bounds.Left + half, bounds.Right - half) is not double centre) return null;
        return CatCanStand(centre - half);
    }

    // For checks (and the timer): go and scratch the post now, as the cat's own visit. False when it may not.
    public bool GoScratchPost()
    {
        if (postTrip != PostTrip.None || !MayAct(AutoKind.PostScratch) || !behavior.SelfCareReady || !PostReady
            || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking)) return false;
        StartPostTrip(asked: false);
        return true;
    }

    // PetBehavior (or Python's SCRATCH) picked a scratch: with the post close enough it is scratched there.
    private bool ScratchOnPostInstead(PetModel _)
    {
        if (postTrip != PostTrip.None || !CanPlay || FeatureOwnsMovement() || BattleActive || !PostReady
            || Math.Abs(DesktopPost!.Motion.X - CatCenter) > PostScratchRules.CommandReach) return false;
        StartPostTrip(asked: false);
        return true;
    }

    // "Do now → Scratch" with the post near: TryStartCommand comes here instead of the gesture on the spot.
    private bool ScratchPostNow()
    {
        if (!PostNear) return false;
        CancelFeatureActivity();
        StartPostTrip(asked: true);
        return true;
    }

    private void StartPostTrip(bool asked)
    {
        postTrip = PostTrip.Walk;
        postTime = 0;
        if (asked) userAsked |= UserAsked.Scratch; else userAsked &= ~UserAsked.Scratch;
        System.Diagnostics.Trace.WriteLine(asked ? "The cat goes to the scratching post (asked)." : "The cat goes to the scratching post.");
    }

    // From TickFeatures, after the naps. True while it owns the cat this frame.
    private bool TickPostTrip(double dt)
    {
        postVisit -= dt;
        if (postTrip == PostTrip.None)
        {
            if (postVisit > 0) return false;
            postVisit = PostScratchRules.NextVisit(featureRandom.NextDouble());
            if (!GoScratchPost()) return false;
        }
        postTime += dt;
        var post = DesktopPost;
        // Picked up, put away, hidden or moved: the trip just ends.
        bool postOk = post != null && post.Window?.IsVisible == true && !post.Held && !post.Press && post.Motion.AtRest && post.Glide == null
            && post.Monitor == CurrentMonitor().DeviceName;
        if (!postOk || !CanPlay) { StopPostTrip(); return false; }
        if (postTrip == PostTrip.Walk)
        {
            if (postTime > PostScratchRules.WalkSeconds || PostStand(post!) is not double target) { StopPostTrip(); return false; }
            if (Math.Abs(target - pet.X) < 4)
            {
                pet.X = target;
                postSide = CatCenter < post!.Motion.X ? -1 : 1;
                // The scratching paws are drawn on the side away from the tail (PetRenderer.DrawGesturePaws):
                // that side goes to the post.
                HoldStill(PetAction.Idle, postSide);
                if (!behavior.StartManual(pet, PetAction.Scratching)) { StopPostTrip(); return false; }
                pet.Facing = postSide;
                postTrip = PostTrip.Scratch;
                postTime = 0;
                System.Diagnostics.Trace.WriteLine("The cat scratches the post.");
                return true;
            }
            WalkToward(target);
            return true;
        }
        if (postTrip == PostTrip.Scratch)
        {
            if (pet.Action == PetAction.Scratching) return true;
            // The scratch ran its length (not cut short by a click or an emotion): a stretch after it.
            if (postTime >= PetSettings.GestureDuration(PetAction.Scratching) - 0.1 && pet.Action == PetAction.Idle && behavior.StartManual(pet, PetAction.Stretching))
            {
                postTrip = PostTrip.Stretch;
                postTime = 0;
                return true;
            }
            StopPostTrip();
            return false;
        }
        if (pet.Action == PetAction.Stretching) return true;
        StopPostTrip();
        return false;
    }

    private void StopPostTrip()
    {
        if (postTrip == PostTrip.None) return;
        var was = postTrip;
        postTrip = PostTrip.None;
        userAsked &= ~UserAsked.Scratch;
        postVisit = Math.Max(postVisit, PostScratchRules.VisitMin);
        if (!dragging && (was == PostTrip.Walk && pet.Action == PetAction.Walking || was == PostTrip.Scratch && pet.Action == PetAction.Scratching
            || was == PostTrip.Stretch && pet.Action == PetAction.Stretching))
        { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    // The post's drawing: the rope shakes and fibres fall while the cat scratches it.
    private void DrawPostScratch(DesktopThing post)
    {
        if (post.Art is not ToyPostArt art) return;
        double time = postTrip == PostTrip.Scratch && pet.Action == PetAction.Scratching ? pet.ActionTime : -1;
        double swing = post.Motion.Feedback > 0 ? Math.Sin(post.Motion.Feedback * 30) * post.Motion.Feedback * 40 : 0;
        if (time < 0 && art.ScratchTime < 0 && swing == art.Swing) return;
        art.ScratchTime = time;
        art.ScratchSide = postSide;
        art.Swing = swing;
        art.InvalidateVisual();
    }
}
