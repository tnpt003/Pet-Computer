using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: upgrade cards between waves, relics and synergies.
public sealed partial class BattleSim
{
    // Up to three different cards the build can still use, one from each group (Defense, Power,
    // Speed) while a group has any left, so every build can always go on; other cards fill the
    // gaps. When the cat is hurt an instant heal takes the place of the group card the build has
    // the fewest points in. Never a card at its limit, never a heal at full HP, and no offer once
    // the stage's maxUpgrades picks are made; nothing left: no choice this time. A group's rare card
    // (RareChance) may stand in for its common one, never as a filler.
    private void OfferUpgrades()
    {
        if (Stage?.MaxUpgrades is int cap && Picks >= cap) return;
        bool Open(UpgradeSpec u) => (StackLimit(u) == 0 || Stacks(u.Id) < StackLimit(u)) && (!u.NeedsApps || AppRules != null) && Carries(u.Skill);
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
        // A utility card (no group: skills, apps, bomb, Shield) now and then takes one place.
        var utility = Settings.UpgradeList.Where(u => u.Group == null && u.Effect != "heal" && Open(u) && !cards.Contains(u)).ToList();
        if (cards.Count > 0 && utility.Count > 0 && utilityRandom.NextDouble() < Settings.UtilityChance)
            cards[utilityRandom.Next(cards.Count)] = utility[utilityRandom.Next(utility.Count)];
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
            // 1.5 M23b.
            case RelicSpec.Magnet: magnetBonus += relic.Amount; if (relic.Penalty > 0) magnetLife = magnetLife > 0 ? Math.Min(magnetLife, relic.Penalty) : relic.Penalty; break;
            case RelicSpec.Thorns: thornsShare += relic.Amount; swingCut += relic.Penalty; break;
            case RelicSpec.Venom: TakeStatusRelic(relic); swingDelay += relic.Penalty; break;
            case RelicSpec.Frost: TakeStatusRelic(relic); swingCut += relic.Penalty; break;
            case RelicSpec.Battery: batteryCut += relic.Amount; NoPotions = true; break;
            case RelicSpec.Helmet: CrushProof = true; moveBonus -= relic.Penalty; break;
        }
    }

    // 1.5 M23b: thorns' share of a slime's own blow sent back, and the skills' recharge with a battery.
    public double ThornsShare => thornsShare;
    public double BatteryCut => batteryCut;

    // The Healing fish's heal and each skill's cooldown in this battle (the relics' changes included).
    public double HealAmount => Settings.HealAmount * (1 + fishBonus);
    public double SkillCooldown(BattleSkill skill) => Math.Max(0.5, 1 - skillCut) * Math.Max(0.3, 1 - batteryCut) * skill switch
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
        // A pick still owed (a cleared boss wave in Infinity): the next offer at once.
        if (bonusPicks > 0 && !Over) { bonusPicks--; OfferUpgrades(); }
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
            case "crit": critChance += card.Amount; break;
            case "execute": executeBonus += card.Amount; break;
            case "comboWindow": comboWindowBonus += card.Amount; break;
            case "landingRush": landingRushBonus += card.Amount; break;
            case "regen": regenRate += card.Amount; break;
            case "stunCut": stunCut += card.Amount; break;
            case "skillCooldown": skillCut += card.Amount; break;
            case "appCharge": appChargeCut += card.Amount; break;
            case "killCharge": killChargeBonus += card.Amount; break;
            case "bomb": bombBonus += card.Amount; break;
            case "shieldTime": shieldBonus += card.Amount; break;
            case "onHit": TakeStatusCard(card); break;
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
            if (y.Group != card.Group || !Allowed(y) || synergies.Contains(y) || points < y.At) continue;
            synergies.Add(y);
            armorCut += y.Armor;
            if (fresh) events.Add(new(BattleEventKind.SynergyUnlocked, catX, i));
        }
    }
}
