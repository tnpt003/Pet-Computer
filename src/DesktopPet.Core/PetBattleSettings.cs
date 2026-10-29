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
    // A combo hit also deals ComboSplash of its damage to every other enemy within ComboSplashRadius
    // DIPs of its target (edge to edge; 0: none) that the swing reaches (in front of the cat, near
    // edge within the weapon's range + 14 DIPs).
    public double ComboSplash { get; init; } = 0.6;
    public double ComboSplashRadius { get; init; } = 60;
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
    // The chance a utility card (no group) takes one place of an upgrade offer (2026-10-24).
    public double UtilityChance { get; init; } = 0.25;
    public double OutroSeconds { get; init; } = 3;
    public double HurtFlashSeconds { get; init; } = 0.35;
    public int MaxEffects { get; init; } = 32;

    // The user's skills.
    public double PushCooldown { get; init; } = 5;
    public double PushSpeed { get; init; } = 520;
    public double PushStunSeconds { get; init; } = 1.4;
    public double HealCooldown { get; init; } = 14;
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
    // 1.5 M05 (PetBattle.Tools.cs). Laser: the dot's time, the reach of its lure, HP a second for a
    // slime on it. Fan: seconds of air, the refill once spent, its reach, the wind's speed and a bump's damage.
    public double LaserCooldown { get; init; } = 14;
    public double LaserSeconds { get; init; } = 4;
    public double LaserLureRange { get; init; } = 300;
    public double LaserDps { get; init; } = 3;
    public double FanSteamSeconds { get; init; } = 3;
    public double FanRefillSeconds { get; init; } = 6;
    public double FanRange { get; init; } = 200;
    public double FanSpeed { get; init; } = 220;
    public double FanBumpDamage { get; init; } = 4;
    // 1.5 M06 (PetBattle.Grabber.cs, PetBattle.Tools.cs). Grabber: the longest hold, the largest kind
    // it lifts (scale), a hit's damage, the daze of a throw, the speed a let-go needs to be a throw.
    // Scissors: a cut's damage, the shortest swipe (DIPs). Spray: seconds of water, the refill once
    // spent, its reach around the cursor, HP a second, how much slower it makes a slime, a soaking's time.
    public double GrabberCooldown { get; init; } = 12;
    public double GrabberHoldSeconds { get; init; } = 3;
    public double GrabberMaxScale { get; init; } = 1.3;
    public double GrabberHitDamage { get; init; } = 8;
    public double GrabberStun { get; init; } = 1;
    public double GrabberThrowSpeed { get; init; } = 300;
    public double ScissorsCooldown { get; init; } = 8;
    public double ScissorsDamage { get; init; } = 6;
    public double ScissorsMinLength { get; init; } = 40;
    public double SprayTankSeconds { get; init; } = 4;
    public double SprayRefillSeconds { get; init; } = 8;
    public double SprayRange { get; init; } = 70;
    public double SprayDps { get; init; } = 2;
    public double SprayChill { get; init; } = 0.35;
    public double SpraySoakSeconds { get; init; } = 1.5;
    // 1.5 M07 (PetBattle.Rage.cs): the rage stance's time, the bar gained by each landed swing of the cat
    // and by each HP it loses (0 and 0: it never fills), how much bigger the cat is drawn, and a raging
    // hit's knock back (× the usual, at least RageKnock DIPs/s).
    public double RageSeconds { get; init; } = 6;
    public double RageHitGain { get; init; } = 0.05;
    public double RageHurtGain { get; init; } = 0.012;
    public double RageScale { get; init; } = 1.3;
    public double RageKnock { get; init; } = 220;
    public double RageKnockFactor { get; init; } = 1.5;

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

    // battle.json "skills": each skill's names and description (null: the built-in texts). The
    // HUD order, aiming and effects stay in code (BattleSkills); validation needs every skill.
    [JsonPropertyName("skills")] public SkillSpec[]? Skills { get; init; }
    [JsonIgnore] public IReadOnlyList<SkillSpec> SkillList => Skills ?? BattleSkills.BuiltIn;
    public SkillSpec Skill(BattleSkill skill)
    {
        foreach (var spec in SkillList) if (string.Equals(spec.Id, skill.ToString(), StringComparison.OrdinalIgnoreCase)) return spec;
        return Array.Find(BattleSkills.BuiltIn, spec => string.Equals(spec.Id, skill.ToString(), StringComparison.OrdinalIgnoreCase))!;
    }

    // The cat's weapons, in the order the battle setup panel shows them. battle.json "weapons"
    // lists them; without it they are the sword (the Cat* numbers above: fast, only a combo
    // knocks back) and the hammer ("hammer": slow, heavy, knocks the slime back on every hit).
    public WeaponSpec Hammer { get; init; } = new();
    [JsonPropertyName("weapons")] public WeaponSpec[]? Weapons { get; init; }
    private IReadOnlyList<WeaponSpec>? weaponList;
    [JsonIgnore] public IReadOnlyList<WeaponSpec> WeaponList => weaponList ??= Weapons ?? new[]
    {
        new WeaponSpec { Id = BattleWeapon.Sword, NameVi = "Kiếm", NameEn = "Sword", HintVi = "nhanh, dễ nối combo", HintEn = "fast, easy combos", Style = "sword", HitEffect = "spark",
            Damage = CatDamage, Range = CatAttackRange, AttackCooldown = CatAttackCooldown, SwingDuration = CatSwingDuration, HitTime = CatHitTime, Knock = 0, StunChance = 0 },
        Hammer with { Id = BattleWeapon.Hammer, NameVi = "Búa", NameEn = "Hammer", HintVi = "chậm, mạnh, đẩy lùi mỗi nhát, đôi khi làm choáng", HintEn = "slow, heavy, knocks back every hit, sometimes stuns", Style = "hammer", HitEffect = Hammer.HitEffect ?? "thud" }
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

    // From battle.json "formations" (1.5 M12): the teams a wave's "formation" group draws from.
    [JsonPropertyName("formations")] public FormationSpec[]? Formations { get; init; }
    [JsonIgnore] public IReadOnlyList<FormationSpec> FormationList => Formations ?? Array.Empty<FormationSpec>();
    public FormationSpec? Formation(string id)
    {
        foreach (var f in FormationList) if (f.Id == id) return f;
        return null;
    }

    // From battle.json "upgrades": the Adventure's between-wave upgrades (null: the built-in seven).
    [JsonPropertyName("upgrades")] public UpgradeSpec[]? Upgrades { get; init; }
    private IReadOnlyList<UpgradeSpec>? upgradeList;
    [JsonIgnore] public IReadOnlyList<UpgradeSpec> UpgradeList => upgradeList ??= Upgrades ?? new[]
    {
        new UpgradeSpec { Id = "sharp", Group = "power", NameVi = "Sắc bén", NameEn = "Sharp edge", DescVi = "+20% sát thương", DescEn = "+20% damage", Effect = "damage", Amount = 0.2, MaxStacks = 3, EndlessMaxStacks = 5 },
        new UpgradeSpec { Id = "quick_feet", Group = "speed", NameVi = "Nhanh chân", NameEn = "Quick feet", DescVi = "+15% tốc độ chạy", DescEn = "+15% running speed", Effect = "moveSpeed", Amount = 0.15, MaxStacks = 2, EndlessMaxStacks = 3 },
        new UpgradeSpec { Id = "fast_paws", Group = "speed", NameVi = "Ra đòn nhanh", NameEn = "Fast paws", DescVi = "−12% thời gian hồi đòn", DescEn = "−12% time between swings", Effect = "attackSpeed", Amount = 0.12, MaxStacks = 3, EndlessMaxStacks = 4 },
        new UpgradeSpec { Id = "heavy_push", Group = "power", NameVi = "Đẩy mạnh", NameEn = "Heavy push", DescVi = "Mỗi nhát đẩy slime lùi thêm", DescEn = "Every hit knocks slimes further", Effect = "knock", Amount = 40, MaxStacks = 2 },
        new UpgradeSpec { Id = "grilled_fish", NameVi = "Cá nướng", NameEn = "Grilled fish", DescVi = "Hồi ngay 30 HP", DescEn = "Get 30 HP back now", Effect = "heal", Amount = 30 },
        new UpgradeSpec { Id = "thick_fur", Group = "defense", NameVi = "Lông dày", NameEn = "Thick fur", DescVi = "−15% sát thương nhận vào", DescEn = "−15% damage taken", Effect = "armor", Amount = 0.15, MaxStacks = 2, EndlessMaxStacks = 3 },
        new UpgradeSpec { Id = "big_heart", Group = "defense", NameVi = "Tim to", NameEn = "Big heart", DescVi = "+20 HP tối đa (hồi luôn 20)", DescEn = "+20 max HP (and 20 HP now)", Effect = "maxHp", Amount = 20, MaxStacks = 2, EndlessMaxStacks = 4 },
        new UpgradeSpec { Id = "turtle_shell", Group = "defense", Rare = true, NameVi = "Mai rùa", NameEn = "Turtle shell", DescVi = "Chặn trọn 1 đòn, mọc lại sau 12 giây", DescEn = "Stops one whole hit, grows back in 12 s", Effect = "shell", Amount = 12, MaxStacks = 1 },
        new UpgradeSpec { Id = "opening_strike", Group = "power", Rare = true, NameVi = "Đòn mở màn", NameEn = "Opening strike", DescVi = "Đòn đầu trúng mỗi quái ×2 sát thương", DescEn = "First hit on each enemy deals ×2", Effect = "opener", Amount = 1, MaxStacks = 1 },
        new UpgradeSpec { Id = "running_start", Group = "speed", Rare = true, NameVi = "Chạy đà", NameEn = "Running start", DescVi = "Chạy một quãng rồi chém: +80% sát thương", DescEn = "Swing after a run: +80% damage", Effect = "momentum", Amount = 0.8, MaxStacks = 1 },
        new UpgradeSpec { Id = "claws", Group = "power", NameVi = "Móng vuốt", NameEn = "Claws", DescVi = "10% đòn đánh gây ×2 sát thương", DescEn = "10% of hits deal ×2 damage", Effect = "crit", Amount = 0.1, MaxStacks = 2, EndlessMaxStacks = 3 },
        new UpgradeSpec { Id = "finishing_blow", Group = "power", Rare = true, NameVi = "Kết liễu", NameEn = "Finishing blow", DescVi = "+50% sát thương lên quái dưới 30% máu", DescEn = "+50% damage to enemies below 30% HP", Effect = "execute", Amount = 0.5, MaxStacks = 1 },
        new UpgradeSpec { Id = "long_rhythm", Group = "speed", NameVi = "Nhịp dài", NameEn = "Long rhythm", DescVi = "Thêm 0,3 giây để nối combo", DescEn = "0.3 s longer to keep a combo going", Effect = "comboWindow", Amount = 0.3, MaxStacks = 2 },
        new UpgradeSpec { Id = "tailwind", Group = "speed", Rare = true, NameVi = "Lướt gió", NameEn = "Tailwind", DescVi = "Vừa được thả xuống: 2 giây đánh nhanh +30%", DescEn = "Put down: 2 s of +30% attack speed", Effect = "landingRush", Amount = 0.3, MaxStacks = 1 },
        new UpgradeSpec { Id = "regrowth", Group = "defense", NameVi = "Hồi phục", NameEn = "Regrowth", DescVi = "Hồi 1 HP mỗi 2 giây khi đang đánh", DescEn = "1 HP back every 2 s in a fight", Effect = "regen", Amount = 0.5, MaxStacks = 2 },
        new UpgradeSpec { Id = "steady", Group = "defense", NameVi = "Kiên cường", NameEn = "Steady", DescVi = "−25% thời gian bị choáng", DescEn = "−25% time stunned", Effect = "stunCut", Amount = 0.25, MaxStacks = 2 },
        new UpgradeSpec { Id = "quick_hands", NameVi = "Tay nhanh", NameEn = "Quick hands", DescVi = "−10% thời gian hồi kỹ năng", DescEn = "−10% skill cooldowns", Effect = "skillCooldown", Amount = 0.1, MaxStacks = 3 },
        new UpgradeSpec { Id = "power_bank", NameVi = "Sạc pin", NameEn = "Power bank", DescVi = "App trên màn hình sạc nhanh hơn 8%", DescEn = "Desktop apps charge 8% faster", Effect = "appCharge", Amount = 0.08, MaxStacks = 3 },
        new UpgradeSpec { Id = "energy_drain", NameVi = "Hút năng lượng", NameEn = "Energy drain", DescVi = "Mỗi quái bị hạ: app sạc thêm 1%", DescEn = "Each defeated enemy: apps charge 1% more", Effect = "killCharge", Amount = 0.01, MaxStacks = 3 },
        new UpgradeSpec { Id = "big_bomb", Skill = "bomb", NameVi = "Bom to", NameEn = "Big bomb", DescVi = "Bom len to hơn và mạnh hơn 20%", DescEn = "Yarn bomb 20% bigger and stronger", Effect = "bomb", Amount = 0.2, MaxStacks = 2 },
        new UpgradeSpec { Id = "thick_bubble", Skill = "shield", NameVi = "Khiên dày", NameEn = "Thick bubble", DescVi = "Khiên bong bóng kéo dài thêm 1 giây", DescEn = "Bubble shield lasts 1 s longer", Effect = "shieldTime", Amount = 1, MaxStacks = 2 },
    };

    // From battle.json "synergies": what 3 points of a group switch on (null: the built-in three).
    [JsonPropertyName("synergies")] public SynergySpec[]? SynergiesData { get; init; }
    private IReadOnlyList<SynergySpec>? synergyList;
    [JsonIgnore] public IReadOnlyList<SynergySpec> SynergyList => synergyList ??= SynergiesData ?? new[]
    {
        new SynergySpec { Id = "thorns", Group = "defense", At = 3, Effect = "thorns", Amount = 0.35, Armor = 0.1, NameVi = "Giáp gai", NameEn = "Thorns",
            DescVi = "Quái đánh trúng mèo nhận lại 35% sát thương; −10% sát thương nhận vào", DescEn = "Enemies that hit the cat take 35% of it back; −10% damage taken" },
        new SynergySpec { Id = "armor_break", Group = "power", At = 3, Effect = "armorBreak", Amount = 0.3, NameVi = "Phá giáp", NameEn = "Armor break",
            DescVi = "Đòn combo +30% sát thương, xuyên giáp đá", DescEn = "Combo hits deal 30% more and pierce stone" },
        new SynergySpec { Id = "afterimage", Group = "speed", At = 3, Effect = "echo", Amount = 0.5, Every = 4, NameVi = "Dư ảnh", NameEn = "Afterimage",
            DescVi = "Cứ 4 đòn trúng, thêm một đòn 50%", DescEn = "Every 4th hit strikes again for 50%" },
        new SynergySpec { Id = "last_stand", Group = "defense", At = 6, Effect = "lastStand", Amount = 1, EndlessOnly = true, NameVi = "Bất khuất", NameEn = "Last stand",
            DescVi = "Mỗi đợt một lần, đòn chí mạng chỉ để mèo còn 1 HP", DescEn = "Once a wave, a fatal hit leaves the cat at 1 HP" },
        new SynergySpec { Id = "fortress", Group = "defense", At = 9, Effect = "autoShield", Amount = 30, EndlessOnly = true, NameVi = "Pháo đài", NameEn = "Fortress",
            DescVi = "HP dưới 30%: tự bật Khiên, mỗi 30 giây một lần", DescEn = "Below 30% HP the Shield goes up by itself, once per 30 s" },
        new SynergySpec { Id = "shockwave", Group = "power", At = 6, Effect = "comboStun", Amount = 0.6, EndlessOnly = true, NameVi = "Chấn động", NameEn = "Shockwave",
            DescVi = "Đòn combo làm choáng quái 0,6 giây", DescEn = "Combo hits stun for 0.6 s" },
        new SynergySpec { Id = "finisher", Group = "power", At = 9, Effect = "finisher", Amount = 0.1, EndlessOnly = true, NameVi = "Hành quyết", NameEn = "Finisher",
            DescVi = "Đòn trúng hạ ngay quái thường dưới 10% máu", DescEn = "A hit finishes off a non-boss below 10% HP" },
        new SynergySpec { Id = "gale", Group = "speed", At = 6, Effect = "rush", Amount = 0.2, EndlessOnly = true, NameVi = "Gió lốc", NameEn = "Gale",
            DescVi = "Sau đòn combo: 3 giây chạy và đánh nhanh +20%", DescEn = "After a combo: 3 s of +20% speed and attacks" },
        new SynergySpec { Id = "afterimage_plus", Group = "speed", At = 9, Effect = "echo", Amount = 0.75, Every = 3, EndlessOnly = true, NameVi = "Dư ảnh+", NameEn = "Afterimage+",
            DescVi = "Cứ 3 đòn trúng, thêm một đòn 75%", DescEn = "Every 3rd hit strikes again for 75%" },
    };

    // From battle.json "campaign": the Adventure chapters (null: no Adventure in this data).
    [JsonPropertyName("campaign")] public CampaignSpec? Campaign { get; init; }
    // From battle.json "expedition": the Expedition run (null: none in this data). PetExpedition.cs.
    [JsonPropertyName("expedition")] public ExpeditionSpec? Expedition { get; init; }
    // From battle.json "endless": Infinity (null: none in this data). PetEndless.cs.
    [JsonPropertyName("endless")] public EndlessSpec? Endless { get; init; }
    // From battle.json "apps": the desktop apps of Adventure, Expedition and Infinity battles
    // (null: none). PetBattle.Apps.cs.
    [JsonPropertyName("apps")] public AppsSpec? Apps { get; init; }

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
    public string Name(string language) => PetText.Data(language, NameVi, NameEn) is { Length: > 0 } name ? name : Id;
    [JsonIgnore] public string Effect => HitEffect ?? (Style == "hammer" ? "thud" : "spark");
    // Where in the swing the hit lands (0–1): the drawing times its slash to this moment.
    [JsonIgnore] public double HitFraction => SwingDuration > 0 ? Math.Clamp(HitTime / SwingDuration, 0.05, 0.95) : 0.45;
    public string Hint(string language) => PetText.Data(language, HintVi, HintEn);
    public double Damage { get; init; } = 45;
    public double Range { get; init; } = 76;
    public double AttackCooldown { get; init; } = 1.35;
    public double SwingDuration { get; init; } = 0.7;
    public double HitTime { get; init; } = 0.45;
    public double Knock { get; init; } = 170;
    // Now and then (StunChance of the landed hits) a hit dazes the enemy StunSeconds (× its
    // stunFactor; never a boss): the hammer's 15 % and 0.8 s.
    public double StunChance { get; init; } = 0.15;
    public double StunSeconds { get; init; } = 0.8;
    // A status its landed hits may leave (1.5 M04; null: none).
    public StatusHit? OnHit { get; init; }
    // 1.5 M08 (PetBattle.Weapons.cs). Sweep: every landed swing also hits every other enemy in reach in
    // front of the cat (the yarn's lash). Pull: DIPs/s a landed hit draws an enemy that keeps its
    // distance (a support kind) toward the cat, up to just short of it. ComboHits: hits a chain needs
    // (null: the battle's comboHits). Launch: DIPs/s a combo hit tosses its enemy up (never a boss, a
    // rock, a big or a firm one), LaunchStun: its daze on landing.
    public bool Sweep { get; init; }
    public double Pull { get; init; }
    public int? ComboHits { get; init; }
    public double Launch { get; init; }
    public double LaunchStun { get; init; }
}

