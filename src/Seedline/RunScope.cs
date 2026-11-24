using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Seedline;

internal static class RunScope
{
    private static readonly ConditionalWeakTable<IRunState, object> multiplayer = new();
    private static readonly HashSet<ModelId> supportedModifiers =
    [
        ModelDb.GetId<Flight>(), ModelDb.GetId<Murderous>(), ModelDb.GetId<Terminal>(),
        ModelDb.GetId<NightTerrors>(), ModelDb.GetId<Midas>(), ModelDb.GetId<BigGameHunter>(),
        ModelDb.GetId<CharacterCards>(), ModelDb.GetId<Hoarder>(), ModelDb.GetId<Draft>()
    ];

    internal static bool Supports(ModelId? modifier) => modifier != null && supportedModifiers.Contains(modifier);
    internal static bool Bypasses(GameMode mode) => mode is GameMode.Blitz or GameMode.Daily;
    internal static bool Bypasses(IRunState? run) => run != null &&
        (Bypasses(run.GameMode) || multiplayer.TryGetValue(run, out _) || run.Modifiers.Any(m => !Supports(m.Id)));
    // Captured checkpoints may contain save-only carriers consumed before live run setup.
    internal static bool Bypasses(SerializableRun save) => Bypasses(save.GameMode) ||
        RunCheckpoints.Find(save) == null && save.Modifiers.Any(m => !Supports(m.Id));
    internal static bool IsBypassed => Bypasses(RunManager.Instance.DebugOnlyGetState());

    // A multiplayer lobby can contain just one player.
    internal static void Multiplayer(RunState run) => multiplayer.GetOrCreateValue(run);
}
