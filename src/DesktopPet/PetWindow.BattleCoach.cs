using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode's lessons on the spot (2026-10-24, for players who do not read the guide): the first
// time something needs the user's hand — a slime winding up next to the cat, a stun, ads, an
// infected icon, malware, a charged app, low HP, the first slimes — the battle slows to CoachSlow
// and a click-through overlay rings the spot with a bouncing hand and a few words. It goes on as
// soon as the user does it, or after CoachTimeout s. Each lesson once (preferences.BattleLessonsSeen;
// How to play on the setup panel shows them all again). A stun is shown without slowing down.
public sealed partial class PetWindow
{
    public const string LessonLift = "lift", LessonStun = "stun", LessonAdsLocked = "ads_locked", LessonInfected = "infected", LessonAds = "ads",
        LessonLoading = "loading", LessonUpdate = "update", LessonVirus = "virus", LessonApp = "app", LessonHeal = "heal", LessonTarget = "target";
    // Most urgent first: only one lesson at a time.
    private static readonly string[] Lessons = { LessonLift, LessonStun, LessonAdsLocked, LessonInfected, LessonAds, LessonLoading, LessonUpdate, LessonVirus, LessonApp, LessonHeal, LessonTarget };
    public const double CoachSlow = 0.25, CoachTimeout = 6;

    private string? coachLesson;
    private double coachAge, coachStartedAt;
    // What the lesson points at (a slime, an icon, malware, an ad) and a number to compare with (clicks left).
    private int coachRef;
    private double coachRefValue;
    private Window? coachWindow;
    private CoachMark? coachMark;
    // Off in the window checks unless a check turns it on (they time the battle); on for the real pet.
    public bool BattleCoachEnabled { get; set; }
    public string? BattleCoachLesson => coachLesson;
    public bool BattleCoachVisible => coachWindow?.IsVisible == true;
    // The battle's time runs this much slower during a lesson.
    public double BattleCoachSlow => coachLesson is null or LessonStun ? 1 : CoachSlow;

    private bool LessonSeen(string id) => preferences.BattleLessonsSeen?.Contains(id) == true;

    // Every battle frame (real time), after the battle's step.
    private void TickBattleCoach(double dt)
    {
        if (battle == null || battle.Over || !BattleCoachEnabled) { EndCoach(false); return; }
        if (coachLesson != null)
        {
            if (battle.Paused || context.Fullscreen) { coachWindow?.Hide(); return; }
            coachAge += dt;
            if (coachAge >= CoachTimeout || LessonDone(coachLesson) || CoachTarget(coachLesson) is not Point at) { EndCoach(true); return; }
            PlaceCoach(at);
            return;
        }
        if (battle.Paused || battle.ChoosingUpgrade || context.Fullscreen || battleTutorialWindow != null) return;
        foreach (var id in Lessons)
            if (!LessonSeen(id) && LessonDue(id)) { StartCoach(id); return; }
    }

