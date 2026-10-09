using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Netrun;

/// <summary>
/// C9 (docs/NOTIFICATION_SOUNDS_C9.md): the setting "Netrun notification sounds: all / only important / off". The game
/// plays a message's or letter's sound from its def; for messages and letters sent by Netrun the sound is held back when
/// the setting says so. A message or letter is Netrun's when a frame of its call stack is in this assembly (they are
/// rare, so the stack walk costs nothing that matters). Important: letters (except neutral ones) and threat or negative
/// messages, plus anything sent inside <see cref="NetrunNotify.Important"/>. Rejected input keeps its sound.
/// </summary>
public static class NetrunNotify
{
    public enum Mode
    {
        All,
        Important,
        Off,
    }

    private static readonly Assembly Own = typeof(NetrunNotify).Assembly;
    private static int _forceImportant;

    /// <summary>Messages and letters sent inside this scope count as important.</summary>
    public static ImportantScope Important()
    {
        _forceImportant++;
        return default;
    }

    public readonly struct ImportantScope : System.IDisposable
    {
        public void Dispose() => _forceImportant = System.Math.Max(0, _forceImportant - 1);
    }

    private static Mode Setting => (Mode)NetrunMod.Settings.NotifySounds;

    /// <summary>Whether a notification of this importance plays its sound now.</summary>
    public static bool Allows(bool important) => Setting switch
    {
        Mode.Off => false,
        Mode.Important => important || _forceImportant > 0,
        _ => true,
    };

    /// <summary>Netrun's own notification sounds (not the sounds of the player's actions).</summary>
    public static bool AllowsOwnSound(string id) => id switch
    {
        "UI_Alert" or "AI_Angry" or "AI_Awaken" or "AI_Whisper" => Allows(true),
        "AI_Request" or "AI_Fulfilled" or "UI_Notify" or "UI_HeatDown" or "AI_Chime" or "AI_Echo" => Allows(false),
        _ => true,
    };

    /// <summary>Sent from Netrun's code (a frame within a few levels up is in this assembly).</summary>
    internal static bool FromNetrun()
    {
        if (Setting == Mode.All)
            return false;
        var trace = new StackTrace(2, false);
        int frames = System.Math.Min(trace.FrameCount, 8);
        for (int i = 0; i < frames; i++)
            if (trace.GetFrame(i)?.GetMethod()?.DeclaringType?.Assembly == Own)
                return true;
        return false;
    }

    internal static bool ImportantMessage(MessageTypeDef def)
        => def == MessageTypeDefOf.ThreatBig || def == MessageTypeDefOf.ThreatSmall || def == MessageTypeDefOf.NegativeEvent
           || def == MessageTypeDefOf.NegativeHealthEvent;

    internal static bool ImportantLetter(LetterDef def) => def != LetterDefOf.NeutralEvent;
}

[HarmonyPatch(typeof(Messages), nameof(Messages.Message), typeof(Message), typeof(bool))]
public static class Patch_Messages_NotifySound
{
    static void Prefix(Message __0, out SoundDef __state)
    {
        __state = null;
        try
        {
            if (__0?.def?.sound == null || __0.def == MessageTypeDefOf.RejectInput || !NetrunNotify.FromNetrun())
                return;
            if (NetrunNotify.Allows(NetrunNotify.ImportantMessage(__0.def)))
                return;
            __state = __0.def.sound;
            __0.def.sound = null;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] notification sound check failed: " + e.Message, 0x4E52_4E53);
        }
    }

    static void Finalizer(Message __0, SoundDef __state)
    {
        if (__state != null && __0?.def != null)
            __0.def.sound = __state;
    }
}

[HarmonyPatch]
public static class Patch_Letters_NotifySound
{
    static MethodBase TargetMethod() => typeof(LetterStack).GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .FirstOrDefault(m => m.Name == nameof(LetterStack.ReceiveLetter) && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(Letter));

    static void Prefix(Letter __0, out SoundDef __state)
    {
        __state = null;
        try
        {
            if (__0?.def?.arriveSound == null || !NetrunNotify.FromNetrun())
                return;
            if (NetrunNotify.Allows(NetrunNotify.ImportantLetter(__0.def)))
                return;
            __state = __0.def.arriveSound;
            __0.def.arriveSound = null;
        }
        catch (System.Exception e)
        {
            Log.ErrorOnce("[Netrun] letter sound check failed: " + e.Message, 0x4E52_4C53);
        }
    }

    static void Finalizer(Letter __0, SoundDef __state)
    {
        if (__state != null && __0?.def != null)
            __0.def.arriveSound = __state;
    }
}
