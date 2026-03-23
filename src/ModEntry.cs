using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Debug;
using SpireChangelog.Commands;
using SpireChangelog.Data;

namespace SpireChangelog;

[ModInitializer("Initialize")]
public static class ModEntry
{
    /// <summary>Keep in sync with mod_manifest.json "version" field.</summary>
    public const string Version = "0.1.2";

    public static Harmony? Harmony { get; private set; }

    public static void Initialize()
    {
        Log.Info($"[SpireChangelog] Loading v{Version}...");

        try
        {
            ChangelogDatabase.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error($"[SpireChangelog] Failed to initialize database: {ex}");
        }

        Harmony = new Harmony("com.kuzyaka.spirechangelog");

        var assembly = Assembly.GetExecutingAssembly();
        foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
        {
            if (type.GetCustomAttribute<HarmonyPatch>() != null)
            {
                try
                {
                    Harmony.CreateClassProcessor(type).Patch();
                    Log.Info($"[SpireChangelog] Patched: {type.FullName}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[SpireChangelog] Failed to patch {type.FullName}: {ex}");
                }
            }
        }

        TryRegisterConsoleCommands();

        var db = ChangelogDatabase.Instance;
        if (db != null)
        {
            Log.Info($"[SpireChangelog] Loaded! Tracking {db.TotalChanges} changes across {db.PatchCount} patches.");
        }
        else
        {
            Log.Warn("[SpireChangelog] Loaded but database is empty.");
        }
    }

    private static bool _consoleRegistered;

    /// <summary>
    /// Try to register console command. Called at init and retried from patches.
    /// DevConsole may not exist at mod init time.
    /// </summary>
    public static void TryRegisterConsoleCommands()
    {
        if (_consoleRegistered) return;

        try
        {
            // Access _instance field directly (property throws if null)
            NDevConsole? instance;
            try { instance = NDevConsole.Instance; }
            catch { return; } // not created yet
            if (instance == null) return;

            var consoleField = AccessTools.Field(typeof(NDevConsole), "_devConsole");
            var devConsole = consoleField?.GetValue(instance);
            if (devConsole == null) return;

            var commandsField = AccessTools.Field(typeof(DevConsole), "_commands");
            var commands = commandsField?.GetValue(devConsole) as Dictionary<string, AbstractConsoleCmd>;
            if (commands == null) return;

            var cmd = new ChangelogCmd();
            commands[cmd.CmdName] = cmd;
            _consoleRegistered = true;
            Log.Info($"[SpireChangelog] Registered console command: {cmd.CmdName}");
        }
        catch (Exception ex)
        {
            Log.Warn($"[SpireChangelog] Console command registration deferred: {ex.Message}");
        }
    }
}