// A skill's texts. Id: push, heal, cheer, bomb or shield. A description's {key} is that
// battle.json number of the battle (e.g. {pushCooldown}), so the HUD follows the numbers.
public sealed record SkillSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Name(string language) => PetText.Data(language, NameVi, NameEn) is { Length: > 0 } name ? name : Id;
    public string Desc(string language) => PetText.Data(language, DescVi, DescEn);
}

// Multipliers on every enemy kind (1 = the kind's own numbers).
public sealed class DifficultySpec
{
    public double EnemyHp { get; init; } = 1;
    public double EnemyDamage { get; init; } = 1;
    public double EnemySpeed { get; init; } = 1;
}

// Push and Bomb are aimed (choose, then click a slime); the others act at once. The last five
// (1.5) are used with the mouse on the field (SkillAim; PetBattle.Tools.cs, PetBattle.Grabber.cs).
public enum BattleSkill { Push, Heal, Cheer, Bomb, Shield, Laser, Grabber, Scissors, Fan, Spray }

// How a chosen skill is used: at once (None), on a clicked slime (Push, Bomb), on a clicked spot of
// the field (Laser), while the mouse is held on the field (Fan, Spray), a slime pressed and dragged
// (Grabber) or a stroke across the field (Scissors).
public enum SkillAim { None, Slime, Point, Hold, Drag, Swipe }

