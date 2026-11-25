using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckCombatRouting()
    {
        string? expected = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            foreach (var model in new CardModel[] { ModelDb.Card<GeneticAlgorithm>(), ModelDb.Card<TheScythe>(), ModelDb.Card<ShowOff>() })
                for (var i = 0; i < 2; i++)
                    await CardPileCmd.Add(run.CreateCard(model, player), PileType.Deck);
            foreach (var defend in player.Deck.Cards.OfType<DefendIronclad>().Take(2))
                CardCmd.Enchant<Goopy>(defend, 1);
            var combat = CombatFixture.CreateActiveCombat(run);
            var pcs = player.PlayerCombatState!;
            var scope = CombatStateScope.Require(combat);
            var strikes = pcs.DrawPile.Cards.OfType<StrikeIronclad>().Cast<CardModel>().ToArray();
            var protectedGroups = pcs.DrawPile.Cards.Where(c => c is GeneticAlgorithm or TheScythe or ShowOff || c.Enchantment is Goopy).GroupBy(c => c.Id).ToArray();
            Assert(protectedGroups.Length == 4, "combat copies retain all four protected deck-link cases");
            foreach (var copies in protectedGroups)
            {
                var pair = copies.ToArray();
                Assert(pair.Length == 2 && CardOrder.CombatEquivalent(pair[0], pair[1]) && !CombatRouting.Equivalent(pair[0], pair[1]),
                    $"{copies.Key}: otherwise equal cards with distinct protected deck links stay separate");
            }
            Assert(CombatRouting.Equivalent(strikes[0], strikes[1]), "ordinary duplicate deck links permit routing");
            foreach (var card in strikes)
            {
                card.RemoveFromCurrentPile();
                pcs.Hand.AddInternal(card);
            }
            var context = new ThrowingPlayerChoiceContext();
            var selector = new TestCardSelector();
            using (CardSelectorStack.SetTestSelector(selector))
            {
                var pending = selector.SetupForAsyncCardSelection();
                var task = CardSelectCmd.FromHand(context, player, new(CardSelectorPrefs.DiscardSelectionPrompt, 0, 1), null,
                    ModelDb.Card<Prepared>());
                Assert(!task.IsCompleted && scope.Selection.Clocks.Count == 0, "opening a combat choice leaves routing clocks untouched");
                pending.SetResult([]);
                await task;
                Assert(scope.Selection.Clocks.Count == 0, "cancelling a combat choice leaves routing clocks untouched");
                var clicked = strikes[branch == 0 ? 0 : 3];
                var entries = strikes.ToDictionary(c => CombatCards.Info(c).Key, HandEntries.Info);
                selector.PrepareToSelect([clicked]);
                var handBefore = pcs.Hand.Cards.ToArray();
                var chosen = (await CardSelectCmd.FromHandForDiscard(context, player,
                    new(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, ModelDb.Card<Prepared>())).Single();
                Assert(ReferenceEquals(chosen, clicked) && pcs.Hand.Cards.SequenceEqual(handBefore),
                    "combat routing preserves the clicked physical model and hand positions");
                Assert(strikes.All(c => ReferenceEquals(HandEntries.Info(c), entries[CombatCards.Info(c).Key]) &&
                    CardOccurrences.Info(c).Address == CombatCards.Info(c).Address &&
                    CardOccurrences.Info(c.DeckVersion!).Address == CombatCards.Info(c).Address),
                    "actual combat choice transfers Oil entries, shuffle metadata and permanent deck links together");
                var actual = CombatCards.Info(chosen).Key + JsonSerializer.Serialize(scope.Selection);
                expected ??= actual;
                Assert(actual == expected, "equivalent duplicate clicks select the same logical occurrence and routing state");
                var discardScope = scope.For("combat routing", "discard", clicked.Id.Entry);
                Assert(scope.Selection.Get(scope.Seed, discardScope, CombatCards.Info(clicked).Key).Renewals == 1,
                    "a committed selection renews its logical clock exactly once");

                var unroutedHand = pcs.Hand.Cards.ToArray();
                var unroutedKeys = unroutedHand.Select(c => CombatCards.Info(c).Key).ToArray();
                var routingBefore = JsonSerializer.Serialize(scope.Selection);
                selector.PrepareToSelect([unroutedHand[^1]]);
                var unrouted = (await CardSelectCmd.FromHand(context, player,
                    new(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, null!)).Single();
                Assert(unrouted == unroutedHand[^1] && pcs.Hand.Cards.SequenceEqual(unroutedHand) &&
                    unroutedHand.Select(c => CombatCards.Info(c).Key).SequenceEqual(unroutedKeys) &&
                    JsonSerializer.Serialize(scope.Selection) == routingBefore,
                    "source-less hand selection preserves the clicked copy, hand order, logical keys and routing clocks");

                var eligible = pcs.Hand.Cards.ToArray();
                var exhaustScope = scope.For("combat routing", "exhaust", clicked.Id.Entry);
                var logicalPicks = scope.Selection.Copy().Select(scope.Seed, exhaustScope,
                    eligible.Select(c => CombatCards.Info(c).Key), 2);
                selector.PrepareToSelect([eligible[^1], eligible[0]]);
                var picked = (await CardSelectCmd.FromHand(context, player,
                    new(CardSelectorPrefs.ExhaustSelectionPrompt, 2), null, ModelDb.Card<Purity>())).ToArray();
                Assert(picked.SequenceEqual(new[] { eligible[^1], eligible[0] }) &&
                    CombatCards.Info(eligible[0]).Key == logicalPicks[0] && CombatCards.Info(eligible[^1]).Key == logicalPicks[1],
                    "batch association follows presentation order while returning the original click order");
            }

            // The real Hologram source establishes retrieval context and moves the clicked discard entry.
            foreach (var card in strikes)
            {
                card.RemoveFromCurrentPile();
                pcs.DiscardPile.AddInternal(card);
            }
            var hologram = combat.CreateCard<Hologram>(player);
            pcs.PlayPile.AddInternal(hologram);
            var discardBefore = pcs.DiscardPile.Cards.ToArray();
            var retrieved = strikes[branch == 0 ? 0 : 3];
            using (CardSelectorStack.SetTestSelector(selector))
            {
                selector.PrepareToSelect([retrieved]);
                var play = new CardPlay
                {
                    Card = hologram,
                    Player = player,
                    Target = null,
                    ResultPile = PileType.Discard,
                    Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                    IsAutoPlay = false,
                    PlayIndex = 0,
                    PlayCount = 1
                };
                await (Task)AccessTools.Method(typeof(Hologram), "OnPlay").Invoke(hologram, [context, play])!;
            }
            Assert(pcs.Hand.Cards.Contains(retrieved) && pcs.DiscardPile.Cards.SequenceEqual(discardBefore.Where(c => c != retrieved)),
                "Hologram retrieves the clicked entry and preserves the remaining discard order");

            var a = strikes[0];
            var b = strikes[1];
            var clone = a.CreateClone();
            pcs.Hand.AddInternal(clone);
            var oldDeckA = a.DeckVersion;
            var oldDeckB = b.DeckVersion;
            var historyPlay = new CardPlay
            {
                Card = a,
                Player = player,
                Target = null,
                ResultPile = PileType.Discard,
                Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                IsAutoPlay = false,
                PlayIndex = 0,
                PlayCount = 1
            };
            CombatManager.Instance.History.CardPlayStarted(combat, historyPlay);
            CombatManager.Instance.History.CardPlayFinished(combat, historyPlay);
            var power = await PowerCmd.Apply<AfterimagePower>(context, player.Creature, 1, player.Creature, null);
            await power!.BeforeCardPlayed(historyPlay);
            Assert(!CombatReferences.PendingEquivalent(a, b), "a pending per-copy effect splits otherwise equivalent cards");
            CombatReferences.Rebind(combat, [a, b], [b, a]);
            Assert(a.DeckVersion == oldDeckB && b.DeckVersion == oldDeckA && clone.CloneOf == b && historyPlay.Card == b,
                "reference rebinding transfers deck links, incoming clone links and shared history CardPlay exactly once");
            var data = AccessTools.Field(typeof(PowerModel), "_internalData").GetValue(power)!;
            var amounts = (Dictionary<CardModel, int>)AccessTools.Field(data.GetType(), "amountsForPlayedCards").GetValue(data)!;
            Assert(amounts.ContainsKey(b) && !amounts.ContainsKey(a), "pending power dictionaries follow logical occurrences");

            // Dowsing room progress sorts the selector, but has no effect on combat equivalence.
            var fresh = combat.CreateCard<Dowsing>(player);
            var progressed = combat.CreateCard<Dowsing>(player);
            progressed.RoomsEntered = 1;
            pcs.DrawPile.AddInternal(fresh);
            pcs.DrawPile.AddInternal(progressed);
            var routingScope = scope.For("combat routing", "retrieval", fresh.Id.Entry);
            var retrievalKeys = scope.Selection.Copy().Select(scope.Seed, routingScope,
                new[] { fresh, progressed }.Select(c => CombatCards.Info(c).Key), 2);
            var stratagem = (StratagemPower)ModelDb.Power<StratagemPower>().ToMutable();
            AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(stratagem, player.Creature);
            stratagem.SetAmount(2);
            using (CardSelectorStack.SetTestSelector(selector))
            {
                selector.PrepareToSelect([fresh, progressed]);
                await stratagem.AfterShuffle(context, player);
            }
            Assert(CombatCards.Info(progressed).Key == retrievalKeys[0] && CombatCards.Info(fresh).Key == retrievalKeys[1],
                "Stratagem routes by selector presentation, independently of draw-pile order and returned click order");
            if (branch == 0)
                await CheckDrawGridRouting(combat, player);

            var hopper = ModelDb.Monster<ThievingHopper>().ToMutable();
            var creature = combat.CreateCreature(hopper, CombatSide.Enemy, null);
            combat.AddCreature(creature);
            await CreatureCmd.SetMaxAndCurrentHp(creature, 100);
            Assert(CombatRouting.Protected(a), "a live Hopper protects all permanent deck links before its theft");
            RunManager.Instance.CleanUp();
        }
        await CheckCombatPlayRouting();
        await CheckPendingCombatRouting();
        await CheckFlorescenceRouting();
        await CheckWahKiRouting();
    }

    private static async Task CheckFlorescenceRouting()
    {
        foreach (var upgraded in new[] { false, true })
        {
            string? expected = null;
            for (var branch = 0; branch < 2; branch++)
            {
                var run = StartGoldRun();
                var player = run.Players[0];
                var combat = CombatFixture.CreateActiveCombat(run);
                var pcs = player.PlayerCombatState!;
                foreach (var card in pcs.Hand.Cards.ToArray()) card.RemoveFromCurrentPile();
                CardModel[] copies = [combat.CreateCard<StrikeIronclad>(player), combat.CreateCard<StrikeIronclad>(player)];
                foreach (var card in copies) pcs.Hand.AddInternal(card);
                var scope = CombatStateScope.Require(combat);
                var address = scope.For("combat routing", "transformation", copies[0].Id.Entry);
                var predicted = scope.Selection.Copy();
                var key = predicted.Select(scope.Seed, address, copies.Select(c => CombatCards.Info(c).Key), 1).Single();
                var clicked = copies[branch];
                var florescence = combat.CreateCard<Florescence>(player);
                if (upgraded) CardCmd.Upgrade(florescence);
                pcs.PlayPile.AddInternal(florescence);
                var selector = new TestCardSelector();
                selector.PrepareToSelect([clicked]);
                using (CardSelectorStack.SetTestSelector(selector))
                {
                    var play = new CardPlay
                    {
                        Card = florescence,
                        Player = player,
                        Target = null,
                        ResultPile = PileType.Discard,
                        Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                        IsAutoPlay = false,
                        PlayIndex = 0,
                        PlayCount = 1
                    };
                    await (Task)AccessTools.Method(typeof(Florescence), "OnPlay").Invoke(florescence,
                        [new ThrowingPlayerChoiceContext(), play])!;
                }
                var shot = pcs.Hand.Cards.OfType<TheShot>().Single();
                Assert(clicked.Pile == null && pcs.Hand.Cards[branch] == shot && shot.IsUpgraded == upgraded &&
                    CombatCards.Info(clicked).Key == key && JsonSerializer.Serialize(scope.Selection) == JsonSerializer.Serialize(predicted),
                    "Florescence uses transformation routing without renewing the retired input and replaces the clicked hand position");
                Assert(CombatCards.Info(shot).Address == scope.For("token occurrence", shot.Id.Entry).At(0).Key,
                    "Florescence assigns The Shot a fresh token occurrence");
                var signature = JsonSerializer.Serialize(scope.Selection) + CombatCards.Info(copies[1 - branch]).Key + CombatCards.Info(shot).Key;
                expected ??= signature;
                Assert(signature == expected, "Florescence leaves identical logical state when either equivalent copy is chosen");
                RunManager.Instance.CleanUp();
            }
        }
    }

    private static async Task CheckWahKiRouting()
    {
        string? expected = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = CombatFixture.CreateActiveCombat(run);
            var pcs = player.PlayerCombatState!;
            CardModel[] copies = [combat.CreateCard<Anger>(player), combat.CreateCard<Anger>(player)];
            foreach (var card in copies) pcs.DiscardPile.AddInternal(card);
            var wahKi = combat.CreateCard<WahKi>(player);
            pcs.PlayPile.AddInternal(wahKi);
            var scope = CombatStateScope.Require(combat);
            var address = scope.For("combat routing", "choice:" + wahKi.Id.Entry, copies[0].Id.Entry);
            var predicted = scope.Selection.Copy();
            var key = predicted.Select(scope.Seed, address, copies.Select(c => CombatCards.Info(c).Key), 1).Single();
            predicted.Renew(scope.Seed, address, key);
            var clicked = copies[branch];
            var selector = new TestCardSelector();
            selector.PrepareToSelect([clicked]);
            using (CardSelectorStack.SetTestSelector(selector))
            {
                var play = new CardPlay
                {
                    Card = wahKi,
                    Player = player,
                    Target = combat.Enemies[0],
                    ResultPile = PileType.Discard,
                    Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                    IsAutoPlay = false,
                    PlayIndex = 0,
                    PlayCount = 1
                };
                await (Task)AccessTools.Method(typeof(WahKi), "OnPlay").Invoke(wahKi,
                    [new ThrowingPlayerChoiceContext(), play])!;
            }
            Assert(pcs.DrawPile.Cards[0] == clicked && pcs.DiscardPile.Cards.SequenceEqual([copies[1 - branch]]) &&
                CombatCards.Info(clicked).Key == key && JsonSerializer.Serialize(scope.Selection) == JsonSerializer.Serialize(predicted),
                "Wah Ki routes its discard choice in its own draw-top scope and preserves the unchosen discard entry");
            var signature = CombatCards.Info(clicked).Key + JsonSerializer.Serialize(scope.Selection);
            expected ??= signature;
            Assert(signature == expected, "Wah Ki selects the same logical occurrence when either equivalent copy is clicked");
            RunManager.Instance.CleanUp();
        }
    }

    private static async Task CheckDrawGridRouting(CombatState combat, Player player)
    {
        var pile = player.PlayerCombatState!.DrawPile;
        var rows = Enumerable.Range(0, 18).Select(i => i % 2 == 0 ? (CardModel)combat.CreateCard<StrikeIronclad>(player) :
            combat.CreateCard<DefendIronclad>(player)).ToArray();
        foreach (var card in rows) pile.AddInternal(card);
        var strikes = rows.OfType<StrikeIronclad>().ToArray();
        var screen = NCombatPileCardSelectScreen.Create(pile, new(CardSelectorPrefs.ExhaustSelectionPrompt, strikes.Length), rows.Contains);
        try
        {
            var grid = screen.GetNode<NCardGrid>("%CardGrid");
            // Exercise vanilla sorting off-tree, without starting a layout animation.
            AccessTools.Field(typeof(NCardGrid), "_cardsAnimatingOutForSetCards").SetValue(grid, true);
            grid.SetCards(rows, PileType.Draw, [SortingOrders.RarityAscending, SortingOrders.AlphabetAscending]);
            var shown = grid.SortedCards.OfType<StrikeIronclad>().Cast<CardModel>().ToArray();
            Assert(!shown.SequenceEqual(strikes), "draw grid reorders tied Strikes in the presentation fixture");
            var state = CombatStateScope.Require(combat);
            var keys = state.Selection.Copy().Select(state.Seed, state.For("combat routing", "retrieval", shown[0].Id.Entry),
                shown.Select(c => CombatCards.Info(c).Key), shown.Length);
            var selection = new CombatRouting.PileSelection("retrieval", rows);
            var pending = selection.Finish(selection.SelectGrid(screen));
            var result = (TaskCompletionSource<IEnumerable<CardModel>>)AccessTools.Field(typeof(NCardGridSelectionScreen), "_completionSource").GetValue(screen)!;
            result.SetResult(shown.Reverse());
            var clicked = await pending;
            Assert(clicked.SequenceEqual(shown.Reverse()) && shown.Select(c => CombatCards.Info(c).Key).SequenceEqual(keys),
                "the real draw grid assigns routing keys in displayed order and preserves click order");
        }
        finally { screen.Free(); }
    }

    private static async Task CheckCombatPlayRouting()
    {
        string? expected = null;
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = CombatFixture.CreateActiveCombat(run);
            RunManager.Instance.ActionExecutor.Pause();
            await CombatFixture.StartTurn();
            var pcs = player.PlayerCombatState!;
            var scope = CombatStateScope.Require(combat);
            var defends = pcs.AllCards.OfType<DefendIronclad>().Cast<CardModel>()
                .OrderBy(c => CombatCards.Info(c).Address, StringComparer.Ordinal).ToArray();
            foreach (var card in defends)
            {
                card.RemoveFromCurrentPile();
                pcs.Hand.AddInternal(card);
            }
            var clicked = defends[branch == 0 ? 0 : defends.Length - 1];
            string Clocks() => JsonSerializer.Serialize(scope.Selection);
            var before = Clocks();
            var associations = defends.Select(c => CombatCards.Info(c).Key).ToArray();
            async Task Execute(PlayCardAction action) => await ((Task)AccessTools.Method(typeof(PlayCardAction), "ExecuteAction")
                .Invoke(action, null)!).WaitAsync(TimeSpan.FromSeconds(15));

            pcs.Energy = 0;
            var invalid = new PlayCardAction(clicked, null);
            await Execute(invalid);
            Assert(invalid.State == GameActionState.Canceled && clicked.Pile == pcs.Hand && Clocks() == before &&
                defends.Select(c => CombatCards.Info(c).Key).SequenceEqual(associations),
                "actual invalid PlayCardAction leaves routing clocks and logical associations untouched");
            var canceled = new PlayCardAction(clicked, null);
            canceled.OnEnqueued(_ => { }, 0);
            canceled.Cancel();
            Assert(canceled.State == GameActionState.Canceled && Clocks() == before,
                "canceling a queued player action consumes no routing opportunity");
            clicked.RemoveFromCurrentPile();
            pcs.DiscardPile.AddInternal(clicked);
            await Execute(new PlayCardAction(clicked, null));
            Assert(Clocks() == before, "a stale action for a card outside hand consumes no routing opportunity");
            clicked.RemoveFromCurrentPile();
            pcs.Hand.AddInternal(clicked);
            pcs.Energy = 3;
            var address = scope.For("combat routing", "play", clicked.Id.Entry);
            var expectedKey = scope.Selection.Copy().Select(scope.Seed, address,
                defends.Select(c => CombatCards.Info(c).Key), 1).Single();
            var handBefore = pcs.Hand.Cards.ToArray();
            var blockBefore = player.Creature.Block;
            await Execute(new PlayCardAction(clicked, null));
            Assert(clicked.Pile == pcs.DiscardPile && pcs.Energy == 2 && player.Creature.Block > blockBefore &&
                pcs.Hand.Cards.SequenceEqual(handBefore.Where(c => c != clicked)),
                "actual successful PlayCardAction spends once, resolves the clicked physical card and preserves other hand positions");
            Assert(CombatCards.Info(clicked).Key == expectedKey &&
                scope.Selection.Get(scope.Seed, address, expectedKey).Renewals == 1 &&
                CombatManager.Instance.History.CardPlaysFinished.Last().CardPlay.Card == clicked,
                "actual player play routes before spending and records its selected logical occurrence exactly once");
            var result = CombatCards.Info(clicked).Key + Clocks();
            expected ??= result;
            Assert(result == expected, "playing either equivalent copy produces identical play-routing state");

            var auto = defends.First(c => c != clicked);
            var autoKey = CombatCards.Info(auto).Key;
            before = Clocks();
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), auto, null);
            Assert(Clocks() == before && CombatCards.Info(auto).Key == autoKey,
                "an already selected auto-play target bypasses player play routing");

            var decisions = combat.CreateCard<DecisionsDecisions>(player);
            pcs.PlayPile.AddInternal(decisions);
            // Keep its draw effect real while preventing a reshuffle from adding more Defend choices.
            foreach (var card in pcs.Hand.Cards.Where(c => c is not DefendIronclad).ToArray())
            {
                card.RemoveFromCurrentPile();
                pcs.DrawPile.AddInternal(card);
            }
            var remaining = pcs.Hand.Cards.OfType<DefendIronclad>().Cast<CardModel>().ToArray();
            var decisionTarget = remaining[branch == 0 ? 0 : remaining.Length - 1];
            SelectionState? expectedState = null;
            string? decisionKey = null;
            var previousPlays = CombatManager.Instance.History.CardPlaysFinished.Count();
            using (CardSelectorStack.SetTestSelector(new PendingRoutingSelector(options =>
            {
                Assert(options.All(c => c is DefendIronclad) && options.All(c => CombatRouting.Equivalent(c, decisionTarget)),
                    "Decisions fixture offers one equivalent Defend class");
                expectedState = scope.Selection.Copy();
                decisionKey = expectedState.Select(scope.Seed, address, options.Select(c => CombatCards.Info(c).Key), 1).Single();
                expectedState.Renew(scope.Seed, address, decisionKey);
                return decisionTarget;
            })))
            {
                var play = new CardPlay
                {
                    Card = decisions,
                    Player = player,
                    Target = null,
                    ResultPile = PileType.Exhaust,
                    Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                    IsAutoPlay = false,
                    PlayIndex = 0,
                    PlayCount = 1
                };
                await ((Task)AccessTools.Method(typeof(DecisionsDecisions), "OnPlay")
                    .Invoke(decisions, [new ThrowingPlayerChoiceContext(), play])!).WaitAsync(TimeSpan.FromSeconds(15));
            }
            Assert(expectedState != null && CombatRouting.Operation(decisions) == "play" && CombatCards.Info(decisionTarget).Key == decisionKey &&
                Clocks() == JsonSerializer.Serialize(expectedState) &&
                CombatManager.Instance.History.CardPlaysFinished.Count() == previousPlays + decisions.DynamicVars.Repeat.IntValue,
                "Decisions, Decisions shares manual play clocks and its repeated auto-plays consume only the initial choice renewal");
            Assert(CombatRouting.Operation(ModelDb.Card<Recall>()) != "retrieval" &&
                CombatRouting.Operation(ModelDb.Card<ThinkingAhead>()) != CombatRouting.Operation(ModelDb.Card<Recall>()),
                "returns to draw-pile top retain source-specific choice scopes");
            before = Clocks();
            using (RunManager.Instance.CardSelectorStack.PushSelector(player, new VakuuCardSelector()))
            {
                var selected = (await CardSelectCmd.FromHand(new ThrowingPlayerChoiceContext(), player,
                    new(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, ModelDb.Card<Prepared>())).Single();
                Assert(selected == pcs.Hand.Cards[0] && Clocks() == before,
                    "Whispering Earring's scripted first-card selector bypasses player-choice routing");
            }
            RunManager.Instance.CleanUp();
        }
    }

    private sealed class PendingRoutingSelector(Func<CardModel[], CardModel> choose) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            Task.FromResult<IEnumerable<CardModel>>([choose(options.ToArray())]);
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException("Unexpected reward choice.");
    }

    private static async Task CheckPendingCombatRouting()
    {
        for (var branch = 0; branch < 2; branch++)
        {
            var run = StartGoldRun();
            var player = run.Players[0];
            var combat = CombatFixture.CreateActiveCombat(run);
            var pcs = player.PlayerCombatState!;
            var context = new ThrowingPlayerChoiceContext();
            var holograms = Enumerable.Range(0, 4).Select(_ => combat.CreateCard<Hologram>(player)).ToArray();
            foreach (var card in holograms)
            {
                CardCmd.Upgrade(card);
                pcs.Hand.AddInternal(card);
                CardCmd.ApplySingleTurnSly(card);
            }
            holograms[3].RemoveFromCurrentPile();
            pcs.DiscardPile.AddInternal(holograms[3]);
            var keys = holograms.Select(c => CombatCards.Info(c).Key).ToArray();
            var choices = 0;
            using (CardSelectorStack.SetTestSelector(new PendingRoutingSelector(options =>
            {
                choices++;
                if (choices == 1)
                {
                    Assert(options.Contains(holograms[1]) && options.Contains(holograms[2]) && options.Contains(holograms[3]),
                        "the first Sly Hologram sees both outstanding queued copies and an unqueued copy");
                    Assert(CardOrder.CombatEquivalent(holograms[1], holograms[2]) &&
                        !CombatReferences.PendingEquivalent(holograms[1], holograms[2]) &&
                        !CombatReferences.PendingEquivalent(holograms[1], holograms[3]),
                        "distinct future Sly positions and queued versus unqueued copies split routing equivalence");
                    return holograms[branch == 0 ? 1 : 2];
                }
                Assert(CombatCards.Info(holograms[1]).Key == keys[1] && CombatCards.Info(holograms[2]).Key == keys[2],
                    "nested retrieval preserves the logical targets held by later Sly iterator positions");
                if (choices == 2)
                {
                    Assert(CombatReferences.PendingEquivalent(holograms[0], holograms[3]),
                        "the resolved first Sly entry is released before the next nested choice");
                    return holograms[3];
                }
                Assert(CombatReferences.PendingEquivalent(holograms[0], holograms[1]),
                    "both completed Sly positions are released while the final entry resolves");
                return options[0];
            })))
                await CardCmd.DiscardAndDraw(context, holograms.Take(3), 0).WaitAsync(TimeSpan.FromSeconds(15));
            Assert(choices == 3 && CombatReferences.PendingEquivalent(holograms[0], holograms[1]),
                "actual Sly discard batch resolves all three choices and releases its pending frame");

            foreach (var pile in pcs.AllPiles)
                foreach (var card in pile.Cards.ToArray()) card.RemoveFromCurrentPile();
            foreach (var enemy in combat.Enemies) await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
            var attacks = Enumerable.Range(0, 4).Select(_ => combat.CreateCard<Graveblast>(player)).ToArray();
            var scope = CombatStateScope.Require(combat);
            var autoScope = scope.For("combat card selection", "auto-play");
            for (var i = 0; i < attacks.Length; i++)
            {
                CardCmd.Upgrade(attacks[i]);
                pcs.DiscardPile.AddInternal(attacks[i]);
                scope.Selection.Get(scope.Seed, autoScope, CombatCards.Info(attacks[i]).Key).Remaining = i + 1;
            }
            var attackKeys = attacks.Select(c => CombatCards.Info(c).Key).ToArray();
            var beatDown = combat.CreateCard<BeatDown>(player);
            pcs.PlayPile.AddInternal(beatDown);
            choices = 0;
            using (CardSelectorStack.SetTestSelector(new PendingRoutingSelector(options =>
            {
                choices++;
                if (choices == 1)
                {
                    Assert(options.Contains(attacks[1]) && options.Contains(attacks[2]) && options.Contains(attacks[3]) &&
                        !CombatReferences.PendingEquivalent(attacks[1], attacks[2]) &&
                        !CombatReferences.PendingEquivalent(attacks[1], attacks[3]),
                        "Beat Down's first Graveblast distinguishes both queued positions and the unsampled duplicate");
                    return attacks[branch == 0 ? 1 : 2];
                }
                Assert(CombatCards.Info(attacks[1]).Key == attackKeys[1] && CombatCards.Info(attacks[2]).Key == attackKeys[2],
                    "Graveblast retrieval preserves Beat Down's suspended attack targets");
                if (choices == 2)
                {
                    Assert(CombatReferences.PendingEquivalent(attacks[0], attacks[3]),
                        "Beat Down releases each resolved attack before the next nested choice");
                    return attacks[3];
                }
                return options[0];
            })))
            {
                var play = new CardPlay
                {
                    Card = beatDown,
                    Player = player,
                    Target = null,
                    ResultPile = PileType.Discard,
                    Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                    IsAutoPlay = false,
                    PlayIndex = 0,
                    PlayCount = 1
                };
                await ((Task)AccessTools.Method(typeof(BeatDown), "OnPlay")
                    .Invoke(beatDown, [context, play])!).WaitAsync(TimeSpan.FromSeconds(15));
            }
            Assert(choices == 3 && CombatReferences.PendingEquivalent(attacks[0], attacks[1]),
                "actual Beat Down resolves its three sampled attacks and releases the pending batch");
            RunManager.Instance.CleanUp();
        }
    }
}
