using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DesktopPet;

// Battle Mode tuning: every number the fight uses (DIPs and seconds). The app reads them from
// Assets/battle.json (BattleData); these defaults are the same numbers and are used when the
// file is broken and by the tests. Enemy kinds and waves come from "enemies"/"waves" in the
// file; without them, from the Slime*/Boss* numbers, Waves and Boss below. Weapons come from
// "weapons"; without it, from the Cat* numbers (the sword) and "hammer".
public sealed class BattleSettings
{
    // The data layout this build reads (BattleData.SchemaVersion); a file for another layout is rejected.
    public int SchemaVersion { get; init; } = 1;
    public double CatMaxHp { get; init; } = 100;
    public double CatDamage { get; init; } = 25;
    // Centre-to-centre distance at which the sword reaches a slime.
    public double CatAttackRange { get; init; } = 76;
    // From the start of one swing to the next (at least the swing itself).
    public double CatAttackCooldown { get; init; } = 0.75;
    public double CatSwingDuration { get; init; } = 0.36;
    // The one moment in a swing that can deal damage.
    public double CatHitTime { get; init; } = 0.16;
    // The cat runs during a battle: its walking speed and acceleration times this.
    public double CatRunScale { get; init; } = 1.8;
    // Combo: ComboHits hits in a row, each within ComboWindow seconds of the last, make the last
    // one a combo hit (damage × ComboDamage, knocks the slime back). A miss or lifting the cat ends the chain.
    public int ComboHits { get; init; } = 3;
    public double ComboWindow { get; init; } = 1.6;
    public double ComboDamage { get; init; } = 2;
    public double ComboKnock { get; init; } = 300;
    [JsonIgnore]
    public double SlimeMaxHp { get; init; } = 50;
    [JsonIgnore]
    public double SlimeSpeed { get; init; } = 34;
    [JsonIgnore]
    public double SlimeDamage { get; init; } = 10;
    [JsonIgnore]
    public double SlimeAttackRange { get; init; } = 64;
    // The slime swells and shows "!" this long before its hit: time for the user to help.
    [JsonIgnore]
    public double SlimeWindup { get; init; } = 0.8;
    [JsonIgnore]
    public double SlimeAttackCooldown { get; init; } = 1.8;
    [JsonIgnore]
    public double SlimeRecover { get; init; } = 0.35;
    public double SlimeWidth { get; init; } = 46;
    public double SlimeHeight { get; init; } = 34;
    public double SpawnDuration { get; init; } = 0.5;
    public double DefeatDuration { get; init; } = 0.6;
    // Slimes per wave. With Boss on, one more wave follows with the Slime King alone.
    [JsonIgnore]
    public int[] Waves { get; init; } = { 2, 3, 4 };
    [JsonIgnore]
    public bool Boss { get; init; } = true;
    public int MaxAlive { get; init; } = 3;
    public double SpawnInterval { get; init; } = 1.6;
    public double FirstWaveDelay { get; init; } = 1.5;
    public double WaveBreakSeconds { get; init; } = 3.5;
    // HP the cat gets back after each wave except the last.
    public double WaveHeal { get; init; } = 15;
    // New slimes appear at least this far from the cat (centre to centre) when room allows.
    public double SpawnMinDistance { get; init; } = 200;
    // After the user puts the cat down: no damage and no new slime attacks on it.
    public double LandingGraceSeconds { get; init; } = 1.5;
    // That protection is given at most once per this many seconds of the cat on the ground in a
    // running battle (counted from when it is given), so lifting and dropping over and over
    // cannot keep the cat safe.
    public double RescueGraceCooldownSeconds { get; init; } = 5;
    // ...and only after a lift this long (a quick lift-and-drop on the spot earns none).
    public double RescueGraceMinLiftSeconds { get; init; } = 0.3;
    // A dashing enemy that runs into the cat stuns it this long (it stands still and does not
    // swing; the user may still lift and throw it). Afterwards it cannot be stunned again for
    // StunImmunitySeconds, so stuns never chain into a lock.
    public double StunDurationSeconds { get; init; } = 1.5;
    public double StunImmunitySeconds { get; init; } = 3;
    // Attack turns: at most MaxAttackers enemies wind up or dash at once (0: no limit), and a new
    // windup starts at least AttackStagger s after the last one, so hits never all land in one frame.
    public int MaxAttackers { get; init; } = 2;
    public double AttackStagger { get; init; } = 0.3;
    // A spawn from the other side of a two-sided wave is announced by an arrow this long before.
    public double IncomingWarning { get; init; } = 1;
    // Elites alive at once (StageSpec.EliteChance rolls them), and the chance a rare card takes its
    // group's place in an upgrade offer.
    public int MaxElites { get; init; } = 1;
    public double RareChance { get; init; } = 0.15;
    public double OutroSeconds { get; init; } = 3;
    public double HurtFlashSeconds { get; init; } = 0.35;
    public int MaxEffects { get; init; } = 32;

    // The user's skills.
    public double PushCooldown { get; init; } = 5;
    public double PushSpeed { get; init; } = 520;
    public double PushStunSeconds { get; init; } = 1.4;
    public double HealCooldown { get; init; } = 20;
    public double HealAmount { get; init; } = 25;
    public double CheerCooldown { get; init; } = 18;
    public double CheerSeconds { get; init; } = 5;
    public double CheerSpeed { get; init; } = 1.6;
    public double CheerDamage { get; init; } = 1.4;
    public double BombCooldown { get; init; } = 12;
    public double BombDelay { get; init; } = 0.5;
    public double BombDamage { get; init; } = 30;
    public double BombRadius { get; init; } = 70;
    public double ShieldCooldown { get; init; } = 15;
    public double ShieldSeconds { get; init; } = 3;

    // The Slime King (last wave).
    [JsonIgnore]
    public double BossMaxHp { get; init; } = 400;
    [JsonIgnore]
    public double BossScale { get; init; } = 2;
    [JsonIgnore]
    public double BossSpeed { get; init; } = 22;
    [JsonIgnore]
    public double BossDamage { get; init; } = 25;
    // The ground slam reaches this far from the boss's centre.
    [JsonIgnore]
    public double BossRange { get; init; } = 110;
    [JsonIgnore]
    public double BossWindup { get; init; } = 1.2;
    [JsonIgnore]
    public double BossAttackCooldown { get; init; } = 2.6;
    // Below these HP fractions: calls two helpers (once), then gets angry (faster, shorter windup).
    [JsonIgnore]
    public double BossSummonAt { get; init; } = 0.5;
    [JsonIgnore]
    public int BossSummonCount { get; init; } = 2;
    [JsonIgnore]
    public double BossEnrageAt { get; init; } = 0.3;
    [JsonIgnore]
    public double BossEnragedSpeed { get; init; } = 32;
    [JsonIgnore]
    public double BossEnragedWindup { get; init; } = 0.8;
    // Heavy: pushes move it and stun it less.
    [JsonIgnore]
    public double BossPushFactor { get; init; } = 0.35;
    [JsonIgnore]
    public double BossStunFactor { get; init; } = 0.4;

    // Difficulty: Easy and Hard scale every enemy's HP, damage and speed; Normal is the numbers above.
    // Threat (an Adventure stage's and wave's): enemy HP × threat, damage × only this share of the
    // rise (damage grows slower); worked out once when an enemy appears. Never stun times.
    public double ThreatDamageShare { get; init; } = 0.5;
    public DifficultySpec Easy { get; init; } = new() { EnemyHp = 0.65, EnemyDamage = 0.5, EnemySpeed = 0.85 };
    public DifficultySpec Hard { get; init; } = new() { EnemyHp = 1.3, EnemyDamage = 1.4, EnemySpeed = 1.15 };
    private static readonly DifficultySpec NormalSpec = new();
    public DifficultySpec Difficulty(BattleDifficulty difficulty) => difficulty switch
    {
        BattleDifficulty.Easy => Easy, BattleDifficulty.Hard => Hard, _ => NormalSpec
    };

    public double SkillCooldown(BattleSkill skill) => BattleSkills.Get(skill).Cooldown(this);

    // The cat's weapons, in the order the battle setup panel shows them. battle.json "weapons"
    // lists them; without it they are the sword (the Cat* numbers above: fast, only a combo
    // knocks back) and the hammer ("hammer": slow, heavy, knocks the slime back on every hit).
    public WeaponSpec Hammer { get; init; } = new();
    [JsonPropertyName("weapons")] public WeaponSpec[]? Weapons { get; init; }
    private IReadOnlyList<WeaponSpec>? weaponList;
    [JsonIgnore] public IReadOnlyList<WeaponSpec> WeaponList => weaponList ??= Weapons ?? new[]
    {
        new WeaponSpec { Id = BattleWeapon.Sword, NameVi = "Kiếm", NameEn = "Sword", HintVi = "nhanh, dễ nối combo", HintEn = "fast, easy combos", Style = "sword", HitEffect = "spark",
            Damage = CatDamage, Range = CatAttackRange, AttackCooldown = CatAttackCooldown, SwingDuration = CatSwingDuration, HitTime = CatHitTime, Knock = 0 },
        Hammer with { Id = BattleWeapon.Hammer, NameVi = "Búa", NameEn = "Hammer", HintVi = "chậm, mạnh, đẩy lùi mỗi nhát", HintEn = "slow, heavy, knocks back every hit", Style = "hammer", HitEffect = Hammer.HitEffect ?? "thud" }
    };
    // Null when no weapon has this id (ids ignore case, so old preferences' "Hammer" still match).
    public WeaponSpec? FindWeapon(string? id)
    {
        foreach (var w in WeaponList) if (string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase)) return w;
        return null;
    }
    // Unknown ids (an old choice no longer in the data) fall back to the first weapon.
    public WeaponSpec Weapon(string? id) => FindWeapon(id) ?? WeaponList[0];

    // From battle.json: every enemy kind and the waves (null: the built-in slime and king).
    [JsonPropertyName("enemies")] public EnemyType[]? Enemies { get; init; }
    [JsonPropertyName("waves")] public WaveSpec[]? Plan { get; init; }

    private IReadOnlyList<EnemyType>? enemyTypes;
    private IReadOnlyList<WaveSpec>? wavePlan;
    [JsonIgnore] public IReadOnlyList<EnemyType> EnemyTypes => enemyTypes ??= Enemies ?? new[] { BuiltInSlime(), BuiltInKing() };
    [JsonIgnore] public IReadOnlyList<WaveSpec> WavePlan => wavePlan ??= Plan ?? BuiltInPlan();
    // Unknown ids fall back to the first kind (validation rejects them in battle.json).
    public EnemyType Enemy(string id)
    {
        foreach (var e in EnemyTypes) if (e.Id == id) return e;
        return EnemyTypes[0];
    }
    [JsonIgnore] public double LargestScale => EnemyTypes.Max(e => e.Scale);

    // From battle.json "upgrades": the Adventure's between-wave upgrades (null: the built-in seven).
    [JsonPropertyName("upgrades")] public UpgradeSpec[]? Upgrades { get; init; }
    private IReadOnlyList<UpgradeSpec>? upgradeList;
    [JsonIgnore] public IReadOnlyList<UpgradeSpec> UpgradeList => upgradeList ??= Upgrades ?? new[]
    {
        new UpgradeSpec { Id = "sharp", Group = "power", NameVi = "Sắc bén", NameEn = "Sharp edge", DescVi = "+20% sát thương", DescEn = "+20% damage", Effect = "damage", Amount = 0.2, MaxStacks = 3 },
        new UpgradeSpec { Id = "quick_feet", Group = "speed", NameVi = "Nhanh chân", NameEn = "Quick feet", DescVi = "+15% tốc độ chạy", DescEn = "+15% running speed", Effect = "moveSpeed", Amount = 0.15, MaxStacks = 2 },
        new UpgradeSpec { Id = "fast_paws", Group = "speed", NameVi = "Ra đòn nhanh", NameEn = "Fast paws", DescVi = "−12% thời gian hồi đòn", DescEn = "−12% time between swings", Effect = "attackSpeed", Amount = 0.12, MaxStacks = 3 },
        new UpgradeSpec { Id = "heavy_push", Group = "power", NameVi = "Đẩy mạnh", NameEn = "Heavy push", DescVi = "Mỗi nhát đẩy slime lùi thêm", DescEn = "Every hit knocks slimes further", Effect = "knock", Amount = 80, MaxStacks = 2 },
        new UpgradeSpec { Id = "grilled_fish", NameVi = "Cá nướng", NameEn = "Grilled fish", DescVi = "Hồi ngay 30 HP", DescEn = "Get 30 HP back now", Effect = "heal", Amount = 30 },
        new UpgradeSpec { Id = "thick_fur", Group = "defense", NameVi = "Lông dày", NameEn = "Thick fur", DescVi = "−15% sát thương nhận vào", DescEn = "−15% damage taken", Effect = "armor", Amount = 0.15, MaxStacks = 2 },
        new UpgradeSpec { Id = "big_heart", Group = "defense", NameVi = "Tim to", NameEn = "Big heart", DescVi = "+20 HP tối đa (hồi luôn 20)", DescEn = "+20 max HP (and 20 HP now)", Effect = "maxHp", Amount = 20, MaxStacks = 2 },
        new UpgradeSpec { Id = "turtle_shell", Group = "defense", Rare = true, NameVi = "Mai rùa", NameEn = "Turtle shell", DescVi = "Chặn trọn 1 đòn, mọc lại sau 12 giây", DescEn = "Stops one whole hit, grows back in 12 s", Effect = "shell", Amount = 12, MaxStacks = 1 },
        new UpgradeSpec { Id = "opening_strike", Group = "power", Rare = true, NameVi = "Đòn mở màn", NameEn = "Opening strike", DescVi = "Đòn đầu trúng mỗi quái ×2 sát thương", DescEn = "First hit on each enemy deals ×2", Effect = "opener", Amount = 1, MaxStacks = 1 },
        new UpgradeSpec { Id = "running_start", Group = "speed", Rare = true, NameVi = "Chạy đà", NameEn = "Running start", DescVi = "Chạy một quãng rồi chém: +80% sát thương", DescEn = "Swing after a run: +80% damage", Effect = "momentum", Amount = 0.8, MaxStacks = 1 },
    };

    // From battle.json "synergies": what 3 points of a group switch on (null: the built-in three).
    [JsonPropertyName("synergies")] public SynergySpec[]? SynergiesData { get; init; }
    private IReadOnlyList<SynergySpec>? synergyList;
    [JsonIgnore] public IReadOnlyList<SynergySpec> SynergyList => synergyList ??= SynergiesData ?? new[]
    {
        new SynergySpec { Id = "thorns", Group = "defense", At = 3, Effect = "thorns", Amount = 0.35, NameVi = "Giáp gai", NameEn = "Thorns",
            DescVi = "Quái đánh trúng mèo nhận lại 35% sát thương", DescEn = "Enemies that hit the cat take 35% of it back" },
        new SynergySpec { Id = "armor_break", Group = "power", At = 3, Effect = "armorBreak", Amount = 0.3, NameVi = "Phá giáp", NameEn = "Armor break",
            DescVi = "Đòn combo +30% sát thương, xuyên giáp đá", DescEn = "Combo hits deal 30% more and pierce stone" },
        new SynergySpec { Id = "afterimage", Group = "speed", At = 3, Effect = "echo", Amount = 0.5, Every = 4, NameVi = "Dư ảnh", NameEn = "Afterimage",
            DescVi = "Cứ 4 đòn trúng, thêm một đòn 50%", DescEn = "Every 4th hit strikes again for 50%" },
    };

    // From battle.json "campaign": the Adventure chapters (null: no Adventure in this data).
    [JsonPropertyName("campaign")] public CampaignSpec? Campaign { get; init; }
    // From battle.json "expedition": the Expedition run (null: none in this data). PetExpedition.cs.
    [JsonPropertyName("expedition")] public ExpeditionSpec? Expedition { get; init; }

    private EnemyType BuiltInSlime() => new()
    {
        Id = "slime", NameVi = "Slime", NameEn = "Slime", MaxHp = SlimeMaxHp, Speed = SlimeSpeed, Damage = SlimeDamage,
        Range = SlimeAttackRange, Windup = SlimeWindup, AttackCooldown = SlimeAttackCooldown, Recover = SlimeRecover, Color = "#7CD36A"
    };

    private EnemyType BuiltInKing() => new()
    {
        Id = "king", NameVi = "Vua Slime", NameEn = "Slime King", MaxHp = BossMaxHp, Speed = BossSpeed, Damage = BossDamage,
        Range = BossRange, Windup = BossWindup, AttackCooldown = BossAttackCooldown, Recover = SlimeRecover * 2, Scale = BossScale,
        Color = "#9B7BE0", AngryColor = "#E86A6A", Boss = true, Slam = true, PushFactor = BossPushFactor, StunFactor = BossStunFactor,
        SummonAt = BossSummonAt, SummonCount = BossSummonCount, SummonType = "slime",
        EnrageAt = BossEnrageAt, EnragedSpeed = BossEnragedSpeed, EnragedWindup = BossEnragedWindup
    };

    private IReadOnlyList<WaveSpec> BuiltInPlan()
    {
        var plan = Waves.Select(count => new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = count } } }).ToList();
        if (Boss) plan.Add(new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "king", Count = 1 } } });
        return plan;
    }
}

