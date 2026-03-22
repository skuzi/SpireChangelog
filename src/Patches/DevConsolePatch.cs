using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Debug;

namespace SpireChangelog.Patches;

/// <summary>
/// Register our console command right after DevConsole is created.
/// </summary>
[HarmonyPatch(typeof(NDevConsole), "_Ready")]
public static class DevConsolePatch
{
    static void Postfix()
    {
        ModEntry.TryRegisterConsoleCommands();
    }
}
