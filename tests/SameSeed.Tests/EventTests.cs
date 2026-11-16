using System.Text.Json;
using Xunit;

namespace SameSeed.Tests;

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
        Assert.True(state.Conditions["A"].Roll > 2);
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
    [InlineData("{\"Act\":0,\"Ordinal\":0,\"Conditions\":{\"A\":{\"Order\":[\"a\"],\"Position\":2,\"Remaining\":0,\"Roll\":0,\"Priority\":1}}}")]
    public void RejectsMalformedSavedEvents(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => EventState.Read(document.RootElement));
    }
}
