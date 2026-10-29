using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DesktopPet;

// One kind of enemy (a definition: every slime of the kind shares it and nothing changes it in
// a battle; each Slime keeps its own HP, place and state). The slime and the Slime King are
// built from BattleSettings' numbers; Assets/battle.json lists every kind with its own numbers,
// so a new variant is data only. Abilities are switches the code supports: Slam (area hit with
// a floor warning), summon helpers below an HP fraction, get angry below another. A new kind
// of ability (flying, shooting) needs code first. Plain data (no WPF) for the simulation and tests.
public sealed class EnemyType
{
    public string Id { get; init; } = "slime";
    // What it is (all current kinds are slimes) and the drawing that shows it ("slime" only).
    public string Family { get; init; } = "slime";
    public string Renderer { get; init; } = "slime";
    public string NameVi { get; init; } = "Slime";
    public string NameEn { get; init; } = "Slime";
    public double MaxHp { get; init; } = 50;
    public double Speed { get; init; } = 34;
    public double Damage { get; init; } = 10;
    // Centre-to-centre reach of its attack.
    public double Range { get; init; } = 64;
    public double Windup { get; init; } = 0.8;
    public double AttackCooldown { get; init; } = 1.8;
    public double Recover { get; init; } = 0.35;
    // Size against the basic slime (1 = SlimeWidth × SlimeHeight).
    public double Scale { get; init; } = 1;
    // Combat size (weapon reach, bomb blast) against the basic slime; unset = Scale, as before.
    public double? HitScale { get; init; }
    [JsonIgnore] public double Hitbox => HitScale ?? Scale;
    public string Color { get; init; } = "#7CD36A";
    public string AngryColor { get; init; } = "#E86A6A";
    // Drawn marks that tell kinds apart at a glance: "none", "speed" (wind streaks behind it
    // while moving), "cracks" (a stony body: wide and low, armour on its front, more cracks when
    // weak), "leaf" (a herbal healer: a leaf cap and a herb pouch) or "cross" (an older "+" mark).
    public string Marks { get; init; } = "none";
    // A boss: crown, its own HP bar on the HUD, a "BOSS!" banner.
    public bool Boss { get; init; }
    // Area attack: a red zone on the floor while winding up, a shock wave when it lands.
    // SlamDamage: what the slam deals (scaled like Damage; 0 = Damage).
    public bool Slam { get; init; }
    public double SlamDamage { get; init; }
    [JsonIgnore] public double SlamShare => Slam && SlamDamage > 0 && Damage > 0 ? SlamDamage / Damage : 1;
    // Pushes move it and stun it this much (1 = like a slime).
    public double PushFactor { get; init; } = 1;
    public double StunFactor { get; init; } = 1;
    // Below SummonAt of its HP (0 = never) it calls SummonCount helpers of SummonType, once.
    public double SummonAt { get; init; }
    public int SummonCount { get; init; }
    public string SummonType { get; init; } = "slime";
    // Below EnrageAt of its HP (0 = never) it moves and winds up faster.
    public double EnrageAt { get; init; }
    public double EnragedSpeed { get; init; }
    public double EnragedWindup { get; init; }
    // Support: every HealCooldown s it winds up HealWindup s (a green "+" warns), then gives the most
    // hurt ally within HealRange up to HealAmount HP (never itself, never past max HP, never a
    // defeated one). HealBudget caps all it can ever heal, so healing can never outlast the user.
    // A push or stun during the windup cancels the heal. HealAmount 0: no healing.
    public double HealAmount { get; init; }
    public double HealRange { get; init; } = 160;
    public double HealCooldown { get; init; } = 4;
    public double HealWindup { get; init; } = 1;
    public double HealBudget { get; init; } = 100;
    // Dash: within DashRange of the cat it winds up (the lane is shown on the floor), then lunges
    // DashDistance DIPs at DashSpeed. Running into the cat hits it once (Damage) and stuns it
    // (BattleSettings.StunDurationSeconds). It replaces the kind's plain attack.
    public bool Dash { get; init; }
    public double DashRange { get; init; } = 170;
    public double DashDistance { get; init; } = 230;
    public double DashSpeed { get; init; } = 420;
    // Block: each hit from the cat's weapon (plain, combo or Afterimage) is blocked with this chance
    // (0 = never): no damage, no knock back. The yarn bomb and Thorns are never blocked, nor a combo
    // with Armor break. After BlockMaxStreak blocks in a row the next hit always lands.
    public double BlockChance { get; init; }
    public int BlockMaxStreak { get; init; } = 2;
    // Support: while any ally that fights (not a support) is on the field it never attacks the cat;
    // it keeps KeepBack DIPs behind the nearest such ally on its side (or that far from the cat),
    // backing off at its own speed. With no fighting ally left it fights like any slime.
    public bool Support { get; init; }
    public double KeepBack { get; init; } = 110;
    // Support far heal: with no hurt ally in HealRange, a fighting ally below FarHealBelow of its HP
    // (0 = never) anywhere on the field gets a heal like any other (same amount, budget, guard; a push
    // or stun cancels it) after a longer windup of FarHealWindup s, once per FarHealCooldown s.
    public double FarHealBelow { get; init; }
    public double FarHealWindup { get; init; } = 1.2;
    public double FarHealCooldown { get; init; } = 8;
    // Blocker: the cat walking on the floor cannot pass it; a target behind it means hitting it first
    // (lifting the cat over it still works). BlockFrontOnly: it blocks only hits from the side it
    // faces. TurnSeconds: how long it takes to face the other way (0: at once); it only winds up
    // facing the cat, so a cat behind it gets a moment of free hits.
    public bool Blocker { get; init; }
    public bool BlockFrontOnly { get; init; }
    public double TurnSeconds { get; init; }
    // Boss rally (during a shield phase, after its helpers came): every RallyEvery s (0 = never) its
    // crown glows RallyCharge s, then one helper of the phase that is not elite (drawn at random) becomes elite
    // for good: its kind's own affix (if any), RallyHp more max HP (and that much HP) and its hits ×
    // RallyDamage. Pushing the boss while its crown glows cancels the call.
    public double RallyEvery { get; init; }
    public double RallyCharge { get; init; } = 0.8;
    public double RallyHp { get; init; } = 0.5;
    public double RallyDamage { get; init; } = 1.25;
    // Boss: dazed this long when its shield breaks (0: not), taking damage as usual.
    public double ShieldBreakStun { get; init; }
    // Boss phases: each fires once when its HP fraction is reached (summon helpers and/or get
    // angry). At most SummonCap of its helpers are alive at once (more calls are skipped).
    public BossPhase[]? Phases { get; init; }
    public int SummonCap { get; init; } = 4;
    // A shield phase's run to the far edge: its speed, and the longest it may take before calling.
    public double RetreatSpeed { get; init; } = 240;
    public double RetreatSeconds { get; init; } = 3;
    // Instead of the run: a leap to that edge (a crouch of LeapCrouch s, then LeapSeconds in the air;
    // the landing hurts no one). The far edge: the one opposite the boss's half of the floor, unless
    // the cat is in that half too (then the other one), so it never lands next to the cat.
    public bool RetreatLeap { get; init; }
    public double LeapCrouch { get; init; } = 0.35;
    public double LeapSeconds { get; init; } = 0.9;
    // A shield phase's helpers are announced this long by rings on the floor where they will appear (0: at once).
    public double SummonWarning { get; init; }
    // Support tactics. HealGuard: the ally being healed takes no damage while the heal winds up (a push
    // or a stun on the healer ends it at once); never a boss, and the same ally only again
    // HealGuardRepeat s after. FleeSpeed: backing off from a cat nearer than 200 DIPs it walks up to
    // this × faster (at 60 DIPs). HideBehindGuard: it keeps behind its guard rather than the nearest ally.
    public bool HealGuard { get; init; }
    public double HealGuardRepeat { get; init; } = 2;
    public double FleeSpeed { get; init; } = 1;
    public bool HideBehindGuard { get; init; }
    // HitAndRun (dash enemies): after a dash it runs this many DIPs away from the cat before coming back.
    public double HitAndRun { get; init; }
    // What one of this kind weighs against a wave's or a stream's budget (WaveSpec.Budget).
    public double Cost { get; init; } = 1;
    // The one elite affix this kind may roll when it spawns (null: never elite).
    public EliteSpec? Elite { get; init; }
    // Its one link with its allies (1.5, PetBattle.Links.cs; null: none).
    public LinkSpec? Link { get; init; }
    [JsonIgnore] public LinkSpec? ActiveLink => Link is { Enabled: true } link ? link : null;
    // An enemy in the air (1.5, PetBattle.Held.cs): it floats Hover DIPs over the floor (0: on the floor).
    // The cat on the floor never goes for it and it never attacks up close; only the cat in the
    // user's hand reaches it (and hits it by itself).
    public double Hover { get; init; }
    // What the thrown cat flying into it does (1.5, PetBattle.Held.cs; null: nothing, like every other enemy).
    public ThrowHitSpec? ThrowHit { get; init; }
    // 1.5 M09 (PetBattle.Spit.cs, PetBattle.Spring.cs): it spits goo from afar (Q1), or lies as a spring
    // trap (Q2). Null: neither.
    public SpitSpec? Spit { get; init; }
    public SpringSpec? Spring { get; init; }
    // 1.5 M11 (PetBattle.Fuse.cs, PetBattle.Split.cs): it lights a fuse next to the cat and blows up (Q6),
    // or breaks into smaller ones when defeated (Q7). Null: neither.
    public FuseSpec? Fuse { get; init; }
    public SplitSpec? Split { get; init; }
    // 1.5 M13 (PetBattle.Tricks.cs), the slimes the cursor beats: it fades in and out (S1), hides in a
    // bubble (S2), comes with copies of itself (S3), or sulks and swells when hit (S4). Null: none.
    public GhostSpec? Ghost { get; init; }
    public BubbleSpec? Bubble { get; init; }
    public MirageSpec? Mirage { get; init; }
    public SulkSpec? Sulk { get; init; }
    [JsonIgnore] public bool UsesMouse => Ghost != null || Bubble != null || Mirage != null || Sulk != null;
    // 1.5 M14 (PetBattle.Fly.cs, PetBattle.Burrow.cs): it flies anywhere over the battle's screen and
    // shoots sticky goo (S5), or digs under the floor and comes up under the cat (S6). Null: neither.
    public FlySpec? Fly { get; init; }
    public BurrowSpec? Burrow { get; init; }
    // 1.5 M15 (PetBattle.Wall.cs): an ice shell the cat's weapon only scratches, broken by the thrown cat
    // (S7), or it climbs the field's left/right edge and drops spikes on the cat (S8). Null: neither. Both
    // need a throwHit (the thrown cat is their answer).
    public IceSpec? Ice { get; init; }
    public ClingSpec? Cling { get; init; }
    // 1.5 M16 (PetBattle.Quills.cs): it puffs up its quills now and then and pricks the cat hitting it (S9),
    // calls more enemies when its clock runs out (S10), or steals an app's charge or a ready skill and runs
    // off with it (S11). Null: none.
    public QuillsSpec? Quills { get; init; }
    public ClockSpec? Clock { get; init; }
    public StealSpec? Steal { get; init; }
    // 1.5 M17 (PetBattle.Zip.cs), the chapter 2 boss's own moves. Roll (a boss only): from afar it winds up
    // and rolls across the floor over the cat. Still: it never moves or attacks (a boss's file shards).
    // RopeCutSpeed/RopeCutStun: a roped shield phase's ropes (BossPhase.Roped) are cut by the cat going
    // through one at RopeCutSpeed DIPs/s or more (held or thrown) or by the Scissors; the helper is dazed
    // RopeCutStun s.
    public RollSpec? Roll { get; init; }
    public bool Still { get; init; }
    public double RopeCutSpeed { get; init; } = 300;
    public double RopeCutStun { get; init; } = 1;
    // 1.5 M18 (PetBattle.Brood.cs, PetBattle.Mutant.cs), bosses only. Brood (B2, the Slime Mother): it lays
    // a clutch of merging slimes now and then; the big one they merge into goes back to it and feeds it HP.
    // CrownLinks (B3, the Mutant King): its rally call also ties a link between its helpers, these kinds in
    // turn (tether or rhythm). Rain: goo drops fall from the sky around the cat now and then. Null: none.
    public BroodSpec? Brood { get; init; }
    public LinkSpec[]? CrownLinks { get; init; }
    public RainSpec? Rain { get; init; }
    // An infected bin may spit one of this kind (with an elite affix). False keeps a kind out of battles
    // that never list it (the 1.5 enemies until their chapter brings them in).
    public bool BinSpit { get; init; } = true;
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
}

// Q1 (1.5 M09): with the cat on the floor between MinRange and Range DIPs away it winds up Windup s
// (an orange ring marks the cat's spot then), then a drop of goo flies Flight s and lands there: a
// cat on the floor within Radius takes the enemy's damage (no stun). Where it lands a puddle stays
// PuddleSeconds (0: none) with PuddleRadius: a dashing enemy going through it lunges DashDistance ×
// further and DashSpeed × faster. Once per Cooldown s. An elite spits EliteDrops drops (the others
// EliteSpread DIPs further on) and its puddles last ElitePuddle × longer.
public sealed class SpitSpec
{
    public double Range { get; init; } = 380;
    public double MinRange { get; init; } = 90;
    public double Windup { get; init; } = 1;
    public double Flight { get; init; } = 0.8;
    public double Radius { get; init; } = 42;
    public double Cooldown { get; init; } = 4;
    public double PuddleSeconds { get; init; } = 6;
    public double PuddleRadius { get; init; } = 48;
    public double DashDistance { get; init; } = 1.5;
    public double DashSpeed { get; init; } = 1.4;
    public int EliteDrops { get; init; } = 2;
    public double EliteSpread { get; init; } = 70;
    public double ElitePuddle { get; init; } = 1.5;
}

// Q2 (1.5 M09): a trap lying on the floor with a ring of Radius. The cat on the floor stepping in sets
// it off: it squeezes Compress s, then springs; the cat still in the ring (and on the floor) takes the
// enemy's damage and is stunned Stun s. Then it rests Rest s. FlattenHits hits (weapon or bomb) flatten
// it for FlatSeconds (no trap meanwhile). With the cat further than CreepRange (0: never) it slides
// towards it. An elite: EliteRadius × the ring, EliteRest s of rest, EliteHits hits to flatten.
public sealed class SpringSpec
{
    public double Radius { get; init; } = 60;
    public double Compress { get; init; } = 0.8;
    public double Stun { get; init; } = 0.8;
    public double Rest { get; init; } = 4;
    public int FlattenHits { get; init; } = 2;
    public double FlatSeconds { get; init; } = 3;
    public double CreepRange { get; init; } = 120;
    public double EliteRadius { get; init; } = 1.35;
    public double EliteRest { get; init; } = 2.5;
    public int EliteHits { get; init; } = 3;
}

// Q6 (1.5 M11): with the cat on the floor within Range DIPs (centre to centre) it stops and lights its fuse:
// Seconds later it blows up (a red ring shows the blast's Radius all along). The cat on the floor in the
// ring takes the enemy's damage and a stun of Stun s; every other enemy in it (not one in the air) loses
// EnemyDamage. It is gone then, not counted as the cat's. Defeated before it is lit it fizzles; defeated
// while lit it blows up on the spot. Knocked off its mount (the mount defeated, the rope cut) it lights a
// DropFuse s fuse where it falls; thrown by the Grabber it blows up where it hits or lands. An elite
// burns EliteSeconds and blasts EliteRadius × as wide.
public sealed class FuseSpec
{
    public double Range { get; init; } = 70;
    public double Seconds { get; init; } = 1.5;
    public double Radius { get; init; } = 90;
    public double Stun { get; init; } = 0.6;
    public double EnemyDamage { get; init; } = 20;
    public double DropFuse { get; init; } = 0.8;
    public double EliteSeconds { get; init; } = 1.1;
    public double EliteRadius { get; init; } = 1.3;
}

// Q7 (1.5 M11): defeated, it breaks into Count enemies of kind Into (an elite into EliteCount), each with
// Health of its own max HP. They do not split again (Into may not split).
public sealed class SplitSpec
{
    public string Into { get; init; } = "";
    public int Count { get; init; } = 2;
    public int EliteCount { get; init; } = 3;
    public double Health { get; init; } = 0.6;
}

// S1 (1.5 M13): Visible s seen, then Hidden s faded out: nothing hits it then (the cat does not go for
// it, blows and bombs pass through), it attacks nothing and drifts through the cat to its other side.
// The cursor over it (or the laser's dot within LaserRange) brings it out and keeps it out Reveal s
// longer. An elite shows itself only EliteVisible s at a time.
public sealed class GhostSpec
{
    public double Visible { get; init; } = 2.5;
    public double Hidden { get; init; } = 2.5;
    public double Reveal { get; init; } = 2;
    public double LaserRange { get; init; } = 160;
    public double EliteVisible { get; init; } = 1.5;
}

// S2 (1.5 M13): it floats in a bubble Lift DIPs up that takes every hit for it (it still attacks). Pops
// clicks on it (an elite's ElitePops), or a Scissors stroke through it, pop the bubble: it drops, stunned
// Stun s, and the bubble grows back Regrow s later.
public sealed class BubbleSpec
{
    public int Pops { get; init; } = 3;
    public int ElitePops { get; init; } = 5;
    public double Regrow { get; init; } = 6;
    public double Stun { get; init; } = 1.5;
    public double Lift { get; init; } = 12;
}

// S3 (1.5 M13): it comes with Copies look-alikes (an elite with EliteCopies). A copy's hits hurt nothing
// and any hit on it makes it vanish (never the cat's kill); the real one defeated, its copies vanish too.
// The cursor over one (or the laser's dot within LaserRange) shows it for what it is Reveal s: a copy
// blurs, the real one gets its shadow.
public sealed class MirageSpec
{
    public int Copies { get; init; } = 2;
    public int EliteCopies { get; init; } = 3;
    public double Reveal { get; init; } = 2;
    public double LaserRange { get; init; } = 160;
}