// Casting: a support enemy winds up a heal (a green "+"), near or far; a push or stun cancels it.
// Dashing: a dash enemy's lunge after its windup (it hits the cat once at most).
public enum SlimeState { Spawning, Moving, Windup, Recover, Stunned, Defeated, Casting, Dashing }

// Where damage on an enemy comes from: the cat's swings (a plain hit or a combo), the yarn bomb,
// or a build bonus (Thorns sends back what the cat took, Afterimage repeats a swing). The bonus
// hits never set off another bonus. App: a desktop app (PetBattle.Apps.cs). Splash: a combo's hit on
// the enemies next to its target.
// Link: the share of a hit a tether's holder takes for its ally.
// Poison: a status's damage over time (PetBattle.Status.cs).
// Tool: the mouse skills' damage (Laser, Fan, Grabber, Scissors, Spray; PetBattle.Tools.cs, PetBattle.Grabber.cs).
public enum DamageSource { Weapon, Combo, Bomb, Reflect, Echo, App, Splash, Link, Throw, Poison, Tool }

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
    // A dash enemy stunned during its windup has lost its sting: its dashes still hurt but never stun the cat.
    public bool Defanged;
    // Guarded by an infected Ctrl+Z's heal until this battle time (no damage meanwhile).
    public double WardUntil = -1;
    // Spat onto the floor by an infected bin (one such alive at a time).
    public bool Spat;
    // An elite whose affix shoots viruses (EliteSpec.VirusEvery): seconds to its next shot.
    public double VirusClock;
    // Links (PetBattle.Links.cs): its link in this battle (null: none, switched off, or a third kind of
    // link); the ally it is tied to or rides (0: none); when it may tie itself again; a rhythm's beats
    // so far and the time into the next one; a merge's time in contact; a rider's height over the floor
    // (drawing); seconds of panic left (an aura holder fell); its speed factor from an ally's aura this frame.
    public LinkSpec? Link;
    public int LinkedTo, BeatCount;
    public double RelinkAt, BeatClock, MergeClock, RideHeight, PanicRemaining;
    public double AuraSpeed = 1;
    public bool Riding => LinkedTo != 0 && Link?.Kind == EnemyLinks.Ride;
    // In the air (its kind's Hover): the bottom of its body this many DIPs over the floor (0: on the floor).
    public double Altitude;
    public bool Airborne => Altitude > 0;
    // Statuses (PetBattle.Status.cs): poison's layers, HP a second, time left, time and HP since its
    // last tick; chill's slow (0–0.9) and time left; stick's time left and the time it cannot stick again.
    public int PoisonLayers;
    public double PoisonDps, PoisonRemaining, PoisonClock, PoisonOwed;
    public double ChillPower, ChillRemaining;
    public double StuckRemaining, StickImmune;
    // Each status's whole time when it last grew (drawing its countdown ring).
    public double PoisonTotal, ChillTotal, StuckTotal;
    public bool Poisoned => PoisonRemaining > 0;
    public bool Chilled => ChillRemaining > 0;
    public bool Stuck => StuckRemaining > 0;
    // Tools (PetBattle.Tools.cs): it is chasing the laser's dot this frame; the wind has it (s left);
    // it may be hurt by a bump again once BumpCooldown is 0.
    public bool Lured;
    public double Blown, BumpCooldown;
    // 1.5 M06: in the Grabber's claw, or flying after it let go (Thrown: fast enough to hurt); how high
    // its bottom is over the floor (DIPs) and its flight's speed (VY up); seconds of the spray's soaking left.
    public bool Grabbed, Tossed, Thrown;
    // Tossed up by a combo of a weapon that launches (PetBattle.Weapons.cs): dazed on landing.
    public bool Launched;
    public double Lift, TossVX, TossVY, WetRemaining;
    public bool Handled => Grabbed || Tossed;
    public bool Wet => WetRemaining > 0;
    // How fast it acts under a chill (1: not chilled).
    public double ChillFactor => ChillRemaining > 0 ? 1 - ChillPower : 1;
    // 1.5 M09 (PetBattle.Spit.cs): the windup is a spit at SpitAt (not a hit); time to its next spit; a
    // dash's factor from a puddle it went through (1: none yet this dash).
    public bool Spitting;
    public double SpitAt, SpitCooldown, DashBoost = 1;
    // 1.5 M09 (PetBattle.Spring.cs): the trap's state, the time left in it and the hits towards flattening it.
    public SpringState Spring;
    public double SpringClock;
    public int SpringHits;
    // 1.5 M11 (PetBattle.Fuse.cs, PetBattle.Split.cs): a lit fuse's seconds left and its whole length
    // (0: not lit); blown up by its own fuse (gone, not drawn as defeated); healed by a healer since it
    // came (a split half's merge waits for it).
    public double FuseLeft, FuseTotal;
    public bool Exploded, Healed;
    public bool Lit => FuseLeft > 0;
    // 1.5 M13 (PetBattle.Tricks.cs). A ghost: faded out (nothing hits it), the time left in its turn seen
    // or hidden, the side of the cat it faded on, and the seconds the cursor or the laser keeps it out.
    public bool Faded;
    public double GhostClock, Revealed;
    public int FadeSide;
    // A bubble: up (it takes every hit), the clicks on it so far, the time until it grows back.
    public bool Bubbled;
    public int Pokes;
    public double BubbleClock;
    // A mirage: the real one this copy imitates (0: not a copy); copies already made; a copy gone.
    public int DecoyOf;
    public bool Cast, Dispelled;
    public bool Decoy => DecoyOf != 0;
    // A sulk: how puffed up it is (1: not; its hits grow with it), how soothed (0–1), the time since the
    // last stroke; soothed and leaving (drawn waving goodbye).
    public double Puff = 1, Calm, CalmIdle;
    public bool Calmed;
    public bool Targetable => State is not (SlimeState.Spawning or SlimeState.Defeated);
    public void Set(SlimeState state)
    {
        if (state == SlimeState.Stunned && State == SlimeState.Windup && Type.Dash) Defanged = true;
        if (state != SlimeState.Windup) Spitting = false;
        State = state; StateTime = 0;
    }
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
    // A click on an ad's × while an infected Tabs keeps the ads open (Value: the ad's id).
    PopupLocked,
    // A boss leaping to its edge (taking off, landing) and the rings where its helpers will appear.
    BossLeaping, BossLanded, BossSummoning,
    // Tactics: a heal that guards its ally, a hit on that guarded ally.
    EnemyWarded, HitWarded,
    // A Push on an elite that ignores it (Value: its id).
    PushResisted,
    // Desktop apps (Value: the icon's id): charged, used, moved to another cell; a slime shot a virus
    // (Value: the slime's id), an icon got infected, was scrubbed once, was cleaned; an infected icon
    // acted against the cat. An ad popup opened, was closed, or let a slime out.
    AppReady, AppUsed, AppMoved, GooShot, AppInfected, AppScrubbed, AppCleaned, AppAttack, PopupOpened, PopupClosed, PopupSpawned,
    // Malware a slime threw onto the field (Value: its id): it opened, the user closed it.
    HazardOpened, HazardClosed,
    // A click on a forced update's Later that did not close it yet; the cat's hit on it (Value: its id).
    HazardTapped, WallHit,
    // virus.exe (Value: the file's id): dropped or copied, deleted by a click; all its copies gone
    // (they infected an icon, or their slime fell).
    VirusFileAdded, VirusFileDeleted, VirusFilesGone,
    // Links (Value: the slime's id): a link made, cut by the user (or its holder defeated), snapped by
    // itself; a rhythm's beat and its strike together; a merge done (Value: the new enemy's id); a
    // rider hopped off or fell; an enemy panicked (an aura holder fell).
    LinkFormed, LinkCut, LinkSnapped, LinkBeat, LinkStrike, Merged, Dismounted, Panicked,
    // The cat in the user's hand hit an enemy in the air; the thrown cat flew into an enemy that
    // reacts to it (Value: the enemy's id).
    HeldHit, ThrowHit,
    // A status took on an enemy (Value: its SlimeStatus).
    StatusApplied,
    // 1.5 M05: the laser's dot put down and gone; the fan started, ran out of air; an enemy blown into
    // another or the edge (Value: its id).
    LaserOn, LaserOff, FanOn, FanSpent, FanBump,
    // 1.5 M06 (Value: the enemy's id): the Grabber lifted one, let it go (Value 1: thrown), a thrown one
    // flew into another or the edge; a Scissors stroke (Value: what it cut and hit); the spray started,
    // ran out of water.
    Grabbed, GrabReleased, GrabHit, Snipped, SprayOn, SpraySpent,
    // 1.5 M07 (PetBattle.Rage.cs): the rage bar filled up, the user set the cat raging, the rage wore off.
    RageFull, Raged, RageEnded,
    // 1.5 M08 (Value: the enemy's id): a combo of a launching weapon tossed it up.
    Launched,
    // 1.5 M09 (Value: the enemy's id): a spitter started its windup (X: where its goo will land); a drop
    // landed (Value 1: on the cat); a puddle made a dash longer; the fan dried a puddle. A spring trap
    // started to squeeze; it sprang (Value 1: on the cat); it was flattened.
    SpitAimed, GooLanded, PuddleBoost, PuddleDried, SpringSqueezed, SpringBounced, SpringFlattened,
    // 1.5 M11 (Value: the enemy's id): a bomb slime lit its fuse; it blew up (Value 1: on the cat); a
    // splitting slime broke apart.
    FuseLit, Blasted, SlimeSplit,
    // 1.5 M12: a formation's first part came (Value: its index in BattleSettings.FormationList).
    FormationArrived,
    // 1.5 M13 (Value: the enemy's id): a ghost faded out, the cursor or the laser brought it out, a hit
    // went through it; a bubble took a hit, was clicked, popped, grew back; a mirage's copies came, a copy
    // vanished; a sulky slime puffed up from a hit, was stroked (its first stroke since it sulked again),
    // was soothed and left.
    GhostFaded, GhostRevealed, GhostMissed, BubbleBlocked, BubblePoked, BubblePopped, BubbleRegrown,
    MirageCast, MirageDispelled, SulkPuffed, SulkPetted, SulkCalmed
}
// A spring trap (PetBattle.Spring.cs): set and waiting, squeezing before it springs, resting after, flattened.
public enum SpringState { Ready, Squeezing, Resting, Flat }
// A drop of goo in flight from a spitter (PetBattle.Spit.cs) and the puddle it leaves.
public sealed class BattleGoo
{
    public int OwnerId;
    public double FromX, X, Flight, Remaining, Damage, Radius, PuddleSeconds, PuddleRadius, DashDistance, DashSpeed;
    public string? Tint;
    // How far along its flight it is (0–1).
    public double Progress => Flight > 0 ? Math.Clamp(1 - Remaining / Flight, 0, 1) : 1;
}
public sealed class BattlePuddle
{
    public double X, Radius, Remaining, Life, DashDistance, DashSpeed;
}
public readonly record struct BattleEvent(BattleEventKind Kind, double X, double Value = 0);
// Number: a floating damage (Value > 0) or heal (Value < 0, shown as +HP above the cat).
// Thud: a heavy weapon's plain hit (dust and a ring on the floor), chosen by the weapon's hitEffect.
// Mend: green pluses rising from an enemy healed by a support enemy.
// Block: grey stone chips and a small stone shield over an enemy that blocked a hit. Immune: a
// gold ring flashing on a shielded boss hit for nothing. Warded: a green ring and "IMMUNE" on an
// enemy guarded by a heal. Apps: Bell (a notification bell dropping on an enemy), EndTask (a
// "not responding" window over the enemy it ends; Value: its width), Rewind (an undo arrow
// circling the cat). Splat (1.5 M09): a spitter's drop of goo landing (Tint: its colour).
// Miss (1.5 M13): a hit that did nothing to a ghost or a bubble (Value 0), or to a sulky slime (Value 1);
// Heart: a sulky slime soothed.
public enum BattleEffectKind { Spark, Poof, Push, Explosion, Slam, Number, Combo, Thud, Mend, Block, Immune, Warded, Bell, EndTask, Rewind, Splat, Miss, Heart }
// How a damage Number reads (display only): a plain hit, a combo hit, a critical (Claws) hit, or poison's tick.
public enum NumberTone { Plain, Combo, Crit, Poison }
public sealed class BattleEffect
{
    public BattleEffectKind Kind;
    public NumberTone Tone;
    // Poof of a defeated slime: its body colour, for the droplets it bursts into (display only).
    public string? Tint;
    // Height is how far above the floor the effect starts.
    public double X, Height, Age, Life, Value;
}
public sealed class BattleBomb
{
    public double X, Remaining;
}

