using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline.Checks;

public static partial class Checks
{
    private static async Task CheckPotionRewardPolicies()
    {
        var effects = RandomAddress.For("potion rarity", "effects", "standard");
        string? expected = null;
        foreach (var blocked in new[] { false, true })
            foreach (var brew in new[] { false, true })
            {
                RunManager.Instance.CleanUp();
                var run = StartGoldRun();
                var player = run.Players[0];
                var state = RunCheckpoints.Require(run);
                if (blocked) player.AddRelicInternal(ModelDb.Relic<Sozu>().ToMutable());
                var before = JsonSerializer.Serialize(state.Rewards.Supplies);
                var vanilla = player.PlayerRng.NonCombatGeneration.ToSerializable();
                if (brew)
                {
                    var potion = ModelDb.Potion<EntropicBrew>().ToMutable();
                    player.AddPotionInternal(potion);
                    potion.RemoveBeforeUse();
                    await (Task)AccessTools.Method(typeof(EntropicBrew), "OnUse").Invoke(potion,
                        [new ThrowingPlayerChoiceContext(), player.Creature])!;
                }
                else
                {
                    var relic = ModelDb.Relic<DelicateFrond>().ToMutable();
                    player.AddRelicInternal(relic);
                    await relic.BeforeCombatStart();
                }
                Assert(vanilla.Equals(player.PlayerRng.NonCombatGeneration.ToSerializable()) &&
                    state.Random.Position(effects) == (blocked ? 0 : player.MaxPotionCount) &&
                    state.Random.Position(RandomAddress.For("potion rarity", "generic", "standard")) == 0,
                    $"{(brew ? "Brew" : "Frond")}/{blocked}: filling consumes only successful effects-source grants");
                if (blocked)
                    Assert(!player.Potions.Any() && JsonSerializer.Serialize(state.Rewards.Supplies) == before,
                        "Sozu-blocked filling preserves potion identity supplies");
                else
                {
                    Assert(!player.HasOpenPotionSlots && player.Potions.All(p => RewardEntries.Find(p) != null),
                        "potion filling acquires addressed entries in every open slot");
                    var actual = string.Join("|", player.Potions.Select(RewardEntries.Find)) + JsonSerializer.Serialize(state.Rewards.Supplies);
                    if (expected == null) expected = actual;
                    else Assert(actual == expected, "Brew and Frond share potion identities, addresses and resulting supply state");
                }
            }

        RunManager.Instance.CleanUp();
        var combatRun = StartGoldRun();
        var owner = combatRun.Players[0];
        var combat = new CombatState(ModelDb.Encounter<AxebotsNormal>().ToMutable(), combatRun);
        combat.AddPlayer(owner);
        CombatManager.Instance.SetUpCombat(combat);
        var alchemize = combat.CreateCard<Alchemize>(owner);
        while (owner.HasOpenPotionSlots) owner.AddPotionInternal(ModelDb.Potion<BlockPotion>().ToMutable());
        var live = RunCheckpoints.Require(combatRun);
        var supplies = JsonSerializer.Serialize(live.Rewards);
        var combatRng = combatRun.Rng.CombatPotionGeneration.ToSerializable();
        Task Play() => (Task)AccessTools.Method(typeof(Alchemize), "OnPlay").Invoke(alchemize,
            [new ThrowingPlayerChoiceContext(), null])!;
        await Play();
        Assert(live.Random.Position(effects) == 0 && JsonSerializer.Serialize(live.Rewards) == supplies,
            "Alchemize with full slots preserves effects rarity and identity progression");
        owner.Potions.First().Discard();
        var existing = owner.Potions.ToArray();
        await Play();
        var acquired = owner.Potions.Except(existing).Single();
        Assert(acquired.CanBeGeneratedInCombat && RewardEntries.Find(acquired) != null &&
            live.Random.Position(effects) == 1 && combatRng.Equals(combatRun.Rng.CombatPotionGeneration.ToSerializable()),
            "successful Alchemize grants an eligible addressed potion and preserves vanilla combat RNG");
        RunManager.Instance.CleanUp();

        foreach (var room in new[] { RoomType.Monster, RoomType.Elite })
            foreach (var gap in new[] { 0.0625f, 0.1875f })
            {
                var run = StartGoldRun();
                var player = run.Players[0];
                var state = RunCheckpoints.Require(run);
                var address = RandomAddress.For("potion drop");
                var roll = (float)AddressRandom.Unit(AddressRandom.UInt64(state.Seed, address.At(0)));
                var initial = roll - gap;
                player.PlayerOdds.PotionReward.OverrideCurrentValue(initial);
                var vanilla = player.PlayerRng.Rewards.ToSerializable();
                var dropped = player.PlayerOdds.PotionReward.Roll(player, room);
                var shouldDrop = room == RoomType.Elite && gap < 0.125f;
                Assert(dropped == shouldDrop &&
                    Math.Abs(player.PlayerOdds.PotionReward.CurrentValue - (initial + (shouldDrop ? -0.1f : 0.1f))) < 0.000001f &&
                    state.Random.Position(address) == 1 && vanilla.Equals(player.PlayerRng.Rewards.ToSerializable()),
                    $"{room}/{gap}: potion drops retain the 12.5-point elite bonus, 10-point update and independent ordinal");
                RunManager.Instance.CleanUp();
            }
    }
}
