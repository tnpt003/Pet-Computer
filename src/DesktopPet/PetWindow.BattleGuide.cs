using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet;

// The Adventure Guide (2026-10-24): chapter 1 is where the game is learned, one kind of guidance
// for everything. When a stage's moment comes (a slime about to hit, the HUD at the start of stage 2,
// malware, a herb slime's heal, the King's moves) the battle stands still — its time, slimes, clock
// and cooldowns — the screen goes dark around the spot, the spot is lit and a short line says what
// to do. A "do" step waits until the user does it (lifting the cat stays possible: the battle is
// frozen, not paused); a "see" step waits for Got it. Every line has Skip the guide (for this run).
// Only in a chapter 1 Adventure stage, each step in its own stage, as often as its Times in one run;
// it is on by default (preferences.AdventureGuide, the stage setup's Guide chip, kept from its last
// Ready), so a won stage teaches again until the user unticks it. Never in a free battle, the Expedition or Infinity. Pause, the menu
// and a fullscreen app hide it like the rest of the battle.
public sealed partial class PetWindow
{
    public const string StageFirstSteps = "c1_first_steps", StageBreakThrough = "c1_break_through", StageHoldOn = "c1_hold_on",
        StageCutSupport = "c1_cut_support", StageSlimeKing = "c1_slime_king";
    public const string GuideLift = "lift", GuideLiftQuick = "lift_quick", GuideHp = "hp", GuideWaves = "waves", GuideSkills = "skills",
        GuidePush = "push", GuideHeal = "heal", GuideCheer = "cheer", GuideBomb = "bomb", GuideShield = "shield", GuideBag = "bag", GuideApps = "apps",
        GuideCards = "cards", GuideSynergy = "synergy", GuideClock = "clock", GuideAds = "ads", GuideLoading = "loading", GuideUpdate = "update",
        GuideVirus = "virus", GuideInfected = "infected", GuidePushRole = "push_role", GuideCutHeal = "cut_heal", GuideElite = "elite",
        GuideKing = "king", GuideSlam = "slam", GuideKingShield = "king_shield", GuideRally = "rally",
        GuideLaser = "laser", GuideFan = "fan", GuideGrabber = "grabber", GuideScissors = "scissors", GuideSpray = "spray";
    // A step for the stage that teaches a skill (its unlocksSkill; chapter 2 from M21), not a stage id.
    public const string GuideTeaches = "teaches:";
    public const int GuideLifts = 5, GuideCutHeals = 4;

    // One step: its stage; Do (waits for Done) or See (waits for Got it; Done may end it sooner);
    // Ok: a do step that may also be passed with Got it; Times: how often in one run; Due: its moment
    // has come (and sets guideRef); Spots: what is lit (DIPs on the battle's monitor; none: the step
    // has nothing left to show and ends); Words: its line.
    private sealed record GuideStep(string Id, string Stage, bool Do, bool Ok, int Times, Func<bool> Due, Func<bool> Done, Func<List<Rect>> Spots, Func<string> Words);

    private GuideStep[]? guideSteps;
    private GuideStep? guideStep;
    private BattleSim? guideBattle;
    private bool guideOn, guideOk;
    // What the step points at (a slime, an icon, malware) and a number to compare with.
    private int guideRef;
    private double guideRefValue;
    private readonly Dictionary<string, int> guideShown = new();
    // The stage setup's Guide chip for the next stage battle (null: preferences.AdventureGuide).
    private bool? guideChoice;
    private Window? guideShadeWindow, guideBubbleWindow;
    private GuideShade? guideShade;
    private TextBlock? guideText, guideOkText, guideSkipText;
    private Border? guideOkButton;
    private (Area Area, double Scale) guidePlaced;
    // Off in the window checks unless a check turns it on (they time the battle); on for the real pet.
    public bool BattleGuideEnabled { get; set; }
    public string? BattleGuideStep => guideStep?.Id;
    public bool BattleGuideOn => guideOn;
    public bool BattleGuideVisible => guideShadeWindow?.IsVisible == true;
    public string BattleGuideText => guideText?.Text ?? "";
    public int BattleGuideShown(string id) => guideShown.GetValueOrDefault(id);
    // The battle's time stands still while a step shows (the card offer has its own pause).
    public bool BattleGuideFreezes => guideStep != null;

    // A stage the guide teaches (chapter 1).
    public bool StageHasGuide(StageSpec? stage) => stage != null && GuideSteps.Any(g => GuideFor(g, stage));
    private static bool GuideFor(GuideStep step, StageSpec stage) => step.Stage == stage.Id
        || (stage.UnlocksSkill is string skill && step.Stage == GuideTeaches + skill.ToLowerInvariant());

