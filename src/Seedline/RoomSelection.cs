using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class RoomSelection
{
    internal static RoomType Resolve(double roll, double monster, double chest, double shop)
    {
        monster = Math.Clamp(monster, 0, 1);
        chest = Math.Clamp(chest, 0, 1 - monster);
        shop = Math.Clamp(shop, 0, 1 - monster - chest);
        var start = Math.Clamp(7d / 15 - monster / 2, chest, 1 - shop - monster);
        return roll < chest ? RoomType.Treasure : roll >= 1 - shop ? RoomType.Shop :
            roll >= start && roll < start + monster ? RoomType.Monster : RoomType.Event;
    }

    [HarmonyPatch(typeof(UnknownMapPointOdds), nameof(UnknownMapPointOdds.Roll))]
    private static class Unknown
    {
        private static bool Prefix(IEnumerable<RoomType> blacklist, IRunState runState, Dictionary<RoomType, float> ____nonEventOdds,
            Dictionary<RoomType, float> ____baseOdds, ref RoomType __result)
        {
            var allowed = Hook.ModifyUnknownMapPointRoomTypes(runState, ____nonEventOdds.Keys.Append(RoomType.Event).Except(blacklist).ToHashSet());
            if (allowed.SetEquals(new[] { RoomType.Event }))
            {
                __result = RoomType.Event;
                return false;
            }
            var state = RunCheckpoints.Require((RunState)runState);
            double Width(RoomType type) => allowed.Contains(type) ? ____nonEventOdds[type] : 0;
            __result = Resolve(state.Random.NextUnit(RandomAddress.For("room type").At(runState.CurrentActIndex)),
                Width(RoomType.Monster), Width(RoomType.Treasure), Width(RoomType.Shop));
            foreach (var (type, chance) in ____baseOdds)
                if (__result == type)
                    ____nonEventOdds[type] = chance;
                else if (allowed.Contains(type))
                    ____nonEventOdds[type] += Hook.ModifyOddsIncreaseForUnrolledRoomType(runState, type, chance);
            return false;
        }
    }

    internal static string Condition(EventModel model) => model switch
    {
        WhisperingHollow => "gold 44",
        WaterloggedScriptorium => "gold 55",
        MorphicGrove or WelcomeToWongos or CrystalSphere => "gold 100",
        EndlessConveyor => "gold 120",
        ZenWeaver => "gold 125",
        LuminousChoir => "gold 149",
        TeaMaster => "gold 150",
        TheLegendsWereTrue => "HP 10",
        SlipperyBridge => "floor over 6",
        UnrestSite or TrashHeap or PunchOff or ColossalFlower or Amalgamator or StoneOfAllTime or RoundTeaParty or GraveOfTheForgotten or
            ThermaeNonupeipe or FakeMerchant or RanwidTheElder or RelicTrader or TheFutureOfPotions => model.Id.Entry,
        _ => "always"
    };

    internal static bool Available(EventModel model, int act) => model switch
    {
        WarHistorianRepy => false,
        BrainLeech or RoomFullOfCheese or TeaMaster => act < 2,
        CrystalSphere or FakeMerchant or PotionCourier or RanwidTheElder or RelicTrader or Symbiote => act > 0,
        DollRoom or MuseumOfTorture or StoneOfAllTime or WelcomeToWongos => act == 1,
        TheLegendsWereTrue => act == 0,
        _ => true
    };

    internal static void EnterActs(RunState run)
    {
        var state = RunCheckpoints.Require(run);
        if (state.Events.Act == run.CurrentActIndex)
            return;
        var all = ModelDb.AllEvents.Where(e => e is not WarHistorianRepy &&
            !(e is ShrineOfTheWarlord && run.Acts[2].Ancient is Tanx) && !(e is ThermaeNonupeipe && run.Acts[2].Ancient is Nonupeipe));
        var orders = all.GroupBy(Condition).ToDictionary(g => g.Key, g =>
        {
            var ids = g.Select(e => e.Id.Entry).Order(StringComparer.Ordinal).ToArray();
            RelicRewards.Shuffle(ids, state.Seed, RandomAddress.For("ID order", g.Key));
            return ids;
        });
        var visited = run.VisitedEventIds.Select(id => id.Entry).ToHashSet(StringComparer.Ordinal);
        for (var act = state.Events.Act + 1; act <= run.CurrentActIndex; act++)
        {
            var available = run.Acts[act].AllEvents.Concat(ModelDb.AllSharedEvents).Where(e => Available(e, act)).Select(e => e.Id.Entry).ToHashSet(StringComparer.Ordinal);
            state.Events.Enter(state.Seed, act, orders, available, visited);
        }
    }

    internal static EventModel SelectEvent(RunState run, IReadOnlyList<EventModel> fallback)
    {
        if (run.CurrentActIndex == 2 && run.Players[0].Deck.Cards.Any(c => c is LanternKey))
            return ModelDb.Event<WarHistorianRepy>();
        EnterActs(run);
        var state = RunCheckpoints.Require(run);
        var models = ModelDb.AllEvents.ToDictionary(e => e.Id.Entry, StringComparer.Ordinal);
        var player = run.Players[0];
        bool Passes(string condition) => condition switch
        {
            "always" => true,
            "gold 44" => player.Gold >= 44,
            "gold 55" => player.Gold >= 55,
            "gold 100" => player.Gold >= 100,
            "gold 120" => player.Gold >= 120,
            "gold 125" => player.Gold >= 125,
            "gold 149" => player.Gold >= 149,
            "gold 150" => player.Gold >= 150,
            "HP 10" => player.Creature.CurrentHp >= 10,
            "floor over 6" => run.TotalFloor > 6,
            _ => models[condition].IsAllowed(run)
        };
        var selected = state.Events.Select(state.Seed, run.VisitedEventIds.Select(id => id.Entry).ToHashSet(StringComparer.Ordinal), Passes,
            id => models[id].IsAllowed(run));
        return selected == null ? fallback[(int)((state.Events.Ordinal - 1) % fallback.Count)] : models[selected];
    }

    [HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEvent))]
    private static class Events
    {
        private static bool Prefix(RunState runState, RoomSet ____rooms, ref EventModel __result)
        {
            __result = SelectEvent(runState, ____rooms.events);
            runState.AddVisitedEvent(__result);
            return false;
        }
    }
}