// The desktop apps: made-up shortcuts the game draws itself (no real app's name, logo or colours),
// standing on a grid of desktop cells above the battle strip. The first five help the cat once
// charged; Popup is the ad an infected Tabs opens on the strip.
public enum BattleAppKind { Tabs, Ping, EndTask, Undo, Trash, Popup }

// One icon of the battle's desktop: its grid cell (column from the left, row up from the strip),
// its charge (0–1, ready at 1) and, while a slime's virus holds it, the slime and the clicks left
// to scrub it. The times (battle time, -1: never) let the views animate what just happened.
public sealed class DesktopApp
{
    public int Id;
    public BattleAppKind Kind;
    public int Col, Row, FromCol, FromRow;
    public double Charge;
    public int InfectedBy, ScrubsLeft;
    public double AttackClock, CrawlClock;
    public double BornAt, MovedAt = -1, UsedAt = -1, ReadyAt = -1, InfectedAt = -1, ScrubbedAt = -1, CleanedAt = -1;
    // The last move was a crawl (slow, infected) rather than a hop.
    public bool Crawled;
    public bool Infected => InfectedBy != 0;
    public bool Ready => Charge >= 1 && !Infected;
}

// An ad on the strip (opened by an infected Tabs): it covers the slimes behind it; left Life s it
// lets a slime out.
public sealed class BattlePopup
{
    public int Id;
    public double X, Age, Life;
}