// S4 (1.5 M13): hits take no HP; each puffs it up by Grow (up to MaxGrow ×; its own hits grow with it).
// Stroking it with the cursor (moving back and forth over it) soothes it: Calm DIPs of strokes (an
// elite's EliteCalm) and it waves goodbye and leaves, counted as the cat's. Left alone Idle s, it sulks
// again by Fade of the way a second.
public sealed class SulkSpec
{
    public double Calm { get; init; } = 1800;
    public double EliteCalm { get; init; } = 2800;
    public double Idle { get; init; } = 1;
    public double Fade { get; init; } = 0.15;
    public double Grow { get; init; } = 0.1;
    public double MaxGrow { get; init; } = 1.6;
}

// S5 (1.5 M14): it flies from spot to spot over the battle's screen (FlySpeed DIPs/s, a new spot every
// Move s, its bottom MinHeight DIPs over the floor or higher), out of reach from the floor. Every
// Cooldown s it aims Aim s at the cat, then shoots a drop of sticky goo (ShotSpeed DIPs/s, Radius DIPs
// round the cat's centre) that hurts the cat even in the user's hand or under landing protection (the
// Shield takes it; never a stun, never out of the hand); on the floor the cat is slowed to Slow × its
// speed for SlowSeconds. An elite shoots EliteShots drops. Stunned (Push, a Grabber yank, HeldHits held
// hits within HeldWindow s, any daze) it falls, lies Downed s on the floor taking DownedDamage × hits,
// crouches Rise s and flies up again.
public sealed class FlySpec
{
    public double FlySpeed { get; init; } = 150;
    public double Move { get; init; } = 5;
    public double MinHeight { get; init; } = 220;
    public double Cooldown { get; init; } = 3.5;
    public double Aim { get; init; } = 0.8;
    public double ShotSpeed { get; init; } = 520;
    public double Radius { get; init; } = 30;
    public double Slow { get; init; } = 0.6;
    public double SlowSeconds { get; init; } = 2;
    public int EliteShots { get; init; } = 2;
    public int HeldHits { get; init; } = 3;
    public int EliteHeldHits { get; init; } = 4;
    public double HeldWindow { get; init; } = 4;
    public double Downed { get; init; } = 3;
    public double DownedDamage { get; init; } = 1.5;
    public double Rise { get; init; } = 1;
}

// S6 (1.5 M14): under the floor (only its mound shows: nothing hits it, it attacks nothing) it goes to
// the cat at Speed × its speed, then a ring of cracks (Radius DIPs) shows Crack s (an elite's EliteCrack)
// where the cat was, and it comes up there: the cat on the floor in the ring is hurt and stunned Stun s.
// Out, it fights Above s (an elite's EliteAbove), digs Dig s and stays under at least Rest s. The cat
// let go onto the ring (cracks showing, or Emerge s into coming up) pins it: dazed Pinned s. The
// Grabber pulls it up from under the floor; it then stays out Pinned s.
public sealed class BurrowSpec
{
    public double Speed { get; init; } = 1.4;
    public double Crack { get; init; } = 1;
    public double EliteCrack { get; init; } = 0.7;
    public double Radius { get; init; } = 55;
    public double Emerge { get; init; } = 0.5;
    public double Stun { get; init; } = 0.8;
    public double Above { get; init; } = 3;
    public double EliteAbove { get; init; } = 2;
    public double Dig { get; init; } = 0.6;
    public double Rest { get; init; } = 4;
    public double Pinned { get; init; } = 3;
}

// S7 (1.5 M15): it comes in with an ice shell. While the shell is on, a hit of the cat's weapon (its swing,
// combo, splash, echo; held or not) takes only Armor × its damage. The thrown cat flying into it at its
// throwHit.minSpeed or more (WetSpeed while the spray has it wet) breaks the shell (and does its throwHit);
// the shell grows back Regrow s later (an elite's EliteRegrow; 0: never).
public sealed class IceSpec
{
    public double Armor { get; init; } = 0.15;
    public double WetSpeed { get; init; } = 400;
    public double Regrow { get; init; } = 10;
    public double EliteRegrow { get; init; } = 6;
}

// S8 (1.5 M15): it goes to the nearest edge of the field and climbs it (Climb DIPs/s) until its bottom is
// Height DIPs over the floor (a second one on the same edge Spacing higher, never over 120). On the edge,
// every Cooldown s it aims Aim s at the cat's spot on the floor (a ring there), then drops a spike that
// lands Flight s later: the cat on the floor within Radius takes its damage (no stun). An elite drops
// EliteSpikes spikes, EliteSpread DIPs apart. The thrown cat flying into it (its throwHit), Push, the
// Grabber or any daze knock it off: it falls, lies dazed Downed s, fights on the floor Ground s and
// goes back up.
public sealed class ClingSpec
{
    public double Height { get; init; } = 60;
    public double Spacing { get; init; } = 46;
    public double Climb { get; init; } = 90;
    public double Cooldown { get; init; } = 3;
    public double Aim { get; init; } = 0.9;
    public double Flight { get; init; } = 0.5;
    public double Radius { get; init; } = 36;
    public int EliteSpikes { get; init; } = 2;
    public double EliteSpread { get; init; } = 60;
    public double Downed { get; init; } = 2.5;
    public double Ground { get; init; } = 4;
}

// S9 (1.5 M16): its quills lie flat Flat s, rise Warn s (seen coming), then stand Puffed s (an elite's
// ElitePuffed). Puffed, a hit of the cat's weapon (swing, combo, splash; not its echo) takes only Armor × its
// damage and pricks the cat for Prick (no stun; the Shield and the Turtle shell stop it). Bombs, skills,
// apps and poison go through. Wet from the spray, its quills lie flat until it is dry.
public sealed class QuillsSpec
{
    public double Flat { get; init; } = 3;
    public double Warn { get; init; } = 0.5;
    public double Puffed { get; init; } = 2.5;
    public double ElitePuffed { get; init; } = 4;
    public double Armor { get; init; } = 0.5;
    public double Prick { get; init; } = 5;
}

// S10 (1.5 M16): its clock runs Seconds (an elite's EliteSeconds) from when it comes in, Stunned × as fast
// while it is dazed. Run out, it calls the enemies of Call (announced at the edge behind the cat, coming
// once the floor has room; the wave waits for them) and starts again, Calls times at most. Defeated, it
// calls no more.
public sealed class ClockSpec
{
    public double Seconds { get; init; } = 20;
    public double EliteSeconds { get; init; } = 15;
    public double Stunned { get; init; } = 0.3;
    public int Calls { get; init; } = 2;
    public string[] Call { get; init; } = { "slime", "slime", "runner" };
}

// S11 (1.5 M16): it never hurts the cat. Its windup next to the cat steals instead: the clean app icon with
// the most charge (at least MinCharge; battles with apps), otherwise the ready skill with the longest
// cooldown. Then it runs Flee × its speed (an elite's EliteFlee) to the field's edge away from the cat and,
// after Linger s there (dazed or lured, the time waits), gets away with it: gone, not a defeat of the cat's.
// Defeated first, it gives back what it took (the charge to its icon, the skill ready again).
public sealed class StealSpec
{
    public double MinCharge { get; init; } = 0.3;
    public double Flee { get; init; } = 1.5;
    public double EliteFlee { get; init; } = 1.9;
    public double Linger { get; init; } = 6;
}

// B1 (1.5 M17): with the cat on the floor between MinRange and Range DIPs away (centre to centre) and
// Every s since its last roll (EnragedEvery once it is angry), the boss winds up Windup s (a lane on the
// floor shows the way), then rolls Distance DIPs at Speed over the floor, whatever the cat does. The cat on
// the floor in its way takes Damage (scaled like its other hits) and a stun of Stun s, once per roll;
// lifting the cat out of the way dodges it. Never while its shield is up.
public sealed class RollSpec
{
    public double Every { get; init; } = 7;
    public double EnragedEvery { get; init; } = 4.5;
    public double MinRange { get; init; } = 140;
    public double Range { get; init; } = 520;
    public double Windup { get; init; } = 1.1;
    public double Speed { get; init; } = 360;
    public double Distance { get; init; } = 460;
    public double Damage { get; init; } = 24;
    public double Stun { get; init; } = 0.9;
}

// B2 (1.5 M18): every Every s (EnragedEvery once it is angry), while its shield is down and it is not
// stunned, the boss lays Count of Enemy (a kind with a merge link) next to it, announced Warning s before by
// rings on the floor; at most its SummonCap of its helpers alive. What its clutch merges into walks back to
// it instead of fighting and, next to it, feeds it FeedRate HP/s out of its own HP until it has none left
// (gone then, not the cat's kill). Defeating it on the way, or while it feeds, keeps the rest from the boss.
public sealed class BroodSpec
{
    public string Enemy { get; init; } = "fuser";
    public int Count { get; init; } = 3;
    public double Every { get; init; } = 10;
    public double EnragedEvery { get; init; } = 6.5;
    public double Warning { get; init; } = 0.6;
    public double FeedRate { get; init; } = 20;
}

// B3 (1.5 M18): every Every s while its shield is down, the boss calls Drops goo drops from the sky: a ring
// on the floor marks each Warning s before it lands, the first on the cat's spot and the others Spacing
// apart on both sides in turn. A cat on the floor within Radius takes Damage (scaled like its other hits,
// no stun); lifting it or the Bubble shield dodges them. PuddleSeconds: a puddle stays that long where each
// lands (a spitter's, with SpitSpec's default boost; 0: none).
public sealed class RainSpec
{
    public double Every { get; init; } = 12;
    public int Drops { get; init; } = 4;
    public double Spacing { get; init; } = 90;
    public double Warning { get; init; } = 1;
    public double Radius { get; init; } = 32;
    public double Damage { get; init; } = 14;
    public double Height { get; init; } = 150;
    public double PuddleSeconds { get; init; }
}

// The thrown cat flying through an enemy at MinSpeed DIPs/s or more: Damage (as is, never blocked)
// and a daze of Stun s; once per enemy per throw. The cat itself is not hurt.
public sealed class ThrowHitSpec
{
    public double MinSpeed { get; init; } = 600;
    public double Damage { get; init; }
    public double Stun { get; init; }
}

// A link between an enemy and its allies (1.5 "Slime phản công", PetBattle.Links.cs): always
// drawn, always breakable, breaking it is rewarded. Enabled false switches it off (a user file's
// "link": { "enabled": false }). At most two kinds of link in one battle. Kinds:
// tether: a rope to the nearest ally within Range (never a boss, never another holder; one without a
//   rope first, else one with room for a second, 1.5 M12: a second holder takes its share of what is left); the holder
//   takes Share of every hit on that ally and keeps behind it. Cut (CutLink) or the holder defeated:
//   the ally is dazed BreakStun s; a rope stretched past Range × 1.5 snaps with no reward. Relink s
//   later it may tie itself to an ally again. The cat held (past the drag threshold) or thrown through
//   the rope at CutSpeed DIPs/s or more cuts it too (1.5 M10). An elite takes EliteShare (0: Share).
// rhythm: a beat every Beat s (a ring over its head); on every Beats-th beat each ally within Range
//   that could attack the cat winds up at once, attack turns or not. A push or a stun on it starts
//   the count again. An elite strikes on every EliteBeats-th beat (0: Beats).
// ride: it rides an ally of kind Mount found within Range (not one already ridden), drawn on top and
//   left alone by the cat; DropRange from the cat it hops off. Its mount defeated (or CutLink): it falls
//   where it is, dazed BreakStun s.
// merge: Count of its kind within Range of each other for MergeSeconds (a bar counts down) become one
//   Into enemy with their HP added up. A push or a stun on one starts its clock again. AfterHeal (1.5
//   M11, Q7): only ones a healer has healed since they came count (split halves made whole again).
// aura: allies within Range move Speed × faster (an elite: EliteSpeed, 0: Speed); defeated, every other
//   enemy (never a boss) panics BreakStun s: runs away from the cat and does not attack.
public sealed class LinkSpec
{
    public string Kind { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public double Range { get; init; } = 200;
    public double BreakStun { get; init; } = 1;
    public double Relink { get; init; } = 6;
    public double Share { get; init; } = 0.5;
    public double EliteShare { get; init; }
    public double CutSpeed { get; init; } = 300;
    public double Beat { get; init; } = 0.8;
    public int Beats { get; init; } = 4;
    public int EliteBeats { get; init; }
    public string? Mount { get; init; }
    public double DropRange { get; init; } = 90;
    public int Count { get; init; } = 3;
    public double MergeSeconds { get; init; } = 2;
    public string? Into { get; init; }
    public bool AfterHeal { get; init; }
    public double Speed { get; init; } = 1.3;
    public double EliteSpeed { get; init; }
}

// A formation (1.5 M12, Enemy Director): a named team the Director draws for a wave's "formation"
// group by weight, among those whose MinThreat the battle's threat reaches and whose cost fits what is
// left of the wave's budget (none: the cheapest). Parts are squads (or single enemies) that come one
// after the other, each from the group's edge, or the other edge after a warning arrow (Side "b").
// The first time one comes in a battle a banner names it with its tip.
public sealed class FormationSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string? TipVi { get; init; }
    public string? TipEn { get; init; }
    public double MinThreat { get; init; }
    public WaveGroup[] Parts { get; init; } = Array.Empty<WaveGroup>();
    // Every enemy of it, in spawn order.
    [JsonIgnore] public IEnumerable<string> Members => Parts.SelectMany(p =>
        p.Squad == null ? Enumerable.Repeat(p.Enemy, Math.Max(0, p.Count)) : p.Squad.SelectMany(m => Enumerable.Repeat(m.Enemy, Math.Max(0, m.Count))));
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Tip(string language) => PetText.Data(language, TipVi ?? "", TipEn ?? "");
}

public static class EnemyLinks
{
    public const string Tether = "tether", Rhythm = "rhythm", Ride = "ride", Merge = "merge", Aura = "aura";
    public static readonly string[] All = { Tether, Rhythm, Ride, Merge, Aura };
    public const int MaxKinds = 2;
    // Ropes one ally may take (1.5 M12: the "Assembly line" formation's two chains on one rock).
    public const int MaxTethers = 2;
}

// An elite affix (Enemy Director): rolled once when a wave or stream spawns the enemy, never for a
// boss's helpers (a boss's rally call makes them elite instead), never on Easy, at most
// BattleSettings.MaxElites alive at once. A gold rim and a
// star tell it apart. A trait per kind (a longer windup with a faster dash, sure blocks of its first
// front hits, a stronger first heal, no Push) and stat multipliers on top. Cost: what being elite
// adds to the budget.
public sealed class EliteSpec
{
    public double WindupAdd { get; init; }
    public double DashSpeed { get; init; } = 1;
    public int SureBlocks { get; init; }
    public double FirstHeal { get; init; } = 1;
    // Stats, applied once when it turns elite (multiplied, rounded like spawn scaling): max HP and
    // damage, the wait between its attacks (windup and recovery stay), its heal cooldown and heal
    // budget (not the amount per heal). PushImmune: the Push skill neither moves, stuns nor
    // interrupts it (it still costs the cooldown).
    public double Hp { get; init; } = 1;
    public double Damage { get; init; } = 1;
    public double AttackCooldown { get; init; } = 1;
    public double HealCooldown { get; init; } = 1;
    public double HealBudget { get; init; } = 1;
    public bool PushImmune { get; init; }
    // Shoots a virus at a clean desktop icon every VirusEvery s (0: never; the first a few seconds after
    // it turns elite), at most AppsSpec.EliteVirusPerWave a wave, in battles with the desktop apps.
    public double VirusEvery { get; init; }
    // The trait its label over the head names (null: none worth a label), the most telling first.
    public string? Tag(string language) =>
        PushImmune ? PetText.Pick(language, "MIỄN ĐẨY", "NO PUSH")
        : AttackCooldown < 1 ? PetText.Pick(language, "ĐÁNH NHANH", "QUICK HITS")
        : Damage > 1 ? PetText.Pick(language, "ĐÒN MẠNH", "HARD HITS")
        : HealCooldown < 1 || HealBudget > 1 || FirstHeal > 1 ? PetText.Pick(language, "HỒI MẠNH", "BIG HEALS")
        : null;
    public double Cost { get; init; } = 1;
    // A short line for the banner the first time an elite of this kind appears in a battle.
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
}

public sealed class BossPhase
{
    public double At { get; init; }
    public string? Summon { get; init; }
    public int Count { get; init; }
    // More helpers of other kinds in the same phase (after Summon × Count).
    public WaveGroup[]? Summons { get; init; }
    public bool Enrage { get; init; }
    // Shield phase: a hit never takes the boss below At (the rest is lost); it puts up a shield
    // (takes no damage), runs to the edge far from the cat, calls its helpers there and keeps the
    // shield until every helper of this phase is defeated. Later phases wait for the shield to drop.
    public bool Shield { get; init; }
    // 1.5 M17 (PetBattle.Zip.cs). Formation: its helpers are also this formation's members (after the
    // others). Roped (a shield phase): every helper comes tied to the boss by a rope and the shield holds
    // while any rope is left (cut it, or defeat its helper). Pack (a shield phase): the boss stays where it
    // is, squeezed small, its helpers (file shards) around it; the shield holds until they are gone.
    public string? Formation { get; init; }
    public bool Roped { get; init; }
    public bool Pack { get; init; }
    // Every helper this phase calls, in order (a formation's members: BattleSettings.PhaseCalls).
    public IEnumerable<string> Calls()
    {
        if (Summon != null) for (int i = 0; i < Count; i++) yield return Summon;
        foreach (var g in Summons ?? Array.Empty<WaveGroup>()) for (int i = 0; i < g.Count; i++) yield return g.Enemy;
    }
}