public enum BattleDifficulty { Easy, Normal, Hard }

// The ids of the two built-in weapons. Weapons are data (battle.json "weapons"), chosen by id.
public static class BattleWeapon
{
    public const string Sword = "sword";
    public const string Hammer = "hammer";
}

// One weapon (a definition shared by every battle that uses it; the swing and its cooldown live
// in BattleSim). Numbers in DIPs and seconds. Knock: DIPs/s every hit sends the slime away from
// the cat (× the enemy's pushFactor), not a stun; a combo hit uses ComboKnock instead if larger.
// Style picks a drawing the renderer knows ("sword" or "hammer"). The number defaults are the
// hammer's (battle.json "hammer" may leave keys out); the sword is built from the Cat* numbers.
public sealed record WeaponSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string HintVi { get; init; } = "";
    public string HintEn { get; init; } = "";
    public string Style { get; init; } = "sword";
    // The mark a plain hit leaves: "spark" (a quick white burst) or "thud" (dust and a ring on
    // the floor, for heavy weapons). Unset: "thud" for the hammer style, "spark" otherwise.
    public string? HitEffect { get; init; }
    public string Name(bool english) => (english ? NameEn : NameVi) is { Length: > 0 } name ? name : Id;
    [JsonIgnore] public string Effect => HitEffect ?? (Style == "hammer" ? "thud" : "spark");
    // Where in the swing the hit lands (0–1): the drawing times its slash to this moment.
    [JsonIgnore] public double HitFraction => SwingDuration > 0 ? Math.Clamp(HitTime / SwingDuration, 0.05, 0.95) : 0.45;
    public string Hint(bool english) => english ? HintEn : HintVi;
    public double Damage { get; init; } = 45;
    public double Range { get; init; } = 76;
    public double AttackCooldown { get; init; } = 1.35;
    public double SwingDuration { get; init; } = 0.7;
    public double HitTime { get; init; } = 0.45;
    public double Knock { get; init; } = 170;
}

// Multipliers on every enemy kind (1 = the kind's own numbers).
public sealed class DifficultySpec
{
    public double EnemyHp { get; init; } = 1;
    public double EnemyDamage { get; init; } = 1;
    public double EnemySpeed { get; init; } = 1;
}

// Push and Bomb are aimed (choose, then click a slime); the others act at once.
public enum BattleSkill { Push, Heal, Cheer, Bomb, Shield }

// Casting: a support enemy winds up a heal (a green "+"), near or far; a push or stun cancels it.
// Dashing: a dash enemy's lunge after its windup (it hits the cat once at most).
public enum SlimeState { Spawning, Moving, Windup, Recover, Stunned, Defeated, Casting, Dashing }

// Where damage on an enemy comes from: the cat's swings (a plain hit or a combo), the yarn bomb,
// or a build bonus (Thorns sends back what the cat took, Afterimage repeats a swing). The bonus
// hits never set off another bonus.
public enum DamageSource { Weapon, Combo, Bomb, Reflect, Echo }

// What a hit on an enemy did: landed, blocked by a stony enemy, or nothing on a shielded boss.
public enum HitResult { Landed, Blocked, Immune }

public sealed class Slime
{
    public int Id { get; init; }
    // Its kind (battle.json "enemies"): abilities and the numbers not copied below.
    public EnemyType Type { get; init; } = new();
    public bool IsBoss => Type.Boss;
    // Centre of the slime on the battle floor (DIPs, screen coordinates).
    public double X;
    public double Hp, MaxHp;
    // Drawn size (DIPs). The combat hitbox (sword reach, bomb blast) is HitWidth.
    public double Width { get; init; }
    public double HitWidth { get; init; }
    public double Height { get; init; }
    // Settable: a boss's rally call makes a helper elite (more HP, harder hits).
    public double Damage { get; set; }
    public double Range { get; init; }
    public SlimeState State { get; private set; }
    public double StateTime;
    public double Cooldown, StunRemaining, KnockVelocity, HitFlash;
    public int Facing = -1;
    public bool Enraged, Summoned;
    // Support: its own heal cooldown, what it may still heal, and the ally it is healing.
    public double HealCooldown, HealLeft;
    public int HealTargetId;
    // Boss phases already fired (bit i = Phases[i]); the boss that called this helper (0: none).
    public int PhasesDone;
    public int SummonedBy;
    // Dash: the way it lunges (fixed when the windup starts, so the warning shows the real lane),
    // the distance still to go and whether this dash already hit the cat.
    public int DashDirection;
    public double DashLeft;
    public bool DashHit;
    // Block: how many hits in a row this one has blocked.
    public int BlockStreak;
    // A support falling back behind a fighting ally (it may then pass through other slimes).
    public bool KeepingBack;
    // Boss shield phase: the shield is up (no damage), it is still running to the far edge (side:
    // +1 right, -1 left; RetreatTime since the phase began) with the helpers it will call there.
    // ShieldTag tells this phase's helpers (their PhaseTag) from any other helper.
    public bool Shielded, Retreating;
    public int RetreatSide, ShieldTag, PhaseTag;
    public double RetreatTime;
    public List<string>? PendingCalls;
    // A leaping boss (RetreatLeap): how deep it crouches (0–1), how high it flies (DIPs over the floor)
    // and where it took off. Its helpers still to come and the warning time left (SummonWarning).
    public double Crouch, LeapHeight, LeapFromX;
    public List<(string Kind, double X)>? CallSpots;
    public double SummonWarn;
    // The helper its rally call is going to (chosen when the crown starts to glow).
    public int RallyTargetId;
    // A support's heals scale like the HP of what it heals (difficulty × threat; 1 at Normal, threat 1).
    public double HealPower = 1;
    // HealGuard: this one is being healed and takes no damage (set by the battle each frame); when it
    // may be guarded again; the ally a healing support guards.
    public bool Mended;
    public double GuardAgainAt;
    public int MendingId;
    // HitAndRun: running off after a dash (DIPs still to go).
    public double RunAwayLeft;
    // Support far heal: its own cooldown, and whether the heal it is winding up is one (any distance).
    public double FarHealCooldown;
    public bool FarHeal;
    // A kind that turns slowly (TurnSeconds): how long it has wanted to face the other way.
    public double TurnTime;
    // A blocker standing guard between the cat and this support (0: none; set by the battle).
    public int GuardOf;
    // Boss rally: time to its next call and the crown's glow still to run.
    public double RallyCooldown, RallyCharge;
    // Its elite affix (null: a plain one), the sure blocks it still has and its next heal's factor.
    // Rallied: made elite by a boss's rally call (not counted against MaxElites).
    public EliteSpec? Elite;
    public bool Rallied;
    public int SureBlocks;
    public double HealBoost = 1;
    // A weapon hit has landed on it (the Opener card's bonus is spent).
    public bool Opened;
    public bool Targetable => State is not (SlimeState.Spawning or SlimeState.Defeated);
    public void Set(SlimeState state) { State = state; StateTime = 0; }
}

public enum BattlePhase { WaveBreak, Fighting, Won, Lost }
public enum BattleEventKind
{
    WaveStarted, SlimeSpawned, Swing, SlimeHit, SlimeDefeated, CatHurt, Pushed, WaveCleared, Won, Lost,
    Healed, Cheered, Shielded, Blocked, BombThrown, BombExploded, BossAppeared, BossSummoned, BossEnraged, ComboHit,
    UpgradeOffered, UpgradeChosen, EnemyHealing, EnemyHealed, CatStunned, SynergyUnlocked, Reflected, Echoed,
    HitBlocked, BossShielded, BossShieldDown, BossImmune, BossRallying, BossRallied, Incoming,
    // An elite appeared (Value: its id); the Turtle shell card stopped a hit on the cat.
    EliteAppeared, ShellBlocked,
    // A boss leaping to its edge (taking off, landing) and the rings where its helpers will appear.
    BossLeaping, BossLanded, BossSummoning,
    // Tactics: a heal that guards its ally, a hit on that guarded ally.
    EnemyWarded, HitWarded,
    // A Push on an elite that ignores it (Value: its id).
    PushResisted
}
public readonly record struct BattleEvent(BattleEventKind Kind, double X, double Value = 0);
// Number: a floating damage (Value > 0) or heal (Value < 0, shown as +HP above the cat).
// Thud: a heavy weapon's plain hit (dust and a ring on the floor), chosen by the weapon's hitEffect.
// Mend: green pluses rising from an enemy healed by a support enemy.
// Block: grey stone chips and a small stone shield over an enemy that blocked a hit. Immune: a
// gold ring flashing on a shielded boss hit for nothing. Warded: a green ring and "IMMUNE" on an
// enemy guarded by a heal.
public enum BattleEffectKind { Spark, Poof, Push, Explosion, Slam, Number, Combo, Thud, Mend, Block, Immune, Warded }
public sealed class BattleEffect
{
    public BattleEffectKind Kind;
    // Height is how far above the floor the effect starts.
    public double X, Height, Age, Life, Value;
}
public sealed class BattleBomb
{
    public double X, Remaining;
}

// What the battle wants the cat to do this frame. Hold: the user has the cat (or it is in
// the air), so the battle leaves it to input and physics.
public enum CatCommand { Hold, Stand, Move }
public readonly record struct CatOrder(CatCommand Command, double X = 0, int Facing = 0);

// The whole fight, without WPF: slimes, the boss, waves, targeting, the sword and the
// user's skills. Time only advances through Step (simulation time), so Pause freezes
// everything. The window turns CatOrder into the pet's normal walking; physics stays in PetPhysics.
public sealed class BattleSim
{
    public BattleSettings Settings { get; }
    // This battle's seed (BattleRandom) and its streams: what spawns where, the cards, the rock's
    // blocks, the elite rolls and the rare cards.
    public int Seed { get; }
    private readonly Random spawnRandom, offerRandom, blockRandom, affixRandom, rarityRandom;
    private readonly List<Slime> slimes = new();
    private readonly List<BattleEffect> effects = new();
    private readonly List<BattleEvent> events = new();
    private readonly List<BattleBomb> bombs = new();
    private readonly double[] cooldowns = new double[5];
    public IReadOnlyList<Slime> Slimes => slimes;
    public IReadOnlyList<BattleEffect> Effects => effects;
    public IReadOnlyList<BattleEvent> Events => events;
    public IReadOnlyList<BattleBomb> Bombs => bombs;
    public BattlePhase Phase { get; private set; } = BattlePhase.WaveBreak;
    public bool Paused { get; set; }
    public double Left { get; private set; }
    public double Right { get; private set; }
    public double CatHp { get; private set; }
    // 1-based number of the current (or last started) wave; 0 before the first.
    public int Wave { get; private set; }
    // The waves of this battle: an Adventure stage's own, otherwise the free battle's.
    public IReadOnlyList<WaveSpec> WavePlan { get; }
    public int WaveCount => WavePlan.Count;
    // The Adventure stage being played (null: a free battle).
    public StageSpec? Stage { get; }
    private readonly int maxAlive;
    // Survive stages: seconds still to hold out (counts only while the battle runs and the cat is
    // on the ground, like the waves). NaN for any other battle.
    public bool Survival => Stage?.Survival == true;
    public double SurviveRemaining { get; private set; } = double.NaN;

