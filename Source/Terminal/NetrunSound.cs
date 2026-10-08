using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Netrun;

/// <summary>
/// The mod's sounds from the chosen sound pack (docs/SOUNDS.md): one-shots by id ("UI_Click", "Dive_ICE", "AI_Awaken")
/// and one ambient loop at a time for the open terminal. Pack "Off" falls back to the game's own click and nothing else.
/// </summary>
public static class NetrunSound
{
    public static readonly string[] Packs = ["Clean", "Minimal", "Neon", "RetroDeck", "Deep"];
    public const string Off = "Off";

    private static readonly Dictionary<string, SoundDef> Cache = [];
    private static string _cachedPack;
    private static float _lastHover = -1f;
    private static float _lastType = -1f;

    private static Sustainer _ambient;
    private static string _ambientId;
    private static float _ambientVolume;
    private static int _ambientFrame;

    private static NetrunSettings Settings => NetrunMod.Settings;
    private static string Pack => Settings.SoundPack.NullOrEmpty() ? Packs[0] : Settings.SoundPack;
    public static bool Enabled => Pack != Off;

    private static SoundDef Def(string id)
    {
        if (_cachedPack != Pack)
        {
            Cache.Clear();
            _cachedPack = Pack;
        }
        if (!Cache.TryGetValue(id, out SoundDef def))
            Cache[id] = def = DefDatabase<SoundDef>.GetNamedSilentFail("NR_" + Pack + "_" + id);
        return def;
    }

    /// <summary>A one-shot from the pack, at the interface volume.</summary>
    public static void Play(string id)
    {
        if (!Enabled || Settings.UiVolume <= 0f || Def(id) is not { } def)
            return;
        SoundInfo info = SoundInfo.OnCamera();
        info.volumeFactor = Settings.UiVolume;
        def.PlayOneShot(info);
    }

    /// <summary>A button press; with sounds off, the game's own click.</summary>
    public static void Click()
    {
        if (Enabled)
            Play("UI_Click");
        else
            SoundDefOf.Click.PlayOneShotOnCamera();
    }

    /// <summary>Hover over a button or node, not more often than once per 80 ms.</summary>
    public static void Hover()
    {
        float now = Time.realtimeSinceStartup;
        // A soft tick when the mouse settles on a button, never a stream of them while it slides across a row.
        if (now - _lastHover < 0.45f)
            return;
        _lastHover = now;
        Play("UI_Hover");
    }

    /// <summary>One key tick of typed text, at most 20 a second.</summary>
    public static void Type()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _lastType < 0.05f)
            return;
        _lastType = now;
        Play("UI_Type");
    }

    /// <summary>A short glitch, only with animations on.</summary>
    public static void Glitch()
    {
        if (Settings.Animations)
            Play("UI_Glitch");
    }

    /// <summary>
    /// Keeps the ambient loop <paramref name="id"/> playing; call it every frame the window is open. A different id,
    /// pack or volume restarts the loop; when the calls stop, the loop ends by itself.
    /// </summary>
    public static void Ambient(string id)
    {
        if (Event.current.type != EventType.Repaint || _ambientFrame == Time.frameCount)
            return;
        _ambientFrame = Time.frameCount;
        float volume = Settings.AmbientVolumeOf(id);
        string key = Pack + "_" + id;
        if (_ambient != null && (_ambient.Ended || _ambientId != key || !Mathf.Approximately(_ambientVolume, volume)))
            EndAmbient();
        if (_ambient == null)
        {
            if (!Enabled || volume <= 0f || Def(id) is not { } def)
                return;
            SoundInfo info = SoundInfo.OnCamera(MaintenanceType.PerFrame);
            info.volumeFactor = volume;
            _ambient = def.TrySpawnSustainer(info);
            _ambientId = key;
            _ambientVolume = volume;
        }
        _ambient?.Maintain();
    }

    public static void EndAmbient()
    {
        if (_ambient != null && !_ambient.Ended)
            _ambient.End();
        _ambient = null;
        _ambientId = null;
    }

    private static readonly Dictionary<Thing, (Sustainer sustainer, string key)> ServerHum = [];
    private static Game _humGame;

    /// <summary>
    /// The AI server hums in the chosen pack while it has power (positional, heard near the server). Called every few
    /// seconds of game time; a new pack or volume restarts the hum.
    /// </summary>
    public static void UpdateServerHum()
    {
        if (_humGame != Current.Game)
        {
            EndServerHum();
            _humGame = Current.Game;
        }
        float hum = Settings.AmbientVolumeOf("Ambient_Server");
        string key = Enabled && hum > 0f ? Pack + "_" + hum.ToString("0.00") : null;
        foreach (Thing server in ServerHum.Keys.ToList())
        {
            (Sustainer sustainer, string playing) = ServerHum[server];
            if (sustainer.Ended || playing != key || !server.Spawned || server.TryGetComp<CompAIServer>()?.Powered != true)
            {
                if (!sustainer.Ended)
                    sustainer.End();
                ServerHum.Remove(server);
            }
        }
        if (key == null || Def("Ambient_Server") is not { } def)
            return;
        foreach (Map map in Find.Maps)
        {
            foreach (Building server in map.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer))
            {
                if (ServerHum.ContainsKey(server) || server.GetComp<CompAIServer>()?.Powered != true)
                    continue;
                SoundInfo info = SoundInfo.InMap(new TargetInfo(server), MaintenanceType.None);
                info.volumeFactor = hum;
                if (def.TrySpawnSustainer(info) is { } sustainer)
                    ServerHum[server] = (sustainer, key);
            }
        }
    }

    /// <summary>For the diagnostics: how many server hums play and at what volume.</summary>
    public static string HumState()
    {
        int playing = ServerHum.Values.Count(h => !h.sustainer.Ended);
        int servers = Find.Maps.Sum(m => m.listerBuildings.AllBuildingsColonistOfDef(NetrunDefOf.NR_AIServer).Count());
        return $"server hum: {playing} playing of {servers} servers, pack {Pack}, volume {Settings.AmbientVolumeOf("Ambient_Server"):0.00}, def {(Def("Ambient_Server") != null ? "ok" : "missing")}";
    }

    private static void EndServerHum()
    {
        foreach ((Sustainer sustainer, string _) in ServerHum.Values)
        {
            if (!sustainer.Ended)
                sustainer.End();
        }
        ServerHum.Clear();
    }

    /// <summary>The pack or a volume changed in the settings.</summary>
    public static void SettingsChanged()
    {
        Cache.Clear();
        _cachedPack = null;
        EndAmbient();
        EndServerHum();
    }
}