    private GuideStep[] GuideSteps => guideSteps ??= new GuideStep[]
    {
        // 1 · First steps: lifting the cat out of a hit, five times, and once for a quick slime's dash.
        new(GuideLift, StageFirstSteps, true, false, GuideLifts,
            () => LiftDue(quick: false), () => BattleLifted, () => LiftSpots(),
            () => GuideTimes(GuideLift) <= 1
                ? L("Slime phồng lên với dấu ! đỏ là sắp đánh. Kéo mèo lên để né: đang bế thì mèo không bị đánh.",
                    "A slime swelling with a red ! is about to hit. Drag the cat up: a carried cat cannot be hit.")
                : LF($"Lại sắp đánh: kéo mèo lên! Thả mèo ra ngoài vòng đỏ để đòn trượt. ({GuideTimes(GuideLift)}/{GuideLifts})",
                     $"Another hit is coming: lift the cat! Put it down outside the red ring and the hit misses. ({GuideTimes(GuideLift)}/{GuideLifts})")),
        new(GuideLiftQuick, StageFirstSteps, true, false, 1,
            () => LiftDue(quick: true), () => BattleLifted, () => LiftSpots(),
            () => L("Slime nhanh gồng rồi lao tới rất nhanh: kéo mèo lên ngay!", "A quick slime winds up, then dashes in fast: lift the cat now!")),

        // 2 · Break through: the HUD, one point at a time, then the cards and their groups.
        See(GuideHp, StageBreakThrough, () => TourDue(), () => Spots(HudPart(battleHud?.HpBlock)),
            () => L("Máu của mèo. Hết máu là thua; giữa các đợt mèo hồi lại một ít.", "The cat's HP. At zero the battle is lost; between waves the cat gets a little back.")),
        See(GuideWaves, StageBreakThrough, () => TourDue(), () => Spots(HudPart(battleHud?.StatusBlock)),
            () => L("Đợt quái đang đánh và số đợt còn lại. Dọn hết các đợt là thắng màn.", "The wave being fought and how many are left. Clear them all to win the stage.")),
        See(GuideSkills, StageBreakThrough, () => TourDue(), () => Spots(SkillBarSpot()),
            () => L("Kỹ năng của mèo: bấm ô để dùng. Vòng quét trên ô là thời gian hồi; đang bế mèo thì kỹ năng chờ.",
                    "The cat's skills: click a slot to use it. The sweep on a slot is its cooldown; while the cat is carried, skills wait.")),
        SkillSee(GuidePush, BattleSkill.Push), SkillSee(GuideHeal, BattleSkill.Heal), SkillSee(GuideCheer, BattleSkill.Cheer),
        SkillSee(GuideBomb, BattleSkill.Bomb), SkillSee(GuideShield, BattleSkill.Shield),
        See(GuideBag, StageBreakThrough, () => TourDue(), () => Spots(HudPart(battleHud?.BagButton)),
            () => L("Túi đồ: chỉ số của mèo, thẻ đã lấy và cộng hưởng. Mở túi thì trận tạm dừng.", "The bag: the cat's stats, the cards taken and the synergies. The battle waits while it is open.")),
        See(GuideApps, StageBreakThrough, () => TourDue() && battle!.Desktop.Count > 0, () => battle!.Desktop.Select(AppSpot).ToList(),
            () => L("Icon app trên màn hình tự sạc. Sáng vàng là đầy: bấm để app giúp mèo.", "The app icons charge by themselves. Glowing gold means full: click one and it helps the cat.")),
        new(GuideCards, StageBreakThrough, false, true, 1, () => CardsDue(), () => !battle!.ChoosingUpgrade, () => Spots(UpgradeSpot()),
            () => L("Dọn xong đợt: chọn 1 trong 3 thẻ rồi Xác nhận. Thẻ giữ tới hết màn, thắng màn thì build mang sang màn sau.",
                    "A wave is cleared: pick 1 of 3 cards, then Confirm. Cards stay for the stage, and a win carries the build to the next one.")),
        new(GuideSynergy, StageBreakThrough, false, true, 1, () => CardsDue() && GuideTimes(GuideCards) > 0, () => !battle!.ChoosingUpgrade, () => Spots(UpgradeBuildSpot()),
            () => L("Mỗi thẻ thuộc một nhóm. Gom đủ thẻ cùng nhóm là mở cộng hưởng, thêm một sức mạnh riêng (xem trong túi đồ).",
                    "Each card belongs to a group. Enough cards of one group switch on a synergy, an extra power (see the bag).")),

        // 3 · Hold on: the clock, then each malware and an infected icon as they come (the stage sends them).
        See(GuideClock, StageHoldOn, () => battle!.Time >= 0.6 && battle.Survival, () => Spots(HudPart(battleHud?.StatusBlock)),
            () => L("Cầm cự tới khi đồng hồ về 0. Bế mèo thì đồng hồ dừng, nên chỉ bế khi cần né đòn.", "Hold out until the clock reaches 0. Carrying the cat stops the clock, so lift it only to dodge.")),
        new(GuideAds, StageHoldOn, true, true, 1,
            () => { guideRefValue = battle!.Popups.Count; return battle.Popups.Count > 0 && battle.Popups.All(p => p.Age >= PopupShown || battle.PopupsLocked); },
            () => battle!.Popups.Count < guideRefValue, () => Spots(battle!.Popups.Select(PopupSpot).ToArray()),
            () => L("App độc: quảng cáo che sân. Bấm × để tắt từng cái (Tab bị nhiễm thì phải gỡ Tab trước).",
                    "Malware: ads cover the floor. Click × to close each one (with Tabs infected, clean Tabs first).")),
        new(GuideLoading, StageHoldOn, true, true, 1,
            () => HazardDue(HazardKind.Loading), () => battle!.FindHazard(guideRef) == null || battle.HoldingHazardId == guideRef,
            // The spinner, the slowing zone on the floor and the cat standing in it.
            () => Spots(FieldSpot(battleView?.SpinnerBounds(guideRef), 4), FieldSpot(battleView?.LoadingZoneBounds(guideRef), 4), CatSpot()),
            () => battle?.AppRules is { LoadingHold: > 0 } rules
                ? LF($"App độc: vòng loading làm mèo chậm hẳn. Giữ chuột lên vòng xoay {Number(rules.LoadingHold, "0.#")} giây để tắt.",
                     $"Malware: the loading ring slows the cat right down. Hold the mouse on the spinner for {Number(rules.LoadingHold, "0.#")} s to close it.")
                : L("App độc: vòng loading làm mèo chậm hẳn. Bấm vòng xoay để tắt.", "Malware: the loading ring slows the cat right down. Click the spinner to close it.")),
        new(GuideUpdate, StageHoldOn, true, true, 1,
            () => HazardDue(HazardKind.Update), () => battle!.FindHazard(guideRef) is not { } wall || wall.ClicksLeft < guideRefValue,
            () => Spots(FieldSpot(battleView?.WallBounds(guideRef))),
            () => L("App độc: Cập nhật bắt buộc dựng tường giữa mèo và slime. Bấm Nhắc sau vài lần để dẹp nó.",
                    "Malware: a forced update stands as a wall between the cat and the slimes. Click Later a few times to get rid of it.")),
        new(GuideVirus, StageHoldOn, true, true, 1,
            () => { var file = battle!.VirusFiles.FirstOrDefault(f => battle.Time - f.BornAt >= VirusShown); guideRef = file?.Id ?? 0; return file != null; },
            () => battle!.VirusFiles.All(f => f.Id != guideRef), () => battle!.VirusFiles.Select(VirusSpot).ToList(),
            () => L("App độc: virus.exe tự nhân bản, đủ bản thì nhiễm một icon. Bấm từng file để xóa.",
                    "Malware: virus.exe copies itself and, with enough copies, infects an icon. Click each file to delete it.")),
        new(GuideInfected, StageHoldOn, true, true, 1,
            () => { var sick = battle!.Desktop.FirstOrDefault(a => a.Infected && a.InfectedAt >= 0 && battle.Time - a.InfectedAt >= InfectedShown); guideRef = sick?.Id ?? 0; guideRefValue = sick?.ScrubbedAt ?? 0; return sick != null; },
            () => battle!.FindApp(guideRef) is not { Infected: true } app || app.ScrubbedAt > guideRefValue,
            () => battle!.FindApp(guideRef) is DesktopApp app ? new List<Rect> { AppSpot(app) } : new(),
            () => L("Slime bắn virus vào icon: icon bị nhiễm quay ra hại mèo. Bấm icon vài lần để gỡ, hoặc hạ con slime đã nhiễm nó.",
                    "A slime shot a virus into an icon: an infected icon turns against the cat. Click it a few times to clean it, or defeat the slime behind it.")),

        // 4 · Cut the support: Push, four times on a herb slime's heal, and the elites.
        See(GuidePushRole, StageCutSupport, () => battle!.Time >= 0.6, () => Spots(SkillSpot(BattleSkill.Push)),
            () => LF($"Màn này luyện {SkillName(BattleSkill.Push)}: hất slime ra, làm nó choáng và cắt ngang đòn gồng hay đòn hồi máu.",
                     $"This stage is about {SkillName(BattleSkill.Push)}: it knocks a slime back, stuns it and cuts a windup or a heal short.")),
        new(GuideCutHeal, StageCutSupport, true, false, GuideCutHeals,
            () => { var healer = battle!.Slimes.FirstOrDefault(s => s.State == SlimeState.Casting && s.Elite is not { PushImmune: true }); guideRef = healer?.Id ?? 0; return healer != null && battle.Ready(BattleSkill.Push) && !BattleHoldsOff; },
            () => battle!.Cooldown(BattleSkill.Push) > 0,
            () => battle!.Find(guideRef) is not { State: SlimeState.Casting } healer ? new()
                : battle.Armed == BattleSkill.Push ? Spots(SlimeSpot(healer)) : Spots(SlimeSpot(healer), SkillSpot(BattleSkill.Push)),
            () => battle?.Armed == BattleSkill.Push
                ? L("Giờ bấm vào slime thảo dược đang gồng.", "Now click the herb slime winding up.")
                : LF($"Slime thảo dược sắp hồi máu cho đồng đội. Bấm {SkillName(BattleSkill.Push)} rồi bấm nó để cắt. ({GuideTimes(GuideCutHeal)}/{GuideCutHeals})",
                     $"A herb slime is about to heal its friends. Click {SkillName(BattleSkill.Push)}, then click it to cut the heal. ({GuideTimes(GuideCutHeal)}/{GuideCutHeals})")),
        See(GuideElite, StageCutSupport, () => { var elite = battle!.Slimes.FirstOrDefault(s => s.Elite != null && s.Targetable && s.State != SlimeState.Spawning); guideRef = elite?.Id ?? 0; return elite != null; },
            () => battle!.Find(guideRef) is { Targetable: true } elite ? Spots(SlimeSpot(elite)) : new(),
            () => L("Slime tinh anh: viền vàng và ngôi sao, mạnh hơn bản thường và có một khả năng riêng (chữ trên đầu). Loại slime nào cũng có tinh anh.",
                    "An elite slime: a gold rim and a star, stronger than the plain one with a trait of its own (the word over its head). Every kind of slime has elites.")),

        // 5 · The Slime King: the King, then each of his moves the first time it comes.
        See(GuideKing, StageSlimeKing, () => King() is { State: not SlimeState.Spawning }, () => King() is Slime king ? Spots(SlimeSpot(king)) : new(),
            () => L("Vua Slime, trùm chương: rất nhiều máu, dậm đất trúng cả vùng, lui về bật khiên và gọi quân. Hạ Vua là xong chương.",
                    "The Slime King, the chapter's boss: lots of HP, a slam that hits a whole area, and he backs off behind a shield to call help. Beat him to finish the chapter.")),
        new(GuideSlam, StageSlimeKing, true, false, 1,
            () => King() is Slime king && king.State == SlimeState.Windup && Math.Abs(king.X - CatX()) <= king.Range + 40 && !BattleHoldsOff && !battle!.CatStunned,
            () => BattleLifted || battle!.ShieldRemaining > 0,
            () => King() is not { State: SlimeState.Windup } king ? new()
                : battle!.Ready(BattleSkill.Shield) ? Spots(CatSpot(), SlamSpot(king), SkillSpot(BattleSkill.Shield)) : Spots(CatSpot(), SlamSpot(king)),
            () => battle?.Ready(BattleSkill.Shield) == true
                ? LF($"Vua sắp dậm đất: vùng đỏ trúng hết. Kéo mèo ra khỏi vùng, hoặc bấm {SkillName(BattleSkill.Shield)}.",
                     $"The King is about to slam: the red zone hits everything. Lift the cat out of it, or click {SkillName(BattleSkill.Shield)}.")
                : L("Vua sắp dậm đất: vùng đỏ trúng hết. Kéo mèo ra khỏi vùng!", "The King is about to slam: the red zone hits everything. Lift the cat out of it!")),
        See(GuideKingShield, StageSlimeKing, () => King() is { Shielded: true }, () => King() is Slime king ? Spots(SlimeSpot(king)) : new(),
            () => L("Vua lui về bật khiên và gọi quân: đánh Vua lúc này không ăn thua. Hạ hết quân để phá khiên.",
                    "The King backs off behind a shield and calls help: hitting him does nothing now. Defeat his helpers to break it.")),
        new(GuideRally, StageSlimeKing, true, false, 1,
            () => King() is { RallyCharge: > 0 } && battle!.Ready(BattleSkill.Push) && !BattleHoldsOff,
            () => battle!.Cooldown(BattleSkill.Push) > 0,
            () => King() is not { RallyCharge: > 0 } king ? new()
                : battle!.Armed == BattleSkill.Push ? Spots(SlimeSpot(king)) : Spots(SlimeSpot(king), SkillSpot(BattleSkill.Push)),
            () => battle?.Armed == BattleSkill.Push
                ? L("Giờ bấm vào Vua.", "Now click the King.")
                : LF($"Vương miện sáng: Vua sắp phong một quân thành tinh anh. Bấm {SkillName(BattleSkill.Push)} rồi bấm Vua để cắt.",
                     $"A glowing crown: the King is about to make a helper elite. Click {SkillName(BattleSkill.Push)}, then click the King to stop it.")),

        // 1.5 M05 · the stage that teaches a new skill: try it once (only while the battle carries it).
        new(GuideLaser, GuideTeaches + "laser", true, true, 1,
            () => ToolDue(BattleSkill.Laser), () => battle!.Cooldown(BattleSkill.Laser) > 0,
            () => battle!.Armed == BattleSkill.Laser ? Spots(FloorSpot()) : Spots(SkillSpot(BattleSkill.Laser)),
            () => battle?.Armed == BattleSkill.Laser
                ? L("Giờ click một chỗ trên sàn, xa mèo một chút: slime quanh đó chạy theo chấm đỏ.", "Now click a spot of the floor a little away from the cat: the slimes around it chase the red dot.")
                : LF($"Kỹ năng mới: {SkillName(BattleSkill.Laser)}. Bấm ô này, rồi click một chỗ trên sàn.", $"A new skill: {SkillName(BattleSkill.Laser)}. Click this slot, then click a spot of the floor.")),
        new(GuideFan, GuideTeaches + "fan", true, true, 1,
            () => ToolDue(BattleSkill.Fan), () => battle!.FanBlowing,
            () => battle!.Armed == BattleSkill.Fan ? Spots(FloorSpot()) : Spots(SkillSpot(BattleSkill.Fan)),
            () => battle?.Armed == BattleSkill.Fan
                ? L("Giờ giữ chuột trên sàn cạnh đám slime: gió thổi chúng ra xa con trỏ. Vòng trên ô là hơi còn lại.", "Now hold the mouse on the floor next to the slimes: the wind blows them away from the cursor. The sweep on the slot is the air used.")
                : LF($"Kỹ năng mới: {SkillName(BattleSkill.Fan)}. Bấm ô này, rồi giữ chuột trên sàn.", $"A new skill: {SkillName(BattleSkill.Fan)}. Click this slot, then hold the mouse on the floor.")),
        // 1.5 M06 · the same for the Grabber, the Scissors and the Spray.
        new(GuideGrabber, GuideTeaches + "grabber", true, true, 1,
            () => ToolDue(BattleSkill.Grabber), () => battle!.GrabberBusy || battle.Cooldown(BattleSkill.Grabber) > 0,
            () => battle!.Armed == BattleSkill.Grabber ? Spots(FloorSpot()) : Spots(SkillSpot(BattleSkill.Grabber)),
            () => battle?.Armed == BattleSkill.Grabber
                ? L("Giờ nhấn giữ một slime, vung chuột về phía slime khác rồi thả ra.", "Now press a slime and fling the mouse toward another slime, then let go.")
                : LF($"Kỹ năng mới: {SkillName(BattleSkill.Grabber)}. Bấm ô này, rồi nhấn giữ một slime nhỏ.", $"A new skill: {SkillName(BattleSkill.Grabber)}. Click this slot, then press a small slime.")),
        new(GuideScissors, GuideTeaches + "scissors", true, true, 1,
            () => ToolDue(BattleSkill.Scissors), () => battle!.Cooldown(BattleSkill.Scissors) > 0,
            () => battle!.Armed == BattleSkill.Scissors ? Spots(FloorSpot()) : Spots(SkillSpot(BattleSkill.Scissors)),
            () => battle?.Armed == BattleSkill.Scissors
                ? L("Giờ kéo chuột thành một nét cắt ngang đám slime (hoặc sợi dây giữa chúng).", "Now drag a stroke across the slimes (or the rope between them).")
                : LF($"Kỹ năng mới: {SkillName(BattleSkill.Scissors)}. Bấm ô này, rồi kéo chuột thành một nét trên sàn.", $"A new skill: {SkillName(BattleSkill.Scissors)}. Click this slot, then drag a stroke across the floor.")),
        new(GuideSpray, GuideTeaches + "spray", true, true, 1,
            () => ToolDue(BattleSkill.Spray), () => battle!.Spraying,
            () => battle!.Armed == BattleSkill.Spray ? Spots(FloorSpot()) : Spots(SkillSpot(BattleSkill.Spray)),
            () => battle?.Armed == BattleSkill.Spray
                ? L("Giờ giữ chuột cạnh đám slime: slime ướt sẽ chậm lại. Vòng trên ô là nước đã dùng.", "Now hold the mouse next to the slimes: wet ones slow down. The sweep on the slot is the water used.")
                : LF($"Kỹ năng mới: {SkillName(BattleSkill.Spray)}. Bấm ô này, rồi giữ chuột trên sàn.", $"A new skill: {SkillName(BattleSkill.Spray)}. Click this slot, then hold the mouse on the floor.")),
    };

    // A skill to teach: carried, ready (or chosen) and slimes on the floor to use it on.
    private bool ToolDue(BattleSkill skill) => battle!.Carries(skill) && TourDue() && !BattleHoldsOff
        && (battle.Ready(skill) || battle.Armed == skill) && battle.Slimes.Any(s => s.Targetable && s.State != SlimeState.Spawning);
    // The floor around the cat (where to click or hold).
    private Rect? FloorSpot() => battleView == null ? null : FieldSpot(battleView.FloorBounds(CatX(), 180), 2);

    private GuideStep See(string id, string stage, Func<bool> due, Func<List<Rect>> spots, Func<string> words) =>
        new(id, stage, false, true, 1, due, () => false, spots, words);
    // A skill of the HUD tour: its name and the data's own description.
    private GuideStep SkillSee(string id, BattleSkill skill) => See(id, StageBreakThrough, () => TourDue(), () => Spots(SkillSpot(skill)),
        () => SkillName(skill) + ": " + BattleSkills.Get(skill).Describe(battle?.Settings ?? BattleTuning, preferences.Language));

    private int GuideTimes(string id) => guideShown.GetValueOrDefault(id);
    private bool TourDue() => battle!.Time >= 0.6 && battle.Phase == BattlePhase.Fighting;
    private bool CardsDue() => battle!.ChoosingUpgrade && upgradeWindow?.IsVisible == true;
    private Slime? King() => battle!.Slimes.FirstOrDefault(s => s.IsBoss && s.Targetable);
    private bool LiftDue(bool quick)
    {
        if (BattleHoldsOff || battle!.CatStunned) return false;
        var hitter = battle.Slimes.FirstOrDefault(s => s.State is SlimeState.Windup && !s.Type.Support && !s.IsBoss && s.Type.Dash == quick
            && Math.Abs(s.X - CatX()) <= s.Range + (s.Type.Dash ? s.Type.DashDistance : 40));
        guideRef = hitter?.Id ?? 0;
        return hitter != null;
    }
    private List<Rect> LiftSpots() => battle!.Find(guideRef) is { State: SlimeState.Windup } hitter ? Spots(CatSpot(), SlimeSpot(hitter)) : new();
    // A step about malware waits until it has finished appearing (the battle stands still during the
    // step, so a half-grown file or a wall still dropping would stay so): the views' appear times.
    private const double PopupShown = 0.3, HazardShown = 0.35, VirusShown = 0.4, InfectedShown = 0.65;
    private bool HazardDue(HazardKind kind)
    {
        var hazard = battle!.Hazards.FirstOrDefault(h => h.Kind == kind && h.Age >= HazardShown);
        guideRef = hazard?.Id ?? 0;
        guideRefValue = hazard?.ClicksLeft ?? 0;
        return hazard != null;
    }

    private double CatX() => pet.X + settings.Width / 2;
    private string SkillName(BattleSkill skill) => BattleSkills.Get(skill).Name(preferences.Language, battle?.Settings ?? BattleTuning);
    private static List<Rect> Spots(params Rect?[] spots) => spots.Where(s => s != null).Select(s => s!.Value).ToList();

    // Every lit spot is measured where the thing is really shown: a rectangle of an element (its own
    // DIPs) through the screen, then in the battle monitor's DIPs, with the same padding all round.
    // Margins, a dragged HUD, the cat's own window and a monitor's DPI all come out right.
    public const double GuidePad = 6;
    private Rect? ScreenSpot(UIElement? element, Rect local, double pad = GuidePad)
    {
        if (element == null || !element.IsVisible || PresentationSource.FromVisual(element) == null) return null;
        var a = element.PointToScreen(local.TopLeft);
        var b = element.PointToScreen(local.BottomRight);
        return new Rect(Math.Min(a.X, b.X) / battleScale - pad, Math.Min(a.Y, b.Y) / battleScale - pad,
            Math.Abs(b.X - a.X) / battleScale + pad * 2, Math.Abs(b.Y - a.Y) / battleScale + pad * 2);
    }
    private Rect? Whole(FrameworkElement? element, double pad = GuidePad) => element == null ? null : ScreenSpot(element, new Rect(element.RenderSize), pad);
    private Rect? FieldSpot(Rect? local, double pad = GuidePad) => local is Rect r ? ScreenSpot(battleView, r, pad) : null;
    // The drawn cat (head to paws, centred on its body), not its whole window: the 112×112 frame sits
    // PickupAnimation.Padding in from the window's corner (room for the pickup's swing).
    private Rect? CatSpot() => ScreenSpot(this, new Rect(PickupAnimation.Padding + PetSettings.CenterX - 42, PickupAnimation.Padding + PetSettings.HeadTop - 4, 84, 112 - PetSettings.HeadTop));
    private Rect? SlimeSpot(Slime s) => battleView == null ? null : FieldSpot(battleView.SlimeBounds(s));
    private Rect? SlamSpot(Slime king) => battleView == null ? null : FieldSpot(battleView.SlamBounds(king), 2);
    private Rect? PopupSpot(BattlePopup ad) => FieldSpot(battleView?.PopupBounds(ad.Id), 4);
    private Rect AppSpot(DesktopApp app) { var c = BattleAppCentre(app); return new(c.X - DesktopAppIcon.TileSize / 2 - GuidePad, c.Y - DesktopAppIcon.TileSize / 2 - GuidePad, DesktopAppIcon.TileSize + GuidePad * 2, DesktopAppIcon.TileSize + GuidePad * 2); }
    private Rect VirusSpot(VirusFile file)
    {
        var cell = AppCellTopLeft(file.Col, file.Row);
        double half = DesktopAppIcon.TileSize / 2 + GuidePad;
        return new(cell.X + DesktopAppIcon.Centre.X - half, cell.Y + DesktopAppIcon.Centre.Y - half, half * 2, half * 2);
    }
    private Rect? SkillSpot(BattleSkill skill) => Whole(battleHud?.Slot(skill));
    // A part of the HUD (null while it is not shown).
    private Rect? HudPart(FrameworkElement? part) => Whole(part);
    // The skill bar: exactly its five slots, not the bar's margins.
    private Rect? SkillBarSpot()
    {
        if (battleHud == null) return null;
        var slots = battleHud.SkillOrder.Select(k => Whole(battleHud.Slot(k), 0)).Where(r => r != null).Select(r => r!.Value).ToList();
        if (slots.Count == 0) return null;
        var all = slots.Aggregate(Rect.Union);
        all.Inflate(GuidePad, GuidePad);
        return all;
    }
    // The card offer's panel (not its window's shadow margin), and the first card's group line.
    private Rect? UpgradeSpot() => upgradeWindow is { IsVisible: true, Content: FrameworkElement panel } ? Whole(panel, 2) : null;
    private Rect? UpgradeBuildSpot()
    {
        if (upgradeWindow is not { IsVisible: true } || upgradeCards.Count == 0) return null;
        var line = upgradeCards[0].Build;
        return line.IsVisible ? Whole(line, 4) : UpgradeSpot();
    }
    public IReadOnlyList<Rect> BattleGuideSpots => guideStep?.Spots() ?? new List<Rect>();

    // The guide runs in a chapter 1 Adventure stage (not the Expedition's encounters, not Infinity).
    private bool GuideFits => BattleGuideEnabled && battle is { Over: false, Stage: { Endless: false } stage } && battleExpedition == null && StageHasGuide(stage);

    // Every battle frame (real time), after the battle's step.
    private void TickBattleGuide()
    {
        if (battle == null || battle.Over) { EndGuide(); return; }
        if (guideBattle != battle)
        {
            EndGuide();
            guideBattle = battle;
            guideShown.Clear();
            guideOn = GuideFits && (guideChoice ?? preferences.AdventureGuide);
            guideChoice = null;
            if (guideOn) System.Diagnostics.Trace.WriteLine($"Adventure Guide on for {battle.Stage!.Id}.");
        }
        if (!guideOn) return;
        var b = battle;
        // The card steps show over the card offer's own pause; every other pause hides the guide.
        bool overCards = battlePause == BattlePause.Upgrade;
        if (guideStep != null)
        {
            bool cards = guideStep.Id is GuideCards or GuideSynergy;
            if ((b.Paused && !(cards && overCards)) || context.Fullscreen) { HideGuide(); return; }
            var spots = guideStep.Spots();
            if ((guideStep.Do || cards ? guideStep.Done() : false) || guideOk || spots.Count == 0) { FinishGuideStep(); return; }
            ShowGuideStep(spots);
            return;
        }
        if (context.Fullscreen || battleTutorialWindow != null) return;
        if (b.Paused && !(overCards && b.ChoosingUpgrade)) return;
        foreach (var step in GuideSteps)
        {
            if (!GuideFor(step, b.Stage!) || GuideTimes(step.Id) >= step.Times) continue;
            bool cards = step.Id is GuideCards or GuideSynergy;
            if (b.Paused != cards || !step.Due()) continue;
            StartGuideStep(step);
            return;
        }
    }

    private void StartGuideStep(GuideStep step)
    {
        guideStep = step;
        guideOk = false;
        guideShown[step.Id] = GuideTimes(step.Id) + 1;
        var spots = step.Spots();
        if (spots.Count == 0) { FinishGuideStep(); return; }
        ShowGuideStep(spots, fresh: true);
        System.Diagnostics.Trace.WriteLine($"Adventure Guide: {step.Id}.");
    }

    private void FinishGuideStep()
    {
        guideStep = null;
        guideOk = false;
        HideGuide();
    }

    // Got it (a see step), or the user skips the guide for the rest of this run.
    public void BattleGuideOk() { if (guideStep is { Ok: true }) guideOk = true; }
    public void SkipBattleGuide()
    {
        if (!guideOn) return;
        guideOn = false;
        FinishGuideStep();
        System.Diagnostics.Trace.WriteLine("Adventure Guide skipped.");
    }

    // The battle ended or the guide is off: everything goes, nothing is kept.
    private void EndGuide()
    {
        guideStep = null;
        guideOk = false;
        guideShadeWindow?.Close(); guideBubbleWindow?.Close();
        guideShadeWindow = guideBubbleWindow = null;
        guideShade = null;
        guidePlaced = default;
    }

    private void HideGuide()
    {
        if (guideShadeWindow?.IsVisible == true) guideShadeWindow.Hide();
        if (guideBubbleWindow?.IsVisible == true) guideBubbleWindow.Hide();
    }

    // The dark shade over the battle's monitor with the spots cut out, and the line beside them.
    private void ShowGuideStep(List<Rect> spots, bool fresh = false)
    {
        var area = battleArea;
        if (guideShadeWindow == null)
        {
            guideShade = new GuideShade();
            guideShadeWindow = CreateOverlayWindow(guideShade, "Pet guide shade", clickThrough: true);
            guideShadeWindow.DpiChanged += (_, _) => guidePlaced = default;
        }
        if (guideBubbleWindow == null) guideBubbleWindow = BuildGuideBubble();
        if (!guideShadeWindow.IsVisible) { guideShadeWindow.Show(); guidePlaced = default; }
        if (guidePlaced != (area, battleScale))
        {
            guidePlaced = (area, battleScale);
            const uint NoActivate = 0x0010;
            SetWindowPos(new WindowInteropHelper(guideShadeWindow).Handle, new IntPtr(-1), (int)Math.Round(area.Left * battleScale), (int)Math.Round(area.Top * battleScale),
                (int)Math.Round((area.Right - area.Left) * battleScale), (int)Math.Round((area.Bottom - area.Top) * battleScale), NoActivate);
        }
        var holes = spots.Select(r => new Rect(r.X - area.Left, r.Y - area.Top, r.Width, r.Height)).ToList();
        if (fresh || !guideShade!.Holes.SequenceEqual(holes)) { guideShade!.Holes = holes; guideShade.InvalidateVisual(); }
        string words = guideStep!.Words();
        if (guideText!.Text != words) guideText.Text = words;
        guideOkButton!.Visibility = guideStep.Ok ? Visibility.Visible : Visibility.Collapsed;
        guideOkText!.Text = L("Hiểu rồi", "Got it");
        guideSkipText!.Text = L("Bỏ qua hướng dẫn", "Skip the guide");
        if (!guideBubbleWindow.IsVisible) guideBubbleWindow.Show();
        if (fresh) { var raise = new WindowInteropHelper(guideBubbleWindow).Handle; SetWindowPos(raise, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); }
        guideBubbleWindow.UpdateLayout();
        // Under the spots when there is room, otherwise over them; centred on them (pushed aside only
        // at the monitor's edge).
        var lit = spots.Aggregate(Rect.Union);
        double w = guideBubbleWindow.ActualWidth, h = guideBubbleWindow.ActualHeight;
        double y = lit.Bottom + 10 + h <= area.Bottom ? lit.Bottom + 10 : lit.Top - 10 - h >= area.Top ? lit.Top - 10 - h : area.Top + 10;
        double x = Math.Clamp(lit.Left + lit.Width / 2 - w / 2, area.Left + 6, Math.Max(area.Left + 6, area.Right - w - 6));
        MoveWindow(guideBubbleWindow, x, y, battleScale);
    }

    private Window BuildGuideBubble()
    {
        guideText = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
        var (ok, okText, _) = HudButton(BattleGuideOk);
        var (skip, skipText, _) = HudButton(SkipBattleGuide, plain: true);
        guideOkButton = ok; guideOkText = okText; guideSkipText = skipText;
        skip.Margin = new Thickness(0, 0, 8, 0);
        var panel = new Border
        {
            Background = HudBack, BorderBrush = GuideShade.Gold, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(6),
            Child = new StackPanel { Children = { guideText, new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { skip, ok } } } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.5 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateOverlayWindow(panel, "Pet guide");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        return window;
    }
}

// The guide's shade: the battle's monitor darkened, the lit spots cut out with a gold rim. Drawn only;
// its window lets every click through, so the lit things are clicked (and the cat lifted) as usual.
public sealed class GuideShade : FrameworkElement
{
    public List<Rect> Holes { get; set; } = new();
    public static readonly Brush Gold = PetUi.Frozen(Color.FromRgb(0xFF, 0xD2, 0x3F));
    private static readonly Brush Dark = PetUi.Frozen(Color.FromArgb(0xA8, 0x08, 0x08, 0x14));
    private static readonly Pen Rim = PetUi.FrozenPen(Gold, 3);

    protected override void OnRender(DrawingContext dc)
    {
        Geometry shade = new RectangleGeometry(new Rect(RenderSize));
        foreach (var hole in Holes) shade = new CombinedGeometry(GeometryCombineMode.Exclude, shade, new RectangleGeometry(hole, 10, 10));
        dc.DrawGeometry(Dark, null, shade);
        foreach (var hole in Holes) dc.DrawRoundedRectangle(null, Rim, hole, 10, 10);
    }
}