// One wave: groups spawn in order (at most MaxAlive at once). A plain group is Count of Enemy.
// Pick: each of the Count spawns is a kind drawn from these weights when the wave starts (Enemy is
// then unused); AtLeast: that many of a kind among them whatever the draw. Squad: Count times these
// members together, side by side (fighters toward the cat, supports behind them), only once there is
// room on the floor for the whole squad (nothing after it spawns before it). Formation (1.5 M12): Count
// times one of these formations (BattleSettings.Formations, by weight), drawn when the wave starts.
public sealed class WaveGroup
{
    public string Enemy { get; init; } = "slime";
    public int Count { get; init; } = 1;
    public Dictionary<string, double>? Pick { get; init; }
    public Dictionary<string, int>? AtLeast { get; init; }
    public WaveGroup[]? Squad { get; init; }
    // Two-sided waves: "a" spawns at the edge far from the cat (as the wave starts), "b" at the
    // other edge after a warning arrow there (BattleSettings.IncomingWarning); unset: either edge.
    public string? Side { get; init; }
    // Every enemy of this group (or squad member) comes elite, whatever the stage's chance or the
    // difficulty (the Adventure Guide shows each kind's elite this way); its kind needs an affix.
    public bool Elite { get; init; }
    public Dictionary<string, double>? Formation { get; init; }
    [JsonIgnore] public bool Fixed => Pick == null && Squad == null && Formation == null;
    // Its kinds are only known once the wave starts (a pick or a formation).
    [JsonIgnore] public bool Drawn => Pick != null || Formation != null;
    [JsonIgnore] public int SquadSize => Squad?.Sum(m => Math.Max(0, m.Count)) ?? 1;
}

// Stream: a survive stage's last wave that never ends: one kind drawn from Weights whenever there is
// room, every Interval seconds at the start of the stage down to IntervalEnd at its end.
public sealed class StreamSpec
{
    public Dictionary<string, double> Weights { get; init; } = new();
    public double Interval { get; init; } = 2;
    public double IntervalEnd { get; init; } = 2;
    // From this many seconds of the clock on (0 = never), spawns come from alternate edges, each
    // after a warning arrow.
    public double PincerAfter { get; init; }
    // The most the enemies on the floor may cost at once (EnemyType.Cost, elites included; 0 = no
    // limit): a drawn kind that does not fit gives way to the cheapest one that does, or waits.
    public double Budget { get; init; }
}

public sealed class WaveSpec
{
    public WaveGroup[] Groups { get; init; } = Array.Empty<WaveGroup>();
    // Enemies of this wave are this much stronger (× the stage's threat; see BattleSettings.ThreatDamageShare).
    public double Threat { get; init; } = 1;
    // The most this wave's enemies may cost in all (EnemyType.Cost; 0 = no limit): a drawn kind that
    // does not fit gives way to the cheapest kind of its pick; what is left pays for elites.
    public double Budget { get; init; }
    public StreamSpec? Stream { get; init; }
    // The fixed kinds in spawn order (drawn kinds are not known before the wave starts).
    public IEnumerable<string> Spawns() => Groups.Where(g => !g.Drawn).SelectMany(g =>
        g.Squad == null ? Enumerable.Repeat(g.Enemy, Math.Max(0, g.Count))
            : Enumerable.Range(0, Math.Max(0, g.Count)).SelectMany(_ => g.Squad.SelectMany(m => Enumerable.Repeat(m.Enemy, Math.Max(0, m.Count)))));
}

// Adventure (battle.json "campaign"): chapters of stages played in order. A stage is a battle
// with its own waves (enemy kinds come from "enemies") and goal; winning it opens the next stage
// of its chapter. Ids are stable: progress (PetAdventure.cs) is saved by them. RulesVersion goes up
// when a stage changes enough that old records should not be compared with new ones.
public sealed class CampaignSpec
{
    public ChapterSpec[] Chapters { get; init; } = Array.Empty<ChapterSpec>();
}

public sealed class ChapterSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public StageSpec[] Stages { get; init; } = Array.Empty<StageSpec>();
    // The setup panel lets the user choose the five skills for this chapter's stages (1.5). Unset
    // (chapter 1): its stages always have the five first skills (BattleSkills.Default).
    public bool SkillChoice { get; init; }
    // 1.5 M21: a stage id (of another chapter); this chapter's first stage opens once it is won
    // (unset: open from the start, as chapter 1).
    public string? UnlockAfter { get; init; }
    // 1.5 M21: slimes drop fish and potions in this chapter's stages (no coins; battle.json "drops").
    public bool Drops { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
}

public sealed class StageSpec
{
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    // One line about what the stage teaches or asks, shown on the map and the setup panel.
    public string GoalVi { get; init; } = "";
    public string GoalEn { get; init; } = "";
    // "clear": defeat every wave. "survive": stay alive SurviveSeconds (the last wave comes again
    // while time is left; the clock stops while the cat is in the user's hands).
    public string Objective { get; init; } = "clear";
    public double? SurviveSeconds { get; init; }
    [JsonIgnore] public bool Survival => Objective == "survive";
    // Infinity's stage (EndlessSpec.Stage, never from a campaign): never won, waves go on.
    public const string EndlessObjective = "endless";
    [JsonIgnore] public bool Endless => Objective == EndlessObjective;
    // Between waves the user picks one of up to three upgrades (battle.json "upgrades") for this run.
    public bool Upgrades { get; init; }
    // At most this many picks in one run of the stage (unset: one after every cleared wave that has
    // another after it; a survive stage would otherwise offer one per repeated wave).
    public int? MaxUpgrades { get; init; }
    // Cards only after every n-th cleared wave (unset: after each; Infinity sets it).
    [JsonIgnore] public int? UpgradeEvery { get; init; }
    // Infinity (EndlessSpec sets them): the elite chance grows by EliteChancePerWave a wave up to
    // EliteChanceMax; one more elite may be alive at once every MoreElitesEvery waves (at most
    // MoreElitesMax more); from wave AllSynergiesAt every synergy is on; a cleared boss wave gives a
    // second pick (BossBonusPick).
    [JsonIgnore] public double EliteChancePerWave { get; init; }
    [JsonIgnore] public double EliteChanceMax { get; init; }
    [JsonIgnore] public int MoreElitesEvery { get; init; }
    [JsonIgnore] public int MoreElitesMax { get; init; }
    [JsonIgnore] public int? AllSynergiesAt { get; init; }
    [JsonIgnore] public bool BossBonusPick { get; init; }
    // Survive stages: a card offer at each of these points of the clock (seconds held out) instead of
    // after cleared waves.
    public double[]? UpgradeAt { get; init; }
    // How strong this stage's enemies are (1: their own numbers; × difficulty and the wave's threat).
    public double Threat { get; init; } = 1;
    // Survive stages: the threat grows by this much per minute held out.
    public double ThreatGrowth { get; init; }
    public int RulesVersion { get; init; } = 1;
    // Enemies on the floor at once (unset: the battle's maxAlive).
    public int? MaxAlive { get; init; }
    // The chance (0–1) that an enemy spawned by a wave or stream with an elite affix is elite
    // (never on Easy; BattleSettings.MaxElites caps how many are alive at once).
    public double EliteChance { get; init; }
    // false: no malware and no infected icons in this stage (the icons still help the cat).
    public bool Malware { get; init; } = true;
    // Malware sent at set times (s of the fight; a survive stage: of its clock), each once: "ads",
    // "loading", "update", "virus" (virus.exe) or "infect" (a slime's virus into an icon). With a plan,
    // random malware and infections wait until MalwareFrom s.
    public MalwareCue[]? MalwarePlan { get; init; }
    public double MalwareFrom { get; init; }
    // 1.5 M21: at most this many malware kinds out at once in this stage (unset: apps.maxMalware).
    public int? MaxMalware { get; init; }
    // 1.5 M21: skill ids the map and the setup panel suggest carrying ("Suggested: …").
    public string[]? SuggestSkills { get; init; }
    // An accessory id (Assets/accessories.json) given once, the first time the stage is won.
    public string? Reward { get; init; }
    // A skill id: winning this stage opens that skill for the setup panel's choice (Adventure.SkillOpen).
    public string? UnlocksSkill { get; init; }
    // 1.5 M20: a fish basket to keep (PetBattle.Basket.cs); every fish carried off loses the stage.
    public BasketSpec? Basket { get; init; }
    // A short tip shown once when the stage first starts (and again from its setup panel).
    public string? TipVi { get; init; }
    public string? TipEn { get; init; }
    public string? Tip(string language) => language == PetLanguage.Vietnamese || TipEn == null ? (PetLanguage.UsesEnglish(language) ? TipEn : TipVi) : PetText.Data(language, TipVi ?? "", TipEn);
    public WaveSpec[] Waves { get; init; } = Array.Empty<WaveSpec>();
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Goal(string language) => PetText.Data(language, GoalVi, GoalEn);
}

// One cue of a stage's malware plan (StageSpec.MalwarePlan).
public sealed class MalwareCue
{
    public double At { get; init; }
    public string Kind { get; init; } = "";
    public static readonly string[] Kinds = { "ads", "loading", "update", "virus", "infect" };
}

// An upgrade offered between the waves of an Adventure stage. A run keeps what it took; in the
// Adventure a won stage passes its build on to the next stage (AdventureProgress.Builds), a free
// battle starts without any. Effect is one the code knows (UpgradeLimits); Amount is a fraction for
// damage/moveSpeed/attackSpeed/armor (0.2 = 20 %), DIPs/s for knock, HP for heal and maxHp. The rare
// effects: shell (the next hit on the cat is stopped whole, then it grows back in Amount s), opener
// (the first landed hit on each enemy deals Amount more), momentum (a swing after a run of
// BattleSim.MomentumRun DIPs deals Amount more).
// MaxStacks: how often one build may take it (0 = no limit). Group (defense, power, speed or none)
// counts toward that group's synergy. Definitions are shared and never changed. 2026-10-24: crit
// (Amount: the chance of a ×2 hit), execute (Amount more on an enemy below 30 % HP), comboWindow
// (seconds more to keep a combo going), landingRush (Amount more attack speed for 2 s after the cat
// is put down), regen (HP a second while fighting), stunCut (shorter stuns on the cat), and the
// utility cards without a group: skillCooldown, appCharge (apps charge faster), killCharge (more
// charge per defeated enemy), bomb (bigger, stronger yarn bomb), shieldTime (seconds more Shield).
public sealed class UpgradeSpec
{
    public string? Group { get; init; }
    public string Id { get; init; } = "";
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Effect { get; init; } = "damage";
    public double Amount { get; init; }
    public int MaxStacks { get; init; }
    // Infinity's own limit (unset: MaxStacks): a run there takes many more cards.
    public int? EndlessMaxStacks { get; init; }
    // Only offered in battles with the desktop apps (its effect is about them).
    [JsonIgnore] public bool NeedsApps => Effect is "appCharge" or "killCharge";
    // A skill id ("bomb", "shield"…): only offered in battles that carry that skill (its effect is about it).
    public string? Skill { get; init; }
    // A rare card (up to two per group, at most once a build): it takes the place of its group's card in an
    // offer with BattleSettings.RareChance. It plays differently, not better in every way.
    public bool Rare { get; init; }
    // Effect "onHit" (1.5 M04): the status the cat's landed weapon hits may leave; Amount is its chance.
    public StatusHit? OnHit { get; init; }
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
}

// A build bonus: once a build holds At points of a group (one per card taken), the effect is on
// for the rest of that build. thorns: Amount of the damage the cat really takes from an enemy's
// hit goes back to that enemy. armorBreak: combo hits deal Amount more (and pierce a rock's block).
// echo: every Every-th landed swing adds a second hit of Amount × its damage (a later echo replaces
// an earlier one). 2026-10-24: lastStand (once a wave a fatal hit leaves 1 HP), autoShield (below
// 30 % HP the Shield goes up by itself, at most once per Amount s), comboStun (combo hits stun
// Amount s), finisher (a landed hit ends a non-boss below Amount of its HP), rush (after a combo,
// 3 s of Amount more speed and attack speed). The bonus hits (reflected, echoed) never set off
// another bonus.
public sealed class SynergySpec
{
    public string Id { get; init; } = "";
    public string Group { get; init; } = "defense";
    public int At { get; init; } = 3;
    public string Effect { get; init; } = "thorns";
    public double Amount { get; init; }
    public int Every { get; init; } = 4;
    // Less damage taken while it is on, on top of the cards' armor (a fraction).
    public double Armor { get; init; }
    // Only in Infinity (its 6- and 9-point tiers, 2026-10-24).
    public bool EndlessOnly { get; init; }
    public string NameVi { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string DescVi { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string Name(string language) => PetText.Data(language, NameVi, NameEn);
    public string Describe(string language) => PetText.Data(language, DescVi, DescEn);
}

// The upgrade groups, in the order cards, the HUD and the setup panel show them.
public static class BuildGroups
{
    public static readonly string[] All = { "defense", "power", "speed" };
    public static string Name(string group, string language) => group switch
    {
        "defense" => PetText.Pick(language, "Phòng thủ", "Defense"),
        "power" => PetText.Pick(language, "Sức mạnh", "Power"),
        _ => PetText.Pick(language, "Tốc độ", "Speed")
    };
}

// Assets/battle.json (embedded) is the built-in battle: its numbers, weapons, enemy kinds and
// waves. The user may add battle.json to the app's data folder (%LOCALAPPDATA%\DesktopPet,
// docs/BATTLE-DATA.md) with only what it changes: it is merged over the built-in file
// ("enemies"/"weapons"/"upgrades"/"synergies"/"skills"/"formations" entries by id, other objects key by key, anything else replaced) and the
// result is validated as a whole. A broken file never stops the pet and is never used in part:
// it is logged, the built-in battle is used and the battle setup panel says why.
public sealed record BattleDataLoad(BattleSettings Settings, bool Custom, string? Error);

public static class BattleData
{
    // The skill ids a "skill" field may name, for error messages.
    public static string KnownSkills => string.Join(", ", BattleSkills.All.Select(info => BattleSkills.Id(info.Skill)));
    public const int SchemaVersion = 1;
    public static string UserFilePath => Path.Combine(PetPreferences.DataDirectory, "battle.json");
    private static readonly Lazy<BattleSettings> Loaded = new(LoadBuiltIn);
    // The built-in battle (the code's defaults if even the embedded file is broken).
    public static BattleSettings Settings => Loaded.Value;

    private static string EmbeddedText()
    {
        using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")
            ?? throw new InvalidDataException("Missing embedded battle.json");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool IsDataError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException;

    private static BattleSettings LoadBuiltIn()
    {
        try { return Parse(EmbeddedText()); }
        catch (Exception ex) when (IsDataError(ex))
        {
            Trace.WriteLine($"Battle data could not be loaded, using the built-in battle: {ex.Message}");
            return new BattleSettings();
        }
    }

    // The battle for the next fight: the built-in one with the user's file over it, if there is
    // one. Read again for each battle setup, so an edit applies to the next battle and never to a
    // running one (a battle keeps the settings it started with).
    public static BattleDataLoad Load(string? userFile)
    {
        if (string.IsNullOrEmpty(userFile) || !File.Exists(userFile)) return new(Settings, false, null);
        try
        {
            return new(ParseWithOverride(EmbeddedText(), File.ReadAllText(userFile)), true, null);
        }
        catch (Exception ex) when (IsDataError(ex))
        {
            Trace.WriteLine($"{userFile} was not used, the built-in battle is: {ex.Message}");
            return new(Settings, false, ex.Message);
        }
    }

    private static readonly JsonDocumentOptions NodeOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // Throws on any mistake in either file or in the merged result.
    public static BattleSettings ParseWithOverride(string builtInJson, string userJson)
    {
        var root = JsonNode.Parse(builtInJson, null, NodeOptions) as JsonObject ?? throw new InvalidDataException("The built-in battle.json is not a JSON object");
        var user = JsonNode.Parse(userJson, null, NodeOptions) as JsonObject ?? throw new InvalidDataException("The file must be a JSON object: { ... }");
        var version = Key(user, "schemaVersion") is string key ? user[key] : null;
        if (version == null) throw new InvalidDataException($"schemaVersion is missing (write \"schemaVersion\": {SchemaVersion})");
        if (version is not JsonValue value || !value.TryGetValue<int>(out int number) || number != SchemaVersion)
            throw new InvalidDataException($"schemaVersion {version.ToJsonString()} is not supported (this version reads {SchemaVersion})");
        DropRetired(user);
        Merge(root, user, "");
        return Parse(root.ToJsonString());
    }

    // Settings of tactics taken out (2026-10-21): a user file that still sets them keeps working;
    // they are dropped with a log line instead of failing as misspelt keys.
    private static readonly string[] RetiredKeys = { "maxFlankers" };
    private static readonly string[] RetiredEnemyKeys = { "fallBackBelow", "flank", "flankSpeed", "teleportBelow", "teleportWindup", "teleportCooldown", "rallySeconds", "rallySpeed" };
    // 2026-10-24: malware comes at random times, no longer instead of a wave's virus.
    private static readonly string[] RetiredAppKeys = { "infectWeight" };
    private static void DropRetired(JsonObject user)
    {
        var dropped = new List<string>();
        void Drop(JsonObject node, string[] keys, string at)
        {
            foreach (var key in keys) if (Key(node, key) is string found) { node.Remove(found); dropped.Add(at + key); }
        }
        Drop(user, RetiredKeys, "");
        if (Key(user, "apps") is string appsKey && user[appsKey] is JsonObject appsNode) Drop(appsNode, RetiredAppKeys, "apps.");
        if (Key(user, "enemies") is string list && user[list] is JsonArray enemies)
            foreach (var entry in enemies.OfType<JsonObject>()) Drop(entry, RetiredEnemyKeys, $"enemies[{IdOf(entry)}].");
        if (dropped.Count > 0) Trace.WriteLine($"battle.json: no longer used, ignored: {string.Join(", ", dropped)}");
    }

    // Keys match without case, as when the file is read.
    private static string? Key(JsonObject node, string key)
    {
        foreach (var pair in node) if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Key;
        return null;
    }

    private static void Merge(JsonObject target, JsonObject source, string path)
    {
        // The Expedition's map is either fixed "steps" or the "kindGacha": a user file giving one
        // replaces the other.
        if (path.Equals("expedition.", StringComparison.OrdinalIgnoreCase))
            foreach (var (given, other) in new[] { ("steps", "kindGacha"), ("kindGacha", "steps") })
                if (Key(source, given) != null && Key(target, other) is string drop) target.Remove(drop);
        foreach (var (key, value) in source.ToList())
        {
            string? existing = Key(target, key);
            var current = existing != null ? target[existing] : null;
            if (value is JsonObject part && current is JsonObject whole) { Merge(whole, part, path + key + "."); continue; }
            bool byId = key.Equals("enemies", StringComparison.OrdinalIgnoreCase) || key.Equals("weapons", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("upgrades", StringComparison.OrdinalIgnoreCase) || key.Equals("synergies", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("skills", StringComparison.OrdinalIgnoreCase) || key.Equals("formations", StringComparison.OrdinalIgnoreCase);
            if (byId && value is JsonArray entries && current is JsonArray list) { MergeById(list, entries, path + key); continue; }
            if (existing != null) target.Remove(existing);
            target[key] = value?.DeepClone();
        }
    }

    // An entry with a known id changes that entry (only the keys it gives); a new id is added.
    private static void MergeById(JsonArray target, JsonArray source, string path)
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] is not JsonObject entry || IdOf(entry) is not string id) throw new InvalidDataException($"{path}[{i}] needs an \"id\"");
            if (!seen.Add(id)) throw new InvalidDataException($"{path}: id \"{id}\" is used twice");
            var match = target.OfType<JsonObject>().FirstOrDefault(e => IdOf(e) == id);
            if (match != null) Merge(match, entry, $"{path}[{id}].");
            else target.Add(entry.DeepClone());
        }
    }

    private static string? IdOf(JsonObject node) =>
        Key(node, "id") is string key && node[key] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        // A misspelt key is an error, not a silently ignored setting.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$");
    // Ids stay stable across versions: lower-case letters, digits, '_' and '-', at most 32.
    private static readonly Regex IdPattern = new("^[a-z0-9_-]{1,32}$");
    // What the code can draw and do; data can only choose among these.
    public static readonly string[] WeaponStyles = { "sword", "hammer", "yarn", "glove" };
    public static readonly string[] WeaponHitEffects = { "spark", "thud" };
    public static readonly string[] EnemyRenderers = { "slime" };
    public static readonly string[] EnemyMarks = { "none", "speed", "cracks", "leaf", "cross", "spout", "spring", "drum", "chain", "swarm", "giant", "bomb", "split", "half", "flag", "ghost", "bubble", "mirage", "sulk", "wings", "mole", "ice", "spikes", "quills", "clock", "thief", "zip", "shard", "mother", "mutant" };
    public static readonly string[] GroupSides = { "a", "b" };
    // Kinds the cursor beats (ghost, bubble, mirage, sulk) that one battle may bring (docs/UPDATE-1.5.md).
    public const int MaxMouseKinds = 2;

    // Throws on any mistake, so a half-valid file is never used.
    public static BattleSettings Parse(Stream stream)
    {
        var settings = JsonSerializer.Deserialize<BattleSettings>(stream, Options) ?? throw new InvalidDataException("battle.json is empty");
        Validate(settings);
        return settings;
    }

    public static BattleSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<BattleSettings>(json, Options) ?? throw new InvalidDataException("battle.json is empty");
        Validate(settings);
        return settings;
    }

