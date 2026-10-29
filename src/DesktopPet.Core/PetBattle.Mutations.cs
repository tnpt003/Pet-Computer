using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: Infinity's stretches and "Mutations" (1.5 M24; EndlessSpec.Eras, MutationRules in PetEndless.cs).
// After each cleared boss wave (and its cards) the user picks one of a few odd rules, shown as cards
// of effect MutationSpec.CardEffect in the usual offer; it holds until the next boss wave is cleared,
// then its reward comes (more cards, a rare card, max HP, a full heal). Losing or ending before that
// gives nothing. Its own random stream, so the other draws stay what they were.
public sealed partial class BattleSim
{
    private readonly Random mutationRandom;
    // The rule in force (null: none), the one just over (not offered again at once), how many were seen through.
    public MutationSpec? Mutation { get; private set; }
    private string? lastMutation;
    public int MutationsCleared { get; private set; }
    // A mutation offer owed after the cards of a cleared boss wave; rare-only offers owed by a reward.
    private bool mutationDue;
    private int rarePicks;
    private MutationRules? MutationRules => Endless ? Stage?.Infinity?.Mutations : null;
    public bool ChoosingMutation => UpgradeOffer is { Count: > 0 } offer && offer[0].Effect == MutationSpec.CardEffect;
    // Waves left until the rule is over (the next boss wave included; in a break, counted from the wave to come).
    public int MutationWavesLeft
    {
        get
        {
            if (Mutation == null || Stage?.Infinity is not { BossEvery: > 0 } inf) return 0;
            int next = Phase == BattlePhase.WaveBreak ? Wave + 1 : Wave;
            return inf.BossEvery - (next - 1) % inf.BossEvery;
        }
    }

    // What the rule in force changes (1 / 0 / false: nothing).
    private double MutationSpeed => Mutation is { Effect: MutationSpec.Swift } m ? 1 + m.Amount : 1;
    private double MutationElite => Mutation is { Effect: MutationSpec.Elite } m ? m.Amount : 0;
    private double MutationHurt => Mutation is { Effect: MutationSpec.Glass } m ? m.Amount : 0;
    private double MutationCooldown => Mutation is { Effect: MutationSpec.Lazy } m ? 1 + m.Amount : 1;
    private bool Hungry => Mutation is { Effect: MutationSpec.Hungry };
    private int MutationAlive => Mutation is { Effect: MutationSpec.Crowd } m ? m.Extra : 0;
    private double MutationDrops => Mutation is { Reward: MutationSpec.DropsReward } m ? m.RewardAmount : 0;
    private double MutationDamage => Mutation is { Reward: MutationSpec.DamageReward } m ? m.RewardAmount : 0;

    // A crowded wave: each drawn group Amount bigger (rounded up).
    private WaveSpec Crowded(WaveSpec wave)
    {
        if (Mutation is not { Effect: MutationSpec.Crowd, Amount: > 0 } m || wave.Groups == null) return wave;
        return new WaveSpec
        {
            Threat = wave.Threat, Budget = wave.Budget, Stream = wave.Stream,
            Groups = wave.Groups.Select(g => g.Pick == null ? g : new WaveGroup { Pick = g.Pick, AtLeast = g.AtLeast, Side = g.Side, Elite = g.Elite,
                Count = Math.Min(50, (int)Math.Ceiling(g.Count * (1 + m.Amount))) }).ToArray()
        };
    }

    // A wave of Infinity begins: the floor is empty, so the links of the waves before are forgotten
    // (each stretch may bring its own two kinds); a stretch's first wave says its name (Value: its index).
    private void EndlessWaveStarted(double catX)
    {
        if (!Endless) return;
        if (Alive == 0) linkKinds.Clear();
        if (Stage?.Infinity is EndlessSpec inf && inf.EraStarts(Wave)) events.Add(new(BattleEventKind.EraStarted, catX, inf.EraIndex(Wave)));
    }

    // A cleared boss wave: the rule in force is over and pays (its picks come after the wave's cards),
    // then a new rule is owed once the cards are chosen.
    private void MutationWaveCleared(double catX)
    {
        if (MutationRules == null || !IsBossWave(Wave)) return;
        if (Mutation is MutationSpec done)
        {
            switch (done.Reward)
            {
                case MutationSpec.MaxHpReward: maxHpBonus += done.RewardAmount; CatHp = Math.Min(CatMaxHp, CatHp + done.RewardAmount); break;
                case MutationSpec.HealReward: CatHp = CatMaxHp; break;
                case MutationSpec.RareReward: rarePicks++; break;
            }
            bonusPicks += done.Cards;
            MutationsCleared++;
            lastMutation = done.Id;
            Mutation = null;
            events.Add(new(BattleEventKind.MutationEnded, catX, Array.IndexOf(MutationRules.List, done)));
        }
        mutationDue = true;
    }

    // The next offer still owed, in order: rare cards, more cards, then the mutation. One that has
    // nothing to show is skipped.
    private void NextOffer()
    {
        while (UpgradeOffer == null && !Over)
        {
            if (rarePicks > 0) { rarePicks--; OfferRares(); continue; }
            if (bonusPicks > 0) { bonusPicks--; OfferUpgrades(); continue; }
            if (mutationDue) { mutationDue = false; OfferMutations(); }
            break;
        }
    }

    // Up to three rare cards the build can still take (none: a plain offer instead).
    private void OfferRares()
    {
        var rares = Settings.UpgradeList.Where(u => u.Rare && (StackLimit(u) == 0 || Stacks(u.Id) < StackLimit(u)) && (!u.NeedsApps || AppRules != null) && Carries(u.Skill)).ToList();
        if (rares.Count == 0) { OfferUpgrades(); return; }
        var cards = new List<UpgradeSpec>();
        while (cards.Count < 3 && rares.Count > 0)
        {
            int i = mutationRandom.Next(rares.Count);
            cards.Add(rares[i]);
            rares.RemoveAt(i);
        }
        UpgradeOffer = cards.ToArray();
        Armed = null;
        events.Add(new(BattleEventKind.UpgradeOffered, 0, UpgradeOffer.Count));
    }

    private void OfferMutations()
    {
        if (MutationRules is not MutationRules rules) return;
        var pool = rules.List.Where(m => m.Id != lastMutation).ToList();
        var cards = new List<UpgradeSpec>();
        while (cards.Count < rules.Offer && pool.Count > 0)
        {
            int i = mutationRandom.Next(pool.Count);
            cards.Add(pool[i].AsCard());
            pool.RemoveAt(i);
        }
        if (cards.Count == 0) return;
        UpgradeOffer = cards.ToArray();
        Armed = null;
        events.Add(new(BattleEventKind.UpgradeOffered, 0, UpgradeOffer.Count));
    }

    // A rule picked from the offer (ChooseUpgrade): in force from the next wave.
    private void StartMutation(string id, double catX)
    {
        if (MutationRules?.Find(id) is not MutationSpec rule) return;
        Mutation = rule;
        events.Add(new(BattleEventKind.MutationStarted, catX, Array.IndexOf(MutationRules.List, rule)));
    }
}