    // This run's build (Adventure stages that allow upgrades): how often each card was taken (the
    // build carried in from the stage before included), and the cards on offer while the user
    // chooses. While choosing, the battle stands still (Step does nothing, the clock and the record
    // time stop). The definitions are never changed; Build() is what a won stage passes on.
    private readonly Dictionary<string, int> taken = new();
    public IReadOnlyDictionary<string, int> UpgradesTaken => taken;
    public IReadOnlyList<UpgradeSpec>? UpgradeOffer { get; private set; }
    public bool ChoosingUpgrade => UpgradeOffer != null;
    private double damageBonus, moveBonus, cooldownCut, knockBonus, armorCut, maxHpBonus;
    // Rare cards: Turtle shell (its regrowth time; 0 without it), Opener and Momentum bonuses.
    private double shellRegrow, openerBonus, momentumBonus;
    // Turtle shell: seconds until it is back (0: ready); hits it stopped.
    public double ShellCooldown { get; private set; }
    public bool HasShell => shellRegrow > 0;
    public bool ShellReady => HasShell && ShellCooldown <= 0;
    public int ShellBlocks { get; private set; }
    // Momentum: DIPs the cat has run on the floor since its last swing, and whether the swing under
    // way is a charged one.
    public const double MomentumRun = 140;
    private double runDistance, runFromX = double.NaN;
    private bool chargedSwing;
    public bool MomentumReady => momentumBonus > 0 && runDistance >= MomentumRun;
    public int ChargedHits { get; private set; }
    public int OpenerHits { get; private set; }
    // Elites spawned in this battle; what the current wave's budget still has for elites.
    public int ElitesSpawned { get; private set; }
    private double eliteBudget = double.PositiveInfinity;
    public int Stacks(string upgrade) => taken.TryGetValue(upgrade, out int n) ? n : 0;
    // Cards chosen in this run (not the carried ones): a stage's maxUpgrades counts these.
    public int Picks { get; private set; }
    public double CatMaxHp => Settings.CatMaxHp + maxHpBonus;
    // A group's points: one per card of that group in the build.
    public int GroupPoints(string group)
    {
        int points = 0;
        foreach (var (id, n) in taken) if (Upgrade(id)?.Group == group) points += n;
        return points;
    }
    private UpgradeSpec? Upgrade(string id) { foreach (var u in Settings.UpgradeList) if (u.Id == id) return u; return null; }
    // Synergies the build has switched on (they stay on for the rest of the build).
    private readonly List<SynergySpec> synergies = new();
    public IReadOnlyList<SynergySpec> Synergies => synergies;
    private SynergySpec? Active(string effect) { foreach (var y in synergies) if (y.Effect == effect) return y; return null; }
    // The synergy this card would switch on now (null: none).
    public SynergySpec? Unlocks(UpgradeSpec card)
    {
        if (card.Group == null) return null;
        int points = GroupPoints(card.Group) + 1;
        foreach (var y in Settings.SynergyList)
            if (y.Group == card.Group && !synergies.Contains(y) && points >= y.At) return y;
        return null;
    }
    // The build to carry on: every card but the instant heals.
    public Dictionary<string, int> Build()
    {
        var build = new Dictionary<string, int>();
        foreach (var (id, n) in taken) if (n > 0 && Upgrade(id) is { Effect: not "heal" }) build[id] = n;
        return build;
    }
    public int Reflects { get; private set; }
    public int Echoes { get; private set; }
    // Hits stony enemies blocked; hits a shielded boss shrugged off; seconds a boss shield was up.
    public int BlocksMade { get; private set; }
    public int ImmuneHits { get; private set; }
    // Hits on an enemy guarded by a heal (HealGuard).
    public int WardedHits { get; private set; }
    public double BossShieldSeconds { get; private set; }
    private int landedSwings;
    // Where the cat stood at the last Step (a boss shield phase runs away from it).
    private double lastCatX, groundX = double.NaN, liftedFor;
    // Multipliers the upgrades give (1 without any). Cuts never go below 30 % of the original.
    public double DamageFactor => 1 + damageBonus;
    public double MoveFactor => 1 + moveBonus;
    public double CooldownFactor => Math.Max(0.3, 1 - cooldownCut);
    public double ArmorFactor => Math.Max(0.3, 1 - armorCut);
    public double KnockBonus => knockBonus;
    public bool IsBossWave(int wave)
    {
        if (wave < 1 || wave > WaveCount) return false;
        foreach (var group in WavePlan[wave - 1].Groups) if (group.Fixed && Settings.Enemy(group.Enemy).Boss) return true;
        return false;
    }
    // Enemies of the current wave still to come, in spawn order: each unit (one enemy or a squad)
    // comes as one. A stream wave has none queued: it draws as it goes.
    private readonly List<SpawnUnit> pending = new();
    private int pendingNext;
    private StreamSpec? stream;
    public bool Streaming => stream != null;
    public int ToSpawn { get { int n = incoming?.Length ?? 0; for (int i = pendingNext; i < pending.Count; i++) n += pending[i].Kinds.Length; return n; } }
    // Two-sided waves: the edge far from the cat when the wave began (-1 left, +1 right), the edge a
    // stream used last, and a spawn announced by an arrow at IncomingX (IncomingEdge) that comes in
    // IncomingRemaining s.
    private int farEdge, streamEdge;
    private string[]? incoming;
    public double IncomingRemaining { get; private set; }
    public double IncomingX { get; private set; }
    public int IncomingEdge { get; private set; }
    public bool Incoming => incoming != null;
    // Survive stages: the card offers of UpgradeAt already made.
    private int timedOffers;
    // An Expedition run's relics (PetExpedition.cs RelicSpec) and what they change; none outside one.
    private readonly List<RelicSpec> relics = new();
    public IReadOnlyList<RelicSpec> Relics => relics;
    private double counterBonus, shieldDelay, fishBonus, fishDelay, recklessBonus, recklessBelow, recklessHurt;
    // Counter bell: the Shield or the Turtle shell stopped a hit, the next landed swing hits harder.
    public bool CounterReady { get; private set; }
    public int CounterHits { get; private set; }
    public bool Reckless => recklessBonus > 0 && CatHp < recklessBelow * CatMaxHp;
    // An Expedition step makes its stage this much stronger (1 otherwise).
    private readonly double threatScale = 1;
    // The threat new enemies get now: the stage's (× an Expedition step's) × the current wave's,
    // growing with the survive clock.
    public double Threat
    {
        get
        {
            if (Stage == null) return 1;
            double threat = Stage.Threat * threatScale * (Wave >= 1 ? WavePlan[Math.Min(Wave, WaveCount) - 1].Threat : 1);
            if (Survival && Stage.SurviveSeconds is double hold) threat *= 1 + Stage.ThreatGrowth * (hold - SurviveRemaining) / 60;
            return threat;
        }
    }
    public int Defeated { get; private set; }
    public bool BossDefeated { get; private set; }
    public EnemyType? DefeatedBoss { get; private set; }
    public double PhaseRemaining { get; private set; }
    public double CatCooldown { get; private set; }
    // Seconds into the current swing; negative when not swinging.
    public double SwingTime { get; private set; } = -1;
    public double SwingProgress => SwingTime < 0 ? -1 : Math.Clamp(SwingTime / arms.SwingDuration, 0, 1);
    // Hits in the current chain, and whether the swing under way would finish it (a gold slash).
    public int Combo { get; private set; }
    public bool ComboSwing { get; private set; }
    public int CombosLanded { get; private set; }
    private double comboClock;
    public int? SelectedId { get; private set; }
    // The slime the cat is going for (selected or chosen automatically).
    public int? TargetId { get; private set; }
    public BattleSkill? Armed { get; private set; }
    public bool PushArmed => Armed == BattleSkill.Push;
    public double PushCooldown => cooldowns[(int)BattleSkill.Push];
    public double CheerRemaining { get; private set; }
    public double ShieldRemaining { get; private set; }
    public double Grace { get; private set; }
    // Until this reaches 0, putting the cat down gives no new protection.
    public double GraceCooldown { get; private set; }
    public bool RescueGraceReady => GraceCooldown <= 0;
    // How the user is handling the cat (SetCatState): held = pressed, carried, in the air or
    // climbing (the battle may not move it); lifted = really off the ground (not a click). A cat
    // that is only pressed is still on the floor: an attack already under way reaches it (no stun).
    public bool CatHeld { get; private set; }
    public bool CatLifted { get; private set; }
    // While the cat is safe in the user's hands the skills wait: no use, no cooldown recovery.
    public bool SkillsLocked => CatHeld;
    public double HurtFlash { get; private set; }
    // The cat is stunned (seconds left; 0: not) and, after a stun, cannot be stunned again yet.
    public double CatStunRemaining { get; private set; }
    public double StunImmunity { get; private set; }
    public bool CatStunned => CatStunRemaining > 0;
    public int StunsTaken { get; private set; }
    // Simulation clock (stops while paused); animations read it instead of wall time.
    public double Time { get; private set; }
    // HP given back after the last cleared wave, and the battle's length (for the result panel).
    public double LastHeal { get; private set; }
    public double FightSeconds { get; private set; }
    public bool Finished => Phase is BattlePhase.Won or BattlePhase.Lost && PhaseRemaining <= 0;
    public bool Over => Phase is BattlePhase.Won or BattlePhase.Lost;
    public Slime? Boss { get { foreach (var s in slimes) if (s.IsBoss && s.State != SlimeState.Defeated) return s; return null; } }
    private int nextId = 1, autoTargetId, swingTargetId, swingFacing = 1;
    private bool swingHit, wasLifted;
    private double spawnTimer;

    public BattleDifficulty Difficulty { get; }
    private readonly DifficultySpec level;
    // This battle's weapon (its definition is shared; the swing and cooldown are below).
    public WeaponSpec Weapon { get; }
    private readonly WeaponSpec arms;

    public BattleSim(BattleSettings settings, double left, double right, Random? random = null, BattleDifficulty difficulty = BattleDifficulty.Normal,
        string weapon = BattleWeapon.Sword, StageSpec? stage = null, IReadOnlyDictionary<string, int>? build = null, int? seed = null,
        double? startHp = null, double threat = 1, IReadOnlyList<string>? relics = null)
    {
        // A given seed wins; otherwise one drawn from the given random (tests), else a fresh one.
        Seed = seed ?? random?.Next() ?? Random.Shared.Next();
        spawnRandom = BattleRandom.Stream(Seed, BattleRandom.Spawn);
        offerRandom = BattleRandom.Stream(Seed, BattleRandom.Offer);
        blockRandom = BattleRandom.Stream(Seed, BattleRandom.Block);
        affixRandom = BattleRandom.Stream(Seed, BattleRandom.Affix);
        rarityRandom = BattleRandom.Stream(Seed, BattleRandom.Rarity);
        Settings = settings;
        Stage = stage;
        WavePlan = stage?.Waves is { Length: > 0 } waves ? waves : settings.WavePlan;
        maxAlive = stage?.MaxAlive ?? settings.MaxAlive;
        if (stage is { Survival: true, SurviveSeconds: double hold }) SurviveRemaining = hold;
        Difficulty = difficulty;
        Weapon = arms = settings.Weapon(weapon);
        level = settings.Difficulty(difficulty);
        Left = left; Right = Math.Max(left + settings.SlimeWidth * settings.LargestScale, right);
        // A carried build (the stage before's): its cards as if taken, within their limits; cards no
        // longer in the data are skipped.
        if (build != null)
            foreach (var (id, count) in build)
                if (Upgrade(id) is UpgradeSpec card && card.Effect != "heal")
                    for (int i = 0; i < count && (card.MaxStacks == 0 || Stacks(id) < card.MaxStacks); i++) Take(card, 0, false);
        // An Expedition run's relics (unknown ones skipped), each its own modifier.
        if (relics != null && settings.Expedition is ExpeditionSpec expedition)
            foreach (var id in relics)
                if (expedition.Relic(id) is RelicSpec relic && !this.relics.Contains(relic)) AddRelic(relic);
        // An Expedition step starts with the HP the last one ended with (within the build's max).
        CatHp = startHp is double hp && double.IsFinite(hp) ? Math.Clamp(hp, 1, CatMaxHp) : CatMaxHp;
        if (stage != null && double.IsFinite(threat) && threat > 0) threatScale = threat;
        PhaseRemaining = settings.FirstWaveDelay;
    }

    public int Alive { get { int n = 0; foreach (var s in slimes) if (s.State != SlimeState.Defeated) n++; return n; } }
    public Slime? Find(int id) { foreach (var s in slimes) if (s.Id == id) return s; return null; }
    public void ClearEvents() => events.Clear();
    public double Cooldown(BattleSkill skill) => cooldowns[(int)skill];
    public static bool IsAimed(BattleSkill skill) => BattleSkills.Get(skill).Aimed;
    public bool Ready(BattleSkill skill) => !Over && !Paused && !ChoosingUpgrade && !SkillsLocked && cooldowns[(int)skill] <= 0;

    // The one description of the user's hold on the cat, shared by Step, the skills and the HUD.
    // A real lift cancels a chosen skill at once, so a later click cannot use it by mistake;
    // a plain press does not (it changes nothing visible).
    public void SetCatState(bool held, bool lifted)
    {
        CatHeld = held;
        CatLifted = held && lifted;
        if (!CatLifted) return;
        wasLifted = true;
        Armed = null;
    }

