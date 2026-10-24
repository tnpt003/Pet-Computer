using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode's lessons on the spot (2026-10-24): Adventure is where the game is learned. Each
// lesson belongs to the chapter 1 stage that teaches it (the table below) and can come in that
// stage or any later one; never in a free battle, the Expedition or Infinity. When its moment
// comes — a slime winding up next to the cat, ads, an infected icon, a skill that would help —
// a click-through overlay rings the spot with a bouncing hand and a few words. The battle runs at
// its own speed. One lesson at a time: not in the first CoachFirst s, CoachGap s apart, and the
// ones that can wait only while no slime is about to hit the cat. A lesson counts as learned
// when the user does it (preferences.BattleLessonsSeen); one that times out or loses its target
// comes again in a later moment, at most CoachTries times a session. How to play on the setup
// panel shows them all again. A stage's tip lists what the stage teaches.
public sealed partial class PetWindow
{
    public const string LessonTarget = "target", LessonLift = "lift", LessonStun = "stun", LessonCards = "cards", LessonApp = "app", LessonHeal = "heal",
        LessonInfected = "infected", LessonAds = "ads", LessonAdsLocked = "ads_locked", LessonBomb = "bomb", LessonShield = "shield",
        LessonPush = "push", LessonLoading = "loading", LessonUpdate = "update", LessonSlam = "slam", LessonVirus = "virus", LessonCheer = "cheer";
    public const double CoachTimeout = 6, CoachFirst = 3, CoachGap = 10;
    public const int CoachTries = 3;
    // The Adventure stages that teach the lessons (chapter 1).
    public const string StageFirstSteps = "c1_first_steps", StageBreakThrough = "c1_break_through", StageHoldOn = "c1_hold_on",
        StageCutSupport = "c1_cut_support", StageSlimeKing = "c1_slime_king";

    // One lesson: the stage that teaches it; Urgent ones come even with a slime about to hit; Due
    // says its moment has come (and sets coachRef); Done, that the user did it; Target, where it
    // points (null: nothing left to point at); Words, its line and whether the hand holds instead
    // of tapping; Topic, its name in the stage tip (null: not listed).
    private sealed record Lesson(string Id, string Stage, bool Urgent, Func<bool> Due, Func<bool> Done, Func<Point?> Target,
        Func<(string Text, bool Hold)> Words, Func<string>? Topic);

    private Lesson[]? lessons;
    private string? coachLesson;
    private BattleSim? coachBattle;
    private double coachAge, coachStartedAt, coachNextAt;
    // What the lesson points at (a slime, an icon, malware, an ad) and a number to compare with (clicks left).
    private int coachRef;
    private double coachRefValue;
    // Lessons that ran out without the user doing them, this session.
    private readonly Dictionary<string, int> coachMisses = new();
    private Window? coachWindow;
    private CoachMark? coachMark;
    // Off in the window checks unless a check turns it on (they time the battle); on for the real pet.
    public bool BattleCoachEnabled { get; set; }
    public string? BattleCoachLesson => coachLesson;
    public bool BattleCoachVisible => coachWindow?.IsVisible == true;

    private bool LessonSeen(string id) => preferences.BattleLessonsSeen?.Contains(id) == true;

