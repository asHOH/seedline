using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Seedline;

internal static class RewardPatches
{
    internal static MethodInfo AsyncMethod(Type type, string name)
    {
        var method = AccessTools.Method(type, name);
        return AccessTools.Method(method.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
    }

    internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions,
        Func<MethodInfo, bool> match, MethodInfo replacement, int expected = 1)
    {
        var count = 0;
        foreach (var instruction in instructions)
        {
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                instruction.operand is MethodInfo method && match(method))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                count++;
            }
            yield return instruction;
        }
        if (count != expected)
            throw new InvalidOperationException($"Expected {expected} reward interception(s) for {replacement.Name}, found {count}.");
    }
}
