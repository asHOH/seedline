using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private sealed class SeekerChoice : ICardSelector
    {
        internal CardModel[] Options = [];
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Options = options.ToArray();
            return Task.FromResult<IEnumerable<CardModel>>(Options.Take(1));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException("Unexpected reward selection.");
    }

    private static async Task CheckCombatSelection()
    {
        string? expected = null;
        string? expectedTheft = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
            combat.AddPlayer(player);
            var hopper = ModelDb.Monster<ThievingHopper>().ToMutable();
            combat.AddCreature(combat.CreateCreature(hopper, CombatSide.Enemy, null));
            CombatManager.Instance.SetUpCombat(combat);
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
            await CreatureCmd.SetMaxAndCurrentHp(combat.Enemies[0], 1000);
            var scope = CombatStateScope.Require(combat);
            var pcs = player.PlayerCombatState!;
            var context = new ThrowingPlayerChoiceContext();
            var vanilla = run.Rng.CombatCardSelection.ToSerializable();
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
            async Task Invoke(CardModel card)
            {
                pcs.PlayPile.AddInternal(card);
                await (Task)AccessTools.Method(card.GetType(), "OnPlay").Invoke(card, [context, Play(card)])!;
            }
            void Move(CardModel card, CardPile pile)
            {
                card.RemoveFromCurrentPile();
                pile.AddInternal(card);
            }
            var cards = pcs.DrawPile.Cards.Take(4).ToArray();
            foreach (var card in branch == 0 ? cards : cards.Reverse())
                Move(card, pcs.Hand);
            var exhaust = scope.For("combat card selection", "exhaust");
            var ordered = cards.OrderBy(c => scope.Selection.Get(scope.Seed, exhaust, CombatCards.Info(c).Key).Remaining)
                .ThenBy(c => CombatCards.Info(c).Key, StringComparer.Ordinal).ToArray();
            if (branch == 1)
                CombatSelection.Select(cards.Reverse(), 1, "unrelated check");
            await Invoke(combat.CreateCard<Cinder>(player));
            Assert(pcs.ExhaustPile.Cards.Contains(ordered[0]), "Cinder consumes the first exhaust clock through its actual source patch");
            await Invoke(combat.CreateCard<TrueGrit>(player));
            var signature = string.Join("|", pcs.ExhaustPile.Cards.Select(c => CombatCards.Info(c).Key));
            expected ??= signature;
            Assert(signature == expected && cards.Sum(c => scope.Selection.Get(scope.Seed, exhaust, CombatCards.Info(c).Key).Renewals) == 2,
                "Cinder and True Grit share progression, ignore hand order and preserve unrelated scopes");
            var snapshot = JsonSerializer.Serialize(scope.Selection);
            Assert(CombatSelection.Select([], 3, "exhaust").Count == 0 &&
                CombatSelection.Select([cards[0]], 1, "exhaust").Single() == cards[0] && snapshot == JsonSerializer.Serialize(scope.Selection),
                "empty and singleton combat selections preserve all clock state");
            var filter = cards.Take(3).ToArray();
            var replay = scope.Selection.Copy();
            var replayKeys = replay.Select(scope.Seed, exhaust, filter.Select(c => CombatCards.Info(c).Key), 2);
            foreach (var key in replayKeys)
                replay.Renew(scope.Seed, exhaust, key);
            Assert(CombatSelection.Select(filter.Reverse(), 2, "exhaust").Select(c => CombatCards.Info(c).Key).SequenceEqual(replayKeys) &&
                JsonSerializer.Serialize(scope.Selection) == JsonSerializer.Serialize(replay),
                "filtered batch selection replays in pre-selection clock order and renews immediately");

            foreach (var card in pcs.Hand.Cards.ToArray())
                Move(card, pcs.ExhaustPile);
            foreach (var card in pcs.DiscardPile.Cards.ToArray())
                Move(card, pcs.ExhaustPile);
            var upgradeCards = new[] { combat.CreateCard<Anger>(player), combat.CreateCard<Anger>(player) };
            foreach (var card in upgradeCards)
                pcs.DiscardPile.AddInternal(card);
            snapshot = JsonSerializer.Serialize(scope.Selection);
            var incidental = scope.Random.Position(scope.For("incidental shuffle"));
            await Invoke(combat.CreateCard<DrainPower>(player));
            Assert(upgradeCards.All(c => c.IsUpgraded) && snapshot == JsonSerializer.Serialize(scope.Selection) &&
                scope.Random.Position(scope.For("incidental shuffle")) == incidental + 1,
                "Drain Power whole-set upgrades preserve clocks and use the shared incidental shuffle");
            await Invoke(combat.CreateCard<TheWorld>(player));
            Assert(upgradeCards.All(c => pcs.Hand.Cards.Contains(c)) && snapshot == JsonSerializer.Serialize(scope.Selection),
                "The World whole-set returns preserve clocks when the selected batch fits");
            foreach (var card in pcs.Hand.Cards.ToArray())
                Move(card, pcs.ExhaustPile);

            // Only the zero-cost cards belong to Power Cell's first tier.
            var zeroes = Enumerable.Range(0, 3).Select(_ => combat.CreateCard<Anger>(player)).ToArray();
            foreach (var card in zeroes)
                pcs.DrawPile.AddInternal(card);
            var cell = (PowerCell)ModelDb.Relic<PowerCell>().ToMutable();
            player.AddRelicInternal(cell);
            await cell.BeforeSideTurnStart(context, CombatSide.Player, [player.Creature], combat);
            var returns = scope.For("combat card selection", "return");
            Assert(pcs.Hand.Cards.Count == 2 && pcs.Hand.Cards.All(zeroes.Contains) &&
                zeroes.Sum(c => scope.Selection.Get(scope.Seed, returns, CombatCards.Info(c).Key).Renewals) == 2,
                "Power Cell samples its eligible first tier and commits exactly two return clocks");

            foreach (var card in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).ToArray())
                Move(card, pcs.ExhaustPile);
            var ordinaryZero = combat.CreateCard<Anger>(player);
            var innateZeroes = Enumerable.Range(0, 3).Select(_ => combat.CreateCard<Anger>(player)).ToArray();
            pcs.DrawPile.AddInternal(ordinaryZero);
            foreach (var card in innateZeroes)
            {
                card.AddKeyword(CardKeyword.Innate);
                pcs.DrawPile.AddInternal(card);
            }
            snapshot = JsonSerializer.Serialize(scope.Selection);
            incidental = scope.Random.Position(scope.For("incidental shuffle"));
            await cell.BeforeSideTurnStart(context, CombatSide.Player, [player.Creature], combat);
            Assert(pcs.Hand.Cards.Count == 2 && pcs.Hand.Cards[0] == ordinaryZero && innateZeroes.Contains(pcs.Hand.Cards[1]) &&
                snapshot == JsonSerializer.Serialize(scope.Selection) && scope.Random.Position(scope.For("incidental shuffle")) == incidental + 2,
                "Power Cell fills its remaining slot from the shuffled Innate tier while preserving both tiers' clocks");

            foreach (var card in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).ToArray())
                Move(card, pcs.ExhaustPile);
            for (var i = 0; i < CardPile.MaxCardsInHand - 1; i++)
                pcs.Hand.AddInternal(combat.CreateCard<Wound>(player));
            var crowded = new[] { combat.CreateCard<Anger>(player), combat.CreateCard<Anger>(player) };
            foreach (var card in crowded)
                pcs.DrawPile.AddInternal(card);
            incidental = scope.Random.Position(scope.For("incidental shuffle"));
            await cell.BeforeSideTurnStart(context, CombatSide.Player, [player.Creature], combat);
            Assert(crowded.Count(c => c.Pile == pcs.Hand) == 1 && crowded.Count(c => c.Pile == pcs.DiscardPile) == 1 &&
                crowded.All(c => scope.Selection.Get(scope.Seed, returns, CombatCards.Info(c).Key).Renewals == 1) &&
                scope.Random.Position(scope.For("incidental shuffle")) == incidental,
                "Power Cell consumes whole-set return clocks when hand space redirects part of the batch");

            foreach (var card in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).ToArray())
                Move(card, pcs.ExhaustPile);
            CardModel[] offered = [combat.CreateCard<Anger>(player), combat.CreateCard<Headbutt>(player),
                combat.CreateCard<TrueGrit>(player), combat.CreateCard<Bash>(player), combat.CreateCard<DefendIronclad>(player)];
            foreach (var card in offered)
                pcs.DrawPile.AddInternal(card);
            var seekerScope = scope.For("combat card selection", ModelDb.GetId<SeekerStrike>().Entry);
            // Force clock membership to differ from pile/display order.
            for (var i = 0; i < offered.Length; i++)
                scope.Selection.Get(scope.Seed, seekerScope, CombatCards.Info(offered[i]).Key).Remaining = offered.Length - i;
            var seekerChoice = new SeekerChoice();
            using (CardSelectorStack.SetTestSelector(seekerChoice))
                await Invoke(combat.CreateCard<SeekerStrike>(player));
            var displayed = offered.Skip(2).OrderBy(c => c.Rarity).ThenBy(c => c).ToArray();
            Assert(seekerChoice.Options.SequenceEqual(displayed) && displayed[0].Pile == pcs.Hand &&
                offered.Skip(2).All(c => scope.Selection.Get(scope.Seed, seekerScope, CombatCards.Info(c).Key).Renewals == 1),
                "Seeker Strike uses clock-selected offer membership in vanilla rarity/identity display order and returns the chosen card");
            foreach (var card in pcs.DrawPile.Cards.ToArray())
                Move(card, pcs.ExhaustPile);
            foreach (var card in offered.Take(2))
                Move(card, pcs.DrawPile);
            string SeekerClocks() => JsonSerializer.Serialize(scope.Selection.Clocks.Where(p =>
                p.Key.StartsWith(seekerScope.Key[..^1] + ",", StringComparison.Ordinal)));
            snapshot = SeekerClocks();
            incidental = scope.Random.Position(scope.For("incidental shuffle"));
            using (CardSelectorStack.SetTestSelector(seekerChoice))
                await Invoke(combat.CreateCard<SeekerStrike>(player));
            Assert(seekerChoice.Options.SequenceEqual(offered.Take(2)) && snapshot == SeekerClocks() &&
                incidental == scope.Random.Position(scope.For("incidental shuffle")),
                "Seeker Strike's whole-set offer preserves clocks and display order without incidental shuffling");

            // Theft retains the linked permanent-deck key even if combat keys differ.
            foreach (var card in pcs.Hand.Cards.ToArray())
                Move(card, pcs.ExhaustPile);
            foreach (var card in pcs.DrawPile.Cards.Concat(pcs.DiscardPile.Cards).Where(c => c.DeckVersion == null).ToArray())
                Move(card, pcs.ExhaustPile);
            foreach (var card in pcs.ExhaustPile.Cards.Where(c => c.DeckVersion != null).ToArray())
                Move(card, pcs.DrawPile);
            if (branch == 1)
                foreach (var card in pcs.DrawPile.Cards.ToArray())
                    pcs.DrawPile.MoveToTopInternal(card);
            var beforeDeck = player.Deck.Cards.ToArray();
            var linked = pcs.DrawPile.Cards.OfType<StrikeIronclad>().ToArray();
            Assert(!CombatRouting.Equivalent(linked[0], linked[1]), "a pending Hopper theft splits equivalent Strikes by deck link");
            await (Task)AccessTools.Method(typeof(ThievingHopper), "ThieveryMove").Invoke(hopper, [new Creature[] { player.Creature }])!;
            var survivors = linked.Where(c => c.Pile == pcs.DrawPile).Take(2).ToArray();
            Assert(CombatRouting.Equivalent(survivors[0], survivors[1]), "after Hopper steals, surviving ordinary deck links route together");
            var stolen = beforeDeck.Single(c => !player.Deck.Cards.Contains(c));
            var theft = CardOccurrences.Info(stolen).Key;
            expectedTheft ??= theft;
            Assert(theft == expectedTheft && vanilla.Equals(run.Rng.CombatCardSelection.ToSerializable()),
                "Hopper theft is stable under pile order and all covered sources preserve vanilla selection RNG");
            RunManager.Instance.CleanUp();
        }
    }
}