// Malware a slime throws onto the field instead of shooting a virus into an icon (2026-10-24).
// Loading: a spinning ring on the floor; the cat inside it runs and swings slower. It wears off
// after Life s, or the user clicks its spinner. Update: a "forced update" window standing on the
// floor as a wall neither the cat nor the slimes (bosses leap over) can pass; done at Life s (the
// cat's hits bring it closer), or the user clicks its Later button ClicksLeft times.
public enum HazardKind { Loading, Update }
public sealed class BattleHazard
{
    public int Id;
    public HazardKind Kind;
    public double X, Age, Life;
    public int ClicksLeft;
    // Battle time of the last hit or click (the view shakes it), -1: none.
    public double HitAt = -1;
    // A loading ring closes after the user holds its spinner AppsSpec.LoadingHold s: how long so far.
    public double Held;
}

// Malware on the desktop grid (2026-10-24): a "virus.exe" file a slime dropped on a free cell. It
// copies itself to a cell next to it now and then; with enough copies they infect a clean icon and
// go. A click deletes one copy; defeating the slime that dropped them deletes them all.
public sealed class VirusFile
{
    public int Id, SlimeId;
    public int Col, Row;
    public double BornAt;
}

// Something on its way between an icon and the floor: what an app sends (ToIcon false) or what goes
// into an icon (a slime's virus, the slimes the bin takes). It acts when it lands (Age = Duration).
// FloorX/Height: the floor end (DIPs; Height above the floor). Targets: the slimes it goes for.
public enum LaunchKind { Tabs, Bell, EndTask, Undo, Trash, Goo, BadBell, Freeze, Mend, Spit, Popup }
public sealed class AppLaunch
{
    public int Id, AppId, SlimeId;
    public LaunchKind Kind;
    public double FloorX, Height, Age, Duration;
    public bool ToIcon;
    public int[] Targets = Array.Empty<int>();
    // Where each target stood and its colour when it flew (the bin's slimes), #RRGGBB.
    public double[] TargetX = Array.Empty<double>();
    public string[] Colors = Array.Empty<string>();
    public double Progress => Duration > 0 ? Math.Clamp(Age / Duration, 0, 1) : 1;
}

