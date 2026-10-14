using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Netrun;

/// <summary>
/// "Copy diagnostics" for bug reports (Maxim, 2026-10-14: the errors are what helps, so they come first): the errors
/// and warnings of Netrun and Hacking Expansion read from the game's own log at the moment of copying (text or stack
/// trace, repeats folded), a count of other mods' errors, versions, the state of the net, changed settings, and the
/// mod list last (for conflicts). No personal data: the Windows user name is cut out of paths; at most 12 KB.
/// </summary>
public static class NetrunDiagnostics
{
    private const int MaxOurs = 25;
    private const int MaxBytes = 12 * 1024;
    private static readonly string[] OurMarks = ["Netrun", "NR_", "HackingExpansion", "Hacking Expansion", "USH_"];

    private static bool IsOurs(string text, string stack)
    {
        foreach (string mark in OurMarks)
        {
            if ((text != null && text.IndexOf(mark, System.StringComparison.Ordinal) >= 0) || (stack != null && stack.IndexOf(mark, System.StringComparison.Ordinal) >= 0))
                return true;
        }
        return false;
    }

    /// <summary>The first stack line in our code (or Hacking Expansion's), to point at the place.</summary>
    private static string OurFrame(string stack)
    {
        if (stack.NullOrEmpty())
            return null;
        foreach (string line in stack.Split('\n'))
        {
            if (line.Contains("Netrun.") || line.Contains("USH_HE."))
                return line.Trim();
        }
        return null;
    }

    public static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hacking Expansion: Netrun diagnostics");
        sb.AppendLine("RimWorld: " + VersionControl.CurrentVersionStringWithRev);
        sb.AppendLine("Netrun: " + Version("GafarovMaxim.HackingExpansionNetrun") + ", Hacking Expansion: " + Version("Ushanka.HackingExpansion") + ", Harmony: " + Version("brrainz.harmony"));
        sb.AppendLine("DLC: " + string.Join(", ", ModsConfig.ActiveModsInLoadOrder.Where(m => m.Official && !m.IsCoreMod).Select(m => m.Name)));

        // The game's log as it is now: ours folded by text, other mods only counted.
        var ours = new Dictionary<string, int>();
        var order = new List<string>();
        int otherErrors = 0, otherWarnings = 0;
        foreach (LogMessage m in Verse.Log.Messages)
        {
            if (m.type != LogMessageType.Error && m.type != LogMessageType.Warning)
                continue;
            string stack = m.StackTrace;
            if (!IsOurs(m.text, stack))
            {
                if (m.type == LogMessageType.Error)
                    otherErrors += m.repeats;
                else
                    otherWarnings += m.repeats;
                continue;
            }
            string first = (m.text ?? string.Empty).Split('\n')[0];
            string frame = OurFrame(stack);
            string line = (m.type == LogMessageType.Error ? "[Error] " : "[Warning] ") + first + (frame == null ? string.Empty : "\n      @ " + frame);
            if (ours.TryGetValue(line, out int n))
                ours[line] = n + m.repeats;
            else
            {
                ours[line] = m.repeats;
                order.Add(line);
            }
        }
        sb.AppendLine();
        sb.AppendLine($"Netrun / Hacking Expansion errors and warnings in this session: {order.Count}");
        if (order.Count == 0)
            sb.AppendLine("  none");
        for (int i = Mathf.Max(0, order.Count - MaxOurs); i < order.Count; i++)
            sb.AppendLine("  " + order[i] + (ours[order[i]] > 1 ? $"  (x{ours[order[i]]})" : string.Empty));
        sb.AppendLine($"Other mods in the same log: {otherErrors} errors, {otherWarnings} warnings");

        if (Current.Game != null)
        {
            sb.AppendLine();
            HeatTracker heat = HeatTracker.Get();
            GatewayManager gate = GatewayManager.Get();
            NetworkGate net = NetworkGate.Get();
            sb.AppendLine($"Game: day {GenDate.DaysPassed}, Heat {heat?.Heat ?? 0f:0} ({heat?.Level}), log {heat?.Log.Count ?? 0}, network open {NetworkGate.IsOpen}, onboarding done {net?.OnboardingDone}");
            sb.AppendLine($"Fixers {FixerManager.Get()?.Fixers.Count ?? 0}, contracts {ContractManager.Get()?.Active.Count() ?? 0} active, AIs {AIManager.Get()?.AIs.Count ?? 0}, "
                          + $"nodes {gate?.Nodes.Count ?? 0}, dive reports {gate?.Reports.Count ?? 0}, headlines {gate?.Headlines.Count ?? 0}");
            sb.AppendLine("Sound: " + NetrunSound.HumState());
        }

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

        // Last, and compact: for spotting conflicts; the report is cut from here when too long.
        List<ModMetaData> mods = ModsConfig.ActiveModsInLoadOrder.ToList();
        sb.AppendLine();
        sb.AppendLine($"Active mods ({mods.Count}, load order):");
        sb.AppendLine(string.Join("; ", mods.Select(m => m.PackageIdPlayerFacing)));

        // No personal data: the user name out of any path.
        string text = sb.ToString();
        string user = System.Environment.UserName;
        if (!user.NullOrEmpty())
            text = text.Replace(user, "<user>");
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            while (Encoding.UTF8.GetByteCount(text) > MaxBytes - 40)
                text = text.Substring(0, text.Length * 9 / 10);
            text += "\n… (cut at 12 KB)";
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
