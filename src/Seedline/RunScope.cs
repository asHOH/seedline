using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Runs;

namespace Seedline;

internal static class RunScope
{
    private static readonly ConditionalWeakTable<IRunState, object> multiplayer = new();

    internal static bool Bypasses(GameMode mode) => mode is GameMode.Blitz or GameMode.Daily;
    internal static bool Bypasses(IRunState? run) => run != null &&
        (Bypasses(run.GameMode) || multiplayer.TryGetValue(run, out _));
    internal static bool IsBypassed => Bypasses(RunManager.Instance.DebugOnlyGetState());

    // A multiplayer lobby can contain just one player.
    internal static void Multiplayer(RunState run) => multiplayer.GetOrCreateValue(run);
}
