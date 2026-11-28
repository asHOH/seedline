using System.Diagnostics;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    private static bool measuringCombatEntry;
    private static int openingCalls;
    private static double openingMs;
    private static long openingBytes;
    private static string Root => System.Environment.GetEnvironmentVariable("SEEDLINE_PERF_ROOT")!;
    private static void Emit(object value) => System.IO.File.AppendAllText(Path.Combine(Root, "measurements.jsonl"), JsonSerializer.Serialize(value) + "\n");
    public static void Initialize() => _ = Run();

    private static void BeginOpening(out (long Timestamp, long Bytes) __state)
    {
        if (!measuringCombatEntry || CombatManager.Instance.IsInProgress)
            throw new InvalidOperationException("Opening shuffle must be measured inside combat setup.");
        openingCalls++;
        var bytes = GC.GetTotalAllocatedBytes(true);
        __state = (Stopwatch.GetTimestamp(), bytes);
    }

    private static void EndOpening((long Timestamp, long Bytes) __state)
    {
        openingMs = Stopwatch.GetElapsedTime(__state.Timestamp).TotalMilliseconds;
        openingBytes = GC.GetTotalAllocatedBytes(true) - __state.Bytes;
    }

    private static async Task Run()
    {
        try
        {
            await NGame.Instance!.GameStartupComplete;
            if (!Path.GetFullPath(OS.GetUserDataDir()).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Performance tests require isolated user data.");
            if (SaveManager.Instance.SettingsSave.VolumeMaster != 0f)
                throw new InvalidOperationException("Performance tests require muted master audio.");
            var release = ReleaseInfoManager.Instance.ReleaseInfo;
            var game = Seedline.GameCompatibility.Check(release?.Version, release?.Commit,
                message => MegaCrit.Sts2.Core.Logging.Log.Warn(message));
            var mod = ModManager.GetLoadedMods().Any(m => m.manifest?.id == "Seedline" && m.errors is not { Count: > 0 });
            if (mod != (System.Environment.GetEnvironmentVariable("SEEDLINE_PERF_MODE") == "seedline"))
                throw new InvalidOperationException("Unexpected mod configuration.");
            var encode = mod ? AccessTools.Method(AccessTools.TypeByName("Seedline.RunCheckpoints"), "Encode")
                .CreateDelegate<Func<SerializableRun, byte[]>>() : null;
            var retrieve = AccessTools.Method(typeof(Hologram), "OnPlay").CreateDelegate<Func<Hologram, PlayerChoiceContext, CardPlay, Task>>();
            new Harmony("Seedline.Performance.OpeningTimer").Patch(
                AccessTools.Method(typeof(CardPile), nameof(CardPile.RandomizeOrderInternal)),
                prefix: new HarmonyMethod(typeof(Probe), nameof(BeginOpening)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(Probe), nameof(EndOpening)) { priority = Priority.Last });
            Emit(new
            {
                op = "environment",
                game,
                mod,
                runtime = System.Environment.Version.ToString(),
                os = System.Environment.OSVersion.ToString(),
                openingTiming = "within-combat-entry"
            });
            TestMode.IsOn = true;
            LocalContext.NetId = 1;
            CardModel[] identities = [ModelDb.Card<Anger>(), ModelDb.Card<Bash>(), ModelDb.Card<PommelStrike>(),
                ModelDb.Card<ShrugItOff>(), ModelDb.Card<Headbutt>(), ModelDb.Card<FlameBarrier>(),
                ModelDb.Card<Whirlwind>(), ModelDb.Card<BodySlam>(), ModelDb.Card<TrueGrit>(),
                ModelDb.Card<BurningPact>(), ModelDb.Card<IronWave>(), ModelDb.Card<BattleTrance>()];
            foreach (var size in new[] { 30, 2000 })
                foreach (var act in new[] { 0, 2 })
                    foreach (var duplicate in size == 2000 ? new[] { true } : new[] { false, true })
                        for (var iteration = -3; iteration < 10; iteration++)
                        {
                            var angerCount = 0;
                            async Task Measure(string op, Func<Task> body)
                            {
                                Emit(new { op = "begin", action = op, size, act = act + 1, duplicate, iteration });
                                var bytes = GC.GetTotalAllocatedBytes(true);
                                var start = Stopwatch.GetTimestamp();
                                await body();
                                var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                                bytes = GC.GetTotalAllocatedBytes(true) - bytes;
                                if (iteration >= 0) Emit(new { op, size, act = act + 1, duplicate, angerCount, iteration, ms, bytes });
                            }
                            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                            var run = RunState.CreateForNewRun([player], ActModel.GetDefaultList().Select(a => a.ToMutable()).ToList(),
                                [], GameMode.Standard, false, 7, "PERFORMANCE");
                            RunManager.Instance.SetUpNewSingleplayer(run, true);
                            LocalContext.NetId = RunManager.Instance.NetService.NetId;
                            var startingCount = player.Deck.Cards.Count;
                            var extra = Enumerable.Range(0, size - startingCount)
                                .Select(i => run.CreateCard(size == 2000
                                    ? identities[i < 1900 ? 0 : 1 + (i - 1900) % (identities.Length - 1)]
                                    : identities[(i + startingCount) % (duplicate ? 2 : identities.Length)], player)).ToArray();
                            await CardPileCmd.Add(extra, PileType.Deck);
                            angerCount = player.Deck.Cards.OfType<Anger>().Count();
                            if (player.Deck.Cards.Count != size || size == 2000 && angerCount != 1900)
                                throw new InvalidOperationException("Unexpected benchmark deck composition.");
                            Emit(new
                            {
                                op = "deck",
                                size,
                                act = act + 1,
                                duplicate,
                                iteration,
                                angerCount,
                                identities = player.Deck.Cards.GroupBy(c => c.Id.Entry).ToDictionary(g => g.Key, g => g.Count())
                            });
                            run.CurrentActIndex = act;
                            var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), run);
                            combat.AddPlayer(player);
                            combat.Encounter!.GenerateMonstersWithSlots(run);
                            foreach (var (monster, slot) in combat.Encounter.MonstersWithSlots)
                                combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
                            openingCalls = 0;
                            await Measure("combat-entry", () =>
                            {
                                measuringCombatEntry = true;
                                try { CombatManager.Instance.SetUpCombat(combat); }
                                finally { measuringCombatEntry = false; }
                                return Task.CompletedTask;
                            });
                            if (openingCalls != 1)
                                throw new InvalidOperationException("Combat setup must perform exactly one opening shuffle.");
                            if (iteration >= 0) Emit(new
                            {
                                op = "opening-shuffle",
                                size,
                                act = act + 1,
                                duplicate,
                                angerCount,
                                iteration,
                                ms = openingMs,
                                bytes = openingBytes,
                                openingCalls
                            });
                            var pcs = player.PlayerCombatState!;
                            if (pcs.DrawPile.Cards.Count != size || !player.IsActiveForHooks)
                                throw new InvalidOperationException("Incomplete combat fixture.");
                            if (mod && (bool)AccessTools.Method(AccessTools.TypeByName("Seedline.CombatCards"), "UseVanilla").Invoke(null, [player])!)
                                throw new InvalidOperationException("Supported deck unexpectedly uses vanilla fallback.");
                            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
                            AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
                            var context = new ThrowingPlayerChoiceContext();
                            var hand = pcs.DrawPile.Cards.Take(5).ToArray();
                            foreach (var card in hand)
                            {
                                card.RemoveFromCurrentPile();
                                pcs.Hand.AddInternal(card, silent: true);
                            }
                            var selector = new TestCardSelector();
                            using (CardSelectorStack.SetTestSelector(selector))
                            {
                                selector.PrepareToSelect([hand[0]]);
                                await Measure("hand-choice", async () =>
                                {
                                    var selected = await CardSelectCmd.FromHandForDiscard(context, player,
                                        new(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, ModelDb.Card<Prepared>());
                                    if (selected.Single() != hand[0]) throw new InvalidOperationException("Wrong hand choice.");
                                });
                                foreach (var card in hand)
                                {
                                    card.RemoveFromCurrentPile();
                                    pcs.DiscardPile.AddInternal(card, silent: true);
                                }
                                foreach (var card in pcs.DrawPile.Cards.ToArray())
                                {
                                    card.RemoveFromCurrentPile();
                                    pcs.DiscardPile.AddInternal(card, silent: true);
                                }
                                var chosen = pcs.DiscardPile.Cards[0];
                                selector.PrepareToSelect([chosen]);
                                var hologram = combat.CreateCard<Hologram>(player);
                                var play = new CardPlay
                                {
                                    Card = hologram,
                                    Player = player,
                                    Target = null,
                                    ResultPile = PileType.Exhaust,
                                    Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
                                    IsAutoPlay = false,
                                    PlayIndex = 0,
                                    PlayCount = 1
                                };
                                await Measure("pile-retrieval", () => retrieve(hologram, context, play));
                                if (pcs.Hand.Cards.Single() != chosen || pcs.DiscardPile.Cards.Count != size - 1)
                                    throw new InvalidOperationException("Retrieval did not move the chosen card.");
                            }
                            var length = 0;
                            await Measure("checkpoint-encode", () =>
                            {
                                var save = RunManager.Instance.ToSave(null);
                                length = encode != null ? encode(save).Length :
                                    JsonSerializer.SerializeToUtf8Bytes(save, JsonSerializationUtility.GetTypeInfo<SerializableRun>()).Length;
                                return Task.CompletedTask;
                            });
                            if (length == 0) throw new InvalidOperationException("Empty checkpoint.");
                            RunManager.Instance.CleanUp();
                        }
            Emit(new { op = "done" });
            ((SceneTree)Engine.GetMainLoop()).Quit();
        }
        catch (Exception error)
        {
            Emit(new { op = "error", error = error.ToString() });
            ((SceneTree)Engine.GetMainLoop()).Quit(1);
        }
    }
}
