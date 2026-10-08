using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "Copy diagnostics" for bug reports: versions, DLC, the mod list, settings that differ from the defaults, the
/// state of the net, and the last Netrun errors and warnings from the log. No personal data: the Windows user name is
/// cut out of paths; at most 8 KB.
/// </summary>
[StaticConstructorOnStartup]
public static class NetrunDiagnostics
{
    private const int MaxLog = 20;
    private const int MaxBytes = 8 * 1024;
    private static readonly Queue<string> Log = new();

    static NetrunDiagnostics() => Application.logMessageReceived += OnLog;

    /// <summary>Keeps the last errors and warnings that mention Netrun (a small ring, cheap to keep).</summary>
    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Log || message == null || message.IndexOf("Netrun", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;
        string first = message.Split('\n')[0];
        string where = stackTrace?.Split('\n').FirstOrDefault(l => l.Contains("Netrun")) ?? string.Empty;
        lock (Log)
        {
            Log.Enqueue($"[{type}] {first}" + (where.NullOrEmpty() ? string.Empty : " @ " + where.Trim()));
            while (Log.Count > MaxLog)
                Log.Dequeue();
        }
    }

    public static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hacking Expansion: Netrun diagnostics");
        sb.AppendLine("RimWorld: " + VersionControl.CurrentVersionStringWithRev);
        sb.AppendLine("Netrun: " + Version("GafarovMaxim.HackingExpansionNetrun") + ", Hacking Expansion: " + Version("Ushanka.HackingExpansion") + ", Harmony: " + Version("brrainz.harmony"));
        sb.AppendLine("DLC: " + string.Join(", ", ModsConfig.ActiveModsInLoadOrder.Where(m => m.Official && !m.IsCoreMod).Select(m => m.Name)));

        // Settings that differ from a fresh copy (by their saved names).
        NetrunSettings now = NetrunMod.Settings, fresh = new();
        var diffs = new List<string>();
        foreach (System.Reflection.FieldInfo f in typeof(NetrunSettings).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            object a = f.GetValue(now), b = f.GetValue(fresh);
            if (!Equals(a, b))
                diffs.Add($"{f.Name}={a}");
        }
        sb.AppendLine("Settings changed: " + (diffs.Count == 0 ? "none" : string.Join(", ", diffs)));

        if (Current.Game != null)
        {
            HeatTracker heat = HeatTracker.Get();
            GatewayManager gate = GatewayManager.Get();
            sb.AppendLine($"Game: Heat {heat?.Heat ?? 0f:0} ({heat?.Level}), log {heat?.Log.Count ?? 0}, network open {NetworkGate.IsOpen}");
            sb.AppendLine($"Fixers {FixerManager.Get()?.Fixers.Count ?? 0}, contracts {ContractManager.Get()?.Active.Count() ?? 0} active, AIs {AIManager.Get()?.AIs.Count ?? 0}, "
                          + $"nodes {gate?.Nodes.Count ?? 0}, dive reports {gate?.Reports.Count ?? 0}, headlines {gate?.Headlines.Count ?? 0}");
            sb.AppendLine("Sound: " + NetrunSound.HumState());
        }

        sb.AppendLine("Last Netrun errors and warnings:");
        lock (Log)
        {
            if (Log.Count == 0)
                sb.AppendLine("  none");
            foreach (string line in Log)
                sb.AppendLine("  " + line);
        }

        sb.AppendLine("Active mods (load order):");
        foreach (ModMetaData mod in ModsConfig.ActiveModsInLoadOrder)
            sb.AppendLine($"  {mod.Name} [{mod.PackageIdPlayerFacing}]");

        // No personal data: the user name out of any path.
        string text = sb.ToString();
        string user = System.Environment.UserName;
        if (!user.NullOrEmpty())
            text = text.Replace(user, "<user>");
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            while (Encoding.UTF8.GetByteCount(text) > MaxBytes - 40)
                text = text.Substring(0, text.Length * 9 / 10);
            text += "\n… (cut at 8 KB)";
        }
        return text;
    }

    private static string Version(string packageId)
    {
        ModMetaData mod = ModsConfig.ActiveModsInLoadOrder.FirstOrDefault(m => m.SamePackageId(packageId, ignorePostfix: true));
        return mod == null ? "not active" : mod.ModVersion.NullOrEmpty() ? "active" : mod.ModVersion;
    }

    public static void CopyToClipboard()
    {
        GUIUtility.systemCopyBuffer = Build();
        Messages.Message("NR_DiagnosticsCopied".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
    }
}