// battle.json "apps": charging, what each app does, how slimes infect icons and what an infected
// icon does. Names and texts are data ("list": one entry per BattleAppKind); the effects are code.
public sealed record AppsSpec
{
    // Charge at the start, and what each defeated enemy adds to every clean icon (shares of a charge).
    public double StartCharge { get; init; } = 0.2;
    public double KillCharge { get; init; } = 0.02;
    // Tab tornado: tabs circle the cat for TabSeconds, hitting every enemy within TabRadius DIPs for
    // TabDamage (× the build's damage) every TabEvery s.
    public double TabSeconds { get; init; } = 6;
    public double TabRadius { get; init; } = 70;
    public double TabDamage { get; init; } = 14;
    public double TabEvery { get; init; } = 0.5;
    // Ping storm: the PingTargets enemies nearest the cat (0: every one) take PingDamage (× the
    // build's damage) and are stunned PingStun s.
    public double PingDamage { get; init; } = 14;
    public double PingStun { get; init; } = 1.5;
    public int PingTargets { get; init; } = 4;
    // End task: the enemy with the most HP is gone; a boss or an elite loses this share of its max HP.
    public double EndTaskShare { get; init; } = 0.25;
    // Undo: the HP the cat lost in the last UndoSeconds comes back.
    public double UndoSeconds { get; init; } = 4;
    // Bin: takes the TrashCount plain enemies (no boss, no elite) nearest the cat.
    public int TrashCount { get; init; } = 2;
    // Viruses: from a threat of InfectMinThreat each wave rolls InfectChance for a slime to shoot one
    // at a clean icon (at most MaxInfected at once); an elite whose affix has VirusEvery shoots one
    // too, at most EliteVirusPerWave a wave. ScrubClicks clicks clean it, or defeating that
    // slime; an infected icon crawls to a cell next to it every CrawlEvery s.
    public double InfectMinThreat { get; init; } = 1.5;
    public double InfectChance { get; init; } = 0.5;
    public int MaxInfected { get; init; } = 1;
    public int ScrubClicks { get; init; } = 3;
    public double CrawlEvery { get; init; } = 9;
    public int EliteVirusPerWave { get; init; } = 2;
    // What an infected icon does, every so often: Tabs opens the ads (AdCount of them; they cannot be
    // closed while it is infected), Ping drops a bell on the cat (BellDamage and a BellStun s stun),
    // End task freezes the cat, Undo heals the most hurt enemy (and guards it MendGuard s), the bin
    // spits out an enemy (an elite of a kind with an affix when SpitElite; one such alive at a time).
    // An ad left PopupSeconds lets a PopupSpawn enemy out (once).
    public double PopupEvery { get; init; } = 8;
    public double PopupSeconds { get; init; } = 8;
    public string PopupSpawn { get; init; } = "slime";
    public int AdCount { get; init; } = 3;
    public double BellEvery { get; init; } = 10;
    public double BellDamage { get; init; } = 6;
    public double BellStun { get; init; } = 1;
    public double FreezeEvery { get; init; } = 12;
    public double FreezeSeconds { get; init; } = 1.2;
    public double MendEvery { get; init; } = 7;
    public double MendShare { get; init; } = 0.15;
    public double MendGuard { get; init; } = 2;
    public double SpitEvery { get; init; } = 15;
    public bool SpitElite { get; init; } = true;
    // Malware (2026-10-24: at random times, no longer instead of a wave's virus): from a threat of
    // MalwareMinThreat, every MalwareEveryMin–MalwareEveryMax s of fighting one kind is drawn by weight
    // among those not out already (a weight of 0 never), at most MaxMalware kinds at once: a loading
    // ring, a forced update, virus.exe or the ads. Loading ring: LoadingRadius DIPs either side of
    // where the cat stood, LoadingSeconds long; inside it the cat runs and swings at LoadingSlow of its
    // speed; holding its spinner LoadingHold s closes it (0: a click does).
    public double MalwareMinThreat { get; init; } = 1.3;
    public double MalwareEveryMin { get; init; } = 15;
    public double MalwareEveryMax { get; init; } = 30;
    public int MaxMalware { get; init; } = 2;
    public double AdWeight { get; init; }
    public double LoadingWeight { get; init; }
    public double LoadingSeconds { get; init; } = 6;
    public double LoadingRadius { get; init; } = 60;
    public double LoadingSlow { get; init; } = 0.6;
    public double LoadingHold { get; init; }
    // Forced update: a wall for UpdateSeconds; each hit of the cat's adds UpdateHitSeconds to its
    // progress; UpdateClicks clicks on Later close it.
    public double UpdateWeight { get; init; }
    public double UpdateSeconds { get; init; } = 8;
    public double UpdateHitSeconds { get; init; } = 1.2;
    public int UpdateClicks { get; init; } = 2;
    // virus.exe: copies itself every VirusExeEvery s; VirusExeCopies of them infect a clean icon.
    public double VirusExeWeight { get; init; }
    public double VirusExeEvery { get; init; } = 6;
    public int VirusExeCopies { get; init; } = 4;
    [JsonPropertyName("list")] public AppSpec[]? List { get; init; }
    [JsonIgnore] public IReadOnlyList<AppSpec> AppList => List ?? BuiltIn;