    // catHeld / lifted: see SetCatState. Putting a lifted cat down grants grace (if ready).
    public CatOrder Step(double dt, double catX, bool catHeld, bool lifted = false)
    {
        SetCatState(catHeld, lifted);
        if (Paused || ChoosingUpgrade || !double.IsFinite(dt) || dt <= 0 || !double.IsFinite(catX)) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        dt = Math.Min(dt, 0.1);
        Time += dt;
        // While the cat is lifted the slimes go by where it last stood on the floor, not by the
        // cursor carrying it.
        if (!CatLifted || !double.IsFinite(groundX)) groundX = catX;
        double floorX = groundX;
        lastCatX = floorX;
        StepEffects(dt);
        HurtFlash = Math.Max(0, HurtFlash - dt);
        // The stun runs on battle time, held or not (lifting neither ends nor stretches it).
        if (CatStunRemaining > 0)
        {
            CatStunRemaining = Math.Max(0, CatStunRemaining - dt);
            if (CatStunRemaining <= 0) StunImmunity = Settings.StunImmunitySeconds;
        }
        else StunImmunity = Math.Max(0, StunImmunity - dt);
        // Skills recover only while the cat is down and at risk; buffs already running wear off as usual.
        // So does the Turtle shell; Momentum counts only the cat's own run on the floor.
        if (!catHeld)
        {
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Math.Max(0, cooldowns[i] - dt);
            ShellCooldown = Math.Max(0, ShellCooldown - dt);
            if (!double.IsNaN(runFromX)) runDistance += Math.Abs(catX - runFromX);
            runFromX = catX;
        }
        else runFromX = double.NaN;
        CheerRemaining = Math.Max(0, CheerRemaining - dt);
        ShieldRemaining = Math.Max(0, ShieldRemaining - dt);
        if (Over)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            StepSlimes(dt, floorX, true);
            return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        }
        comboClock += dt;
        if (CatLifted) liftedFor += dt; // How long this rescue has lasted (battle time).
        if (catHeld) { CancelSwing(); Combo = 0; }
        else
        {
            // Landing after a real lift: protection, unless it was given too recently (then the
            // time left, if any, is neither renewed nor extended).
            if (wasLifted && liftedFor >= Settings.RescueGraceMinLiftSeconds && GraceCooldown <= 0)
            {
                Grace = Settings.LandingGraceSeconds;
                GraceCooldown = Settings.RescueGraceCooldownSeconds;
            }
            wasLifted = false;
            liftedFor = 0;
            Grace = Math.Max(0, Grace - dt);
            GraceCooldown = Math.Max(0, GraceCooldown - dt);
        }
        CatCooldown = Math.Max(0, CatCooldown - dt);
        StepBombs(dt);
        StepSlimes(dt, floorX, catHeld);
        if (Over) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        // Holding the cat freezes the waves (no spawning, no progress) and a survive stage's clock.
        if (!catHeld) StepWaves(dt, catX);
        if (!catHeld && Survival && !Over && !ChoosingUpgrade)
        {
            SurviveRemaining = Math.Max(0, SurviveRemaining - dt);
            if (SurviveRemaining <= 0) Finish(BattlePhase.Won);
            // Timed card offers (the clock stands still while the user chooses).
            else if (Stage is { Upgrades: true, UpgradeAt: { } offers, SurviveSeconds: double hold } && timedOffers < offers.Length && hold - SurviveRemaining >= offers[timedOffers])
            {
                timedOffers++;
                OfferUpgrades();
            }
        }
        if (ChoosingUpgrade) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        if (catHeld) { TargetId = PickTarget(floorX)?.Id; return new(CatCommand.Hold); }
        return CatTurn(dt, catX);
    }

    private void CancelSwing() { SwingTime = -1; swingHit = false; ComboSwing = false; chargedSwing = false; }

    // The chain only lasts ComboWindow seconds after its last hit.
    // Hits in the current chain (0 once it lapsed); the HUD shows it as ×N.
    public int LiveCombo => Combo > 0 && comboClock <= Settings.ComboWindow ? Combo : 0;

    private CatOrder CatTurn(double dt, double catX)
    {
        var target = Over ? null : PickTarget(catX);
        TargetId = target?.Id;
        if (SwingTime >= 0)
        {
            SwingTime += dt;
            if (!swingHit && SwingTime >= arms.HitTime) { swingHit = true; ResolveHit(catX); }
            if (SwingTime >= arms.SwingDuration) CancelSwing();
            return new(CatCommand.Stand, catX, swingFacing);
        }
        BlockedById = 0;
        if (CatStunned) return new(CatCommand.Stand, catX);
        // Put down on top of a blocker: a few steps off it to the nearest free side.
        if (Unstick(catX) is double free) return new(CatCommand.Move, free, free < catX ? -1 : 1);
        if (target == null) return new(CatCommand.Stand, catX);
        // A blocker on the floor between the cat and its target is hit first (the target keeps its marker).
        if (BlockerBetween(catX, target) is Slime wall) { BlockedById = wall.Id; target = wall; }
        double dx = target.X - catX;
        int facing = dx < 0 ? -1 : 1;
        double reach = Reach(target);
        if (Math.Abs(dx) <= reach)
        {
            if (CatCooldown <= 0)
            {
                SwingTime = 0; swingHit = false; swingTargetId = target.Id; swingFacing = facing;
                ComboSwing = LiveCombo == Settings.ComboHits - 1;
                // Momentum: a swing after a run is a charged one (the run starts again either way).
                chargedSwing = MomentumReady;
                runDistance = 0;
                double cooldown = arms.AttackCooldown * CooldownFactor / (CheerRemaining > 0 ? Settings.CheerSpeed : 1);
                CatCooldown = Math.Max(cooldown, arms.SwingDuration);
                events.Add(new(BattleEventKind.Swing, catX));
            }
            return new(CatCommand.Stand, catX, facing);
        }
        return new(CatCommand.Move, target.X - facing * reach * 0.7, facing);
    }

    // The blocker standing in the cat's way to its target (null: the way is clear).
    public int BlockedById { get; private set; }

    private Slime? BlockerBetween(double catX, Slime target)
    {
        double dx = target.X - catX;
        Slime? wall = null;
        foreach (var s in slimes)
            if (s != target && s.Type.Blocker && s.Targetable && (s.X - catX) * dx > 0 && Math.Abs(s.X - catX) < Math.Abs(dx) &&
                (wall == null || Math.Abs(s.X - catX) < Math.Abs(wall.X - catX))) wall = s;
        return wall;
    }

    // Where the cat goes when it stands inside a blocker (null: it does not): just past the blocker's
    // edge on the cat's side, or the other side when that spot is in another blocker or off the floor.
    private double? Unstick(double catX)
    {
        Slime? inside = null;
        foreach (var s in slimes)
            if (s.Type.Blocker && s.Targetable && Math.Abs(s.X - catX) < s.HitWidth / 2 + CatHalfWidth - 4) { inside = s; break; }
        if (inside == null) return null;
        double clear = inside.HitWidth / 2 + CatHalfWidth + 4; // The window stops walking within 6 DIPs of an order.
        double near = inside.X + (catX < inside.X ? -clear : clear), far = inside.X + (catX < inside.X ? clear : -clear);
        bool Free(double x)
        {
            if (x < Left + CatHalfWidth || x > Right - CatHalfWidth) return false;
            foreach (var s in slimes) if (s.Type.Blocker && s.Targetable && Math.Abs(s.X - x) < s.HitWidth / 2 + CatHalfWidth - 4) return false;
            return true;
        }
        // Neither side free (blockers side by side): the near side; the next step frees it from the next one.
        return Free(near) || !Free(far) ? Math.Clamp(near, Left + CatHalfWidth, Right - CatHalfWidth) : far;
    }

    // A wider slime is reached from further away (the sword hits its near edge).
    private double Reach(Slime slime) => arms.Range + (slime.HitWidth - Settings.SlimeWidth) / 2;

    // The swing's single hit: only its own target, only if still in reach at this moment.
    private void ResolveHit(double catX)
    {
        var slime = Find(swingTargetId);
        if (slime == null || !slime.Targetable || Math.Abs(slime.X - catX) > Reach(slime) + 14) { Combo = 0; return; } // A miss ends the chain.
        Combo = LiveCombo + 1;
        comboClock = 0;
        bool combo = Combo >= Settings.ComboHits;
        double damage = arms.Damage * DamageFactor * (CheerRemaining > 0 ? Settings.CheerDamage : 1) * (combo ? Settings.ComboDamage : 1);
        // Armor break: combo hits hit harder.
        if (combo && Active("armorBreak") is SynergySpec breaker) damage *= 1 + breaker.Amount;
        // Rare cards: the first landed hit on each enemy (Opener) and a swing after a run (Momentum)
        // hit harder; an Afterimage echo repeats the swing without them.
        double plain = damage;
        bool opening = openerBonus > 0 && !slime.Opened, charged = chargedSwing;
        chargedSwing = false;
        if (opening) damage *= 1 + openerBonus;
        if (charged) damage *= 1 + momentumBonus;
        if (Reckless) damage *= 1 + recklessBonus;
        bool countered = CounterReady;
        if (countered) damage *= 1 + counterBonus;
        double knock = arms.Knock + knockBonus;
        events.Add(new(combo ? BattleEventKind.ComboHit : BattleEventKind.SlimeHit, slime.X));
        AddEffect(combo || charged || countered ? BattleEffectKind.Combo : arms.Effect == "thud" ? BattleEffectKind.Thud : BattleEffectKind.Spark, slime.X, slime.Height / 2);
        if (combo) { Combo = 0; CombosLanded++; }
        // A blocked or shrugged-off hit (or the last one) moves nothing.
        var result = Damage(slime, damage, combo ? DamageSource.Combo : DamageSource.Weapon);
        // Only a hit that lands opens an enemy (a blocked one leaves the Opener bonus for the next).
        if (result == HitResult.Landed)
        {
            slime.Opened = true;
            if (opening) OpenerHits++;
            if (charged) ChargedHits++;
            if (countered) { CounterReady = false; CounterHits++; }
        }
        bool landed = result == HitResult.Landed && slime.State != SlimeState.Defeated;
        // A combo knocks back away from the cat (heavy kinds less), but does not stun; a heavy
        // weapon knocks back on every hit. An elite that ignores Push stands its ground (it still takes the damage).
        if (landed && (combo || knock > 0) && slime.Elite is not { PushImmune: true })
            slime.KnockVelocity = (slime.X < catX ? -1 : 1) * (combo ? Math.Max(Settings.ComboKnock, knock) : knock) * slime.Type.PushFactor;
        // Afterimage: every Every-th landed swing hits a second time (an echo counts nothing itself).
        landedSwings++;
        if (Active("echo") is SynergySpec echo && landedSwings % echo.Every == 0 && slime.Targetable)
        {
            Echoes++;
            AddEffect(BattleEffectKind.Spark, slime.X + (slime.X < catX ? -8 : 8), slime.Height * 0.7);
            events.Add(new(BattleEventKind.Echoed, slime.X));
            Damage(slime, plain * echo.Amount, DamageSource.Echo);
        }
    }

    // Every hit on an enemy goes through here: Landed, or why it did nothing.
    private HitResult Damage(Slime slime, double amount, DamageSource source)
    {
        if (!slime.Targetable || amount <= 0) return HitResult.Landed;
        if (slime.Shielded)
        {
            // A boss in its shield phase: nothing gets through (not even the bomb).
            ImmuneHits++;
            AddEffect(BattleEffectKind.Immune, slime.X, slime.Height / 2, slime.Width);
            events.Add(new(BattleEventKind.BossImmune, slime.X));
            return HitResult.Immune;
        }
        if (slime.Mended)
        {
            // Guarded by a heal winding up on it (push the healer to end it).
            WardedHits++;
            AddEffect(BattleEffectKind.Warded, slime.X, slime.Height / 2, slime.Width);
            events.Add(new(BattleEventKind.HitWarded, slime.X));
            return HitResult.Immune;
        }
        if (Blocks(slime, source)) return HitResult.Blocked;
        // A hit never takes a boss past its next shield phase: it stops there, the rest is lost.
        slime.Hp = Math.Max(ShieldFloor(slime), slime.Hp - amount);
        slime.HitFlash = 0.18;
        AddEffect(BattleEffectKind.Number, slime.X + (slime.IsBoss ? slime.Width * 0.38 : 0), slime.Height + 10, Math.Round(amount));
        if (slime.Hp <= 0)
        {
            slime.Set(SlimeState.Defeated);
            slime.KnockVelocity = 0;
            Defeated++;
            if (slime.IsBoss) { BossDefeated = true; DefeatedBoss = slime.Type; }
            if (SelectedId == slime.Id) SelectedId = null;
            // A defeated healer's guard ends with it (after the whole blast for a bomb, which hits
            // everything in it at once).
            if (slime.MendingId != 0) { slime.MendingId = 0; if (!blasting) RefreshGuards(); }
            AddEffect(BattleEffectKind.Poof, slime.X, slime.Height / 2, slime.IsBoss ? 2 : 1);
            events.Add(new(BattleEventKind.SlimeDefeated, slime.X, slime.IsBoss ? 1 : 0));
            return HitResult.Landed;
        }
        var type = slime.Type;
        double fraction = slime.Hp / slime.MaxHp;
        if (type.SummonAt > 0 && !slime.Summoned && fraction <= type.SummonAt)
        {
            slime.Summoned = true;
            var helper = Settings.Enemy(type.SummonType);
            for (int i = 0; i < type.SummonCount; i++)
                AddSlime(slime.X + (i % 2 == 0 ? -1 : 1) * (slime.Width / 2 + 30 + 20 * (i / 2)), helper).SummonedBy = slime.Id;
            events.Add(new(BattleEventKind.BossSummoned, slime.X));
        }
        if (type.EnrageAt > 0 && !slime.Enraged && fraction <= type.EnrageAt)
        {
            slime.Enraged = true;
            events.Add(new(BattleEventKind.BossEnraged, slime.X));
        }
        if (type.Phases is { } phases)
            for (int i = 0; i < phases.Length && i < 31; i++)
            {
                var phase = phases[i];
                // The same product as ShieldFloor, so a hit stopped on a shield phase's line starts it.
                if ((slime.PhasesDone & (1 << i)) != 0 || slime.Hp > phase.At * slime.MaxHp) continue;
                slime.PhasesDone |= 1 << i; // Once, even if the call is skipped below.
                if (phase.Shield)
                {
                    StartShield(slime, phase, i + 1);
                    if (phase.Enrage && !slime.Enraged) { slime.Enraged = true; events.Add(new(BattleEventKind.BossEnraged, slime.X)); }
                    break; // Later phases wait for the shield to drop.
                }
                if (CallHelpers(slime, phase.Calls().ToList(), 0, 0) > 0) events.Add(new(BattleEventKind.BossSummoned, slime.X));
                if (phase.Enrage && !slime.Enraged)
                {
                    slime.Enraged = true;
                    events.Add(new(BattleEventKind.BossEnraged, slime.X));
                }
            }
        return HitResult.Landed;
    }

    // A stony enemy's block roll for one hit (rolled once per hit, never per frame).
    private bool Blocks(Slime slime, DamageSource source)
    {
        var type = slime.Type;
        if (type.BlockChance <= 0 || source is DamageSource.Bomb or DamageSource.Reflect) return false;
        // A front-only blocker: a hit from behind always lands (and ends its streak).
        if (type.BlockFrontOnly && (lastCatX - slime.X) * slime.Facing <= 0) { slime.BlockStreak = 0; return false; }
        // Armor break: combo hits go straight through.
        if (source == DamageSource.Combo && Active("armorBreak") != null) { slime.BlockStreak = 0; return false; }
        // An elite's sure blocks come first (no roll); then the usual roll and streak limit.
        bool sure = slime.SureBlocks > 0;
        if (!sure && (slime.BlockStreak >= type.BlockMaxStreak || blockRandom.NextDouble() >= type.BlockChance)) { slime.BlockStreak = 0; return false; }
        if (sure) slime.SureBlocks--;
        slime.BlockStreak++;
        BlocksMade++;
        AddEffect(BattleEffectKind.Block, slime.X, slime.Height * 0.6);
        events.Add(new(BattleEventKind.HitBlocked, slime.X));
        return true;
    }

    // The lowest HP a hit may leave: the line of the first shield phase still to come (0: none).
    private static double ShieldFloor(Slime slime)
    {
        double floor = 0;
        if (slime.Type.Phases is { } phases)
            for (int i = 0; i < phases.Length && i < 31; i++)
            {
                double line = phases[i].At * slime.MaxHp;
                if (phases[i].Shield && (slime.PhasesDone & (1 << i)) == 0 && line < slime.Hp) floor = Math.Max(floor, line);
            }
        return floor;
    }

    // The shield goes up at once; the boss drops what it was doing and runs to the edge far from the cat.
    private void StartShield(Slime boss, BossPhase phase, int tag)
    {
        boss.Shielded = boss.Retreating = true;
        boss.ShieldTag = tag;
        boss.RetreatTime = 0;
        boss.RetreatSide = RetreatEdge(boss.X, lastCatX);
        boss.Crouch = boss.LeapHeight = 0;
        boss.LeapFromX = boss.X;
        boss.PendingCalls = phase.Calls().ToList();
        boss.RallyCooldown = boss.Type.RallyEvery / 2;
        boss.RallyCharge = 0;
        if (boss.State is SlimeState.Windup or SlimeState.Dashing or SlimeState.Casting) boss.Set(SlimeState.Moving);
        events.Add(new(BattleEventKind.BossShielded, boss.X));
    }

    // The edge a shield phase goes to (+1 right, -1 left): the one opposite the boss's half of the
    // floor, unless the cat is in that half too; then the other one (never next to the cat).
    private int RetreatEdge(double bossX, double catX)
    {
        double middle = (Left + Right) / 2;
        int edge = bossX >= middle ? -1 : 1;
        int catHalf = catX >= middle ? 1 : -1;
        return edge == catHalf ? -edge : edge;
    }

    // Calls helpers next to the boss, within its SummonCap of live helpers; returns how many came.
    // side 0: both sides in turn (as before); otherwise all on the open side, away from that edge.
    private int CallHelpers(Slime boss, List<string> calls, int tag, int side) => SpawnHelpers(boss, HelperSpots(boss, calls, side), tag);

    // Where the helpers will stand (as many as the SummonCap leaves room for). On the open side they
    // line up by role from the boss outwards: supports next to it, blockers in front of them, plain
    // fighters, then dashers nearest the cat.
    private List<(string Kind, double X)> HelperSpots(Slime boss, List<string> calls, int side)
    {
        int alive = 0;
        foreach (var s in slimes) if (s.SummonedBy == boss.Id && s.State != SlimeState.Defeated) alive++;
        int count = Math.Min(calls.Count, Math.Max(0, boss.Type.SummonCap - alive));
        var spots = new List<(string Kind, double X)>();
        if (side == 0)
        {
            for (int k = 0; k < count; k++) spots.Add((calls[k], boss.X + (k % 2 == 0 ? -1 : 1) * (boss.Width / 2 + 30 + 20 * (k / 2))));
            return spots;
        }
        static int Rank(EnemyType t) => t.Support ? 0 : t.Blocker ? 1 : t.Dash ? 3 : 2;
        var ordered = calls.Take(count).Select((kind, i) => (Kind: kind, Order: i)).OrderBy(c => Rank(Settings.Enemy(c.Kind))).ThenBy(c => c.Order).Select(c => c.Kind).ToList();
        double offset = boss.Width / 2 + 10, previous = 0;
        foreach (var kind in ordered)
        {
            double w = Settings.SlimeWidth * Settings.Enemy(kind).Scale;
            offset += previous == 0 ? w / 2 : (previous + w) / 2 * 0.85;
            previous = w;
            spots.Add((kind, boss.X - side * offset));
        }
        return spots;
    }

    // The helpers appear at their spots (still within the SummonCap), never on top of the cat: at
    // least SquadCatGap away, on the boss's side of it.
    private int SpawnHelpers(Slime boss, List<(string Kind, double X)> spots, int tag)
    {
        int alive = 0;
        foreach (var s in slimes) if (s.SummonedBy == boss.Id && s.State != SlimeState.Defeated) alive++;
        int count = Math.Min(spots.Count, Math.Max(0, boss.Type.SummonCap - alive));
        for (int k = 0; k < count; k++)
        {
            double x = spots[k].X;
            if (Math.Abs(x - lastCatX) < SquadCatGap) x = lastCatX + (boss.X >= lastCatX ? 1 : -1) * SquadCatGap;
            var helper = AddSlime(x, Settings.Enemy(spots[k].Kind));
            helper.SummonedBy = boss.Id;
            helper.PhaseTag = tag;
            helper.Facing = lastCatX < helper.X ? -1 : 1;
        }
        return count;
    }

    // The leap (RetreatLeap): a crouch where it stands (a stun or push makes it wait), then a flight to
    // the goal nothing can stop; true once it has landed.
    private bool StepLeap(Slime boss, double dt, double goal)
    {
        var type = boss.Type;
        if (boss.RetreatTime < type.LeapCrouch)
        {
            if (boss.State != SlimeState.Moving) return false;
            boss.RetreatTime += dt;
            boss.Crouch = Math.Clamp(boss.RetreatTime / type.LeapCrouch, 0, 1);
            boss.LeapFromX = boss.X;
            boss.Facing = goal < boss.X ? -1 : 1;
            if (boss.RetreatTime >= type.LeapCrouch) events.Add(new(BattleEventKind.BossLeaping, boss.X));
            return false;
        }
        boss.RetreatTime += dt;
        boss.Crouch = 0;
        boss.KnockVelocity = 0;
        double k = Math.Clamp((boss.RetreatTime - type.LeapCrouch) / type.LeapSeconds, 0, 1);
        boss.X = boss.LeapFromX + (goal - boss.LeapFromX) * k;
        boss.LeapHeight = Math.Sin(Math.PI * k) * LeapPeak(Math.Abs(goal - boss.LeapFromX));
        if (k < 1) return false;
        boss.LeapHeight = 0;
        AddEffect(BattleEffectKind.Thud, boss.X, 0);
        AddEffect(BattleEffectKind.Poof, boss.X, 4, 2);
        events.Add(new(BattleEventKind.BossLanded, boss.X));
        return true;
    }

    // How high a leap flies: higher for a longer one, never out of the battle strip.
    public static double LeapPeak(double distance) => Math.Clamp(25 + distance * 0.08, 25, 80);

    // The shield phase on the move: the run to the far edge (at most RetreatSeconds), then the call;
    // the shield drops once every helper of this phase is gone (at once if none could come).
    private void StepShield(Slime boss, double dt, double minX, double maxX)
    {
        BossShieldSeconds += dt;
        if (boss.Retreating)
        {
            double goal = boss.RetreatSide > 0 ? maxX : minX;
            if (boss.Type.RetreatLeap) { if (!StepLeap(boss, dt, goal)) return; }
            else
            {
                boss.RetreatTime += dt;
                if (boss.State == SlimeState.Moving)
                {
                    double step = boss.Type.RetreatSpeed * level.EnemySpeed * dt;
                    boss.X = Math.Clamp(boss.X + Math.Sign(goal - boss.X) * Math.Min(Math.Abs(goal - boss.X), step), minX, maxX);
                    boss.Facing = goal < boss.X ? -1 : 1;
                }
                if (Math.Abs(goal - boss.X) > 2 && boss.RetreatTime < boss.Type.RetreatSeconds) return;
            }
            boss.Retreating = false;
            boss.Facing = -boss.RetreatSide;
            var spots = HelperSpots(boss, boss.PendingCalls ?? new List<string>(), boss.RetreatSide);
            boss.PendingCalls = null;
            // Announced first: rings on the floor where they will appear (the shield holds meanwhile).
            if (boss.Type.SummonWarning > 0 && spots.Count > 0)
            {
                boss.CallSpots = spots;
                boss.SummonWarn = boss.Type.SummonWarning;
                events.Add(new(BattleEventKind.BossSummoning, boss.X));
                return;
            }
            if (SpawnHelpers(boss, spots, boss.ShieldTag) > 0) { events.Add(new(BattleEventKind.BossSummoned, boss.X)); return; }
        }
        if (boss.CallSpots != null)
        {
            boss.SummonWarn = Math.Max(0, boss.SummonWarn - dt);
            if (boss.SummonWarn > 0) return;
            var called = boss.CallSpots;
            boss.CallSpots = null;
            if (SpawnHelpers(boss, called, boss.ShieldTag) > 0) { events.Add(new(BattleEventKind.BossSummoned, boss.X)); return; }
        }
        // While this phase's helpers stand, the boss may rally the fighter nearest the cat that is not elite yet.
        bool helpers = false;
        Slime? nearest = null;
        foreach (var s in slimes)
        {
            if (s.SummonedBy != boss.Id || s.PhaseTag != boss.ShieldTag || s.State == SlimeState.Defeated) continue;
            helpers = true;
            if (!s.Type.Support && s.Elite == null && s.Targetable && (nearest == null || Math.Abs(s.X - lastCatX) < Math.Abs(nearest.X - lastCatX))) nearest = s;
        }
        if (helpers) { StepRally(boss, dt, nearest); return; }
        boss.Shielded = false;
        boss.ShieldTag = 0;
        boss.RallyCharge = 0;
        events.Add(new(BattleEventKind.BossShieldDown, boss.X));
        // An opening: dazed for a moment, taking damage as usual.
        if (boss.Type.ShieldBreakStun > 0)
        {
            boss.KnockVelocity = 0;
            boss.StunRemaining = boss.Type.ShieldBreakStun;
            boss.Set(SlimeState.Stunned);
        }
    }

    // The rally call: every RallyEvery s the crown glows RallyCharge s (a push now cuts it short),
    // then the chosen helper becomes elite for good (MakeElite).
    private void StepRally(Slime boss, double dt, Slime? helper)
    {
        var type = boss.Type;
        if (type.RallyEvery <= 0) return;
        if (boss.RallyCharge > 0)
        {
            if (boss.State == SlimeState.Stunned) { boss.RallyCharge = 0; boss.RallyTargetId = 0; boss.RallyCooldown = type.RallyEvery; return; }
            boss.RallyCharge = Math.Max(0, boss.RallyCharge - dt);
            if (boss.RallyCharge > 0) return;
            boss.RallyCooldown = type.RallyEvery;
            // The helper the glow pointed at, or the nearest one if that one is gone.
            if (Find(boss.RallyTargetId) is { Targetable: true, Elite: null } chosen) helper = chosen;
            boss.RallyTargetId = 0;
            if (helper == null) return;
            MakeElite(helper, type);
            events.Add(new(BattleEventKind.BossRallied, helper.X, helper.Id));
            return;
        }
        boss.RallyCooldown -= dt;
        if (boss.RallyCooldown > 0 || helper == null || boss.State == SlimeState.Stunned) return;
        boss.RallyCharge = type.RallyCharge;
        boss.RallyTargetId = helper.Id;
        events.Add(new(BattleEventKind.BossRallying, boss.X));
    }

    // The helper a glowing crown is about to rally (null when it is not rallying).
    public Slime? RallyTarget(Slime boss) => boss.RallyCharge > 0 ? Find(boss.RallyTargetId) : null;

    // Rallied: its kind's own elite affix if it has one (a plain one otherwise), RallyHp more max HP
    // (that much HP at once) and its hits × RallyDamage. Never counted against MaxElites.
    private static readonly EliteSpec PlainElite = new();
    private static void MakeElite(Slime helper, EnemyType boss)
    {
        helper.Rallied = true;
        ApplyElite(helper, helper.Type.Elite ?? PlainElite);
        double more = Math.Round(helper.MaxHp * boss.RallyHp);
        helper.MaxHp += more;
        helper.Hp += more;
        helper.Damage = Math.Round(helper.Damage * boss.RallyDamage);
    }

    // Turning elite, once per enemy (a roll at spawn or a rally): the affix's trait and its stats on
    // top of what difficulty and threat gave it. More max HP comes with that much HP at once.
    private static void ApplyElite(Slime slime, EliteSpec elite)
    {
        if (slime.Elite != null) return;
        slime.Elite = elite;
        slime.SureBlocks = elite.SureBlocks;
        slime.HealBoost = elite.FirstHeal;
        double more = Math.Round(slime.MaxHp * (elite.Hp - 1));
        slime.MaxHp += more;
        slime.Hp += more;
        if (elite.Damage != 1) slime.Damage = Math.Round(slime.Damage * elite.Damage);
        if (elite.HealBudget != 1) slime.HealLeft = Math.Round(slime.HealLeft * elite.HealBudget);
        slime.HealCooldown *= elite.HealCooldown;
    }

    // The wait after an attack and after a heal (an elite's are shorter).
    private static double AttackCooldown(Slime slime) => slime.Type.AttackCooldown * (slime.Elite?.AttackCooldown ?? 1);
    private static double HealCooldown(Slime slime) => slime.Type.HealCooldown * (slime.Elite?.HealCooldown ?? 1);

    private Slime? PickTarget(double catX)
    {
        if (SelectedId is int selected)
        {
            var chosen = Find(selected);
            // A shielded boss stays chosen; the cat goes for the others until its shield drops.
            if (chosen != null && chosen.Targetable && !chosen.Shielded && !chosen.Mended) return chosen;
            if (chosen == null || !chosen.Targetable) SelectedId = null;
        }
        // Never a shielded boss: the cat cannot hurt it (with nothing else to hit it waits).
        // One guarded by a heal is left alone too while there is another to hit.
        Slime? nearest = null, guarded = null;
        foreach (var s in slimes)
        {
            if (!s.Targetable || s.Shielded) continue;
            if (s.Mended) { if (guarded == null || Math.Abs(s.X - catX) < Math.Abs(guarded.X - catX)) guarded = s; continue; }
            if (nearest == null || Math.Abs(s.X - catX) < Math.Abs(nearest.X - catX)) nearest = s;
        }
        nearest ??= guarded;
        // Keep the current choice unless another slime is clearly closer (no dithering).
        var current = Find(autoTargetId);
        if (current != null && current.Targetable && !current.Shielded && (!current.Mended || nearest == current) && nearest != null &&
            Math.Abs(current.X - catX) <= Math.Abs(nearest.X - catX) + 40) nearest = current;
        autoTargetId = nearest?.Id ?? 0;
        return nearest;
    }

    private (double Min, double Max) Bounds(Slime slime)
    {
        double min = Left + slime.Width / 2;
        return (min, Math.Max(min, Right - slime.Width / 2));
    }

    private void StepSlimes(double dt, double catX, bool catHeld)
    {
        var s = Settings;
        // Who guards whom is decided a few times a second, from where the cat was then (a reaction
        // delay, never where it will be); the slimes still move every frame.
        decisionClock -= dt;
        if (decisionClock <= 0 && !Over)
        {
            decisionClock = DecisionInterval;
            decisionCatX = catX;
            AssignGuards();
        }
        RefreshGuards();
        // By index over the slimes there were at the start: a hit on the cat can send damage back
        // (Thorns) and make a boss call helpers, which join the list and move from the next frame.
        int count = slimes.Count;
        for (int index = 0; index < count; index++)
        {
            var slime = slimes[index];
            var (minX, maxX) = Bounds(slime);
            slime.StateTime += dt;
            slime.HitFlash = Math.Max(0, slime.HitFlash - dt);
            slime.Cooldown = Math.Max(0, slime.Cooldown - dt);
            slime.HealCooldown = Math.Max(0, slime.HealCooldown - dt);
            slime.FarHealCooldown = Math.Max(0, slime.FarHealCooldown - dt);
            if (slime.KnockVelocity != 0)
            {
                slime.X += slime.KnockVelocity * dt;
                slime.KnockVelocity *= Math.Exp(-6 * dt);
                if (Math.Abs(slime.KnockVelocity) < 5) slime.KnockVelocity = 0;
            }
            slime.X = Math.Clamp(slime.X, minX, maxX);
            if (Over) continue; // The outro: slimes just bounce in place.
            if (slime.Shielded && slime.State != SlimeState.Defeated)
            {
                StepShield(slime, dt, minX, maxX);
                if (slime.Retreating && slime.State == SlimeState.Moving) continue; // Running for the edge: nothing else.
            }
            double dx = catX - slime.X;
            switch (slime.State)
            {
                case SlimeState.Spawning:
                    if (slime.StateTime >= s.SpawnDuration * (slime.IsBoss ? 2 : 1)) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Stunned:
                    slime.StunRemaining -= dt;
                    if (slime.StunRemaining <= 0) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Recover:
                    if (slime.StateTime >= slime.Type.Recover) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Windup:
                    // A rescue never freezes the field: the windup runs on its announced time, so no
                    // attack waits to land on the cat's way down. Falling while the cat is lifted it
                    // misses; once the cat is down again it lands as usual (in reach, no landing
                    // protection), so lifting and dropping on the spot cannot cancel every hit. A cat
                    // only pressed (not lifted) is on the floor and is hit too.
                    if (slime.StateTime >= Windup(slime))
                    {
                        if (slime.Type.Dash) { slime.DashLeft = slime.Type.DashDistance; slime.DashHit = false; slime.Set(SlimeState.Dashing); }
                        else Strike(slime, catX);
                    }
                    break;
                case SlimeState.Dashing:
                    StepDash(slime, dt, catX, minX, maxX);
                    break;
                case SlimeState.Casting:
                    if (slime.StateTime >= HealWindup(slime)) FinishHeal(slime);
                    break;
                case SlimeState.Moving:
                    // A support enemy heals a hurt ally when it can (before thinking of the cat): one in
                    // reach, or else a badly hurt one anywhere on the field (a far heal).
                    if (slime.Type.HealAmount > 0 && slime.HealCooldown <= 0 && slime.HealLeft > 0 && (HealTarget(slime) ?? FarHealTarget(slime)) is Slime ally)
                    {
                        slime.FarHeal = Math.Abs(ally.X - slime.X) > slime.Type.HealRange;
                        slime.HealTargetId = ally.Id;
                        slime.Facing = ally.X < slime.X ? -1 : 1;
                        slime.Set(SlimeState.Casting);
                        events.Add(new(BattleEventKind.EnemyHealing, slime.X, slime.FarHeal ? 1 : 0));
                        // HealGuard: the ally takes no damage while the heal winds up (never a boss; the
                        // same ally only again HealGuardRepeat s after this heal would end).
                        slime.MendingId = 0;
                        if (slime.Type.HealGuard && !ally.IsBoss && ally.GuardAgainAt <= Time)
                        {
                            slime.MendingId = ally.Id;
                            ally.Mended = true;
                            ally.GuardAgainAt = Time + HealWindup(slime) + slime.Type.HealGuardRepeat;
                            events.Add(new(BattleEventKind.EnemyWarded, ally.X));
                        }
                        break;
                    }
                    // A support keeps behind its fighting allies and leaves the cat to them.
                    if (slime.Type.Support && (slime.KeepingBack = KeepBack(slime, dt, catX, minX, maxX))) break;
                    // A dasher runs off after its lunge.
                    if (RunAway(slime, dt, catX, minX, maxX)) break;
                    // While the user carries the cat the slimes follow it along the floor, but never
                    // wind up: a lifted cat cannot be hit. A slow-turning kind faces the cat only after
                    // its turn, and neither walks nor winds up meanwhile.
                    bool facingCat = Math.Abs(dx) <= 1 || Turn(slime, dx < 0 ? -1 : 1, dt);
                    // A dash enemy starts its windup from further away; its lane is set now.
                    double reach = slime.Type.Dash ? slime.Type.DashRange : slime.Range;
                    if (!catHeld && facingCat && Math.Abs(dx) <= reach && slime.Cooldown <= 0 && Grace <= 0 && MayAttack(slime))
                    {
                        slime.DashDirection = slime.Facing;
                        slime.Set(SlimeState.Windup);
                        lastWindup = Time;
                    }
                    // A guard holds its post between the cat and its support (it still hits a cat in reach).
                    else if (facingCat && GuardPost(slime) is double post)
                    {
                        if (Math.Abs(post - slime.X) > 6) slime.X = Math.Clamp(slime.X + Math.Sign(post - slime.X) * Math.Min(Math.Abs(post - slime.X), Speed(slime) * dt), minX, maxX);
                    }
                    // A shielded boss guards its edge: it slams a cat that comes close but never walks up.
                    else if (facingCat && Math.Abs(dx) > reach * 0.75 && !slime.Shielded)
                        slime.X =Math.Clamp(slime.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), Speed(slime) * dt), minX, maxX);
                    break;
            }
            if (Over) break;
        }
        // Plain loops here and below: this runs every frame, so no per-call lambdas or lists.
        for (int i = slimes.Count - 1; i >= 0; i--)
            if (slimes[i].State == SlimeState.Defeated && slimes[i].StateTime >= s.DefeatDuration * (slimes[i].IsBoss ? 1.5 : 1)) slimes.RemoveAt(i);
        Separate();
    }

    public double Windup(Slime slime) => (slime.Enraged ? slime.Type.EnragedWindup : slime.Type.Windup) + (slime.Elite?.WindupAdd ?? 0);
    // A far heal winds up longer than one in reach.
    public static double HealWindup(Slime healer) => healer.FarHeal ? healer.Type.FarHealWindup : healer.Type.HealWindup;

    // How often guards are chosen (8 times a second) and where the cat stood when they last were.
    private const double DecisionInterval = 0.125;
    private double decisionClock, decisionCatX;
    // When the last windup began (attack turns).
    private double lastWindup = double.NegativeInfinity;

    // Faces `want` now, or after the kind's TurnSeconds of wanting to; true once it does.
    private static bool Turn(Slime slime, int want, double dt)
    {
        if (slime.Facing == want) { slime.TurnTime = 0; return true; }
        slime.TurnTime += dt;
        if (slime.Type.TurnSeconds > 0 && slime.TurnTime < slime.Type.TurnSeconds) return false;
        slime.Facing = want;
        slime.TurnTime = 0;
        return true;
    }

    // Attack turns: a free turn (fewer than MaxAttackers winding up or dashing) and AttackStagger
    // since the last windup. The boss never waits for one (its slam is the fight's centre).
    private bool MayAttack(Slime slime)
    {
        if (slime.IsBoss) return true;
        if (Time - lastWindup < Settings.AttackStagger) return false;
        if (Settings.MaxAttackers <= 0) return true;
        int busy = 0;
        foreach (var s in slimes) if (s != slime && s.State is SlimeState.Windup or SlimeState.Dashing) busy++;
        return busy < Settings.MaxAttackers;
    }

    // Each support still standing gets the nearest free blocker as its guard.
    private void AssignGuards()
    {
        foreach (var s in slimes) s.GuardOf = 0;
        foreach (var support in slimes)
        {
            if (!support.Type.Support || support.State == SlimeState.Defeated) continue;
            Slime? guard = null;
            foreach (var s in slimes)
                if (s.Type.Blocker && s.GuardOf == 0 && s.Targetable && !s.Shielded && (guard == null || Math.Abs(s.X - support.X) < Math.Abs(guard.X - support.X))) guard = s;
            if (guard != null) guard.GuardOf = support.Id;
        }
    }

    // A guard's post: between the cat (where it was at the last decision) and its support, the
    // support's KeepBack in front of it (closer when the cat is nearer). Null: not guarding.
    private double? GuardPost(Slime guard)
    {
        if (guard.GuardOf == 0 || Find(guard.GuardOf) is not { State: not SlimeState.Defeated } support) return null;
        int side = support.X >= decisionCatX ? 1 : -1;
        return support.X - side * Math.Min(support.Type.KeepBack, Math.Max(0, Math.Abs(support.X - decisionCatX) - 40));
    }

    // A far heal (FarHealBelow > 0, once per FarHealCooldown s): the most hurt fighting ally below
    // FarHealBelow of its HP, wherever it is on the field (null: none, or not yet).
    private Slime? FarHealTarget(Slime healer)
    {
        var type = healer.Type;
        if (type.FarHealBelow <= 0 || healer.FarHealCooldown > 0) return null;
        Slime? hurt = null;
        foreach (var s in slimes)
            if (s != healer && s.Targetable && !s.Type.Support && !s.Shielded && s.Hp < s.MaxHp * type.FarHealBelow &&
                (hurt == null || s.Hp / s.MaxHp < hurt.Hp / hurt.MaxHp)) hurt = s;
        return hurt;
    }

    // The most hurt ally in reach: alive, not this one, below its max HP.
    private Slime? HealTarget(Slime healer)
    {
        Slime? best = null;
        foreach (var s in slimes)
        {
            if (s == healer || !s.Targetable || s.Shielded || s.Hp >= s.MaxHp || Math.Abs(s.X - healer.X) > healer.Type.HealRange) continue;
            if (best == null || s.MaxHp - s.Hp > best.MaxHp - best.Hp) best = s;
        }
        return best;
    }

    // A support with a fighting ally (any enemy but a support, even one still appearing) on the
    // field: it stays on its side of the cat, KeepBack DIPs behind the nearest such ally there (or
    // that far from the cat when none is), walking at its own speed (slower than the cat runs, so
    // the cat catches it; at the field's edge it stops). False: no fighting ally, so it fights.
    private bool KeepBack(Slime support, double dt, double catX, double minX, double maxX)
    {
        int side = support.X >= catX ? 1 : -1;
        bool allies = false;
        double nearest = double.NaN, guard = double.NaN;
        foreach (var s in slimes)
        {
            if (s == support || s.State == SlimeState.Defeated || s.Type.Support) continue;
            allies = true;
            double ahead = (s.X - catX) * side;
            if (ahead >= 0 && (double.IsNaN(nearest) || ahead < nearest)) nearest = ahead;
            if (ahead >= 0 && s.GuardOf == support.Id && s.Targetable) guard = ahead;
        }
        if (!allies) return false;
        // HideBehindGuard: behind its own guard (the rock between it and the cat), not just any ally.
        if (support.Type.HideBehindGuard && !double.IsNaN(guard)) nearest = guard;
        double want = Math.Clamp(catX + side * ((double.IsNaN(nearest) ? 0 : nearest) + support.Type.KeepBack), minX, maxX);
        double gap = want - support.X;
        support.Facing = catX < support.X ? -1 : 1;
        // FleeSpeed: backing off from a cat that comes close it hurries (still slower than the cat runs).
        double speed = Speed(support);
        if (Math.Sign(gap) == side) speed *= FleeFactor(support, Math.Abs(support.X - catX));
        if (Math.Abs(gap) > 6) support.X = Math.Clamp(support.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), speed * dt), minX, maxX);
        return true;
    }

    // How much faster a support backs off with the cat this close: 1 from 200 DIPs, FleeSpeed at 60.
    public static double FleeFactor(Slime support, double distance) =>
        1 + (support.Type.FleeSpeed - 1) * Math.Clamp((200 - distance) / 140, 0, 1);

    // HitAndRun: after its dash a dasher runs straight away from the cat (to the field's edge at most).
    private bool RunAway(Slime slime, double dt, double catX, double minX, double maxX)
    {
        if (slime.RunAwayLeft <= 0) return false;
        int away = slime.X >= catX ? 1 : -1;
        double before = slime.X, step = Speed(slime) * dt;
        slime.X = Math.Clamp(slime.X + away * step, minX, maxX);
        slime.Facing = away;
        slime.RunAwayLeft = slime.X == before ? 0 : slime.RunAwayLeft - step;
        return true;
    }

    // Who a heal guards: only allies of supports still winding up their heal on them. Run again the
    // moment a guard ends (heal done, healer pushed or defeated), so no hit falls in a stale guard;
    // another healer's guard still standing is kept.
    private void RefreshGuards()
    {
        foreach (var e in slimes) e.Mended = false;
        foreach (var e in slimes)
            if (e.State == SlimeState.Casting && e.MendingId != 0 && Find(e.MendingId) is { Targetable: true } guarded) guarded.Mended = true;
    }

    // The heal lands if its ally is still there, alive and in reach (a little slack for movement);
    // a far heal reaches it anywhere.
    private void FinishHeal(Slime healer)
    {
        healer.MendingId = 0;
        healer.Set(SlimeState.Recover);
        RefreshGuards();
        healer.HealCooldown = HealCooldown(healer);
        bool far = healer.FarHeal;
        if (far) healer.FarHealCooldown = healer.Type.FarHealCooldown;
        healer.FarHeal = false;
        var ally = Find(healer.HealTargetId);
        healer.HealTargetId = 0;
        if (ally == null || !ally.Targetable || (!far && Math.Abs(ally.X - healer.X) > healer.Type.HealRange + 20)) return;
        // Its heals scale like enemy HP (HealPower); an elite's first heal that lands is stronger (still within its budget).
        double heal = healer.Type.HealAmount * healer.HealPower * healer.HealBoost;
        if (healer.HealPower != 1) heal = Math.Round(heal);
        double amount = Math.Min(Math.Min(heal, ally.MaxHp - ally.Hp), healer.HealLeft);
        if (amount <= 0) return;
        ally.Hp += amount;
        healer.HealLeft -= amount;
        healer.HealBoost = 1;
        AddEffect(BattleEffectKind.Mend, ally.X, ally.Height / 2);
        AddEffect(BattleEffectKind.Number, ally.X + (ally.IsBoss ? ally.Width * 0.38 : 0), ally.Height + 10, -Math.Round(amount));
        events.Add(new(BattleEventKind.EnemyHealed, ally.X, amount));
    }

    // The ally a support enemy is healing right now (null when it is not casting).
    public Slime? Healing(Slime healer) => healer.State == SlimeState.Casting ? Find(healer.HealTargetId) : null;
    private double Speed(Slime slime) => (slime.Enraged ? slime.Type.EnragedSpeed : slime.Type.Speed) * level.EnemySpeed;

    private readonly List<Slime> scratch = new();
    private static readonly Comparison<Slime> ByX = (a, b) => a.X.CompareTo(b.X);

    // Slimes do not stack on one spot. A support falling back stays out of this: it has to slip past
    // the ally it gets behind (pushing each other, the two would jam and hold the ally off the cat).
    // Blockers keep apart only from each other: they bar the cat's way, not their allies' (a rock in
    // front would otherwise shield the cat from everything behind it).
    private void Separate()
    {
        Separate(false);
        Separate(true);
    }

    private void Separate(bool blockers)
    {
        var live = scratch;
        live.Clear();
        // A leaping boss flies over the others.
        foreach (var slime in slimes) if (slime.State != SlimeState.Defeated && !slime.KeepingBack && slime.LeapHeight <= 0 && slime.Type.Blocker == blockers) live.Add(slime);
        live.Sort(ByX);
        for (int i = 1; i < live.Count; i++)
        {
            double gap = (live[i].Width + live[i - 1].Width) / 2 * 0.7;
            double overlap = gap - (live[i].X - live[i - 1].X);
            if (overlap <= 0) continue;
            var (minA, maxA) = Bounds(live[i - 1]);
            var (minB, maxB) = Bounds(live[i]);
            live[i - 1].X = Math.Clamp(live[i - 1].X - overlap / 2, minA, maxA);
            live[i].X = Math.Clamp(live[i].X + overlap / 2, minB, maxB);
        }
    }

    private void Strike(Slime slime, double catX)
    {
        slime.Set(SlimeState.Recover);
        slime.Cooldown = AttackCooldown(slime);
        if (slime.Type.Slam) AddEffect(BattleEffectKind.Slam, slime.X, 0, slime.Range);
        // Missed: the cat moved away, is protected, or is off the floor right now.
        if (CatLifted || Math.Abs(catX - slime.X) > slime.Range + 12 || Grace > 0) return;
        HurtCat(slime, catX, false);
    }

    // Half the cat's width on the floor: a dash that passes over this hits it.
    public const double CatHalfWidth = 18;

    // The lunge is fast, so the hit is checked along the whole path of this frame (never skipped
    // between two frames), once per dash, and only while the cat is on the floor (a pressed cat
    // too, without the stun) and not under landing protection. The dash runs its locked lane to
    // the end whatever the cat does, never turning after it.
    private void StepDash(Slime slime, double dt, double catX, double minX, double maxX)
    {
        double before = slime.X;
        double step = Math.Min(slime.DashLeft, slime.Type.DashSpeed * (slime.Elite?.DashSpeed ?? 1) * level.EnemySpeed * dt);
        slime.X = Math.Clamp(slime.X + slime.DashDirection * step, minX, maxX);
        slime.DashLeft = slime.X == before ? 0 : slime.DashLeft - step; // Stopped by the field's edge.
        double reach = slime.HitWidth / 2 + CatHalfWidth;
        if (!slime.DashHit && !CatLifted && Grace <= 0 && catX >= Math.Min(before, slime.X) - reach && catX <= Math.Max(before, slime.X) + reach)
        {
            slime.DashHit = true;
            HurtCat(slime, catX, true);
        }
        if (slime.DashLeft <= 0 && !Over)
        {
            slime.Set(SlimeState.Recover);
            slime.Cooldown = AttackCooldown(slime);
            slime.RunAwayLeft = slime.Type.HitAndRun; // Hit and run: off it goes once it recovers.
        }
    }

    // Every hit on the cat: the Shield takes all of it (no damage, no stun), otherwise armor
    // lessens it. stun: also stun the cat, unless it is in the user's hands, already stunned or
    // still immune after its last stun.
    private void HurtCat(Slime from, double catX, bool stun)
    {
        if (ShieldRemaining > 0)
        {
            events.Add(new(BattleEventKind.Blocked, catX));
            if (counterBonus > 0) CounterReady = true;
            return;
        }
        // Turtle shell: one whole hit stopped (no damage, no stun), then it grows back.
        if (ShellReady)
        {
            ShellCooldown = shellRegrow;
            ShellBlocks++;
            if (counterBonus > 0) CounterReady = true;
            AddEffect(BattleEffectKind.Block, catX, 50);
            events.Add(new(BattleEventKind.ShellBlocked, catX));
            return;
        }
        double hurt = from.Damage * ArmorFactor * (1 + recklessHurt);
        CatHp = Math.Max(0, CatHp - hurt);
        HurtFlash = Settings.HurtFlashSeconds;
        events.Add(new(BattleEventKind.CatHurt, catX, hurt));
        if (CatHp <= 0) { Finish(BattlePhase.Lost); return; }
        // Thorns: part of what the cat really took goes back to the attacker.
        if (Active("thorns") is SynergySpec thorns && hurt > 0 && from.Targetable && !from.Shielded)
        {
            Reflects++;
            events.Add(new(BattleEventKind.Reflected, from.X));
            Damage(from, hurt * thorns.Amount, DamageSource.Reflect);
            if (Over) return;
        }
        if (stun && !CatHeld && !CatStunned && StunImmunity <= 0 && Settings.StunDurationSeconds > 0)
        {
            CatStunRemaining = Settings.StunDurationSeconds;
            StunsTaken++;
            CancelSwing();
            events.Add(new(BattleEventKind.CatStunned, catX));
        }
    }

    // True while one bomb's blast is dealt (see StepBombs).
    private bool blasting;

    private void StepBombs(double dt)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var bomb = bombs[i];
            bomb.Remaining -= dt;
            if (bomb.Remaining > 0) continue;
            bombs.RemoveAt(i);
            AddEffect(BattleEffectKind.Explosion, bomb.X, 0, Settings.BombRadius);
            events.Add(new(BattleEventKind.BombExploded, bomb.X));
            // One blast: a healer and the ally it guards caught together keep the guard for it,
            // whichever comes first in the list.
            blasting = true;
            foreach (var slime in slimes.ToArray())
                if (slime.Targetable && Math.Abs(slime.X - bomb.X) <= Settings.BombRadius + slime.HitWidth / 2)
                    Damage(slime, Settings.BombDamage, DamageSource.Bomb);
            blasting = false;
            RefreshGuards();
        }
    }

    private void StepWaves(double dt, double catX)
    {
        if (Phase == BattlePhase.WaveBreak)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            if (PhaseRemaining > 0) return;
            Wave++;
            pending.Clear(); pendingNext = 0;
            farEdge = Far(catX); streamEdge = 0;
            var wave = WavePlan[Math.Min(Wave, WaveCount) - 1]; // A survive stage repeats its last wave.
            stream = wave.Stream;
            if (stream == null) pending.AddRange(SpawnDirector.Units(wave, spawnRandom, Cost));
            eliteBudget = SpawnDirector.BudgetLeft(wave, pending, Cost);
            Phase = BattlePhase.Fighting;
            spawnTimer = 0;
            events.Add(new(BattleEventKind.WaveStarted, catX, Wave));
        }
        spawnTimer = Math.Max(0, spawnTimer - dt);
        // An announced spawn comes once its arrow has run out and the floor has room; nothing else
        // spawns before it.
        if (incoming != null)
        {
            IncomingRemaining = Math.Max(0, IncomingRemaining - dt);
            if (IncomingRemaining > 0 || Alive + incoming.Length > maxAlive) return;
            SpawnUnit(catX, incoming, IncomingEdge);
            incoming = null;
            spawnTimer = NextSpawnIn();
            return;
        }
        if (stream != null)
        {
            // Never more than maxAlive at once, however long the stream runs.
            if (Alive < maxAlive && spawnTimer <= 0)
            {
                var kind = new[] { SpawnDirector.Pick(stream.Weights, spawnRandom) };
                // Its budget: a kind that does not fit gives way to the cheapest; nothing fits, it waits.
                if (stream.Budget > 0 && AliveCost() + Cost(kind[0]) > stream.Budget)
                {
                    kind[0] = SpawnDirector.Cheapest(stream.Weights, Cost);
                    if (AliveCost() + Cost(kind[0]) > stream.Budget) return;
                }
                // Late in the stage the stream comes from both sides in turn, each spawn announced.
                if (stream.PincerAfter > 0 && Stage?.SurviveSeconds is double hold && hold - SurviveRemaining >= stream.PincerAfter)
                {
                    streamEdge = streamEdge == 0 ? Far(catX) : -streamEdge;
                    Announce(catX, kind, streamEdge);
                }
                else { SpawnUnit(catX, kind, 0); spawnTimer = NextSpawnIn(); }
            }
            return; // Only the survive clock ends it.
        }
        // A unit waits until the floor has room for all of it (later ones wait behind it). Side "a"
        // comes from the edge far from the cat, "b" from the other one after a warning arrow.
        if (pendingNext < pending.Count && Alive + pending[pendingNext].Kinds.Length <= maxAlive && spawnTimer <= 0)
        {
            var unit = pending[pendingNext++];
            if (unit.Side == "b") Announce(catX, unit.Kinds, -farEdge);
            else { SpawnUnit(catX, unit.Kinds, unit.Side == "a" ? farEdge : 0); spawnTimer = NextSpawnIn(); }
        }
        if (ToSpawn > 0 || Alive > 0 || bombs.Count > 0) return;
        if (Wave >= WaveCount && !Survival) { Finish(BattlePhase.Won); return; }
        Phase = BattlePhase.WaveBreak;
        PhaseRemaining = Settings.WaveBreakSeconds;
        LastHeal = Math.Max(0, Math.Min(Settings.WaveHeal, CatMaxHp - CatHp));
        CatHp += LastHeal;
        if (LastHeal > 0) AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(LastHeal));
        events.Add(new(BattleEventKind.WaveCleared, catX));
        if (Stage is { Upgrades: true, UpgradeAt: null }) OfferUpgrades();
    }

    // Up to three different cards the build can still use, one from each group (Defense, Power,
    // Speed) while a group has any left, so every build can always go on; other cards fill the
    // gaps. When the cat is hurt an instant heal takes the place of the group card the build has
    // the fewest points in. Never a card at its limit, never a heal at full HP, and no offer once
    // the stage's maxUpgrades picks are made; nothing left: no choice this time. A group's rare card
    // (RareChance) may stand in for its common one, never as a filler.
    private void OfferUpgrades()
    {
        if (Stage?.MaxUpgrades is int cap && Picks >= cap) return;
        bool Open(UpgradeSpec u) => u.MaxStacks == 0 || Stacks(u.Id) < u.MaxStacks;
        var cards = new List<UpgradeSpec>();
        foreach (var group in BuildGroups.All)
        {
            var options = Settings.UpgradeList.Where(u => u.Group == group && !u.Rare && Open(u)).ToList();
            UpgradeSpec? card = options.Count > 0 ? options[offerRandom.Next(options.Count)] : null;
            // Its rare card (not taken yet) takes the group's place now and then, rolled on its own
            // stream so the common cards stay what they would have been.
            var rares = Settings.UpgradeList.Where(u => u.Group == group && u.Rare && Open(u)).ToList();
            if (rares.Count > 0 && rarityRandom.NextDouble() < Settings.RareChance) card = rares[rarityRandom.Next(rares.Count)];
            if (card != null) cards.Add(card);
        }
        var rest = Settings.UpgradeList.Where(u => !cards.Contains(u) && !u.Rare && u.Effect != "heal" && Open(u)).ToList();
        while (cards.Count < 3 && rest.Count > 0)
        {
            int i = offerRandom.Next(rest.Count);
            cards.Add(rest[i]);
            rest.RemoveAt(i);
        }
        var heals = Settings.UpgradeList.Where(u => u.Effect == "heal" && Open(u)).ToList();
        if (CatHp < CatMaxHp && heals.Count > 0)
        {
            var heal = heals[offerRandom.Next(heals.Count)];
            if (cards.Count < 3) cards.Add(heal);
            else
            {
                // A filler card (no group) goes first, then the group the build has least of.
                int Points(UpgradeSpec u) => u.Group == null ? -1 : GroupPoints(u.Group);
                int fewest = cards.Min(Points);
                var tied = Enumerable.Range(0, cards.Count).Where(i => Points(cards[i]) == fewest).ToList();
                cards[tied[offerRandom.Next(tied.Count)]] = heal;
            }
        }
        if (cards.Count == 0) return;
        UpgradeOffer = cards.ToArray();
        Armed = null;
        events.Add(new(BattleEventKind.UpgradeOffered, 0, UpgradeOffer.Count));
    }

    // The offer a wave would make now, for an Expedition rest or swap (outside any battle: the
    // caller makes a sim for it alone). Null: nothing to offer.
    public IReadOnlyList<UpgradeSpec>? OfferCards()
    {
        if (UpgradeOffer == null) OfferUpgrades();
        return UpgradeOffer;
    }

    // A relic's gain and price (see RelicSpec), apart from the cards' bonuses.
    private void AddRelic(RelicSpec relic)
    {
        relics.Add(relic);
        switch (relic.Effect)
        {
            case RelicSpec.Counter: counterBonus += relic.Amount; shieldDelay += relic.Penalty; break;
            case RelicSpec.Relief: fishBonus += relic.Amount; fishDelay += relic.Penalty; break;
            case RelicSpec.Reckless: recklessBonus += relic.Amount; recklessBelow = Math.Max(recklessBelow, relic.Below); recklessHurt += relic.Penalty; break;
            case RelicSpec.Light: moveBonus += relic.Amount; cooldownCut += relic.Extra; maxHpBonus -= relic.Penalty; break;
        }
    }

    // The Healing fish's heal and each skill's cooldown in this battle (the relics' changes included).
    public double HealAmount => Settings.HealAmount * (1 + fishBonus);
    public double SkillCooldown(BattleSkill skill) => skill switch
    {
        BattleSkill.Shield => Settings.SkillCooldown(skill) + shieldDelay,
        BattleSkill.Heal => Settings.SkillCooldown(skill) * (1 + fishDelay),
        _ => Settings.SkillCooldown(skill)
    };

    // The user's pick: only a card on offer, only once (a second click finds no offer).
    public bool ChooseUpgrade(string id, double catX = 0)
    {
        if (UpgradeOffer == null || Over) return false;
        UpgradeSpec? pick = null;
        foreach (var u in UpgradeOffer) if (u.Id == id) pick = u;
        if (pick == null) return false;
        UpgradeOffer = null;
        Picks++;
        Take(pick, catX, true);
        events.Add(new(BattleEventKind.UpgradeChosen, catX));
        return true;
    }

    // A card goes into the build. fresh: chosen now (a heal or more max HP heals at once and a new
    // synergy is announced); otherwise carried in from the stage before.
    private void Take(UpgradeSpec card, double catX, bool fresh)
    {
        taken[card.Id] = Stacks(card.Id) + 1;
        switch (card.Effect)
        {
            case "damage": damageBonus += card.Amount; break;
            case "moveSpeed": moveBonus += card.Amount; break;
            case "attackSpeed": cooldownCut += card.Amount; break;
            case "knock": knockBonus += card.Amount; break;
            case "armor": armorCut += card.Amount; break;
            case "shell": shellRegrow = card.Amount; break; // Ready at once.
            case "opener": openerBonus += card.Amount; break;
            case "momentum": momentumBonus += card.Amount; break;
            case "maxHp":
                maxHpBonus += card.Amount;
                if (fresh) { CatHp += card.Amount; AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(card.Amount)); }
                break;
            case "heal":
                double healed = Math.Min(card.Amount, CatMaxHp - CatHp);
                CatHp += healed;
                if (healed > 0) AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(healed));
                break;
        }
        if (card.Group == null) return;
        int points = GroupPoints(card.Group);
        for (int i = 0; i < Settings.SynergyList.Count; i++)
        {
            var y = Settings.SynergyList[i];
            if (y.Group != card.Group || synergies.Contains(y) || points < y.At) continue;
            synergies.Add(y);
            if (fresh) events.Add(new(BattleEventKind.SynergyUnlocked, catX, i));
        }
    }

    // The edge further from the cat: -1 left, +1 right.
    private int Far(double catX) => catX - Left >= Right - catX ? -1 : 1;

    // The wait before the next spawn: a stream's (shorter as the clock runs), otherwise SpawnInterval.
    private double NextSpawnIn()
    {
        if (stream == null) return Settings.SpawnInterval;
        double progress = Stage?.SurviveSeconds is double hold && hold > 0 ? Math.Clamp(1 - SurviveRemaining / hold, 0, 1) : 0;
        return stream.Interval + (stream.IntervalEnd - stream.Interval) * progress;
    }

    // A spawn from behind: an arrow at that edge first (IncomingWarning s). A cat standing at that
    // edge gets it from the other edge instead, unannounced (never on top of the cat).
    private void Announce(double catX, string[] kinds, int edge)
    {
        double x = EdgePoint(edge, Settings.SlimeWidth * kinds.Max(k => Settings.Enemy(k).Scale), 0);
        if (Math.Abs(x - catX) < Settings.SpawnMinDistance)
        {
            SpawnUnit(catX, kinds, -edge);
            spawnTimer = NextSpawnIn();
            return;
        }
        incoming = kinds;
        IncomingEdge = edge;
        IncomingX = x;
        IncomingRemaining = Settings.IncomingWarning;
        events.Add(new(BattleEventKind.Incoming, x));
    }

    private void SpawnUnit(double catX, string[] kinds, int edge)
    {
        if (kinds.Length == 1) Spawn(catX, Settings.Enemy(kinds[0]), edge);
        else SpawnSquad(catX, kinds.Select(Settings.Enemy).ToArray(), edge);
    }

    // edge: -1 / +1 that edge, 0 either. A lone support (no edge asked) joins the fighting ally
    // furthest from the cat, on its far side, when there is one.
    private void Spawn(double catX, EnemyType type, int edge = 0)
    {
        double w = Settings.SlimeWidth * type.Scale;
        double x = SpawnPoint(catX, w, edge);
        if (type.Support && edge == 0)
        {
            Slime? back = null;
            foreach (var s in slimes)
                if (s.State != SlimeState.Defeated && !s.Type.Support && !s.Shielded && (back == null || Math.Abs(s.X - catX) > Math.Abs(back.X - catX))) back = s; // Even one still appearing.
            if (back != null)
            {
                int side = back.X >= catX ? 1 : -1;
                double nearAlly = Math.Clamp(back.X + side * (back.Width / 2 + w / 2 + 10), Left + w / 2, Math.Max(Left + w / 2, Right - w / 2));
                if (Math.Abs(nearAlly - catX) >= SquadCatGap) x = nearAlly;
            }
        }
        var slime = AddSlime(x, type);
        slime.Facing = catX < x ? -1 : 1;
        events.Add(new(type.Boss ? BattleEventKind.BossAppeared : BattleEventKind.SlimeSpawned, x));
        RollElite(slime);
    }

    // What an enemy weighs against a budget (EnemyType.Cost).
    private double Cost(string kind) => Settings.Enemy(kind).Cost;

    // What the enemies the waves sent cost now (a boss's helpers aside), elites with their surcharge.
    private double AliveCost()
    {
        double cost = 0;
        foreach (var s in slimes)
            if (s.State != SlimeState.Defeated && s.SummonedBy == 0) cost += s.Type.Cost + (s.Elite?.Cost ?? 0);
        return cost;
    }

    // An enemy a wave or stream just sent may turn elite: a stage with an elite chance, not on Easy,
    // a kind with an affix, fewer than MaxElites alive, budget for it; then one roll of its own stream.
    private void RollElite(Slime slime)
    {
        if (Stage is not { EliteChance: > 0 } stage || Difficulty == BattleDifficulty.Easy || slime.Type.Elite is not EliteSpec elite) return;
        int elites = 0;
        foreach (var s in slimes) if (s.Elite != null && !s.Rallied && s.State != SlimeState.Defeated) elites++;
        if (elites >= Settings.MaxElites) return;
        if (stream is { Budget: > 0 } flow ? AliveCost() + elite.Cost > flow.Budget : elite.Cost > eliteBudget) return;
        if (affixRandom.NextDouble() >= stage.EliteChance) return;
        if (stream == null) eliteBudget -= elite.Cost;
        ApplyElite(slime, elite);
        ElitesSpawned++;
        events.Add(new(BattleEventKind.EliteAppeared, slime.X, slime.Id));
    }

    // A point near an edge (-1 left, +1 right), up to `spread` DIPs in.
    private double EdgePoint(int edge, double w, double spread)
    {
        double minX = Left + w / 2, maxX = Math.Max(minX, Right - w / 2);
        return edge < 0 ? Math.Min(maxX, minX + spawnRandom.NextDouble() * spread) : Math.Max(minX, maxX - spawnRandom.NextDouble() * spread);
    }

    // Near one edge: the asked one, or a random one at least SpawnMinDistance from the cat when room allows.
    private double SpawnPoint(double catX, double w, int edge = 0)
    {
        if (edge != 0) return EdgePoint(edge, w, 30);
        double minX = Left + w / 2, maxX = Math.Max(minX, Right - w / 2);
        double left = Math.Min(maxX, minX + spawnRandom.NextDouble() * 60), right = Math.Max(minX, maxX - spawnRandom.NextDouble() * 60);
        double first = spawnRandom.Next(2) == 0 ? left : right, second = first == left ? right : left;
        return Math.Abs(first - catX) >= Settings.SpawnMinDistance ? first
            : Math.Abs(second - catX) >= Settings.SpawnMinDistance ? second
            : Math.Abs(first - catX) >= Math.Abs(second - catX) ? first : second;
    }

    // Closest a squad member appears to the cat when the floor allows (centre to centre).
    public const double SquadCatGap = 90;

    // A squad appears together near one edge, side by side: its supports at the back (the edge),
    // its fighters in front of them toward the cat, never on top of the cat when there is room.
    private void SpawnSquad(double catX, EnemyType[] members, int edge = 0)
    {
        var order = members.Where(m => m.Support).Concat(members.Where(m => !m.Support)).ToArray();
        double back = SpawnPoint(catX, Settings.SlimeWidth * order.Max(m => m.Scale), edge);
        int toward = catX >= back ? 1 : -1;
        // Spacing a little wider than Separate keeps, so they do not shove each other on arrival;
        // squeezed (down to a third of it) when the cat stands too close to fit them all.
        var gaps = new double[order.Length];
        for (int i = 1; i < order.Length; i++) gaps[i] = Settings.SlimeWidth * (order[i].Scale + order[i - 1].Scale) / 2 * 0.8;
        double span = gaps.Sum(), room = Math.Abs(catX - back) - SquadCatGap;
        double squeeze = span > 0 && room < span ? Math.Clamp(room / span, 1.0 / 3, 1) : 1;
        double x = back;
        for (int i = 0; i < order.Length; i++)
        {
            x += toward * gaps[i] * squeeze;
            var slime = AddSlime(x, order[i]);
            slime.Facing = catX < slime.X ? -1 : 1;
            RollElite(slime);
        }
        events.Add(new(BattleEventKind.SlimeSpawned, back));
    }

    private Slime AddSlime(double x, EnemyType type)
    {
        // Difficulty × the threat of now, once. Scaled numbers round to whole ones so the HUD and
        // damage numbers stay tidy; Normal at threat 1 is untouched.
        var (threatHp, threatDamage) = SpawnDirector.Scale(Threat, Settings.ThreatDamageShare);
        double hpFactor = level.EnemyHp * threatHp, damageFactor = level.EnemyDamage * threatDamage;
        double hp = hpFactor != 1 ? Math.Max(1, Math.Round(type.MaxHp * hpFactor)) : type.MaxHp;
        var slime = new Slime
        {
            Id = nextId++, Type = type, Width = Settings.SlimeWidth * type.Scale, HitWidth = Settings.SlimeWidth * type.Hitbox, Height = Settings.SlimeHeight * type.Scale,
            Hp = hp, MaxHp = hp, Damage = damageFactor != 1 ? Math.Round(type.Damage * damageFactor) : type.Damage, Range = type.Range
        };
        // A support enemy starts with half its heal cooldown and its whole budget; its heals (and the
        // budget) grow with the same factor as enemy HP, so healing keeps up on later stages.
        slime.HealCooldown = type.HealCooldown / 2;
        slime.HealPower = hpFactor;
        slime.HealLeft = hpFactor != 1 ? Math.Round(type.HealBudget * hpFactor) : type.HealBudget;
        var (minX, maxX) = Bounds(slime);
        slime.X = Math.Clamp(x, minX, maxX);
        slime.Set(SlimeState.Spawning);
        slimes.Add(slime);
        return slime;
    }

    private void Finish(BattlePhase phase)
    {
        Phase = phase;
        UpgradeOffer = null;
        PhaseRemaining = Settings.OutroSeconds;
        FightSeconds = Time;
        Armed = null;
        SelectedId = null;
        TargetId = null;
        BlockedById = 0;
        bombs.Clear();
        incoming = null;
        IncomingRemaining = 0;
        CancelSwing();
        Combo = 0;
        CatStunRemaining = StunImmunity = 0;
        foreach (var s in slimes)
        {
            if (s.State is SlimeState.Windup or SlimeState.Casting or SlimeState.Dashing) s.Set(SlimeState.Moving);
            s.RallyCharge = 0;
            s.FarHeal = false;
            // A leap in the air comes down where it is; announced helpers never come.
            s.Crouch = s.LeapHeight = 0;
            s.CallSpots = null;
            s.Mended = false;
            s.MendingId = 0;
            s.RunAwayLeft = 0;
        }
        events.Add(new(phase == BattlePhase.Won ? BattleEventKind.Won : BattleEventKind.Lost, 0));
    }

    // Click on a slime: the cat goes for it first. Never deals damage by itself.
    public bool Select(int id)
    {
        if (Over || Paused || Find(id) is not { Targetable: true }) return false;
        SelectedId = id;
        return true;
    }

    // Choose an aimed skill (the next slime click uses it), or null to cancel the choice.
    public bool Arm(BattleSkill? skill)
    {
        if (skill is BattleSkill chosen && (!IsAimed(chosen) || !Ready(chosen))) return false;
        Armed = skill;
        return true;
    }

    public bool ArmPush(bool armed) => armed ? Arm(BattleSkill.Push) : Arm(null) || true;

    // An instant skill (Heal, Cheer, Shield).
    public bool Use(BattleSkill skill, double catX)
    {
        if (IsAimed(skill) || !Ready(skill)) return false;
        switch (skill)
        {
            case BattleSkill.Heal:
                double healed = Math.Min(HealAmount, CatMaxHp - CatHp);
                if (healed <= 0) return false; // Full HP: keep the fish for later.
                CatHp += healed;
                AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(healed));
                events.Add(new(BattleEventKind.Healed, catX, healed));
                break;
            case BattleSkill.Cheer:
                CheerRemaining = Settings.CheerSeconds;
                events.Add(new(BattleEventKind.Cheered, catX));
                break;
            default:
                ShieldRemaining = Settings.ShieldSeconds;
                events.Add(new(BattleEventKind.Shielded, catX));
                break;
        }
        cooldowns[(int)skill] = SkillCooldown(skill);
        return true;
    }

    // An aimed skill on a slime. Neither changes the cat's target.
    // direction (Push only): +1 to the right, -1 to the left; null pushes it away from the cat.
    public bool Use(BattleSkill skill, int id, double catX, int? direction = null)
    {
        var slime = Find(id);
        if (!IsAimed(skill) || !Ready(skill) || slime is not { Targetable: true }) return false;
        if (skill == BattleSkill.Push && slime.Elite is { PushImmune: true })
        {
            // It stands firm: no knock back, no stun, its windup goes on; the skill is still spent.
            AddEffect(BattleEffectKind.Block, slime.X, slime.Height / 2);
            events.Add(new(BattleEventKind.PushResisted, slime.X, slime.Id));
        }
        else if (skill == BattleSkill.Push)
        {
            // Knock the slime away from the cat and stun it briefly; the boss is heavy.
            int push = direction is int chosen && chosen != 0 ? Math.Sign(chosen) : slime.X < catX ? -1 : 1;
            // A heal being wound up is cancelled and costs its full cooldown (a far heal's too).
            if (slime.State == SlimeState.Casting)
            {
                slime.HealCooldown = HealCooldown(slime); slime.HealTargetId = 0;
                if (slime.FarHeal) slime.FarHealCooldown = slime.Type.FarHealCooldown;
                slime.FarHeal = false;
                slime.MendingId = 0; // Its guard ends with it (RefreshGuards below).
            }
            slime.KnockVelocity = push * Settings.PushSpeed * slime.Type.PushFactor;
            slime.StunRemaining = Settings.PushStunSeconds * slime.Type.StunFactor;
            slime.Set(SlimeState.Stunned);
            RefreshGuards();
            AddEffect(BattleEffectKind.Push, slime.X, slime.Height / 2);
            events.Add(new(BattleEventKind.Pushed, slime.X));
        }
        else
        {
            // A ball of yarn lands on the slime's spot and bursts after a short delay.
            bombs.Add(new BattleBomb { X = slime.X, Remaining = Settings.BombDelay });
            events.Add(new(BattleEventKind.BombThrown, slime.X));
        }
        cooldowns[(int)skill] = SkillCooldown(skill);
        if (Armed == skill) Armed = null;
        return true;
    }

    // Push direction from where the slime is clicked (DIPs from its centre): the left half sends it
    // right, the right half left (poked from that side); the very centre leaves the default.
    public static int? PushDirection(double offset) => Math.Abs(offset) < 3 ? null : offset < 0 ? 1 : -1;

    public bool UsePush(int id, double catX, int? direction = null) => Use(BattleSkill.Push, id, catX, direction);

    // The work area changed (taskbar, resolution) or the cat now fights on another monitor:
    // keep every slime at the same relative spot inside the new floor.
    public void SetArea(double left, double right)
    {
        right = Math.Max(left + Settings.SlimeWidth * Settings.LargestScale, right);
        double oldWidth = Math.Max(1, Right - Left), newWidth = right - left;
        foreach (var s in slimes)
        {
            s.X = left + (s.X - Left) / oldWidth * newWidth;
            s.LeapFromX = left + (s.LeapFromX - Left) / oldWidth * newWidth;
            if (s.CallSpots is { } spots) for (int i = 0; i < spots.Count; i++) spots[i] = (spots[i].Kind, left + (spots[i].X - Left) / oldWidth * newWidth);
        }
        IncomingX = left + (IncomingX - Left) / oldWidth * newWidth;
        foreach (var e in effects) e.X = left + (e.X - Left) / oldWidth * newWidth;
        foreach (var b in bombs) b.X = left + (b.X - Left) / oldWidth * newWidth;
        Left = left; Right = right;
        foreach (var s in slimes) { var (minX, maxX) = Bounds(s); s.X = Math.Clamp(s.X, minX, maxX); }
    }

    private void AddEffect(BattleEffectKind kind, double x, double height = 0, double value = 0)
    {
        if (effects.Count >= Settings.MaxEffects) effects.RemoveAt(0);
        double life = kind switch
        {
            BattleEffectKind.Poof => 0.6 * Math.Max(1, value), BattleEffectKind.Number => 0.9, BattleEffectKind.Combo => 0.7, BattleEffectKind.Mend => 0.8,
            BattleEffectKind.Explosion or BattleEffectKind.Slam => 0.5, BattleEffectKind.Warded => 0.6, _ => 0.35
        };
        effects.Add(new BattleEffect { Kind = kind, X = x, Height = height, Life = life, Value = value });
    }

    private void StepEffects(double dt)
    {
        foreach (var e in effects) e.Age += dt;
        for (int i = effects.Count - 1; i >= 0; i--) if (effects[i].Age >= effects[i].Life) effects.RemoveAt(i);
    }
}
