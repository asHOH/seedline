using System.Text.Json;
using Xunit;

namespace Seedline.Tests;

public sealed class EventTests
{
    private static EventState Start(int act = 0)
    {
        var state = new EventState();
        state.Enter("seed", act, new Dictionary<string, string[]> { ["A"] = ["a", "b"], ["B"] = ["c", "d", "e"] },
            ["a", "b", "c", "d", "e"], []);
        return state;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReloadReplaysSelectionAndDeepCopiesState(int act)
    {
        var state = Start(act);
        HashSet<string> visited = [];
        visited.Add(state.Select("seed", visited, _ => true, _ => true)!);
        var frozen = state.Copy();
        var json = JsonSerializer.Serialize(state);
        using var document = JsonDocument.Parse(json);
        var loaded = EventState.Read(document.RootElement);
        Assert.Equal(state.Select("seed", visited, _ => true, _ => true), loaded.Select("seed", visited, _ => true, _ => true));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(loaded));
        Assert.Equal(json, JsonSerializer.Serialize(frozen));
    }

    [Fact]
    public void FailedConditionsKeepTheirIdsAndWrapAfterCountsAreSpent()
    {
        var state = Start();
        state.Conditions["A"].Priority = 0;
        state.Conditions["B"].Priority = 10;
        var first = state.Select("seed", [], c => c == "B", _ => true);
        Assert.Equal("c", first);
        Assert.Equal(0, state.Conditions["A"].Position);
        Assert.Equal(0, state.Conditions["A"].Remaining);
        HashSet<string> visited = ["c", "d", "e"];
        var next = state.Select("seed", visited, _ => true, _ => true);
        Assert.Equal("a", next);
        Assert.True(state.Conditions["A"].SequencePosition > 2);
    }

    [Fact]
    public void ActOneRetainsPrioritiesAndContinuesSequencesBetweenSelections()
    {
        var state = Start();
        state.Conditions["A"].Priority = 0;
        state.Conditions["A"].SequencePosition = 7;
        state.Conditions["B"].Priority = 100;
        state.Conditions["B"].SequencePosition = 11;

        Assert.Equal("a", state.Select("seed", [], _ => true, _ => true));
        Assert.Equal(8, state.Conditions["A"].SequencePosition);
        Assert.Equal(11, state.Conditions["B"].SequencePosition);
        Assert.Equal(100, state.Conditions["B"].Priority);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void LaterActsRefreshPrioritiesAndSequencesBetweenSelections(int act)
    {
        var state = Start(act);
        HashSet<string> visited = [state.Select("seed", [], _ => true, _ => true)!];
        var expected = state.Copy();
        foreach (var condition in state.Conditions.Values)
        {
            condition.Priority = 100;
            condition.SequencePosition = 100;
        }

        Assert.Equal(expected.Select("seed", visited, _ => true, _ => true), state.Select("seed", visited, _ => true, _ => true));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SameSelectedEventLeavesSameStateAcrossConditionChanges(int act)
    {
        var orders = new Dictionary<string, string[]> { ["A"] = ["a", "b"], ["B"] = ["c", "d", "e"], ["C"] = ["f", "g"] };
        for (var seed = 0; seed < 100; seed++)
        {
            var text = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var initial = new EventState();
            initial.Enter(text, act, orders, ["a", "b", "c", "d", "e", "f", "g"], []);
            var results = new Dictionary<string, string>();
            for (var mask = 1; mask < 8; mask++)
            {
                var state = initial.Copy();
                bool Passes(string key) => (mask & (1 << (key[0] - 'A'))) != 0;
                var selected = state.Select(text, [], Passes, id => orders.Any(p => Passes(p.Key) && p.Value.Contains(id)))!;
                var serialized = JsonSerializer.Serialize(state);
                if (results.TryGetValue(selected, out var previous))
                    Assert.Equal(previous, serialized);
                else
                    results.Add(selected, serialized);
            }
        }
    }

    [Fact]
    public void LateFailuresAdvanceIdsAndFallbackTerminates()
    {
        var state = Start();
        state.Conditions["A"].Priority = 0;
        state.Conditions["B"].Priority = 100;
        Assert.Equal("b", state.Select("seed", [], _ => true, id => id != "a"));
        Assert.Equal(2, state.Conditions["A"].Position);
        Assert.Null(state.Select("seed", [], _ => false, _ => false));
        Assert.Equal(2, state.Ordinal);
    }

    [Fact]
    public void ActTransitionKeepsSurvivorOrderAndInsertsIncomingInTheirInitialOrder()
    {
        var state = new EventState();
        var initial = new Dictionary<string, string[]> { ["same"] = ["A", "B", "C", "D", "E"] };
        state.Enter("seed", 0, initial, ["A", "B", "C"], []);
        state.Conditions["same"].Position = 2;
        state.Enter("seed", 1, initial, ["A", "B", "C", "D", "E"], ["B"]);
        var order = state.Conditions["same"].Order;
        Assert.Equal(["A", "C"], order.Where(id => id is "A" or "C"));
        Assert.Equal(["D", "E"], order.Where(id => id is "D" or "E"));
        Assert.Equal(4, state.Conditions["same"].Remaining);
        Assert.Equal(0, state.Conditions["same"].Position);
    }

    [Fact]
    public void ConditionRacePreservesCountWeightedFirstChoice()
    {
        var a = 0;
        for (var seed = 0; seed < 4000; seed++)
        {
            var state = new EventState();
            var text = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            state.Enter(text, 0, new Dictionary<string, string[]> { ["A"] = ["a", "b"], ["B"] = ["c", "d", "e"] }, ["a", "b", "c", "d", "e"], []);
            if (state.Select(text, [], _ => true, _ => true) == "a")
                a++;
        }
        Assert.InRange(a, 1480, 1720);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Act\":0,\"Ordinal\":0,\"Conditions\":null}")]
    [InlineData("{\"Act\":0,\"Ordinal\":0,\"Conditions\":{\"A\":{\"Order\":[\"a\"],\"Position\":2,\"Remaining\":0,\"SequencePosition\":0,\"Priority\":1}}}")]
    public void RejectsMalformedSavedEvents(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => EventState.Read(document.RootElement));
    }
}