    // Whether a lesson's moment has come (and what it points at).
    private bool LessonDue(string id)
    {
        var b = battle!;
        double catX = pet.X + settings.Width / 2;
        switch (id)
        {
            case LessonLift:
                if (BattleHoldsOff || b.CatStunned) return false;
                var hitter = b.Slimes.FirstOrDefault(s => s.State is SlimeState.Windup && !s.Type.Support && Math.Abs(s.X - catX) <= s.Range + (s.Type.Dash ? s.Type.DashDistance : 40));
                coachRef = hitter?.Id ?? 0;
                return hitter != null;
            case LessonStun: return b.CatStunned;
            case LessonAdsLocked: return b.Popups.Count > 0 && b.PopupsLocked;
            case LessonInfected:
                var sick = b.Desktop.FirstOrDefault(a => a.Infected);
                coachRef = sick?.Id ?? 0;
                coachRefValue = sick?.ScrubbedAt ?? 0;
                return sick != null;
            case LessonAds:
                coachRef = b.Popups.Count > 0 && !b.PopupsLocked ? b.Popups[0].Id : 0;
                return coachRef != 0;
            case LessonLoading:
            case LessonUpdate:
                var kind = id == LessonLoading ? HazardKind.Loading : HazardKind.Update;
                var hazard = b.Hazards.FirstOrDefault(h => h.Kind == kind);
                coachRef = hazard?.Id ?? 0;
                coachRefValue = hazard?.ClicksLeft ?? 0;
                return hazard != null;
            case LessonVirus:
                coachRef = b.VirusFiles.Count > 0 ? b.VirusFiles[0].Id : 0;
                return coachRef != 0;
            case LessonApp:
                var ready = b.Desktop.FirstOrDefault(a => a.Ready);
                coachRef = ready?.Id ?? 0;
                return ready != null && !BattleHoldsOff;
            case LessonHeal: return b.CatHp < b.CatMaxHp * 0.6 && b.Cooldown(BattleSkill.Heal) <= 0 && !BattleHoldsOff;
            default:
                var near = b.Slimes.Where(s => s.Targetable).OrderBy(s => Math.Abs(s.X - catX)).FirstOrDefault();
                coachRef = near?.Id ?? 0;
                return near != null && b.SelectedId == null && b.Phase == BattlePhase.Fighting;
        }
    }

    // The user did what the lesson asked (or the thing it pointed at is gone).
    private bool LessonDone(string id)
    {
        var b = battle!;
        return id switch
        {
            LessonLift => BattleLifted || b.Find(coachRef) is not { State: SlimeState.Windup },
            LessonStun => !b.CatStunned,
            LessonAdsLocked => !b.PopupsLocked || b.Popups.Count == 0,
            LessonInfected => b.FindApp(coachRef) is not { Infected: true } app || app.ScrubbedAt > coachRefValue,
            LessonAds => b.FindPopup(coachRef) == null,
            LessonLoading => b.FindHazard(coachRef) == null || b.HoldingHazardId == coachRef,
            LessonUpdate => b.FindHazard(coachRef) is not { } wall || wall.ClicksLeft < coachRefValue,
            LessonVirus => b.VirusFiles.All(f => f.Id != coachRef),
            LessonApp => b.FindApp(coachRef) is not { } used || !used.Ready || used.UsedAt >= coachStartedAt,
            LessonHeal => b.Cooldown(BattleSkill.Heal) > 0,
            _ => b.SelectedId != null || b.Find(coachRef) is not { Targetable: true }
        };
    }

    // Where the lesson points (DIPs on the battle's monitor); null: nothing to point at any more.
    private Point? CoachTarget(string id)
    {
        var b = battle!;
        var area = battleArea;
        double floor = area.Bottom - 2;
        Point Cat() => new(pet.X + PetSettings.CenterX, pet.Y + PetSettings.ChestY);
        switch (id)
        {
            case LessonLift:
            case LessonStun:
                return Cat();
            case LessonAdsLocked:
                return b.Desktop.FirstOrDefault(a => a.Infected && a.Kind == BattleAppKind.Tabs) is DesktopApp tabs ? BattleAppCentre(tabs) : null;
            case LessonInfected:
            case LessonApp:
                return b.FindApp(coachRef) is DesktopApp app ? BattleAppCentre(app) : null;
            case LessonAds:
                return b.FindPopup(coachRef) is BattlePopup ad ? new Point(ad.X + BattleSim.PopupWidth / 2 - 10, floor - 2 - BattleSim.PopupHeight + 9) : null;
            case LessonLoading:
                return b.FindHazard(coachRef) is BattleHazard ring ? new Point(ring.X, floor - 132) : null;
            case LessonUpdate:
                return b.FindHazard(coachRef) is BattleHazard wall ? new Point(wall.X, floor - 15.5) : null;
            case LessonVirus:
                if (b.VirusFiles.FirstOrDefault(f => f.Id == coachRef) is not VirusFile file) return null;
                var cell = AppCellTopLeft(file.Col, file.Row);
                return new Point(cell.X + DesktopAppIcon.Centre.X, cell.Y + DesktopAppIcon.Centre.Y);
            case LessonHeal:
                if (battleHud == null || battleHudWindow?.Content is not Visual root) return null;
                var slot = battleHud.Slot(BattleSkill.Heal);
                var inHud = slot.TranslatePoint(new Point(slot.ActualWidth / 2, slot.ActualHeight / 2), (UIElement)root);
                return new Point(battleHudAt.X + inHud.X, battleHudAt.Y + inHud.Y);
            default:
                return b.Find(coachRef) is { Targetable: true } slime ? new Point(slime.X, floor - slime.Height / 2) : null;
        }
    }