    private static string N(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public static void Validate(BattleSettings s)
    {
        void Positive(double value, string name) { if (!double.IsFinite(value) || value <= 0) throw new InvalidDataException($"{name} must be a positive number"); }
        void NotNegative(double value, string name) { if (!double.IsFinite(value) || value < 0) throw new InvalidDataException($"{name} must not be negative"); }
        void Within(double value, double min, double max, string name) { if (!double.IsFinite(value) || value < min || value > max) throw new InvalidDataException($"{name} must be {N(min)}–{N(max)}"); }
        void Id(string? id, string name) { if (id == null || !IdPattern.IsMatch(id)) throw new InvalidDataException($"{name} must be 1–32 lower-case letters, digits, _ or - (\"{id}\")"); }
        void Text(string? text, string name) { if (text == null || text.Length > 32) throw new InvalidDataException($"{name} must be at most 32 characters"); }
        if (s.SchemaVersion != SchemaVersion) throw new InvalidDataException($"schemaVersion {s.SchemaVersion} is not supported (this version reads {SchemaVersion})");
        Positive(s.CatMaxHp, "catMaxHp"); NotNegative(s.CatDamage, "catDamage"); Positive(s.CatAttackRange, "catAttackRange");
        Positive(s.CatAttackCooldown, "catAttackCooldown"); Positive(s.CatRunScale, "catRunScale");
        if (s.CatRunScale > 3) throw new InvalidDataException("catRunScale must be at most 3"); Positive(s.CatSwingDuration, "catSwingDuration");
        if (s.ComboHits is < 2 or > 6) throw new InvalidDataException("comboHits must be 2–6");
        Positive(s.ComboWindow, "comboWindow"); Positive(s.ComboDamage, "comboDamage"); NotNegative(s.ComboKnock, "comboKnock");
        Within(s.ComboSplash, 0, 2, "comboSplash"); Within(s.ComboSplashRadius, 0, 300, "comboSplashRadius");
        if (!(s.CatHitTime > 0 && s.CatHitTime < s.CatSwingDuration)) throw new InvalidDataException("catHitTime must fall inside the swing");
        if (s.Hammer == null) throw new InvalidDataException("hammer is missing");
        if (s.Weapons is { Length: 0 }) throw new InvalidDataException("weapons must list at least one weapon");
        var weaponIds = new HashSet<string>();
        for (int i = 0; i < s.WeaponList.Count; i++)
        {
            var w = s.WeaponList[i] ?? throw new InvalidDataException($"weapons[{i}] is empty");
            string at = $"weapons[{(string.IsNullOrEmpty(w.Id) ? i.ToString() : w.Id)}]";
            Id(w.Id, at + ".id");
            if (!weaponIds.Add(w.Id)) throw new InvalidDataException($"weapon id \"{w.Id}\" is used twice");
            if (Array.IndexOf(WeaponStyles, w.Style) < 0) throw new InvalidDataException($"{at}.style must be one of: {string.Join(", ", WeaponStyles)}");
            if (w.HitEffect != null && Array.IndexOf(WeaponHitEffects, w.HitEffect) < 0) throw new InvalidDataException($"{at}.hitEffect must be one of: {string.Join(", ", WeaponHitEffects)}");
            Text(w.NameVi, at + ".nameVi"); Text(w.NameEn, at + ".nameEn");
            if (w.HintVi == null || w.HintEn == null || w.HintVi.Length > 80 || w.HintEn.Length > 80) throw new InvalidDataException($"{at} hints must be at most 80 characters");
            Within(w.Damage, 0, 1000, at + ".damage"); Within(w.Range, 10, 300, at + ".range");
            Within(w.AttackCooldown, 0.1, 10, at + ".attackCooldown"); Within(w.SwingDuration, 0.05, 5, at + ".swingDuration");
            if (!(w.HitTime > 0 && w.HitTime < w.SwingDuration)) throw new InvalidDataException(at + ".hitTime must fall inside the swing");
            Within(w.Knock, 0, 1000, at + ".knock");
            Within(w.StunChance, 0, 1, at + ".stunChance"); Within(w.StunSeconds, 0, 3, at + ".stunSeconds");
            if (w.OnHit != null) StatusHit.Validate(w.OnHit, at + ".onHit");
            Within(w.Pull, 0, 1000, at + ".pull"); Within(w.Launch, 0, 1500, at + ".launch"); Within(w.LaunchStun, 0, 3, at + ".launchStun");
            if (w.ComboHits is < 2 or > 6) throw new InvalidDataException(at + ".comboHits must be 2–6");
        }
        NotNegative(s.LandingGraceSeconds, "landingGraceSeconds");
        if (s.LandingGraceSeconds > 5) throw new InvalidDataException("landingGraceSeconds must be at most 5");
        NotNegative(s.RescueGraceCooldownSeconds, "rescueGraceCooldownSeconds");
        Within(s.StunDurationSeconds, 0, 5, "stunDurationSeconds"); Within(s.StunImmunitySeconds, 0, 30, "stunImmunitySeconds");
        if (s.RescueGraceCooldownSeconds > 60) throw new InvalidDataException("rescueGraceCooldownSeconds must be at most 60");
        Within(s.RescueGraceMinLiftSeconds, 0, 2, "rescueGraceMinLiftSeconds");
        Positive(s.SlimeWidth, "slimeWidth"); Positive(s.SlimeHeight, "slimeHeight"); Positive(s.MaxAlive, "maxAlive");
        foreach (var skill in BattleSkills.All) Positive(skill.Cooldown(s), skill.Skill + " cooldown");
        if (s.Skills != null)
        {
            string known = KnownSkills;
            var skillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < s.Skills.Length; i++)
            {
                var k = s.Skills[i] ?? throw new InvalidDataException($"skills[{i}] is empty");
                string at = $"skills[{(string.IsNullOrEmpty(k.Id) ? i.ToString() : k.Id)}]";
                if (!BattleSkills.All.Any(info => string.Equals(info.Skill.ToString(), k.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"{at}.id must be one of: {known}");
                if (!skillIds.Add(k.Id)) throw new InvalidDataException($"skill id \"{k.Id}\" is used twice");
                Text(k.NameVi, at + ".nameVi"); Text(k.NameEn, at + ".nameEn");
                if (k.NameVi.Length == 0 || k.NameEn.Length == 0) throw new InvalidDataException($"{at} needs nameVi and nameEn");
                foreach (var (desc, key) in new[] { (k.DescVi, "descVi"), (k.DescEn, "descEn") })
                {
                    if (desc == null || desc.Length == 0 || desc.Length > 220) throw new InvalidDataException($"{at}.{key} must be 1–220 characters");
                    if (BattleSkills.UnknownKey(desc) is string unknown) throw new InvalidDataException($"{at}.{key}: {{{unknown}}} is not a battle.json number");
                }
            }
            if (skillIds.Count != BattleSkills.All.Length) throw new InvalidDataException($"skills must list every skill: {known}");
        }
        if (s.Apps is AppsSpec apps)
        {
            Within(apps.StartCharge, 0, 1, "apps.startCharge"); Within(apps.KillCharge, 0, 1, "apps.killCharge");
            Within(apps.TabSeconds, 0.5, 30, "apps.tabSeconds"); Within(apps.TabRadius, 10, 300, "apps.tabRadius");
            Within(apps.TabDamage, 0, 1000, "apps.tabDamage"); Within(apps.TabEvery, 0.1, 5, "apps.tabEvery");
            Within(apps.PingDamage, 0, 1000, "apps.pingDamage"); Within(apps.PingStun, 0, 5, "apps.pingStun");
            if (apps.PingTargets is < 0 or > 20) throw new InvalidDataException("apps.pingTargets must be 0–20 (0: every enemy)");
            Within(apps.EndTaskShare, 0, 1, "apps.endTaskShare"); Within(apps.UndoSeconds, 0.5, 30, "apps.undoSeconds");
            if (apps.TrashCount is < 1 or > 5) throw new InvalidDataException("apps.trashCount must be 1–5");
            Within(apps.InfectMinThreat, 0, 100, "apps.infectMinThreat"); Within(apps.InfectChance, 0, 1, "apps.infectChance");
            if (apps.MaxInfected is < 0 or > 5) throw new InvalidDataException("apps.maxInfected must be 0–5");
            if (apps.ScrubClicks is < 1 or > 10) throw new InvalidDataException("apps.scrubClicks must be 1–10");
            if (apps.EliteVirusPerWave is < 0 or > 10) throw new InvalidDataException("apps.eliteVirusPerWave must be 0–10");
            if (apps.AdCount is < 1 or > 5) throw new InvalidDataException("apps.adCount must be 1–5");
            Within(apps.BellStun, 0, 5, "apps.bellStun"); Within(apps.MendGuard, 0, 10, "apps.mendGuard");
            Within(apps.MalwareMinThreat, 0, 100, "apps.malwareMinThreat"); Within(apps.MalwareEveryMin, 3, 300, "apps.malwareEveryMin");
            Within(apps.MalwareEveryMax, apps.MalwareEveryMin, 300, "apps.malwareEveryMax");
            if (apps.MaxMalware is < 1 or > 4) throw new InvalidDataException("apps.maxMalware must be 1–4");
            Within(apps.AdWeight, 0, 100, "apps.adWeight"); Within(apps.LoadingHold, 0, 10, "apps.loadingHold");
            Within(apps.CrawlEvery, 1, 120, "apps.crawlEvery");
            foreach (var (value, key) in new[] { (apps.PopupEvery, "popupEvery"), (apps.BellEvery, "bellEvery"), (apps.FreezeEvery, "freezeEvery"), (apps.MendEvery, "mendEvery"), (apps.SpitEvery, "spitEvery") })
                Within(value, 1, 120, "apps." + key);
            Within(apps.PopupSeconds, 2, 60, "apps.popupSeconds"); Within(apps.BellDamage, 0, 1000, "apps.bellDamage");
            Within(apps.FreezeSeconds, 0, 5, "apps.freezeSeconds"); Within(apps.MendShare, 0, 1, "apps.mendShare");
            Within(apps.LoadingWeight, 0, 100, "apps.loadingWeight");
            Within(apps.LoadingSeconds, 1, 30, "apps.loadingSeconds"); Within(apps.LoadingRadius, 20, 200, "apps.loadingRadius");
            Within(apps.LoadingSlow, 0.2, 1, "apps.loadingSlow");
            Within(apps.UpdateWeight, 0, 100, "apps.updateWeight"); Within(apps.UpdateSeconds, 2, 30, "apps.updateSeconds");
            Within(apps.UpdateHitSeconds, 0, 10, "apps.updateHitSeconds");
            if (apps.UpdateClicks is < 1 or > 10) throw new InvalidDataException("apps.updateClicks must be 1–10");
            Within(apps.VirusExeWeight, 0, 100, "apps.virusExeWeight"); Within(apps.VirusExeEvery, 1, 60, "apps.virusExeEvery");
            if (apps.VirusExeCopies is < 2 or > 8) throw new InvalidDataException("apps.virusExeCopies must be 2–8");
            if (!s.EnemyTypes.Any(e => e?.Id == apps.PopupSpawn)) throw new InvalidDataException($"apps.popupSpawn \"{apps.PopupSpawn}\" is not an enemy id");
            if (apps.List != null)
            {
                var kinds = Enum.GetNames<BattleAppKind>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < apps.List.Length; i++)
                {
                    var a = apps.List[i] ?? throw new InvalidDataException($"apps.list[{i}] is empty");
                    string at = $"apps.list[{(string.IsNullOrEmpty(a.Id) ? i.ToString() : a.Id)}]";
                    if (!kinds.Any(k => string.Equals(k, a.Id, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException($"{at}.id must be one of: {string.Join(", ", kinds.Select(k => k.ToLowerInvariant()))}");
                    if (!seen.Add(a.Id)) throw new InvalidDataException($"app id \"{a.Id}\" is used twice");
                    Text(a.NameVi, at + ".nameVi"); Text(a.NameEn, at + ".nameEn");
                    foreach (var text in new[] { a.DescVi, a.DescEn, a.BadVi, a.BadEn })
                        if (text == null || text.Length > 120) throw new InvalidDataException($"{at} descriptions (desc, bad) must be at most 120 characters");
                    bool icon = !string.Equals(a.Id, nameof(BattleAppKind.Popup), StringComparison.OrdinalIgnoreCase);
                    if (icon) Within(a.Charge, 1, 600, at + ".charge");
                    else if (a.Charge != 0) throw new InvalidDataException($"{at}.charge must be 0 (the popup is never an icon)");
                    if (a.Start is double start) Within(start, 0, 1, at + ".start");
                }
                if (seen.Count != kinds.Length) throw new InvalidDataException($"apps.list must list every app: {string.Join(", ", kinds.Select(k => k.ToLowerInvariant()))}");
            }
        }
        Positive(s.HealAmount, "healAmount"); Positive(s.CheerSeconds, "cheerSeconds"); Positive(s.CheerSpeed, "cheerSpeed");
        Positive(s.BombRadius, "bombRadius"); NotNegative(s.BombDamage, "bombDamage"); Positive(s.ShieldSeconds, "shieldSeconds");
        Within(s.LaserCooldown, 1, 120, "laserCooldown"); Within(s.LaserSeconds, 0.5, 15, "laserSeconds");
        Within(s.LaserLureRange, 20, 2000, "laserLureRange"); Within(s.LaserDps, 0, 100, "laserDps");
        Within(s.FanSteamSeconds, 0.5, 30, "fanSteamSeconds"); Within(s.FanRefillSeconds, 1, 120, "fanRefillSeconds");
        Within(s.FanRange, 20, 1000, "fanRange"); Within(s.FanSpeed, 0, 1500, "fanSpeed"); Within(s.FanBumpDamage, 0, 100, "fanBumpDamage");
        Within(s.GrabberCooldown, 1, 120, "grabberCooldown"); Within(s.GrabberHoldSeconds, 0.5, 15, "grabberHoldSeconds");
        Within(s.GrabberMaxScale, 0.5, 2.3, "grabberMaxScale"); Within(s.GrabberHitDamage, 0, 100, "grabberHitDamage");
        Within(s.GrabberStun, 0, 5, "grabberStun"); Within(s.GrabberThrowSpeed, 50, 1500, "grabberThrowSpeed");
        Within(s.ScissorsCooldown, 1, 120, "scissorsCooldown"); Within(s.ScissorsDamage, 0, 100, "scissorsDamage"); Within(s.ScissorsMinLength, 10, 400, "scissorsMinLength");
        Within(s.SprayTankSeconds, 0.5, 30, "sprayTankSeconds"); Within(s.SprayRefillSeconds, 1, 120, "sprayRefillSeconds");
        Within(s.SprayRange, 10, 500, "sprayRange"); Within(s.SprayDps, 0, 100, "sprayDps");
        Within(s.SprayChill, 0.05, 0.9, "sprayChill"); Within(s.SpraySoakSeconds, 0.5, 10, "spraySoakSeconds");
        Within(s.RageSeconds, 1, 30, "rageSeconds"); Within(s.RageHitGain, 0, 1, "rageHitGain"); Within(s.RageHurtGain, 0, 1, "rageHurtGain");
        Within(s.RageScale, 1, 1.4, "rageScale"); Within(s.RageKnock, 0, 1000, "rageKnock"); Within(s.RageKnockFactor, 1, 5, "rageKnockFactor");
        foreach (var (level, name) in new[] { (s.Easy, "easy"), (s.Hard, "hard") })
            foreach (var (value, key) in new[] { (level.EnemyHp, "enemyHp"), (level.EnemyDamage, "enemyDamage"), (level.EnemySpeed, "enemySpeed") })
                if (!double.IsFinite(value) || value < 0.25 || value > 3) throw new InvalidDataException($"{name}.{key} must be 0.25–3");
        var ids = new HashSet<string>();
        if (s.Enemies is { Length: 0 }) throw new InvalidDataException("enemies must list at least one enemy");
        for (int i = 0; i < s.EnemyTypes.Count; i++)
        {
            var e = s.EnemyTypes[i] ?? throw new InvalidDataException($"enemies[{i}] is empty");
            string at = $"enemies[{(string.IsNullOrEmpty(e.Id) ? i.ToString() : e.Id)}]";
            Id(e.Id, at + ".id");
            if (!ids.Add(e.Id)) throw new InvalidDataException($"enemy id \"{e.Id}\" is used twice");
            Id(e.Family, at + ".family");
            if (Array.IndexOf(EnemyRenderers, e.Renderer) < 0) throw new InvalidDataException($"{at}.renderer must be one of: {string.Join(", ", EnemyRenderers)}");
            Text(e.NameVi, at + ".nameVi"); Text(e.NameEn, at + ".nameEn");
            Within(e.MaxHp, 1, 100000, at + ".maxHp"); Within(e.Speed, 0, 400, at + ".speed"); Within(e.Damage, 0, 1000, at + ".damage");
            Within(e.Range, 10, 400, at + ".range"); Within(e.Windup, 0.1, 10, at + ".windup"); Within(e.AttackCooldown, 0.1, 30, at + ".attackCooldown");
            Within(e.Recover, 0, 10, at + ".recover");
            // The battle strip is 170 DIPs tall: bigger enemies would be cut off.
            Within(e.Scale, 0.5, 2.3, at + ".scale");
            if (e.HitScale is double hit) Within(hit, 0.3, 2.3, at + ".hitScale");
            if (e.Color == null || e.AngryColor == null || !Hex.IsMatch(e.Color) || !Hex.IsMatch(e.AngryColor)) throw new InvalidDataException(at + " colors must be #RRGGBB");
            if (Array.IndexOf(EnemyMarks, e.Marks) < 0) throw new InvalidDataException($"{at}.marks must be one of: {string.Join(", ", EnemyMarks)}");
            Within(e.PushFactor, 0, 3, at + ".pushFactor"); Within(e.StunFactor, 0, 3, at + ".stunFactor");
            Within(e.SlamDamage, 0, 1000, at + ".slamDamage");
            if (e.SlamDamage > 0 && !e.Slam) throw new InvalidDataException(at + ".slamDamage is for an enemy that slams");
            if (!(e.SummonAt >= 0 && e.SummonAt < 1) || !(e.EnrageAt >= 0 && e.EnrageAt < 1)) throw new InvalidDataException(at + " summonAt/enrageAt must be 0–1");
            if (e.SummonAt > 0 && e.SummonCount is < 1 or > 6) throw new InvalidDataException(at + ".summonCount must be 1–6");
            if (e.EnrageAt > 0 || e.Phases?.Any(p => p is { Enrage: true }) == true) { Within(e.EnragedSpeed, 0.1, 400, at + ".enragedSpeed"); Within(e.EnragedWindup, 0.1, 10, at + ".enragedWindup"); }
            Within(e.HealAmount, 0, 1000, at + ".healAmount");
            if (e.HealAmount > 0)
            {
                Within(e.HealRange, 10, 600, at + ".healRange"); Within(e.HealCooldown, 0.5, 60, at + ".healCooldown");
                Within(e.HealWindup, 0.2, 5, at + ".healWindup"); Within(e.HealBudget, 1, 100000, at + ".healBudget");
            }
            if (e.SummonCap is < 1 or > 10) throw new InvalidDataException(at + ".summonCap must be 1–10");
            Within(e.BlockChance, 0, 0.9, at + ".blockChance");
            if (e.BlockMaxStreak is < 1 or > 10) throw new InvalidDataException(at + ".blockMaxStreak must be 1–10");
            if (e.Support) Within(e.KeepBack, 0, 600, at + ".keepBack");
            Within(e.FarHealBelow, 0, 0.9, at + ".farHealBelow");
            if (e.FarHealBelow > 0)
            {
                if (!e.Support || e.HealAmount <= 0) throw new InvalidDataException(at + ".farHealBelow is for a support enemy that heals");
                Within(e.FarHealWindup, 0.2, 5, at + ".farHealWindup"); Within(e.FarHealCooldown, 1, 60, at + ".farHealCooldown");
            }
            Within(e.TurnSeconds, 0, 3, at + ".turnSeconds");
            Within(e.RallyEvery, 0, 60, at + ".rallyEvery");
            if (e.RallyEvery > 0)
            {
                if (e.Phases?.Any(p => p is { Shield: true }) != true) throw new InvalidDataException(at + ".rallyEvery needs a shield phase");
                Within(e.RallyCharge, 0.2, 5, at + ".rallyCharge"); Within(e.RallyHp, 0, 3, at + ".rallyHp"); Within(e.RallyDamage, 1, 3, at + ".rallyDamage");
            }
            Within(e.ShieldBreakStun, 0, 5, at + ".shieldBreakStun");
            if (e.Phases?.Any(p => p is { Shield: true }) == true)
            {
                Within(e.RetreatSpeed, 10, 600, at + ".retreatSpeed"); Within(e.RetreatSeconds, 0.2, 10, at + ".retreatSeconds");
                Within(e.LeapCrouch, 0.1, 2, at + ".leapCrouch"); Within(e.LeapSeconds, 0.3, 3, at + ".leapSeconds"); Within(e.SummonWarning, 0, 3, at + ".summonWarning");
            }
            else if (e.RetreatLeap || e.SummonWarning > 0) throw new InvalidDataException(at + ".retreatLeap/summonWarning need a shield phase");
            if (e.Dash)
            {
                Within(e.DashRange, 20, 600, at + ".dashRange"); Within(e.DashDistance, 20, 800, at + ".dashDistance");
                Within(e.DashSpeed, 50, 1500, at + ".dashSpeed");
            }
            Within(e.Cost, 0, 10, at + ".cost");
            if (e.HealGuard && e.HealAmount <= 0) throw new InvalidDataException(at + ".healGuard is for an enemy that heals");
            Within(e.HealGuardRepeat, 0, 10, at + ".healGuardRepeat"); Within(e.FleeSpeed, 1, 4, at + ".fleeSpeed");
            if ((e.FleeSpeed > 1 || e.HideBehindGuard) && !e.Support) throw new InvalidDataException(at + ".fleeSpeed/hideBehindGuard are for a support enemy");
            Within(e.HitAndRun, 0, 600, at + ".hitAndRun");
            if (e.HitAndRun > 0 && !e.Dash) throw new InvalidDataException(at + ".hitAndRun is for a dash enemy");
            if (e.Elite is EliteSpec elite)
            {
                if (e.Boss) throw new InvalidDataException(at + ".elite is not for a boss");
                Within(elite.WindupAdd, 0, 3, at + ".elite.windupAdd"); Within(elite.DashSpeed, 1, 3, at + ".elite.dashSpeed");
                if (elite.SureBlocks is < 0 or > 5) throw new InvalidDataException(at + ".elite.sureBlocks must be 0–5");
                Within(elite.FirstHeal, 1, 5, at + ".elite.firstHeal"); Within(elite.Cost, 0, 10, at + ".elite.cost");
                if ((elite.WindupAdd > 0 || elite.DashSpeed > 1) && !e.Dash) throw new InvalidDataException(at + ".elite: windupAdd/dashSpeed are for a dash enemy");
                if (elite.SureBlocks > 0 && e.BlockChance <= 0) throw new InvalidDataException(at + ".elite.sureBlocks is for an enemy that blocks");
                if (elite.FirstHeal > 1 && e.HealAmount <= 0) throw new InvalidDataException(at + ".elite.firstHeal is for an enemy that heals");
                Within(elite.Hp, 1, 3, at + ".elite.hp"); Within(elite.Damage, 1, 3, at + ".elite.damage");
                Within(elite.AttackCooldown, 0.2, 1, at + ".elite.attackCooldown"); Within(elite.HealCooldown, 0.2, 1, at + ".elite.healCooldown");
                Within(elite.HealBudget, 1, 3, at + ".elite.healBudget");
                if (elite.VirusEvery != 0) Within(elite.VirusEvery, 5, 120, at + ".elite.virusEvery");
                if ((elite.HealCooldown < 1 || elite.HealBudget > 1) && e.HealAmount <= 0) throw new InvalidDataException(at + ".elite: healCooldown/healBudget are for an enemy that heals");
                if (elite.WindupAdd <= 0 && elite.DashSpeed <= 1 && elite.SureBlocks == 0 && elite.FirstHeal <= 1 && elite.Hp <= 1 && elite.Damage <= 1
                    && elite.AttackCooldown >= 1 && elite.HealCooldown >= 1 && elite.HealBudget <= 1 && !elite.PushImmune && elite.VirusEvery <= 0
                    && e.Spit == null && e.Spring == null && e.Fuse == null && e.Split == null && !e.UsesMouse && e.Fly == null && e.Burrow == null
                    && e.Ice == null && e.Cling == null && e.Quills == null && e.Clock == null && e.Steal == null) throw new InvalidDataException(at + ".elite changes nothing");
                if (elite.DescVi == null || elite.DescEn == null || elite.DescVi.Length > 60 || elite.DescEn.Length > 60) throw new InvalidDataException(at + ".elite descriptions must be at most 60 characters");
            }
            if (e.Link is LinkSpec link)
            {
                if (Array.IndexOf(EnemyLinks.All, link.Kind) < 0) throw new InvalidDataException($"{at}.link.kind must be one of: {string.Join(", ", EnemyLinks.All)}");
                if (e.Boss) throw new InvalidDataException(at + ".link is not for a boss");
                Within(link.Range, 20, 800, at + ".link.range"); Within(link.BreakStun, 0, 5, at + ".link.breakStun"); Within(link.Relink, 0, 60, at + ".link.relink");
                if (link.Kind == EnemyLinks.Tether)
                {
                    Within(link.Share, 0.05, 0.95, at + ".link.share"); Within(link.CutSpeed, 100, 5000, at + ".link.cutSpeed");
                    if (link.EliteShare != 0) Within(link.EliteShare, 0.05, 0.95, at + ".link.eliteShare");
                }
                if (link.Kind == EnemyLinks.Rhythm)
                {
                    Within(link.Beat, 0.2, 3, at + ".link.beat");
                    if (link.Beats is < 2 or > 8) throw new InvalidDataException(at + ".link.beats must be 2–8");
                    if (link.EliteBeats is not 0 and (< 2 or > 8)) throw new InvalidDataException(at + ".link.eliteBeats must be 2–8");
                }
                if (link.Kind == EnemyLinks.Ride) Within(link.DropRange, 20, 400, at + ".link.dropRange");
                if (link.Kind == EnemyLinks.Merge)
                {
                    if (link.Count is < 2 or > 6) throw new InvalidDataException(at + ".link.count must be 2–6");
                    Within(link.MergeSeconds, 0.5, 10, at + ".link.mergeSeconds");
                }
                if (link.AfterHeal && link.Kind != EnemyLinks.Merge) throw new InvalidDataException(at + ".link.afterHeal is for a merge link");
                if (link.Kind == EnemyLinks.Aura) Within(link.Speed, 1, 3, at + ".link.speed");
                if (link.EliteSpeed != 0 && (link.Kind != EnemyLinks.Aura || link.EliteSpeed is < 1 or > 3)) throw new InvalidDataException(at + ".link.eliteSpeed is 1–3, for an aura link");
            }
            Within(e.Hover, 0, 2000, at + ".hover");
            if (e.Hover > 0 && (e.Boss || e.Blocker || e.Dash || e.Slam || e.Link != null)) throw new InvalidDataException(at + ".hover is not for a boss, blocker, dash, slam or linked enemy");
            if (e.ThrowHit is ThrowHitSpec thrown)
            {
                Within(thrown.MinSpeed, 100, 5000, at + ".throwHit.minSpeed");
                Within(thrown.Damage, 0, 1000, at + ".throwHit.damage"); Within(thrown.Stun, 0, 5, at + ".throwHit.stun");
                if (thrown.Damage <= 0 && thrown.Stun <= 0) throw new InvalidDataException(at + ".throwHit changes nothing");
            }
            if (e.Spit is SpitSpec spit)
            {
                if (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Spring != null) throw new InvalidDataException(at + ".spit is not for a boss, blocker, dash, slam, hovering or spring enemy");
                Within(spit.Range, 100, 900, at + ".spit.range"); Within(spit.MinRange, 0, 400, at + ".spit.minRange");
                if (spit.MinRange >= spit.Range) throw new InvalidDataException(at + ".spit.minRange must be below its range");
                Within(spit.Windup, 0.3, 5, at + ".spit.windup"); Within(spit.Flight, 0.2, 3, at + ".spit.flight");
                Within(spit.Radius, 10, 200, at + ".spit.radius"); Within(spit.Cooldown, 0.5, 30, at + ".spit.cooldown");
                Within(spit.PuddleSeconds, 0, 30, at + ".spit.puddleSeconds"); Within(spit.PuddleRadius, 10, 200, at + ".spit.puddleRadius");
                Within(spit.DashDistance, 1, 3, at + ".spit.dashDistance"); Within(spit.DashSpeed, 1, 3, at + ".spit.dashSpeed");
                if (spit.EliteDrops is < 1 or > 3) throw new InvalidDataException(at + ".spit.eliteDrops must be 1–3");
                Within(spit.EliteSpread, 0, 300, at + ".spit.eliteSpread"); Within(spit.ElitePuddle, 1, 3, at + ".spit.elitePuddle");
            }
            if (e.Spring is SpringSpec spring)
            {
                if (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Support || e.Link != null || e.HealAmount > 0)
                    throw new InvalidDataException(at + ".spring is not for a boss, blocker, dash, slam, hovering, support, healing or linked enemy");
                Within(spring.Radius, 20, 200, at + ".spring.radius"); Within(spring.Compress, 0.3, 3, at + ".spring.compress");
                Within(spring.Stun, 0, 3, at + ".spring.stun"); Within(spring.Rest, 0.5, 20, at + ".spring.rest");
                if (spring.FlattenHits is < 1 or > 10 || spring.EliteHits is < 1 or > 10) throw new InvalidDataException(at + ".spring.flattenHits/eliteHits must be 1–10");
                Within(spring.FlatSeconds, 0.5, 15, at + ".spring.flatSeconds"); Within(spring.CreepRange, 0, 1500, at + ".spring.creepRange");
                Within(spring.EliteRadius, 1, 2, at + ".spring.eliteRadius"); Within(spring.EliteRest, 0.5, 20, at + ".spring.eliteRest");
            }
            if (e.Fuse is FuseSpec fuse)
            {
                if (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Support || e.Spit != null || e.Spring != null || e.HealAmount > 0)
                    throw new InvalidDataException(at + ".fuse is not for a boss, blocker, dash, slam, hovering, support, spitting, spring or healing enemy");
                Within(fuse.Range, 20, 400, at + ".fuse.range"); Within(fuse.Seconds, 0.3, 5, at + ".fuse.seconds");
                Within(fuse.Radius, 20, 300, at + ".fuse.radius"); Within(fuse.Stun, 0, 3, at + ".fuse.stun");
                Within(fuse.EnemyDamage, 0, 1000, at + ".fuse.enemyDamage"); Within(fuse.DropFuse, 0.2, 5, at + ".fuse.dropFuse");
                Within(fuse.EliteSeconds, 0.3, 5, at + ".fuse.eliteSeconds"); Within(fuse.EliteRadius, 1, 2, at + ".fuse.eliteRadius");
            }
            if (e.Split is SplitSpec split)
            {
                if (e.Boss) throw new InvalidDataException(at + ".split is not for a boss");
                if (split.Count is < 2 or > 4 || split.EliteCount is < 2 or > 4) throw new InvalidDataException(at + ".split.count/eliteCount must be 2–4");
                Within(split.Health, 0.1, 1, at + ".split.health");
            }
            // 1.5 M14: a flyer or a burrower is nothing else besides.
            if (e.Fly != null || e.Burrow != null)
            {
                if (e.Fly != null && e.Burrow != null) throw new InvalidDataException(at + " may have only one of fly and burrow");
                if (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Support || e.HealAmount > 0 || e.Link != null || e.Spit != null || e.Spring != null
                    || e.Fuse != null || e.Split != null || e.UsesMouse)
                    throw new InvalidDataException(at + ": fly and burrow are not for a boss, blocker, dash, slam, hovering, support, healing, linked, spitting, spring, bomb, splitting or cursor enemy");
            }
            if (e.Fly is FlySpec fly)
            {
                Within(fly.FlySpeed, 20, 1000, at + ".fly.flySpeed"); Within(fly.Move, 1, 30, at + ".fly.move");
                Within(fly.MinHeight, 60, 1500, at + ".fly.minHeight"); Within(fly.Cooldown, 0.5, 30, at + ".fly.cooldown");
                Within(fly.Aim, 0.2, 5, at + ".fly.aim"); Within(fly.ShotSpeed, 100, 3000, at + ".fly.shotSpeed");
                Within(fly.Radius, 5, 120, at + ".fly.radius"); Within(fly.Slow, 0.1, 1, at + ".fly.slow"); Within(fly.SlowSeconds, 0, 10, at + ".fly.slowSeconds");
                if (fly.EliteShots is < 1 or > 4) throw new InvalidDataException(at + ".fly.eliteShots must be 1–4");
                if (fly.HeldHits is < 1 or > 10 || fly.EliteHeldHits is < 1 or > 10) throw new InvalidDataException(at + ".fly.heldHits/eliteHeldHits must be 1–10");
                Within(fly.HeldWindow, 0.5, 20, at + ".fly.heldWindow"); Within(fly.Downed, 0.5, 15, at + ".fly.downed");
                Within(fly.DownedDamage, 1, 4, at + ".fly.downedDamage"); Within(fly.Rise, 0.2, 5, at + ".fly.rise");
            }
            if (e.Burrow is BurrowSpec burrow)
            {
                Within(burrow.Speed, 0.2, 5, at + ".burrow.speed"); Within(burrow.Crack, 0.2, 5, at + ".burrow.crack"); Within(burrow.EliteCrack, 0.2, 5, at + ".burrow.eliteCrack");
                Within(burrow.Radius, 20, 200, at + ".burrow.radius"); Within(burrow.Emerge, 0.1, 3, at + ".burrow.emerge"); Within(burrow.Stun, 0, 5, at + ".burrow.stun");
                Within(burrow.Above, 0.5, 20, at + ".burrow.above"); Within(burrow.EliteAbove, 0.5, 20, at + ".burrow.eliteAbove");
                Within(burrow.Dig, 0.1, 3, at + ".burrow.dig"); Within(burrow.Rest, 0, 30, at + ".burrow.rest"); Within(burrow.Pinned, 0.5, 10, at + ".burrow.pinned");
            }
            // 1.5 M15: an ice shell or a wall climber is nothing else besides, and the thrown cat beats it.
            if (e.Ice != null || e.Cling != null)
            {
                if (e.Ice != null && e.Cling != null) throw new InvalidDataException(at + " may have only one of ice and cling");
                if (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Support || e.HealAmount > 0 || e.Link != null || e.Spit != null || e.Spring != null
                    || e.Fuse != null || e.Split != null || e.UsesMouse || e.Fly != null || e.Burrow != null)
                    throw new InvalidDataException(at + ": ice and cling are not for a boss, blocker, dash, slam, hovering, support, healing, linked, spitting, spring, bomb, splitting, cursor, flying or burrowing enemy");
                if (e.ThrowHit == null) throw new InvalidDataException(at + ": ice and cling need a throwHit");
            }
            if (e.Ice is IceSpec ice)
            {
                Within(ice.Armor, 0, 0.9, at + ".ice.armor"); Within(ice.WetSpeed, 100, 5000, at + ".ice.wetSpeed");
                if (ice.WetSpeed > e.ThrowHit!.MinSpeed) throw new InvalidDataException(at + ".ice.wetSpeed must be at most throwHit.minSpeed");
                Within(ice.Regrow, 0, 60, at + ".ice.regrow"); Within(ice.EliteRegrow, 0, 60, at + ".ice.eliteRegrow");
            }
            if (e.Cling is ClingSpec cling)
            {
                Within(cling.Height, 20, BattleSim.ClingTop, at + ".cling.height"); Within(cling.Spacing, 0, 100, at + ".cling.spacing");
                Within(cling.Climb, 10, 1000, at + ".cling.climb"); Within(cling.Cooldown, 0.5, 30, at + ".cling.cooldown");
                Within(cling.Aim, 0.2, 5, at + ".cling.aim"); Within(cling.Flight, 0.1, 3, at + ".cling.flight"); Within(cling.Radius, 10, 150, at + ".cling.radius");
                if (cling.EliteSpikes is < 1 or > 4) throw new InvalidDataException(at + ".cling.eliteSpikes must be 1–4");
                Within(cling.EliteSpread, 0, 300, at + ".cling.eliteSpread"); Within(cling.Downed, 0.5, 15, at + ".cling.downed"); Within(cling.Ground, 0, 30, at + ".cling.ground");
            }
            // 1.5 M16: a hedgehog, a timer or a thief is nothing else besides.
            if (e.Quills != null || e.Clock != null || e.Steal != null)
            {
                if ((e.Quills != null ? 1 : 0) + (e.Clock != null ? 1 : 0) + (e.Steal != null ? 1 : 0) > 1) throw new InvalidDataException(at + " may have only one of quills, clock and steal");
                if (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Support || e.HealAmount > 0 || e.Link != null || e.Spit != null || e.Spring != null
                    || e.Fuse != null || e.Split != null || e.UsesMouse || e.Fly != null || e.Burrow != null || e.Ice != null || e.Cling != null || e.SummonAt > 0 || e.Phases != null)
                    throw new InvalidDataException(at + ": quills, clock and steal are not for a boss, blocker, dash, slam, hovering, support, healing, linked, spitting, spring, bomb, splitting, cursor, flying, burrowing, ice, climbing or summoning enemy");
            }
            if (e.Quills is QuillsSpec quills)
            {
                Within(quills.Flat, 0.5, 30, at + ".quills.flat"); Within(quills.Warn, 0, 3, at + ".quills.warn");
                Within(quills.Puffed, 0.5, 30, at + ".quills.puffed"); Within(quills.ElitePuffed, 0.5, 30, at + ".quills.elitePuffed");
                Within(quills.Armor, 0, 1, at + ".quills.armor"); Within(quills.Prick, 0, 100, at + ".quills.prick");
            }
            if (e.Clock is ClockSpec clock)
            {
                Within(clock.Seconds, 3, 120, at + ".clock.seconds"); Within(clock.EliteSeconds, 3, 120, at + ".clock.eliteSeconds");
                Within(clock.Stunned, 0, 1, at + ".clock.stunned");
                if (clock.Calls is < 1 or > 5) throw new InvalidDataException(at + ".clock.calls must be 1–5");
                if (clock.Call is not { Length: >= 1 and <= 4 }) throw new InvalidDataException(at + ".clock.call must name 1–4 enemies");
            }
            if (e.Steal is StealSpec steal)
            {
                Within(steal.MinCharge, 0.05, 1, at + ".steal.minCharge"); Within(steal.Flee, 0.5, 4, at + ".steal.flee");
                Within(steal.EliteFlee, 0.5, 4, at + ".steal.eliteFlee"); Within(steal.Linger, 0.5, 30, at + ".steal.linger");
            }
            // 1.5 M13: one cursor trick per kind, never on a boss.
            if (e.UsesMouse)
            {
                if ((e.Ghost != null ? 1 : 0) + (e.Bubble != null ? 1 : 0) + (e.Mirage != null ? 1 : 0) + (e.Sulk != null ? 1 : 0) > 1)
                    throw new InvalidDataException(at + " may have only one of ghost, bubble, mirage and sulk");
                if (e.Boss || e.Blocker || e.Hover > 0 || e.Spring != null || e.Fuse != null || e.Split != null || e.Link != null)
                    throw new InvalidDataException(at + ": ghost, bubble, mirage and sulk are not for a boss, blocker, hovering, spring, bomb, splitting or linked enemy");
            }
            if (e.Ghost is GhostSpec ghost)
            {
                Within(ghost.Visible, 0.5, 10, at + ".ghost.visible"); Within(ghost.Hidden, 0.5, 10, at + ".ghost.hidden");
                Within(ghost.Reveal, 0.5, 10, at + ".ghost.reveal"); Within(ghost.LaserRange, 0, 600, at + ".ghost.laserRange");
                Within(ghost.EliteVisible, 0.5, 10, at + ".ghost.eliteVisible");
            }
            if (e.Bubble is BubbleSpec bubble)
            {
                if (bubble.Pops is < 1 or > 10 || bubble.ElitePops is < 1 or > 10) throw new InvalidDataException(at + ".bubble.pops/elitePops must be 1–10");
                Within(bubble.Regrow, 1, 60, at + ".bubble.regrow"); Within(bubble.Stun, 0, 5, at + ".bubble.stun"); Within(bubble.Lift, 0, 40, at + ".bubble.lift");
            }
            if (e.Mirage is MirageSpec mirage)
            {
                if (e.Support || e.HealAmount > 0) throw new InvalidDataException(at + ".mirage is not for a support or healing enemy");
                if (mirage.Copies is < 1 or > 4 || mirage.EliteCopies is < 1 or > 4) throw new InvalidDataException(at + ".mirage.copies/eliteCopies must be 1–4");
                Within(mirage.Reveal, 0.5, 10, at + ".mirage.reveal"); Within(mirage.LaserRange, 0, 600, at + ".mirage.laserRange");
            }
            if (e.Sulk is SulkSpec sulk)
            {
                if (e.HealAmount > 0) throw new InvalidDataException(at + ".sulk is not for a healing enemy");
                Within(sulk.Calm, 50, 10000, at + ".sulk.calm"); Within(sulk.EliteCalm, 50, 10000, at + ".sulk.eliteCalm");
                Within(sulk.Idle, 0, 10, at + ".sulk.idle"); Within(sulk.Fade, 0, 2, at + ".sulk.fade");
                Within(sulk.Grow, 0, 1, at + ".sulk.grow"); Within(sulk.MaxGrow, 1, 2.5, at + ".sulk.maxGrow");
            }
            if (e.Phases is { } phases)
            {
                if (phases.Length is 0 or > 6) throw new InvalidDataException(at + ".phases must list 1–6 phases");
                foreach (var p in phases)
                {
                    if (p == null || !(p.At > 0 && p.At < 1)) throw new InvalidDataException(at + ".phases: each \"at\" must be between 0 and 1");
                    if (p.Summon == null && p.Summons == null && p.Formation == null && !p.Enrage) throw new InvalidDataException(at + ".phases: a phase must summon or enrage");
                    if (p.Summon != null && p.Count is < 1 or > 6) throw new InvalidDataException(at + ".phases: count must be 1–6");
                    if (p.Summons != null && (p.Summons.Length is 0 or > 4 || p.Summons.Any(g => g == null || !g.Fixed || g.Count is < 1 or > 6)))
                        throw new InvalidDataException(at + ".phases: summons must list 1–4 plain groups of 1–6");
                    // 1.5 M17: a formation it unpacks, ropes to its helpers, packing itself small.
                    if (p.Formation != null && s.Formation(p.Formation) == null) throw new InvalidDataException($"{at}.phases names an unknown formation \"{p.Formation}\"");
                    if ((p.Roped || p.Pack) && !p.Shield) throw new InvalidDataException(at + ".phases: roped and pack are for a shield phase");
                    if (p.Roped && p.Pack) throw new InvalidDataException(at + ".phases: a phase is roped or packs, not both");
                    if (s.PhaseCalls(p).Count() > 8) throw new InvalidDataException(at + ".phases: a phase calls at most 8 helpers");
                    // A shield only drops when its helpers are gone: it needs some.
                    if (p.Shield && !s.PhaseCalls(p).Any()) throw new InvalidDataException(at + ".phases: a shield phase must summon");
                }
            }
            if (e.Roll is RollSpec roll)
            {
                if (!e.Boss) throw new InvalidDataException(at + ".roll is for a boss");
                Within(roll.Every, 1, 60, at + ".roll.every"); Within(roll.EnragedEvery, 1, 60, at + ".roll.enragedEvery");
                Within(roll.MinRange, 0, 800, at + ".roll.minRange"); Within(roll.Range, 50, 1500, at + ".roll.range");
                if (roll.MinRange >= roll.Range) throw new InvalidDataException(at + ".roll.minRange must be below its range");
                Within(roll.Windup, 0.3, 5, at + ".roll.windup"); Within(roll.Speed, 50, 1500, at + ".roll.speed");
                Within(roll.Distance, 50, 1500, at + ".roll.distance"); Within(roll.Damage, 0, 1000, at + ".roll.damage"); Within(roll.Stun, 0, 5, at + ".roll.stun");
            }
            // 1.5 M18: a brood, crown links, a goo rain (bosses only).
            if ((e.Brood != null || e.CrownLinks != null || e.Rain != null) && !e.Boss) throw new InvalidDataException(at + ".brood/crownLinks/rain are for a boss");
            if (e.Brood is BroodSpec brood)
            {
                if (brood.Count is < 1 or > 6) throw new InvalidDataException(at + ".brood.count must be 1–6");
                Within(brood.Every, 2, 60, at + ".brood.every"); Within(brood.EnragedEvery, 2, 60, at + ".brood.enragedEvery");
                Within(brood.Warning, 0, 3, at + ".brood.warning"); Within(brood.FeedRate, 0, 500, at + ".brood.feedRate");
            }
            if (e.CrownLinks is { } crown)
            {
                if (e.RallyEvery <= 0) throw new InvalidDataException(at + ".crownLinks need a rally call (rallyEvery)");
                if (crown.Length is 0 or > 4 || crown.Any(l => l == null || l.Kind is not (EnemyLinks.Tether or EnemyLinks.Rhythm)))
                    throw new InvalidDataException(at + ".crownLinks must list 1–4 tether or rhythm links");
                foreach (var l in crown)
                {
                    Within(l.Range, 20, 800, at + ".crownLinks.range"); Within(l.BreakStun, 0, 5, at + ".crownLinks.breakStun"); Within(l.Relink, 0, 60, at + ".crownLinks.relink");
                    if (l.Kind == EnemyLinks.Tether) { Within(l.Share, 0.05, 0.95, at + ".crownLinks.share"); Within(l.CutSpeed, 100, 5000, at + ".crownLinks.cutSpeed"); }
                    else { Within(l.Beat, 0.2, 3, at + ".crownLinks.beat"); if (l.Beats is < 2 or > 8) throw new InvalidDataException(at + ".crownLinks.beats must be 2–8"); }
                }
            }
            if (e.Rain is RainSpec rain)
            {
                if (rain.Drops is < 1 or > 8) throw new InvalidDataException(at + ".rain.drops must be 1–8");
                Within(rain.Every, 2, 60, at + ".rain.every"); Within(rain.Spacing, 20, 400, at + ".rain.spacing"); Within(rain.Warning, 0.3, 5, at + ".rain.warning");
                Within(rain.Radius, 10, 200, at + ".rain.radius"); Within(rain.Damage, 0, 1000, at + ".rain.damage"); Within(rain.Height, 40, 400, at + ".rain.height");
                Within(rain.PuddleSeconds, 0, 30, at + ".rain.puddleSeconds");
            }
            Within(e.RopeCutSpeed, 100, 5000, at + ".ropeCutSpeed"); Within(e.RopeCutStun, 0, 5, at + ".ropeCutStun");
            if (e.Still && (e.Boss || e.Blocker || e.Dash || e.Slam || e.Hover > 0 || e.Support || e.HealAmount > 0 || e.Link != null || e.Spit != null || e.Spring != null
                || e.Fuse != null || e.Split != null || e.UsesMouse || e.Fly != null || e.Burrow != null || e.Ice != null || e.Cling != null || e.Quills != null
                || e.Clock != null || e.Steal != null || e.SummonAt > 0 || e.Phases != null))
                throw new InvalidDataException(at + ": still is only for a plain enemy (no boss, attack, link or 1.5 trick)");
        }
        // A rider's mount and a merge's result: known enemies without a link of that kind (no chains).
        foreach (var e in s.EnemyTypes)
        {
            if (e.Link is not LinkSpec link) continue;
            if (link.Kind == EnemyLinks.Ride && (link.Mount == null || !ids.Contains(link.Mount) || s.Enemy(link.Mount) is { Boss: true } or { Link.Kind: EnemyLinks.Ride }))
                throw new InvalidDataException($"enemies[{e.Id}].link.mount must name a known enemy that is not a boss and does not ride");
            if (link.Kind == EnemyLinks.Merge && (link.Into == null || !ids.Contains(link.Into) || s.Enemy(link.Into) is { Boss: true } or { Link.Kind: EnemyLinks.Merge }))
                throw new InvalidDataException($"enemies[{e.Id}].link.into must name a known enemy that is not a boss and does not merge");
            if (link.Kind is not EnemyLinks.Ride && link.Mount != null) throw new InvalidDataException($"enemies[{e.Id}].link.mount is for a ride link");
            if (link.Kind is not EnemyLinks.Merge && link.Into != null) throw new InvalidDataException($"enemies[{e.Id}].link.into is for a merge link");
        }
        // What a boss's brood lays (1.5 M18): a known enemy with a merge link, not a boss, that summons nobody.
        foreach (var e in s.EnemyTypes)
            if (e.Brood is BroodSpec brood && (!ids.Contains(brood.Enemy) || s.Enemy(brood.Enemy) is { Boss: true } or not { Link.Kind: EnemyLinks.Merge } or { SummonAt: > 0 } or { Phases: not null }))
                throw new InvalidDataException($"enemies[{e.Id}].brood.enemy must name a known enemy with a merge link that is not a boss");
        // What a splitting enemy breaks into: a known enemy that is not a boss and does not split (no chains).
        foreach (var e in s.EnemyTypes)
            if (e.Split is SplitSpec split && (!ids.Contains(split.Into) || s.Enemy(split.Into) is { Boss: true } or { Split: not null }))
                throw new InvalidDataException($"enemies[{e.Id}].split.into must name a known enemy that is not a boss and does not split");
        // What a timer calls: known enemies that are not bosses and call nobody in turn (1.5 M16).
        foreach (var e in s.EnemyTypes)
            if (e.Clock is ClockSpec clock)
                foreach (var call in clock.Call)
                    if (call == null || !ids.Contains(call) || s.Enemy(call) is { Boss: true } or { Clock: not null })
                        throw new InvalidDataException($"enemies[{e.Id}].clock.call names \"{call}\": an unknown enemy, a boss or another timer");
        // A summoned helper may not summon in turn (no chains of calls).
        bool Summons(EnemyType e) => e.SummonAt > 0 || e.Phases?.Any(p => s.PhaseCalls(p).Any()) == true;
        foreach (var e in s.EnemyTypes)
            foreach (var p in e.Phases ?? Array.Empty<BossPhase>())
                foreach (var call in s.PhaseCalls(p).Distinct())
                    if (!ids.Contains(call) || Summons(s.Enemy(call)))
                        throw new InvalidDataException($"enemies[{e.Id}] phase summons \"{call}\": an unknown enemy (or one that summons too)");
        foreach (var e in s.EnemyTypes)
            if (e.SummonAt > 0 && (!ids.Contains(e.SummonType) || Summons(s.Enemy(e.SummonType))))
                throw new InvalidDataException($"enemies[{e.Id}] summons \"{e.SummonType}\": an unknown enemy (or one that summons too)");
        // Drawn kinds: known, never a boss (a boss only comes where a wave names it), some weight.
        void Weights(Dictionary<string, double>? weights, string at)
        {
            if (weights == null || weights.Count is 0 or > 8) throw new InvalidDataException($"{at} must list 1–8 enemies");
            foreach (var (kind, weight) in weights)
            {
                if (!ids.Contains(kind) || s.Enemy(kind).Boss) throw new InvalidDataException($"{at} names \"{kind}\": an unknown enemy or a boss");
                Within(weight, 0, 100, $"{at}.{kind}");
            }
            if (weights.Values.Sum() <= 0) throw new InvalidDataException($"{at} needs some weight above 0");
        }
        // Formations (1.5 M12): named teams of 1–4 parts, each a plain group or a squad of known non-boss enemies.
        var formationIds = new HashSet<string>();
        foreach (var f in s.FormationList)
        {
            if (f == null) throw new InvalidDataException("formations has an empty entry");
            string at = $"formations[{(string.IsNullOrEmpty(f.Id) ? "?" : f.Id)}]";
            Id(f.Id, at + ".id");
            if (!formationIds.Add(f.Id)) throw new InvalidDataException($"formation id \"{f.Id}\" is used twice");
            Text(f.NameVi, at + ".nameVi"); Text(f.NameEn, at + ".nameEn");
            if (f.TipVi?.Length > 80 || f.TipEn?.Length > 80 || (f.TipVi == null) != (f.TipEn == null)) throw new InvalidDataException(at + ": tipVi and tipEn go together, at most 80 characters");
            Within(f.MinThreat, 0, 3, at + ".minThreat");
            if (f.Parts == null || f.Parts.Length is 0 or > 4) throw new InvalidDataException(at + ".parts must list 1–4 parts");
            foreach (var part in f.Parts)
            {
                if (part == null || part.Drawn) throw new InvalidDataException(at + ".parts: a part is a plain group or a squad (no pick, no formation)");
                if (part.Side != null && Array.IndexOf(GroupSides, part.Side) < 0) throw new InvalidDataException(at + ".parts: side must be \"a\" or \"b\"");
                if (part.Squad != null)
                {
                    if (part.Count != 1 || part.Squad.Length is 0 or > 4 || part.Squad.Any(m => m == null || !m.Fixed || !ids.Contains(m.Enemy) || s.Enemy(m.Enemy).Boss || m.Count is < 1 or > 6) || part.SquadSize is < 2 or > 8)
                        throw new InvalidDataException(at + ".parts: a squad lists 1–4 known, non-boss enemies (2–8 in all, counts 1–6) and comes once");
                }
                else if (!ids.Contains(part.Enemy) || s.Enemy(part.Enemy).Boss || part.Count is < 1 or > 6) throw new InvalidDataException(at + ".parts: a plain part names a known, non-boss enemy, count 1–6");
                foreach (var kind in part.Squad?.Where(m => m.Elite || part.Elite).Select(m => m.Enemy) ?? (part.Elite ? new[] { part.Enemy } : Array.Empty<string>()))
                    if (s.Enemy(kind).Elite == null) throw new InvalidDataException($"{at}.parts.elite: \"{kind}\" has no elite affix");
            }
            if (f.Members.Count() > 12) throw new InvalidDataException(at + " has more than 12 enemies");
        }
        // maxAlive: what the battle allows on the floor (a squad must fit); survival: a survive stage.
        void Waves(IReadOnlyList<WaveSpec>? waves, string at, int maxAlive, bool survival)
        {
            if (waves == null || waves.Count == 0) throw new InvalidDataException($"{at} needs at least one wave");
            if (waves.Count > 30) throw new InvalidDataException($"{at} may have at most 30 waves");
            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave == null) throw new InvalidDataException($"{at}[{i}] is empty");
                Within(wave.Threat, 0.5, 3, $"{at}[{i}].threat");
                if (wave.Stream is StreamSpec stream)
                {
                    // It never ends, so only a survive stage's clock can finish it.
                    if (!survival || i != waves.Count - 1) throw new InvalidDataException($"{at}[{i}].stream is only for the last wave of a survive stage");
                    Weights(stream.Weights, $"{at}[{i}].stream.weights");
                    Within(stream.Interval, 0.3, 20, $"{at}[{i}].stream.interval"); Within(stream.IntervalEnd, 0.3, 20, $"{at}[{i}].stream.intervalEnd");
                    Within(stream.PincerAfter, 0, 600, $"{at}[{i}].stream.pincerAfter");
                    Within(stream.Budget, 0, 100, $"{at}[{i}].stream.budget");
                    // Some drawn kind must always fit an empty floor, or the stream would never spawn.
                    if (stream.Budget > 0 && stream.Weights.Where(w => w.Value > 0).Min(w => s.Enemy(w.Key).Cost) > stream.Budget)
                        throw new InvalidDataException($"{at}[{i}].stream.budget is below the cheapest kind it draws");
                    if (wave.Groups is { Length: > 0 }) throw new InvalidDataException($"{at}[{i}]: a stream wave has no groups");
                    continue;
                }
                if (wave.Groups == null || wave.Groups.Length == 0) throw new InvalidDataException($"{at}[{i}] needs at least one group");
                Within(wave.Budget, 0, 100, $"{at}[{i}].budget");
                foreach (var g in wave.Groups)
                {
                    if (g == null) throw new InvalidDataException($"{at}[{i}] has an empty group");
                    if (g.Count is < 1 or > 50) throw new InvalidDataException($"{at}[{i}] {g.Enemy}: count must be 1–50");
                    if (g.Side != null && Array.IndexOf(GroupSides, g.Side) < 0) throw new InvalidDataException($"{at}[{i}].side must be \"a\" or \"b\"");
                    if (g.Pick != null && g.Squad != null) throw new InvalidDataException($"{at}[{i}]: a group is a pick or a squad, not both");
                    if (g.Formation != null)
                    {
                        if (g.Pick != null || g.Squad != null || g.AtLeast != null || g.Elite) throw new InvalidDataException($"{at}[{i}]: a formation group has no pick, squad, atLeast or elite");
                        if (g.Count > 4) throw new InvalidDataException($"{at}[{i}].formation: count must be 1–4");
                        if (g.Formation.Count is 0 or > 8) throw new InvalidDataException($"{at}[{i}].formation must list 1–8 formations");
                        foreach (var (id, weight) in g.Formation)
                        {
                            if (s.Formation(id) is not FormationSpec f) throw new InvalidDataException($"{at}[{i}].formation names an unknown formation \"{id}\"");
                            Within(weight, 0, 100, $"{at}[{i}].formation.{id}");
                            // A part that never fits would hold the wave for ever.
                            if (f.Parts.Any(p => p.SquadSize > maxAlive)) throw new InvalidDataException($"{at}[{i}].formation \"{id}\" has a part bigger than maxAlive ({maxAlive})");
                        }
                        if (g.Formation.Values.Sum() <= 0) throw new InvalidDataException($"{at}[{i}].formation needs some weight above 0");
                        continue;
                    }
                    foreach (var kind in g.Squad?.Where(m => m is { Elite: true }).Select(m => m.Enemy) ?? (g.Elite ? g.Pick?.Keys ?? (IEnumerable<string>)new[] { g.Enemy } : Array.Empty<string>()))
                        if (ids.Contains(kind) && s.Enemy(kind).Elite == null) throw new InvalidDataException($"{at}[{i}].elite: \"{kind}\" has no elite affix");
                    if (g.Pick != null)
                    {
                        Weights(g.Pick, $"{at}[{i}].pick");
                        foreach (var (kind, n) in g.AtLeast ?? new())
                            if (!ids.Contains(kind) || s.Enemy(kind).Boss || n < 0) throw new InvalidDataException($"{at}[{i}].atLeast names \"{kind}\": an unknown enemy or a boss (or a negative count)");
                        if ((g.AtLeast?.Values.Sum() ?? 0) > g.Count) throw new InvalidDataException($"{at}[{i}].atLeast asks for more than count");
                        continue;
                    }
                    if (g.AtLeast != null) throw new InvalidDataException($"{at}[{i}].atLeast goes with pick");
                    if (g.Squad != null)
                    {
                        if (g.Squad.Length is 0 or > 4 || g.Squad.Any(m => m == null || !m.Fixed || !ids.Contains(m.Enemy) || s.Enemy(m.Enemy).Boss || m.Count is < 1 or > 6))
                            throw new InvalidDataException($"{at}[{i}].squad must list 1–4 known, non-boss enemies with counts 1–6");
                        // A squad that never fits would hold the wave for ever.
                        if (g.SquadSize is < 2 or > 8 || g.SquadSize > maxAlive) throw new InvalidDataException($"{at}[{i}].squad has {g.SquadSize} enemies: it needs 2–8 and at most maxAlive ({maxAlive})");
                        continue;
                    }
                    if (!ids.Contains(g.Enemy)) throw new InvalidDataException($"{at}[{i}] names an unknown enemy \"{g.Enemy}\"");
                }
            }
            // At most two kinds of link in one battle: every kind its waves can bring (drawn ones,
            // helpers a boss or an enemy calls, mounts, merged results) counts.
            var reach = new Stack<string>();
            foreach (var wave in waves)
            {
                foreach (var kind in wave.Stream?.Weights.Keys ?? Enumerable.Empty<string>()) reach.Push(kind);
                foreach (var g in wave.Groups ?? Array.Empty<WaveGroup>())
                    if (g.Pick != null) foreach (var kind in g.Pick.Keys) reach.Push(kind);
                    else if (g.Formation != null) { foreach (var id in g.Formation.Keys) foreach (var kind in s.Formation(id)?.Members ?? Enumerable.Empty<string>()) reach.Push(kind); }
                    else if (g.Squad != null) foreach (var m in g.Squad) reach.Push(m.Enemy);
                    else reach.Push(g.Enemy);
            }
            var met = new HashSet<string>();
            var links = new HashSet<string>();
            var mouse = new HashSet<string>();
            while (reach.Count > 0)
            {
                string kind = reach.Pop();
                if (!ids.Contains(kind) || !met.Add(kind)) continue;
                var e = s.Enemy(kind);
                if (e.UsesMouse) mouse.Add(kind);
                if (e.ActiveLink is LinkSpec link)
                {
                    links.Add(link.Kind);
                    if (link.Mount != null) reach.Push(link.Mount);
                    if (link.Into != null) reach.Push(link.Into);
                }
                if (e.SummonAt > 0) reach.Push(e.SummonType);
                if (e.Split != null) reach.Push(e.Split.Into);
                foreach (var p in e.Phases ?? Array.Empty<BossPhase>()) foreach (var call in s.PhaseCalls(p)) reach.Push(call);
                // 1.5 M18: what a brood lays, the links a crown ties.
                if (e.Brood != null) reach.Push(e.Brood.Enemy);
                foreach (var l in e.CrownLinks ?? Array.Empty<LinkSpec>()) if (l.Enabled) links.Add(l.Kind);
            }
            if (links.Count > EnemyLinks.MaxKinds)
                throw new InvalidDataException($"{at} brings {links.Count} kinds of link ({string.Join(", ", links)}): at most {EnemyLinks.MaxKinds} in one battle");
            // The cursor is not overworked: at most two kinds the cursor beats (1.5 M13) in one battle.
            if (mouse.Count > MaxMouseKinds)
                throw new InvalidDataException($"{at} brings {mouse.Count} kinds the cursor beats ({string.Join(", ", mouse)}): at most {MaxMouseKinds} in one battle");
        }
        Waves(s.WavePlan, "waves", s.MaxAlive, false);
        if (s.Upgrades is { Length: 0 }) throw new InvalidDataException("upgrades must list at least one upgrade");
        var upgradeIds = new HashSet<string>();
        foreach (var u in s.UpgradeList)
        {
            if (u == null) throw new InvalidDataException("upgrades has an empty entry");
            string at = $"upgrades[{(string.IsNullOrEmpty(u.Id) ? "?" : u.Id)}]";
            Id(u.Id, at + ".id");
            if (!upgradeIds.Add(u.Id)) throw new InvalidDataException($"upgrade id \"{u.Id}\" is used twice");
            Text(u.NameVi, at + ".nameVi"); Text(u.NameEn, at + ".nameEn");
            if (u.DescVi == null || u.DescEn == null || u.DescVi.Length > 80 || u.DescEn.Length > 80) throw new InvalidDataException($"{at} descriptions must be at most 80 characters");
            if (!UpgradeLimits.TryGetValue(u.Effect ?? "", out var limit)) throw new InvalidDataException($"{at}.effect must be one of: {string.Join(", ", UpgradeLimits.Keys)}");
            Within(u.Amount, limit.Min, limit.Max, at + ".amount");
            if (u.MaxStacks is < 0 or > 10) throw new InvalidDataException($"{at}.maxStacks must be 0–10 (0: no limit)");
            if (u.Group != null && Array.IndexOf(BuildGroups.All, u.Group) < 0) throw new InvalidDataException($"{at}.group must be one of: {string.Join(", ", BuildGroups.All)} (or left out)");
            if (u.Group != null && u.Effect == "heal") throw new InvalidDataException($"{at}: a heal card cannot belong to a group");
            if (u.Rare && (u.Group == null || u.MaxStacks != 1)) throw new InvalidDataException($"{at}: a rare card needs a group and maxStacks 1");
            if ((u.Effect == "onHit") != (u.OnHit != null)) throw new InvalidDataException($"{at}: effect \"onHit\" needs an \"onHit\" object, and only it may have one");
            if (u.OnHit != null)
            {
                StatusHit.Validate(u.OnHit, at + ".onHit");
                if (u.OnHit.Chance != 1) throw new InvalidDataException($"{at}.onHit.chance must be left out (a card's chance is its amount)");
            }
            if (u.Skill != null && BattleSkills.Parse(u.Skill) == null) throw new InvalidDataException($"{at}.skill must be a skill id: {KnownSkills}");
        }
        foreach (var group in BuildGroups.All)
            if (s.UpgradeList.Count(u => u.Rare && u.Group == group) > 2) throw new InvalidDataException($"upgrades: at most two rare cards per group ({group})");
        foreach (var u in s.UpgradeList)
            if (u.EndlessMaxStacks is int endlessMax && (endlessMax is < 1 or > 10 || u.MaxStacks == 0 || endlessMax < u.MaxStacks || u.Rare))
                throw new InvalidDataException($"upgrades[{u.Id}].endlessMaxStacks must be 1–10, at least maxStacks (not for a rare or unlimited card)");
        Within(s.UtilityChance, 0, 1, "utilityChance");
        Within(s.RareChance, 0, 1, "rareChance");
        if (s.MaxElites is < 0 or > 5) throw new InvalidDataException("maxElites must be 0–5");
        var synergyIds = new HashSet<string>();
        foreach (var y in s.SynergyList)
        {
            if (y == null) throw new InvalidDataException("synergies has an empty entry");
            string at = $"synergies[{(string.IsNullOrEmpty(y.Id) ? "?" : y.Id)}]";
            Id(y.Id, at + ".id");
            if (!synergyIds.Add(y.Id)) throw new InvalidDataException($"synergy id \"{y.Id}\" is used twice");
            Text(y.NameVi, at + ".nameVi"); Text(y.NameEn, at + ".nameEn");
            if (y.DescVi == null || y.DescEn == null || y.DescVi.Length > 80 || y.DescEn.Length > 80) throw new InvalidDataException($"{at} descriptions must be at most 80 characters");
            if (Array.IndexOf(BuildGroups.All, y.Group) < 0) throw new InvalidDataException($"{at}.group must be one of: {string.Join(", ", BuildGroups.All)}");
            if (!SynergyLimits.TryGetValue(y.Effect ?? "", out var limit)) throw new InvalidDataException($"{at}.effect must be one of: {string.Join(", ", SynergyLimits.Keys)}");
            Within(y.Amount, limit.Min, limit.Max, at + ".amount");
            if (y.At is < 1 or > 10) throw new InvalidDataException($"{at}.at must be 1–10");
            if (y.Effect == "echo" && y.Every is < 2 or > 10) throw new InvalidDataException($"{at}.every must be 2–10");
            Within(y.Armor, 0, 0.5, at + ".armor");
        }
        Within(s.ThreatDamageShare, 0, 1, "threatDamageShare");
        if (s.MaxAttackers is < 0 or > 10) throw new InvalidDataException("maxAttackers must be 0–10 (0: no limit)");
        Within(s.AttackStagger, 0, 3, "attackStagger"); Within(s.IncomingWarning, 0, 5, "incomingWarning");
        if (s.Campaign is CampaignSpec campaign) ValidateCampaign(campaign, (waves, at, alive, survival) => Waves(waves, at, alive ?? s.MaxAlive, survival), Id, Text);
        if (s.Endless is EndlessSpec endless) EndlessSpec.Validate(endless, s);
        if (s.Drops is DropSpec drops) DropSpec.Validate(drops);
        if (s.Expedition is ExpeditionSpec expedition) ExpeditionSpec.Validate(expedition, s, (waves, at, alive, survival) => Waves(waves, at, alive ?? s.MaxAlive, survival), Id, Text);
    }

    public static readonly string[] StageObjectives = { "clear", "survive" };
    // What an upgrade can do, and its amount's range (fractions, DIPs/s or HP).
    public static readonly Dictionary<string, (double Min, double Max)> UpgradeLimits = new()
    {
        ["damage"] = (0.01, 2), ["moveSpeed"] = (0.01, 1), ["attackSpeed"] = (0.01, 0.5),
        ["knock"] = (1, 500), ["heal"] = (1, 1000), ["armor"] = (0.01, 0.5), ["maxHp"] = (1, 500),
        // Rare cards' effects: shell (seconds to grow back), opener and momentum (fractions).
        ["shell"] = (3, 60), ["opener"] = (0.1, 3), ["momentum"] = (0.1, 3),
        // 2026-10-24.
        ["crit"] = (0.01, 1), ["execute"] = (0.1, 3), ["comboWindow"] = (0.05, 2), ["landingRush"] = (0.05, 2),
        ["regen"] = (0.05, 10), ["stunCut"] = (0.05, 0.9), ["skillCooldown"] = (0.01, 0.5), ["appCharge"] = (0.01, 0.5),
        ["killCharge"] = (0.005, 0.2), ["bomb"] = (0.05, 2), ["shieldTime"] = (0.2, 5),
        // 1.5 M04: the chance a landed weapon hit leaves the card's onHit status.
        ["onHit"] = (0.01, 1)
    };
    public static readonly Dictionary<string, (double Min, double Max)> SynergyLimits = new()
    {
        ["thorns"] = (0.05, 2), ["armorBreak"] = (0.05, 3), ["echo"] = (0.1, 2),
        ["lastStand"] = (1, 1), ["autoShield"] = (5, 120), ["comboStun"] = (0.1, 3), ["finisher"] = (0.01, 0.5), ["rush"] = (0.05, 1)
    };

    private static void ValidateCampaign(CampaignSpec campaign, Action<IReadOnlyList<WaveSpec>?, string, int?, bool> waves, Action<string?, string> id, Action<string?, string> text)
    {
        if (campaign.Chapters == null || campaign.Chapters.Length == 0) throw new InvalidDataException("campaign.chapters must list at least one chapter");
        var chapterIds = new HashSet<string>();
        // Stage ids are unique across the whole campaign: progress is saved by them alone.
        var stageIds = new HashSet<string>();
        for (int c = 0; c < campaign.Chapters.Length; c++)
        {
            var chapter = campaign.Chapters[c] ?? throw new InvalidDataException($"campaign.chapters[{c}] is empty");
            string at = $"campaign.chapters[{(string.IsNullOrEmpty(chapter.Id) ? c.ToString() : chapter.Id)}]";
            id(chapter.Id, at + ".id");
            if (!chapterIds.Add(chapter.Id)) throw new InvalidDataException($"chapter id \"{chapter.Id}\" is used twice");
            text(chapter.NameVi, at + ".nameVi"); text(chapter.NameEn, at + ".nameEn");
            if (chapter.Stages == null || chapter.Stages.Length is < 1 or > 12) throw new InvalidDataException($"{at}.stages must list 1–12 stages");
            for (int i = 0; i < chapter.Stages.Length; i++)
            {
                var stage = chapter.Stages[i] ?? throw new InvalidDataException($"{at}.stages[{i}] is empty");
                string where = $"{at}.stages[{(string.IsNullOrEmpty(stage.Id) ? i.ToString() : stage.Id)}]";
                id(stage.Id, where + ".id");
                if (!stageIds.Add(stage.Id)) throw new InvalidDataException($"stage id \"{stage.Id}\" is used twice");
                ValidateStage(stage, where, waves, text);
            }
        }
        // 1.5 M21: a chapter opens after a stage of an earlier chapter (no loops, nothing unknown).
        for (int c = 0; c < campaign.Chapters.Length; c++)
            if (campaign.Chapters[c].UnlockAfter is string after
                && !campaign.Chapters.Take(c).Any(earlier => earlier.Stages.Any(s => s.Id == after)))
                throw new InvalidDataException($"campaign.chapters[{campaign.Chapters[c].Id}].unlockAfter must name a stage of an earlier chapter");
    }

    // One stage's own rules (an Adventure stage or an Expedition encounter); its id is checked by the caller.
    internal static void ValidateStage(StageSpec stage, string where, Action<IReadOnlyList<WaveSpec>?, string, int?, bool> waves, Action<string?, string> text)
    {
        text(stage.NameVi, where + ".nameVi"); text(stage.NameEn, where + ".nameEn");
        if (stage.GoalVi == null || stage.GoalEn == null || stage.GoalVi.Length > 160 || stage.GoalEn.Length > 160)
            throw new InvalidDataException($"{where} goals must be at most 160 characters");
        if (Array.IndexOf(StageObjectives, stage.Objective) < 0) throw new InvalidDataException($"{where}.objective must be one of: {string.Join(", ", StageObjectives)}");
        if (stage.RulesVersion is < 1 or > 1000) throw new InvalidDataException($"{where}.rulesVersion must be 1–1000");
        if (stage.MaxAlive is int alive && alive is < 1 or > 10) throw new InvalidDataException($"{where}.maxAlive must be 1–10");
        if (stage.MaxUpgrades is int picks && picks is < 1 or > 30) throw new InvalidDataException($"{where}.maxUpgrades must be 1–30");
        if (!double.IsFinite(stage.EliteChance) || stage.EliteChance is < 0 or > 1) throw new InvalidDataException($"{where}.eliteChance must be 0–1");
        if (!double.IsFinite(stage.MalwareFrom) || stage.MalwareFrom is < 0 or > 600) throw new InvalidDataException($"{where}.malwareFrom must be 0–600");
        if (stage.MalwarePlan is { } plan)
        {
            if (!stage.Malware || plan.Length is 0 or > 12) throw new InvalidDataException($"{where}.malwarePlan must list 1–12 cues, in a stage with malware");
            foreach (var cue in plan)
                if (cue == null || Array.IndexOf(MalwareCue.Kinds, cue.Kind) < 0 || !double.IsFinite(cue.At) || cue.At is < 0 or > 600)
                    throw new InvalidDataException($"{where}.malwarePlan: each cue needs \"at\" 0–600 and a kind of {string.Join(", ", MalwareCue.Kinds)}");
        }
        if (stage.Survival && stage.SurviveSeconds is not (>= 10 and <= 600)) throw new InvalidDataException($"{where}.surviveSeconds must be 10–600 for a survive stage");
        if (!stage.Survival && stage.SurviveSeconds != null) throw new InvalidDataException($"{where}.surviveSeconds is only for a survive stage");
        waves(stage.Waves, where + ".waves", stage.MaxAlive, stage.Survival);
        if (!double.IsFinite(stage.Threat) || stage.Threat is < 0.5 or > 3) throw new InvalidDataException($"{where}.threat must be 0.5–3");
        if (!double.IsFinite(stage.ThreatGrowth) || stage.ThreatGrowth is < 0 or > 2 || (stage.ThreatGrowth > 0 && !stage.Survival))
            throw new InvalidDataException($"{where}.threatGrowth must be 0–2 (per minute), and only for a survive stage");
        if (stage.UpgradeAt is { } offers)
        {
            if (!stage.Survival || !stage.Upgrades) throw new InvalidDataException($"{where}.upgradeAt is only for a survive stage with upgrades");
            if (offers.Length is 0 or > 10) throw new InvalidDataException($"{where}.upgradeAt must list 1–10 times");
            for (int k = 0; k < offers.Length; k++)
                if (!double.IsFinite(offers[k]) || offers[k] < 1 || offers[k] >= stage.SurviveSeconds || (k > 0 && offers[k] <= offers[k - 1]))
                    throw new InvalidDataException($"{where}.upgradeAt must rise and stay inside the survive time");
        }
        if ((stage.TipVi == null) != (stage.TipEn == null) || stage.TipVi?.Length > 400 || stage.TipEn?.Length > 400)
            throw new InvalidDataException($"{where} tips need both languages, at most 400 characters each");
        if (stage.UnlocksSkill != null && BattleSkills.Parse(stage.UnlocksSkill) == null)
            throw new InvalidDataException($"{where}.unlocksSkill must be a skill id: {BattleData.KnownSkills}");
        if (stage.MaxMalware is int malware && (malware is < 1 or > 4 || !stage.Malware)) throw new InvalidDataException($"{where}.maxMalware must be 1–4, in a stage with malware");
        if (stage.SuggestSkills is { } suggest && (suggest.Length is 0 or > BattleSkills.Carry || suggest.Any(k => BattleSkills.Parse(k) == null) || suggest.Distinct().Count() != suggest.Length))
            throw new InvalidDataException($"{where}.suggestSkills must list 1–{BattleSkills.Carry} different skill ids: {BattleData.KnownSkills}");
        if (stage.Basket is BasketSpec basket) BasketSpec.Validate(basket, where);
        if (stage.Reward != null && PetAccessories.Find(stage.Reward) is not { Reward: not null })
            throw new InvalidDataException($"{where}.reward \"{stage.Reward}\" is not a reward accessory in accessories.json");
    }
}

// The user's skills, in HUD order: how each is used and which number is its cooldown (code).
// Names and descriptions are data (battle.json "skills", SkillSpec); a description's {key} is
// filled with that number of the battle, so battle.json changes show in the HUD. Adding a
// skill: a BattleSkill value, an entry here and in BuiltIn/"skills", its HudGlyph and its
// effect in BattleSim.Use. A battle carries Carry of them (its loadout, chosen on the setup
// panel); Working: false for a skill that has no effect yet (shown locked, never ready).
// Aim: how a chosen skill is used on the field (none: it acts at once when its slot is clicked).
public sealed record BattleSkillInfo(BattleSkill Skill, SkillAim Aim, Func<BattleSettings, double> Cooldown, bool Working = true)
{
    public bool Aimed => Aim != SkillAim.None;
    // Without a battle's settings: the built-in data's names.
    public string Name(string language, BattleSettings? s = null) => (s ?? BattleData.Settings).Skill(Skill).Name(language);
    public string Describe(BattleSettings s, string language) => BattleSkills.Fill(s.Skill(Skill).Desc(language), s);
}

public static class BattleSkills
{
    private static string N(double value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    public static readonly BattleSkillInfo[] All =
    {
        new(BattleSkill.Push, SkillAim.Slime, s => s.PushCooldown),
        new(BattleSkill.Heal, SkillAim.None, s => s.HealCooldown),
        new(BattleSkill.Cheer, SkillAim.None, s => s.CheerCooldown),
        new(BattleSkill.Bomb, SkillAim.Slime, s => s.BombCooldown),
        new(BattleSkill.Shield, SkillAim.None, s => s.ShieldCooldown),
        // 1.5 (docs/UPDATE-1.5.md): Laser and Fan since M05 (PetBattle.Tools.cs), Grabber, Scissors and
        // Spray since M06 (PetBattle.Grabber.cs, PetBattle.Tools.cs); a hold skill's cooldown is its
        // tank's refill once spent.
        new(BattleSkill.Laser, SkillAim.Point, s => s.LaserCooldown),
        new(BattleSkill.Grabber, SkillAim.Drag, s => s.GrabberCooldown),
        new(BattleSkill.Scissors, SkillAim.Swipe, s => s.ScissorsCooldown),
        new(BattleSkill.Fan, SkillAim.Hold, s => s.FanRefillSeconds),
        new(BattleSkill.Spray, SkillAim.Hold, s => s.SprayRefillSeconds),
    };

    public static BattleSkillInfo Get(BattleSkill skill) => All[Array.FindIndex(All, info => info.Skill == skill)];

    // The HUD's five slots. Default: the five first skills (chapter 1 always has them, and so does a
    // battle given no loadout).
    public const int Carry = 5;
    public static readonly BattleSkill[] Default = { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Cheer, BattleSkill.Bomb, BattleSkill.Shield };
    public static bool IsDefault(IReadOnlyList<BattleSkill> skills) => skills.SequenceEqual(Default);

    // A skill id as battle.json and the saved choices write it ("push", "laser"; case ignored).
    public static string Id(BattleSkill skill) => skill.ToString().ToLowerInvariant();
    public static BattleSkill? Parse(string? id) =>
        Enum.TryParse<BattleSkill>(id, true, out var skill) && Enum.IsDefined(skill) && !int.TryParse(id, out _) ? skill : null;

    // A loadout in HUD order (the order of All), or null when it is not Carry different known
    // skills. Saved ids that no longer fit fall back to Default (Loadout). Which skills the user may
    // choose (open, working) is the setup panel's rule (PetWindow.BattleSkillOpen).
    public static BattleSkill[]? Order(IEnumerable<BattleSkill> skills)
    {
        var set = skills.ToHashSet();
        if (set.Count != Carry || set.Any(skill => !Enum.IsDefined(skill))) return null;
        return All.Select(info => info.Skill).Where(set.Contains).ToArray();
    }
    public static BattleSkill[] Loadout(IEnumerable<string>? ids)
    {
        if (ids == null) return Default;
        var skills = new List<BattleSkill>();
        foreach (var id in ids) if (Parse(id) is BattleSkill skill) skills.Add(skill); else return Default;
        return Order(skills) ?? Default;
    }
    // What to save: null for the default (an old file and a new one read the same).
    public static List<string>? Save(IReadOnlyList<BattleSkill> skills) => IsDefault(skills) ? null : skills.Select(Id).ToList();

    // The texts when battle.json has no "skills" (the shipped file says the same).
    public static readonly SkillSpec[] BuiltIn =
    {
        new() { Id = "push", NameVi = "Đẩy quái", NameEn = "Push",
            DescVi = "Đẩy quái — chọn rồi click một slime (nửa trái: đẩy sang phải, nửa phải: sang trái), choáng {pushStunSeconds} giây. Hồi {pushCooldown} giây.",
            DescEn = "Push — choose, then click a slime (left half: push right, right half: push left); stuns {pushStunSeconds} s. {pushCooldown} s cooldown." },
        new() { Id = "heal", NameVi = "Cá hồi máu", NameEn = "Healing fish",
            DescVi = "Cá hồi máu — mèo hồi {healAmount} HP. Hồi {healCooldown} giây.",
            DescEn = "Healing fish — the cat gets {healAmount} HP back. {healCooldown} s cooldown." },
        new() { Id = "cheer", NameVi = "Cổ vũ", NameEn = "Cheer",
            DescVi = "Cổ vũ — {cheerSeconds} giây mèo đánh nhanh ×{cheerSpeed}, mạnh ×{cheerDamage}. Hồi {cheerCooldown} giây.",
            DescEn = "Cheer — for {cheerSeconds} s the cat attacks ×{cheerSpeed} as often, ×{cheerDamage} as hard. {cheerCooldown} s cooldown." },
        new() { Id = "bomb", NameVi = "Bom len", NameEn = "Yarn bomb",
            DescVi = "Bom len — chọn rồi click một slime: nổ sau {bombDelay} giây, {bombDamage} sát thương cho mọi quái quanh đó. Hồi {bombCooldown} giây.",
            DescEn = "Yarn bomb — choose, then click a slime: bursts after {bombDelay} s for {bombDamage} damage to every slime nearby. {bombCooldown} s cooldown." },
        new() { Id = "shield", NameVi = "Khiên bong bóng", NameEn = "Bubble shield",
            DescVi = "Khiên bong bóng — {shieldSeconds} giây mèo không mất máu. Hồi {shieldCooldown} giây.",
            DescEn = "Bubble shield — for {shieldSeconds} s the cat takes no damage. {shieldCooldown} s cooldown." },
        new() { Id = "laser", NameVi = "Đèn laser", NameEn = "Laser pointer",
            DescVi = "Đèn laser — chọn rồi click một chỗ: {laserSeconds} giây slime gần đó chạy theo chấm đỏ, không đánh mèo; con trên chấm mất {laserDps} máu/giây. Hồi {laserCooldown} giây.",
            DescEn = "Laser pointer — choose, then click a spot: for {laserSeconds} s nearby slimes chase the red dot, not the cat; one on the dot loses {laserDps} HP/s. {laserCooldown} s cooldown." },
        new() { Id = "grabber", NameVi = "Kẹp gắp", NameEn = "Grabber",
            DescVi = "Kẹp gắp — chọn rồi nhấn giữ một slime nhỏ (không phải đá hay boss) và vung chuột để ném: trúng slime khác thì cả hai mất {grabberHitDamage} máu, con kia choáng. Hồi {grabberCooldown} giây.",
            DescEn = "Grabber — choose, then press a small slime (not a rock or a boss) and fling the mouse: thrown into another, both lose {grabberHitDamage} HP and the other is dazed. {grabberCooldown} s cooldown." },
        new() { Id = "scissors", NameVi = "Kéo cắt", NameEn = "Scissors",
            DescVi = "Kéo cắt — chọn rồi kéo chuột thành một nét: cắt đứt dây nhầy, slime nằm trên nét mất {scissorsDamage} máu. Hồi {scissorsCooldown} giây.",
            DescEn = "Scissors — choose, then drag a stroke: it cuts slime ropes, and each slime on it loses {scissorsDamage} HP. {scissorsCooldown} s cooldown." },
        new() { Id = "fan", NameVi = "Quạt", NameEn = "Fan",
            DescVi = "Quạt — chọn rồi giữ chuột: thổi slime ra xa con trỏ, va nhau hoặc va mép mất {fanBumpDamage} máu. Hơi đủ {fanSteamSeconds} giây; cạn thì chờ {fanRefillSeconds} giây.",
            DescEn = "Fan — choose, then hold the mouse: blows slimes away from the cursor; one blown into another or the edge loses {fanBumpDamage} HP. {fanSteamSeconds} s of air; empty, it refills in {fanRefillSeconds} s." },
        new() { Id = "spray", NameVi = "Bình xịt nước", NameEn = "Water spray",
            DescVi = "Bình xịt nước — chọn rồi giữ chuột: slime bị xịt ướt sũng, chậm lại, co nhỏ và mất {sprayDps} máu/giây. Nước đủ {sprayTankSeconds} giây; cạn thì chờ {sprayRefillSeconds} giây.",
            DescEn = "Water spray — choose, then hold the mouse: soaked slimes slow down, shrink and lose {sprayDps} HP/s. {sprayTankSeconds} s of water; empty, it refills in {sprayRefillSeconds} s." },
    };

    // {key}: any top-level number of battle.json (case ignored).
    private static readonly Regex Placeholder = new(@"\{([A-Za-z]+)\}");
    private static readonly Dictionary<string, System.Reflection.PropertyInfo> Numbers = typeof(BattleSettings)
        .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
        .Where(p => (p.PropertyType == typeof(double) || p.PropertyType == typeof(int)) && p.GetIndexParameters().Length == 0 &&
                    !Attribute.IsDefined(p, typeof(JsonIgnoreAttribute)))
        .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public static string Fill(string text, BattleSettings s) => Placeholder.Replace(text, m =>
        Numbers.TryGetValue(m.Groups[1].Value, out var p) ? N(Convert.ToDouble(p.GetValue(s))) : m.Value);

    // The first {key} that is not a battle.json number, or null.
    public static string? UnknownKey(string text) =>
        Placeholder.Matches(text).Select(m => m.Groups[1].Value).FirstOrDefault(key => !Numbers.ContainsKey(key));
}