    // Most urgent first: when two are due, the first one in this list comes.
    private Lesson[] Lessons => lessons ??= new Lesson[]
    {
        new(LessonLift, StageFirstSteps, true,
            () => { var s = HitterNearCat(boss: false); coachRef = s?.Id ?? 0; return s != null && !BattleHoldsOff && !battle!.CatStunned; },
            () => BattleLifted,
            () => battle!.Find(coachRef) is { State: SlimeState.Windup } ? CatPoint() : null,
            () => (L("Kéo mèo lên!", "Lift the cat!"), true),
            () => L("kéo mèo lên khi slime gồng", "lift the cat when a slime winds up")),
        new(LessonSlam, StageSlimeKing, true,
            () => { var s = HitterNearCat(boss: true); coachRef = s?.Id ?? 0; return s != null && !BattleHoldsOff && !battle!.CatStunned; },
            () => BattleLifted || battle!.ShieldRemaining > 0,
            () => battle!.Find(coachRef) is not { State: SlimeState.Windup } ? null : battle.Ready(BattleSkill.Shield) ? SkillPoint(BattleSkill.Shield) : CatPoint(),
            () => battle!.Ready(BattleSkill.Shield)
                ? (LF($"Bấm {SkillName(BattleSkill.Shield)} chặn cú dậm", $"Use {SkillName(BattleSkill.Shield)} against the slam"), false)
                : (L("Kéo mèo lên né cú dậm", "Lift the cat out of the slam"), true),
            () => L("né cú dậm của trùm", "dodge the boss's slam")),
        new(LessonStun, StageFirstSteps, true,
            () => battle!.CatStunned,
            () => !battle!.CatStunned,
            () => CatPoint(),
            () => (L("Mèo choáng, chờ chút", "Stunned: wait a moment"), false),
            () => L("khi mèo bị choáng", "when the cat is stunned")),
        new(LessonCards, StageBreakThrough, true,
            () => battle!.ChoosingUpgrade && upgradeWindow?.IsVisible == true,
            () => !battle!.ChoosingUpgrade,
            () => upgradeWindow?.IsVisible == true ? new Point(upgradeAt.X, upgradeAt.Y + 28) : null,
            () => (L("Chọn một thẻ rồi Xác nhận: build mang sang màn sau", "Pick a card and Confirm: the build goes on to the next stage"), false),
            () => L("chọn thẻ, build mang sang màn sau", "pick cards, the build carries on")),
        new(LessonAdsLocked, StageHoldOn, false,
            () => battle!.Popups.Count > 0 && battle.PopupsLocked,
            () => !battle!.PopupsLocked,
            () => battle!.Popups.Count > 0 && battle.Desktop.FirstOrDefault(a => a.Infected && a.Kind == BattleAppKind.Tabs) is DesktopApp tabs ? BattleAppCentre(tabs) : null,
            () => (L("Gỡ virus khỏi Tab trước", "Clean the Tabs icon first"), false),
            null),
        new(LessonInfected, StageHoldOn, false,
            () => { var sick = battle!.Desktop.FirstOrDefault(a => a.Infected); coachRef = sick?.Id ?? 0; coachRefValue = sick?.ScrubbedAt ?? 0; return sick != null; },
            () => battle!.FindApp(coachRef) is not { Infected: true } app || app.ScrubbedAt > coachRefValue,
            () => battle!.FindApp(coachRef) is DesktopApp app ? BattleAppCentre(app) : null,
            () => (LF($"Bấm {battle?.FindApp(coachRef)?.ScrubsLeft ?? 1} lần để gỡ virus", $"Click it {battle?.FindApp(coachRef)?.ScrubsLeft ?? 1}× to clean it"), false),
            () => L("gỡ virus khỏi icon", "clean infected icons")),
        new(LessonAds, StageHoldOn, false,
            () => { coachRef = battle!.Popups.Count > 0 && !battle.PopupsLocked ? battle.Popups[0].Id : 0; return coachRef != 0; },
            () => battle!.FindPopup(coachRef) == null,
            () => battle!.FindPopup(coachRef) is BattlePopup ad ? new Point(ad.X + BattleSim.PopupWidth / 2 - 10, Floor() - 2 - BattleSim.PopupHeight + 9) : null,
            () => (L("Bấm × để tắt", "Click × to close"), false),
            () => L("tắt quảng cáo", "close ads")),
        new(LessonLoading, StageCutSupport, false,
            () => HazardDue(HazardKind.Loading),
            () => battle!.FindHazard(coachRef) == null || battle.HoldingHazardId == coachRef,
            () => battle!.FindHazard(coachRef) is BattleHazard ring ? new Point(ring.X, Floor() - 132) : null,
            () => battle?.AppRules is { LoadingHold: > 0 } rules
                ? (LF($"Giữ chuột {rules.LoadingHold.ToString("0.#", CultureInfo.InvariantCulture)} giây", $"Hold it {rules.LoadingHold.ToString("0.#", CultureInfo.InvariantCulture)} s"), true)
                : (L("Bấm để tắt", "Click to close"), false),
            () => L("tắt vòng loading", "stop the loading ring")),
        new(LessonUpdate, StageCutSupport, false,
            () => HazardDue(HazardKind.Update),
            () => battle!.FindHazard(coachRef) is not { } wall || wall.ClicksLeft < coachRefValue,
            () => battle!.FindHazard(coachRef) is BattleHazard wall ? new Point(wall.X, Floor() - 15.5) : null,
            () => (L("Bấm Nhắc sau", "Click Later"), false),
            () => L("gỡ Cập nhật bắt buộc", "get rid of a forced update")),
        new(LessonVirus, StageSlimeKing, false,
            () => { coachRef = battle!.VirusFiles.Count > 0 ? battle.VirusFiles[0].Id : 0; return coachRef != 0; },
            () => battle!.VirusFiles.All(f => f.Id != coachRef),
            () =>
            {
                if (battle!.VirusFiles.FirstOrDefault(f => f.Id == coachRef) is not VirusFile file) return null;
                var cell = AppCellTopLeft(file.Col, file.Row);
                return new Point(cell.X + DesktopAppIcon.Centre.X, cell.Y + DesktopAppIcon.Centre.Y);
            },
            () => (L("Bấm để xóa virus.exe", "Click to delete virus.exe"), false),
            () => L("xóa virus.exe", "delete virus.exe")),
        new(LessonPush, StageCutSupport, false,
            () => { var s = battle!.Slimes.FirstOrDefault(e => e.State == SlimeState.Casting); coachRef = s?.Id ?? 0; return s != null && SkillDue(BattleSkill.Push); },
            () => battle!.Cooldown(BattleSkill.Push) > 0,
            () => battle!.Find(coachRef) is not { State: SlimeState.Casting } healer ? null
                : battle.Armed == BattleSkill.Push ? new Point(healer.X, Floor() - healer.Height / 2) : SkillPoint(BattleSkill.Push),
            () => battle?.Armed == BattleSkill.Push
                ? (L("Bấm slime đang hồi máu", "Click the healing slime"), false)
                : (LF($"Bấm {SkillName(BattleSkill.Push)} để cắt hồi máu", $"Use {SkillName(BattleSkill.Push)} to stop the heal"), false),
            () => LF($"{SkillName(BattleSkill.Push)} cắt hồi máu", $"{SkillName(BattleSkill.Push)} stops a heal")),
        new(LessonShield, StageHoldOn, false,
            () => SkillDue(BattleSkill.Shield) && battle!.Slimes.Count(s => s.Targetable && Math.Abs(s.X - CatX()) <= 140) >= 3,
            () => battle!.Cooldown(BattleSkill.Shield) > 0,
            () => SkillPoint(BattleSkill.Shield),
            () => (LF($"Bị vây: bấm {SkillName(BattleSkill.Shield)}", $"Surrounded: use {SkillName(BattleSkill.Shield)}"), false),
            () => LF($"{SkillName(BattleSkill.Shield)} khi bị vây", $"{SkillName(BattleSkill.Shield)} when surrounded")),
        new(LessonBomb, StageHoldOn, false,
            () => { var s = BombSpot(); coachRef = s?.Id ?? 0; return s != null && SkillDue(BattleSkill.Bomb); },
            () => battle!.Cooldown(BattleSkill.Bomb) > 0,
            () => battle!.Find(coachRef) is not { Targetable: true } middle ? null
                : battle.Armed == BattleSkill.Bomb ? new Point(middle.X, Floor() - middle.Height / 2) : SkillPoint(BattleSkill.Bomb),
            () => battle?.Armed == BattleSkill.Bomb
                ? (L("Bấm vào giữa đám slime", "Click the middle of the bunch"), false)
                : (LF($"Slime đứng sát: bấm {SkillName(BattleSkill.Bomb)}", $"A bunch of slimes: use {SkillName(BattleSkill.Bomb)}"), false),
            () => LF($"{SkillName(BattleSkill.Bomb)} vào đám slime", $"{SkillName(BattleSkill.Bomb)} on a bunch")),
        new(LessonApp, StageBreakThrough, false,
            () => { var ready = battle!.Desktop.FirstOrDefault(a => a.Ready); coachRef = ready?.Id ?? 0; return ready != null && !BattleHoldsOff; },
            () => battle!.FindApp(coachRef) is { } used && used.UsedAt >= coachStartedAt,
            () => battle!.FindApp(coachRef) is { Ready: true } app ? BattleAppCentre(app) : null,
            () => (L("App đầy rồi: bấm để dùng", "Charged: click to use it"), false),
            () => L("dùng app đã sạc", "use charged apps")),
        new(LessonHeal, StageBreakThrough, false,
            () => battle!.CatHp < battle.CatMaxHp * 0.6 && SkillDue(BattleSkill.Heal),
            () => battle!.Cooldown(BattleSkill.Heal) > 0,
            () => SkillPoint(BattleSkill.Heal),
            () => (LF($"Bấm {SkillName(BattleSkill.Heal)}", $"Use {SkillName(BattleSkill.Heal)}"), false),
            () => LF($"{SkillName(BattleSkill.Heal)} khi máu thấp", $"{SkillName(BattleSkill.Heal)} when HP is low")),
        new(LessonCheer, StageSlimeKing, false,
            () => SkillDue(BattleSkill.Cheer) && battle!.Slimes.Count(s => s.Targetable) >= 3,
            () => battle!.Cooldown(BattleSkill.Cheer) > 0,
            () => SkillPoint(BattleSkill.Cheer),
            () => (LF($"Đông slime: bấm {SkillName(BattleSkill.Cheer)}", $"Many slimes: use {SkillName(BattleSkill.Cheer)}"), false),
            () => LF($"{SkillName(BattleSkill.Cheer)} khi đông slime", $"{SkillName(BattleSkill.Cheer)} against many slimes")),
        new(LessonTarget, StageFirstSteps, false,
            () =>
            {
                var near = battle!.Slimes.Where(s => s.Targetable).OrderBy(s => Math.Abs(s.X - CatX())).FirstOrDefault();
                coachRef = near?.Id ?? 0;
                return near != null && battle.SelectedId == null && battle.Phase == BattlePhase.Fighting;
            },
            () => battle!.SelectedId != null,
            () => battle!.Find(coachRef) is { Targetable: true } slime ? new Point(slime.X, Floor() - slime.Height / 2) : null,
            () => (L("Bấm chọn slime để đánh", "Click a slime to attack it"), false),
            () => L("chọn slime để đánh", "pick the slime to attack")),
    };

    private double CatX() => pet.X + settings.Width / 2;
    private Point CatPoint() => new(pet.X + PetSettings.CenterX, pet.Y + PetSettings.ChestY);
    private double Floor() => battleArea.Bottom - 2;
    private string SkillName(BattleSkill skill) => BattleSkills.Get(skill).Name(preferences.Language, battle?.Settings ?? BattleTuning);
    // A skill lesson waits until the skill is ready and the cat is on the ground.
    private bool SkillDue(BattleSkill skill) => battle!.Ready(skill) && !BattleHoldsOff;
    // A slime (the boss, or any other) winding up an attack that reaches the cat.
    private Slime? HitterNearCat(bool boss) => battle!.Slimes.FirstOrDefault(s => s.State is SlimeState.Windup && !s.Type.Support && s.Type.Slam == boss
        && Math.Abs(s.X - CatX()) <= s.Range + (s.Type.Dash ? s.Type.DashDistance : 40));
    private bool DangerNearCat => HitterNearCat(false) != null || HitterNearCat(true) != null;
    // A slime with at least two others within the bomb's reach.
    private Slime? BombSpot()
    {
        var b = battle!;
        var up = b.Slimes.Where(s => s.Targetable).ToList();
        return up.FirstOrDefault(s => up.Count(o => Math.Abs(o.X - s.X) <= b.Settings.BombRadius) >= 3);
    }
    private bool HazardDue(HazardKind kind)
    {
        var hazard = battle!.Hazards.FirstOrDefault(h => h.Kind == kind);
        coachRef = hazard?.Id ?? 0;
        coachRefValue = hazard?.ClicksLeft ?? 0;
        return hazard != null;
    }
    // The middle of a skill's slot on the HUD (DIPs on the battle's monitor).
    private Point? SkillPoint(BattleSkill skill)
    {
        if (battleHud == null || battleHudWindow?.Content is not Visual root) return null;
        var slot = battleHud.Slot(skill);
        var inHud = slot.TranslatePoint(new Point(slot.ActualWidth / 2, slot.ActualHeight / 2), (UIElement)root);
        return new Point(battleHudAt.X + inHud.X, battleHudAt.Y + inHud.Y);
    }

    // The stage's place in its chapter (-1: not a campaign stage).
    private int StageOrder(string id)
    {
        foreach (var chapter in BattleTuning.Campaign?.Chapters ?? Array.Empty<ChapterSpec>())
            if (Array.FindIndex(chapter.Stages, s => s.Id == id) is int at and >= 0) return at;
        return -1;
    }

    // Lessons run only in an Adventure stage (not the Expedition's encounters, not Infinity), and
    // a lesson only from the stage that teaches it on (a stage the data does not have: anywhere).
    private bool CoachOn => BattleCoachEnabled && battle is { Over: false, Stage: { Endless: false } } && battleExpedition == null;
    private bool LessonHere(Lesson lesson)
    {
        int teaches = StageOrder(lesson.Stage);
        return teaches < 0 || teaches <= StageOrder(battle!.Stage!.Id);
    }

    // What a stage teaches, for its tip ("In this stage: …"); null when it teaches nothing.
    private string? StageLessonsLine(StageSpec stage)
    {
        var topics = Lessons.Where(l => l.Stage == stage.Id && l.Topic != null).Select(l => l.Topic!()).ToList();
        return topics.Count == 0 ? null : L("Màn này học: ", "In this stage: ") + string.Join(", ", topics) + ".";
    }

    // Every battle frame (real time), after the battle's step.
    private void TickBattleCoach(double dt)
    {
        if (!CoachOn) { EndCoach(false); return; }
        if (coachBattle != battle) { EndCoach(false); coachBattle = battle; coachNextAt = CoachFirst; }
        var b = battle!;
        // The card lesson is shown over the card offer's own pause; every other pause hides a lesson.
        bool cardsOnly = battlePause == BattlePause.Upgrade;
        if (coachLesson != null)
        {
            var lesson = Lessons.First(l => l.Id == coachLesson);
            if ((b.Paused && !(cardsOnly && lesson.Id == LessonCards)) || context.Fullscreen) { coachWindow?.Hide(); return; }
            coachAge += dt;
            // The stun lesson only tells: running out counts as learned.
            if (lesson.Done() || (lesson.Id == LessonStun && coachAge >= CoachTimeout)) { FinishCoach(true); return; }
            if (coachAge >= CoachTimeout || lesson.Target() is not Point at) { FinishCoach(false); return; }
            PlaceCoach(lesson, at);
            return;
        }
        if (context.Fullscreen || battleTutorialWindow != null || b.Time < coachNextAt) return;
        if (b.Paused && !(cardsOnly && b.ChoosingUpgrade)) return;
        bool danger = DangerNearCat;
        foreach (var lesson in Lessons)
        {
            if (LessonSeen(lesson.Id) || (b.Paused && lesson.Id != LessonCards) || (danger && !lesson.Urgent) || !LessonHere(lesson) || !lesson.Due()) continue;
            StartCoach(lesson);
            return;
        }
    }

    private void StartCoach(Lesson lesson)
    {
        coachLesson = lesson.Id;
        coachAge = 0;
        coachStartedAt = battle!.Time;
        if (lesson.Target() is not Point at) { FinishCoach(false); return; }
        coachMark ??= new CoachMark();
        coachMark.Font = PetUi.TextFont;
        if (coachWindow == null)
        {
            coachWindow = CreateOverlayWindow(coachMark, "Pet coach", clickThrough: true);
            coachWindow.Width = CoachMark.ViewWidth; coachWindow.Height = CoachMark.ViewHeight;
            coachWindow.Show();
        }
        PlaceCoach(lesson, at);
        System.Diagnostics.Trace.WriteLine($"Battle lesson: {lesson.Id}.");
    }

    // The ring's centre on the target; the words go to whichever side has room (the card lesson's
    // to the left of the offer, off its cards, when there is room).
    private void PlaceCoach(Lesson lesson, Point at)
    {
        if (coachWindow == null || coachMark == null) return;
        if (!coachWindow.IsVisible) coachWindow.Show();
        var area = battleArea;
        double side = CoachMark.ViewWidth - CoachMark.Ring;
        bool left = lesson.Id == LessonCards ? at.X - side >= area.Left : at.X + side > area.Right;
        // The words change with the moment (a skill armed, clicks left).
        (coachMark.Text, coachMark.Hold) = lesson.Words();
        coachMark.Flip = left;
        coachMark.Time = coachAge;
        coachMark.InvalidateVisual();
        double x = left ? at.X - side : at.X - CoachMark.Ring;
        double y = Math.Clamp(at.Y - CoachMark.Ring, area.Top, Math.Max(area.Top, area.Bottom - CoachMark.ViewHeight));
        MoveWindow(coachWindow, x, y, battleScale);
    }

    // learned: the user did it, so it never comes again; otherwise it comes back at a later moment
    // until it has run out CoachTries times this session. The next lesson waits CoachGap s.
    private void FinishCoach(bool learned)
    {
        if (coachLesson is string id && !LessonSeen(id))
        {
            int missed = learned ? 0 : coachMisses.GetValueOrDefault(id) + 1;
            if (!learned) coachMisses[id] = missed;
            if (learned || missed >= CoachTries)
            {
                (preferences.BattleLessonsSeen ??= new()).Add(id);
                SavePreferences();
            }
        }
        if (battle != null) coachNextAt = battle.Time + CoachGap;
        CloseCoach();
    }

    // The battle ended or lessons went off: the mark goes, nothing is counted.
    private void EndCoach(bool seen)
    {
        if (seen) { FinishCoach(true); return; }
        CloseCoach();
    }

    private void CloseCoach()
    {
        coachLesson = null;
        coachWindow?.Close();
        coachWindow = null;
    }

    // How to play shows the lessons again in the next Adventure battles.
    private void ResetBattleLessons()
    {
        coachMisses.Clear();
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