    public AppSpec App(BattleAppKind kind)
    {
        foreach (var spec in AppList) if (string.Equals(spec.Id, kind.ToString(), StringComparison.OrdinalIgnoreCase)) return spec;
        return Array.Find(BuiltIn, spec => string.Equals(spec.Id, kind.ToString(), StringComparison.OrdinalIgnoreCase))!;
    }

    // How often an infected icon of this kind acts (s).
    public double AttackEvery(BattleAppKind kind) => kind switch
    {
        BattleAppKind.Tabs => PopupEvery, BattleAppKind.Ping => BellEvery, BattleAppKind.EndTask => FreezeEvery,
        BattleAppKind.Undo => MendEvery, _ => SpitEvery
    };

    // The texts when "apps" has no "list" (the shipped file has the descriptions too).
    public static readonly AppSpec[] BuiltIn =
    {
        new() { Id = "tabs", NameVi = "Tab Lốc Xoáy", NameEn = "Tab Tornado", Charge = 40 },
        new() { Id = "ping", NameVi = "Bão Thông Báo", NameEn = "Ping Storm", Charge = 55 },
        new() { Id = "endtask", NameVi = "End Task", NameEn = "End Task", Charge = 80 },
        new() { Id = "undo", NameVi = "Ctrl+Z", NameEn = "Ctrl+Z", Charge = 90 },
        new() { Id = "trash", NameVi = "Thùng Rác", NameEn = "Bin", Charge = 70 },
        new() { Id = "popup", NameVi = "Quảng cáo", NameEn = "Ad popup" }
    };
}

// One app's texts: what it does (Desc) and what it does once infected (Bad); Charge: seconds of
// battle to charge it (0 for the popup, which is never an icon); Start: its charge when the battle
// starts (null: the rules' StartCharge), so the first apps come early even in a short stage.
public sealed record AppSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string BadVi { get; init; } = "";
    public string BadEn { get; init; } = "";
    public double Charge { get; init; }
    public double? Start { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn) is { Length: > 0 } name ? name : Id;
    public string Desc(string language) => PetText.Data(language, DescVi, DescEn);
    public string Bad(string language) => PetText.Data(language, BadVi, BadEn);
}

// What the battle wants the cat to do this frame. Hold: the user has the cat (or it is in
// the air), so the battle leaves it to input and physics.
public enum CatCommand { Hold, Stand, Move }
public readonly record struct CatOrder(CatCommand Command, double X = 0, int Facing = 0);
