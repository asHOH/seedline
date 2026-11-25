using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    // Predict clocks directly from the policy, without reading or copying SelectionState.
    private sealed class CombatSelectionOracle(CombatStateScope scope)
    {
        private readonly Dictionary<string, (double Remaining, long Renewals)> clocks = new(StringComparer.Ordinal);
        private long incidentalOrdinal;
        private double Roll(RandomAddress address, long renewal) =>
            -Math.Log(AddressRandom.OpenUnit(AddressRandom.UInt64(scope.Seed, address.At(renewal))));

        internal CardModel[] Pick(IEnumerable<CardModel> candidates, int count, string group, bool whole = false)
        {
            var cards = candidates.ToArray();
            count = Math.Min(count, cards.Length);
            if (count <= 0) return [];
            if (cards.Length == 1) return cards;
            if (whole && count == cards.Length)
            {
                var ordered = cards.OrderBy(c => CombatCards.Info(c).Address, StringComparer.Ordinal).ToArray();
                var address = RandomAddress.For("incidental shuffle").With(scope.Address.Key);
                for (var i = ordered.Length - 1; i > 0; i--)
                {
                    var j = AddressRandom.IntInclusive(scope.Seed, address.At(incidentalOrdinal++), 0, i);
                    (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
                }
                return ordered;
            }
            var selection = RandomAddress.For("combat card selection", group).With(scope.Address.Key);
            RandomAddress Address(CardModel card) => selection.With(CombatCards.Info(card).Key);
            foreach (var card in cards)
                clocks.TryAdd(Address(card).Key, (Roll(Address(card), 0), 0));
            var picks = cards.OrderBy(c => clocks[Address(c).Key].Remaining)
                .ThenBy(c => CombatCards.Info(c).Key, StringComparer.Ordinal).Take(count).ToArray();
            var elapsed = clocks[Address(picks[^1]).Key].Remaining;
            foreach (var card in cards)
            {
                var address = Address(card);
                var previous = clocks[address.Key];
                clocks[address.Key] = picks.Contains(card)
                    ? (Roll(address, previous.Renewals + 1), previous.Renewals + 1)
                    : (previous.Remaining - elapsed, previous.Renewals);
            }
            return picks;
        }

        internal void Check(string label)
        {
            var actual = scope.Selection.Clocks.Where(p => p.Key.StartsWith("[\"combat card selection\",", StringComparison.Ordinal)).ToArray();
            Assert(actual.Length == clocks.Count && actual.All(p => clocks.TryGetValue(p.Key, out var expected) &&
                p.Value.Renewals == expected.Renewals && Math.Abs(p.Value.Remaining - expected.Remaining) < 1e-12) &&
                scope.Random.Position(RandomAddress.For("incidental shuffle").With(scope.Address.Key)) == incidentalOrdinal,
                $"{label}: exact renewal rolls, survivor subtraction, inactive clocks and incidental progression match the independent oracle");
        }
    }

    private static async Task CheckCombatSelectionPolicy()
    {
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = CombatFixture.CreateActiveCombat(run);
            await CreatureCmd.SetMaxAndCurrentHp(combat.Enemies[0], 10000);
            var pcs = player.PlayerCombatState!;
            var scope = CombatStateScope.Require(combat);
            var oracle = new CombatSelectionOracle(scope);
            var vanilla = run.Rng.CombatCardSelection.ToSerializable();
            var context = new ThrowingPlayerChoiceContext();
            void Move(CardModel card, CardPile pile)
            {
                card.RemoveFromCurrentPile();
                pile.AddInternal(card);
            }
            void Clear()
            {
                foreach (var card in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).Concat(pcs.DiscardPile.Cards).Concat(pcs.PlayPile.Cards).ToArray())
                    Move(card, pcs.ExhaustPile);
            }
            CardModel[] Add<T>(int count, CardPile pile) where T : CardModel
            {
                var cards = Enumerable.Range(0, count).Select(_ => (CardModel)combat.CreateCard<T>(player)).ToArray();
                foreach (var card in branch == 0 ? cards : cards.Reverse()) pile.AddInternal(card);
                return cards;
            }
            CardPlay Play(CardModel card) => new()
            {
                Card = card,
                Player = player,
                Target = combat.Enemies[0],
                ResultPile = PileType.Discard,
                Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1
            };
            async Task Invoke<T>(bool upgraded = false) where T : CardModel
            {
                var card = combat.CreateCard<T>(player);
                if (upgraded) CardCmd.Upgrade(card);
                pcs.PlayPile.AddInternal(card);
                await (Task)AccessTools.Method(typeof(T), "OnPlay").Invoke(card, [context, Play(card)])!;
                card.RemoveFromCurrentPile();
            }

            Clear();
            var exhaustCards = Add<StrikeIronclad>(4, pcs.Hand);
            var chosen = oracle.Pick(pcs.Hand.Cards, 1, "exhaust").Single();
            await Invoke<Cinder>();
            Assert(chosen.Pile == pcs.ExhaustPile, "Cinder selects the independently predicted initial exhaust clock");
            oracle.Check("Cinder");
            Move(chosen, pcs.Hand);
            Move(exhaustCards.First(c => c != chosen), pcs.DiscardPile);
            CardCmd.Upgrade(chosen);
            chosen = oracle.Pick(pcs.Hand.Cards, 1, "exhaust").Single();
            await Invoke<TrueGrit>();
            Assert(chosen.Pile == pcs.ExhaustPile, "True Grit continues exhaust clocks after a return, upgrade and eligibility change");
            oracle.Check("True Grit");
            foreach (var card in pcs.Hand.Cards.Skip(1).ToArray()) Move(card, pcs.DiscardPile);
            chosen = oracle.Pick(pcs.Hand.Cards, 1, "exhaust").Single();
            await Invoke<TrueGrit>();
            Assert(chosen.Pile == pcs.ExhaustPile, "a real forced-singleton exhaust retains existing selection history");
            await Invoke<TrueGrit>();
            oracle.Check("singleton and empty exhaust");

            Clear();
            var upgrades = Add<Anger>(4, pcs.DrawPile);
            var cracker = (StoneCracker)ModelDb.Relic<StoneCracker>().ToMutable();
            player.AddRelicInternal(cracker);
            var picked = oracle.Pick(upgrades, 2, "upgrade");
            await cracker.AfterRoomEntered(new CombatRoom(combat));
            Assert(upgrades.Where(c => c.IsUpgraded).ToHashSet().SetEquals(picked), "Stone Cracker samples exact upgrade clocks");
            oracle.Check("Stone Cracker");
            CardCmd.Downgrade(picked[0]);
            foreach (var card in upgrades) Move(card, pcs.DiscardPile);
            picked = oracle.Pick(upgrades.Where(c => c.IsUpgradable), 2, "upgrade");
            await Invoke<DrainPower>();
            Assert(picked.All(c => c.IsUpgraded), "Drain Power continues Stone Cracker's clocks through a filtered batch");
            oracle.Check("Drain Power");

            foreach (var card in upgrades)
            {
                if (card.IsUpgraded) CardCmd.Downgrade(card);
                Move(card, pcs.Hand);
            }
            var tune = (await PowerCmd.Apply<TunePower>(context, player.Creature, 2, player.Creature, null))!;
            async Task TriggerTune()
            {
                combat.RoundNumber++;
                pcs.IncrementTurnNumber();
                var kiPlay = Play(combat.CreateCard<KiChant>(player));
                CombatManager.Instance.History.CardPlayStarted(combat, kiPlay);
                await tune.AfterCardPlayed(context, kiPlay);
            }
            picked = oracle.Pick(upgrades, 2, "upgrade");
            await TriggerTune();
            Assert(upgrades.Where(c => c.IsUpgraded).ToHashSet().SetEquals(picked),
                "Tune shares exact upgrade clocks with Stone Cracker and Drain Power");
            oracle.Check("partial Tune");
            var laterKi = Play(combat.CreateCard<KiChant>(player));
            CombatManager.Instance.History.CardPlayStarted(combat, laterKi);
            await tune.AfterCardPlayed(context, laterKi);
            await tune.AfterCardPlayed(context, Play(combat.CreateCard<Anger>(player)));
            Assert(upgrades.Count(c => c.IsUpgraded) == 2, "Tune ignores later Ki plays and non-Ki cards");
            oracle.Check("non-triggering Tune");
            oracle.Pick(upgrades.Where(c => c.IsUpgradable), 2, "upgrade", whole: true);
            await TriggerTune();
            Assert(upgrades.All(c => c.IsUpgraded), "whole-set Tune upgrades every eligible hand card");
            oracle.Check("whole-set Tune");
            CardCmd.Downgrade(upgrades[0]);
            await TriggerTune();
            Assert(upgrades[0].IsUpgraded, "singleton Tune retains existing selection history");
            await TriggerTune();
            oracle.Check("singleton and empty Tune");
            player.Creature.RemovePowerInternal(tune);

            Clear();
            var attacks = Add<StrikeIronclad>(3, pcs.Hand);
            Add<DefendIronclad>(1, pcs.Hand);
            Add<Wound>(1, pcs.Hand);
            var first = oracle.Pick(attacks, 1, "auto-play").Single();
            var second = oracle.Pick(attacks.Where(c => c != first), 1, "auto-play").Single();
            var history = CombatManager.Instance.History.CardPlaysStarted.Count();
            var stampede = (await PowerCmd.Apply<StampedePower>(context, player.Creature, 2, player.Creature, null))!;
            await stampede.AfterAutoPostPlayPhaseEntered(context, player);
            Assert(CombatManager.Instance.History.CardPlaysStarted.Skip(history).Select(e => e.CardPlay.Card).SequenceEqual(new[] { first, second }),
                "Stampede commits sequential auto-play clocks rather than a single batch snapshot");
            oracle.Check("Stampede");
            chosen = oracle.Pick(pcs.Hand.Cards.Where(c => c is not Wound), 1, "auto-play").Single();
            var simpleton = (await PowerCmd.Apply<SimpletonPower>(context, player.Creature, 1, player.Creature, null))!;
            await simpleton.AfterAutoPostPlayPhaseEntered(context, player);
            Assert(CombatManager.Instance.History.CardPlaysStarted.Last().CardPlay.Card == chosen,
                "Simpleton shares Stampede's clocks while excluding Unplayable cards");
            oracle.Check("Simpleton");
            Add<StrikeIronclad>(2, pcs.DiscardPile);
            picked = oracle.Pick(pcs.DiscardPile.Cards.Where(c => c.Type == CardType.Attack), 3, "auto-play");
            history = CombatManager.Instance.History.CardPlaysStarted.Count();
            await Invoke<BeatDown>();
            Assert(CombatManager.Instance.History.CardPlaysStarted.Skip(history).Select(e => e.CardPlay.Card).SequenceEqual(picked),
                "Beat Down processes its snapshot in pre-selection clock order after sequential auto-plays");
            oracle.Check("Beat Down");

            Clear();
            var foolCards = Add<Anger>(3, pcs.Hand);
            Add<Wound>(2, pcs.Hand);
            first = oracle.Pick(foolCards, 1, "auto-play").Single();
            second = oracle.Pick(foolCards.Where(c => c != first), 1, "auto-play").Single();
            history = CombatManager.Instance.History.CardPlaysStarted.Count();
            await Invoke<TheFool>();
            Assert(CombatManager.Instance.History.CardPlaysStarted.Skip(history).Select(e => e.CardPlay.Card).SequenceEqual(new[] { first, second }),
                "The Fool shares sequential auto-play clocks and excludes Unplayable cards");
            oracle.Check("The Fool");
            foreach (var card in pcs.Hand.Cards.Where(c => c is not Wound).ToArray()) Move(card, pcs.ExhaustPile);
            history = CombatManager.Instance.History.CardPlaysStarted.Count();
            await Invoke<TheFool>();
            Assert(CombatManager.Instance.History.CardPlaysStarted.Count() == history && pcs.Hand.Cards.All(c => c is Wound),
                "The Fool does not fall back to auto-playing an entirely Unplayable hand");
            oracle.Check("Unplayable-only The Fool");

            Clear();
            var sadness = Add<Sadness>(5, pcs.Hand);
            chosen = oracle.Pick(sadness, 1, "Sadness conversion").Single();
            await Invoke<PunchOfJoy>();
            Assert(chosen.Pile == null && pcs.Hand.Cards.Count(c => c is Joy) == 1, "Punch of Joy transforms the exact clock-selected Sadness");
            oracle.Check("Punch of Joy");
            var ecstasy = (await PowerCmd.Apply<EcstasyPower>(context, player.Creature, 2, player.Creature, null))!;
            picked = oracle.Pick(pcs.Hand.Cards.OfType<Sadness>(), 2, "Sadness conversion");
            await ecstasy.AfterPlayerTurnStart(context, player);
            Assert(picked.All(c => c.Pile == null) && pcs.Hand.Cards.Count(c => c is Joy) == 3,
                "Ecstasy continues Punch of Joy's survivor clocks for partial conversion");
            oracle.Check("partial Ecstasy");
            oracle.Pick(pcs.Hand.Cards.OfType<Sadness>(), 2, "Sadness conversion", whole: true);
            await ecstasy.AfterPlayerTurnStart(context, player);
            Assert(pcs.Hand.Cards.All(c => c is Joy), "whole-set Ecstasy converts the remaining Sadness");
            oracle.Check("whole-set Ecstasy");

            Clear();
            sadness = Add<Sadness>(4, pcs.Hand);
            chosen = oracle.Pick(sadness, 1, "Sadness conversion").Single();
            await Invoke<Growth>(upgraded: true);
            var growth = pcs.Hand.Cards.OfType<Growth>().Single();
            Assert(chosen.Pile == null && growth.IsUpgraded &&
                CombatCards.Info(growth).Address == scope.For("card grant", ModelDb.GetId<Growth>().Entry).At(0).At(0).Key,
                "Growth converts the clock-selected Sadness into an upgraded copy with a fresh Growth-source occurrence");
            oracle.Check("Growth");
            picked = oracle.Pick(pcs.Hand.Cards.OfType<Sadness>(), 2, "Sadness conversion");
            await Invoke<PunchOfJoy>(upgraded: true);
            Assert(picked.All(c => c.Pile == null) && pcs.Hand.Cards.Count(c => c is Joy) == 2,
                "Punch of Joy+ selects both conversion targets through the shared clocks");
            oracle.Check("partial Punch of Joy+");
            await Invoke<Growth>();
            Assert(!pcs.Hand.Cards.OfType<Sadness>().Any(), "singleton Growth converts the remaining Sadness");
            await Invoke<Growth>();
            oracle.Check("singleton and empty Growth");
            Clear();
            sadness = Add<Sadness>(2, pcs.Hand);
            oracle.Pick(sadness, 2, "Sadness conversion", whole: true);
            await Invoke<PunchOfJoy>(upgraded: true);
            Assert(pcs.Hand.Cards.Count == 2 && pcs.Hand.Cards.All(c => c is Joy),
                "whole-set Punch of Joy+ transforms both Sadness");
            oracle.Check("whole-set Punch of Joy+");

            Clear();
            var returns = Add<Anger>(3, pcs.DiscardPile);
            picked = oracle.Pick(returns, 5, "return", whole: true);
            await Invoke<TheWorld>();
            Assert(pcs.Hand.Cards.SequenceEqual(picked), "The World uses exact address-ordered incidental shuffling for a fitting whole set");
            oracle.Check("The World");
            foreach (var card in returns) Move(card, pcs.DiscardPile);
            var aggression = (await PowerCmd.Apply<AggressionPower>(context, player.Creature, 3, player.Creature, null))!;
            picked = oracle.Pick(returns, 3, ModelDb.GetId<AggressionPower>().Entry, whole: true);
            await aggression.BeforeSideTurnStart(context, CombatSide.Player, [player.Creature], combat);
            Assert(pcs.Hand.Cards.SequenceEqual(picked) && returns.All(c => c.IsUpgraded),
                "Aggression shares incidental shuffle progression with other whole-set sources and retains its upgrades");
            oracle.Check("Aggression");
            Assert(vanilla.Equals(run.Rng.CombatCardSelection.ToSerializable()), "actual shared-clock sources leave vanilla selection RNG untouched");
            RunManager.Instance.CleanUp();
        }
    }
}
