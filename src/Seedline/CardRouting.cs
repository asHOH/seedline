using MegaCrit.Sts2.Core.Models;

namespace Seedline;

internal static class CardRouting
{
    internal static void Commit(IEnumerable<IEnumerable<CardModel>> groups, HashSet<CardModel> clicked,
        SelectionState clocks, string seed, Func<string, RandomAddress> scopeForIdentity, bool renew,
        Func<CardModel, string> key, Func<CardModel, string> address,
        Action<IReadOnlyList<CardModel>, IReadOnlyList<CardModel>> reassociate)
    {
        foreach (var copies in groups)
        {
            // Pair logical picks with clicked copies in captured presentation order.
            var picks = copies.Where(clicked.Contains).ToArray();
            if (picks.Length == 0)
                continue;
            var scope = scopeForIdentity(picks[0].Id.Entry);
            var byKey = copies.ToDictionary(key, StringComparer.Ordinal);
            var keys = clocks.Select(seed, scope, byKey.Keys, picks.Length);
            var chosen = keys.Select(k => byKey[k]).ToArray();
            var logical = chosen.Concat(copies.Except(chosen).OrderBy(address, StringComparer.Ordinal)).ToArray();
            var physical = picks.Concat(copies.Where(c => !clicked.Contains(c))).ToArray();
            DecisionTrace.Record("routing", () => new
            {
                seed,
                scope = scope.Key,
                renew,
                associations = physical.Select((card, i) => new { identity = card.Id.Entry, before = address(card), after = address(logical[i]), key = key(logical[i]) })
            });
            if (!physical.SequenceEqual(logical, ReferenceEqualityComparer.Instance))
                reassociate(physical, logical);
            if (renew)
                foreach (var k in keys)
                    clocks.Renew(seed, scope, k);
        }
    }
}