    private (string Text, bool Hold) CoachText(string id) => id switch
    {
        LessonLift => (L("Kéo mèo lên!", "Lift the cat!"), true),
        LessonStun => (L("Mèo choáng, chờ chút", "Stunned: wait a moment"), false),
        LessonAdsLocked => (L("Gỡ virus khỏi Tab trước", "Clean the Tabs icon first"), false),
        LessonInfected => (LF($"Bấm {battle?.FindApp(coachRef)?.ScrubsLeft ?? 1} lần để gỡ virus", $"Click it {battle?.FindApp(coachRef)?.ScrubsLeft ?? 1}× to clean it"), false),
        LessonAds => (L("Bấm × để tắt", "Click × to close"), false),
        LessonLoading => (battle?.AppRules is { LoadingHold: > 0 } rules
            ? LF($"Giữ chuột {rules.LoadingHold.ToString("0.#", CultureInfo.InvariantCulture)} giây", $"Hold it {rules.LoadingHold.ToString("0.#", CultureInfo.InvariantCulture)} s")
            : L("Bấm để tắt", "Click to close"), battle?.AppRules is { LoadingHold: > 0 }),
        LessonUpdate => (L("Bấm Nhắc sau", "Click Later"), false),
        LessonVirus => (L("Bấm để xóa virus.exe", "Click to delete virus.exe"), false),
        LessonApp => (L("App đầy rồi: bấm để dùng", "Charged: click to use it"), false),
        LessonHeal => (L("Bấm Cá hồi máu", "Use the healing fish"), false),
        _ => (L("Bấm chọn slime để đánh", "Click a slime to attack it"), false)
    };

    private void StartCoach(string id)
    {
        coachLesson = id;
        coachAge = 0;
        coachStartedAt = battle!.Time;
        if (CoachTarget(id) is not Point at) { EndCoach(true); return; }
        var (text, hold) = CoachText(id);
        coachMark ??= new CoachMark();
        coachMark.Text = text;
        coachMark.Hold = hold;
        coachMark.Font = PetUi.TextFont;
        if (coachWindow == null)
        {
            coachWindow = CreateOverlayWindow(coachMark, "Pet coach", clickThrough: true);
            coachWindow.Width = CoachMark.ViewWidth; coachWindow.Height = CoachMark.ViewHeight;
            coachWindow.Show();
        }
        PlaceCoach(at);
        System.Diagnostics.Trace.WriteLine($"Battle lesson: {id}.");
    }

    // The ring's centre on the target; the words go to whichever side has room.
    private void PlaceCoach(Point at)
    {
        if (coachWindow == null || coachMark == null) return;
        if (!coachWindow.IsVisible) coachWindow.Show();
        var area = battleArea;
        bool left = at.X + CoachMark.ViewWidth - CoachMark.Ring > area.Right;
        coachMark.Flip = left;
        coachMark.Time = coachAge;
        coachMark.InvalidateVisual();
        double x = left ? at.X - (CoachMark.ViewWidth - CoachMark.Ring) : at.X - CoachMark.Ring;
        double y = Math.Clamp(at.Y - CoachMark.Ring, area.Top, Math.Max(area.Top, area.Bottom - CoachMark.ViewHeight));
        MoveWindow(coachWindow, x, y, battleScale);
    }

    // seen: the lesson ran its course (done or timed out) and is not shown again.
    private void EndCoach(bool seen)
    {
        if (seen && coachLesson != null && !LessonSeen(coachLesson))
        {
            (preferences.BattleLessonsSeen ??= new()).Add(coachLesson);
            SavePreferences();
        }
        coachLesson = null;
        coachWindow?.Close();
        coachWindow = null;
    }

    // How to play shows the lessons again in the next battles.
    private void ResetBattleLessons()
    {
        if (preferences.BattleLessonsSeen is not { Count: > 0 }) return;
        preferences.BattleLessonsSeen.Clear();
        SavePreferences();
    }
}

// The lesson's mark: a pulsing gold ring on the spot, a hand tapping it (or pressing and holding),
// and a short line in a dark bubble beside it. Drawn only; the window lets every click through.
public sealed class CoachMark : FrameworkElement
{
    public const double ViewWidth = 300, ViewHeight = 110, Ring = 46;
    public string Text { get; set; } = "";
    public bool Hold { get; set; }
    public bool Flip { get; set; }
    public double Time { get; set; }
    public FontFamily Font { get; set; } = new("Segoe UI");
    private static readonly Pen RingPen = PetUi.FrozenPen(PetUi.Frozen(Color.FromRgb(0xFF, 0xD2, 0x3F)), 3.5);
    private static readonly Pen RingHalo = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0x90, 255, 255, 255)), 7);
    private static readonly Brush Bubble = PetUi.Frozen(Color.FromArgb(0xE6, 0x30, 0x2B, 0x48));
    private static readonly Pen BubbleRim = PetUi.FrozenPen(PetUi.Frozen(Color.FromRgb(0xFF, 0xD2, 0x3F)), 1.5);
    private static readonly Brush Skin = PetUi.Frozen(Color.FromRgb(0xFF, 0xF3, 0xE2));
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.Frozen(Color.FromRgb(0x30, 0x2B, 0x48)), 1.8);
    private static readonly Geometry Hand = PetUi.FrozenGeometry(
        "M 0,0 C 2,-1 5,0 5,3 L 5,14 C 7,12 11,12 12,15 C 14,13 18,13 19,16 C 21,15 25,16 25,20 L 25,30 C 25,38 20,42 13,42 C 6,42 2,38 0,32 L -6,22 C -8,19 -5,16 -2,18 L 0,21 Z");

    protected override void OnRender(DrawingContext dc)
    {
        double cx = Flip ? ViewWidth - Ring : Ring, cy = Ring;
        // The ring breathes; a held lesson's ring shrinks onto the spot again and again.
        double pulse = Hold ? 1 - (Time % 1.2) / 1.2 * 0.35 : 1 + 0.12 * Math.Sin(Time * 6);
        double r = 24 * pulse;
        dc.DrawEllipse(null, RingHalo, new Point(cx, cy), r, r);
        dc.DrawEllipse(null, RingPen, new Point(cx, cy), r, r);
        // The hand comes from below, its finger on the spot: it taps (or stays pressed when held).
        double tap = Hold ? 2 : 6 * Math.Abs(Math.Sin(Time * 5));
        dc.PushTransform(new TranslateTransform(cx + 2, cy + 6 + tap));
        dc.PushTransform(new RotateTransform(Flip ? 20 : -20));
        dc.DrawGeometry(Skin, Ink, Hand);
        dc.Pop(); dc.Pop();
        if (Text.Length == 0) return;
        var words = new FormattedText(Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(Font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            15, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = ViewWidth - Ring * 2 - 20 };
        double w = words.Width + 20, h = words.Height + 12;
        double bx = Flip ? cx - Ring - 4 - w : cx + Ring + 4, by = Math.Max(2, cy - h / 2);
        dc.DrawRoundedRectangle(Bubble, BubbleRim, new Rect(bx, by, w, h), 9, 9);
        dc.DrawText(words, new Point(bx + 10, by + 6));
    }
}
