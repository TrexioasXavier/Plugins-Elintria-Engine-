// ─────────────────────────────────────────────────────────────────────────────
//  BspBehaviours - what each Half-Life entity class does in Play mode
//
//  A behaviour is made from the entity's classname when Play starts (Create
//  below) and lives inside its BspEntity: triggers fire when touched, relays
//  pass a trigger on, a multi_manager fires a list on a timetable, sounds play
//  and stop, items are picked up. Movers (doors, buttons, trains) are in
//  BspMovers.cs, monsters and scripted sequences in BspNpc.cs, effects in
//  BspEffects.cs.
//
//  Times and distances are the map's own (seconds, Half-Life units).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Elintria;

public abstract class BspBehaviour
{
    public BspEntity e;
    protected BspWorld World => BspWorld.Instance;
    protected float Now => Time.time;

    public virtual void Start() { }
    /// <summary>Every entity has started and registered.</summary>
    public virtual void MapStarted() { }
    public virtual void Update(float dt) { }
    public virtual void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) { }
    /// <summary>The player (or a monster) is inside this entity's volume.</summary>
    public virtual void Touch(BspEntity other) { }
    public virtual void OnKilled() { }
    /// <summary>Blocks movement (hull traces collide with it).</summary>
    public virtual bool Solid => false;
    /// <summary>Its volume is checked for touches each frame.</summary>
    public virtual bool Touchable => false;
    /// <summary>A monster touching it counts (trigger "Monsters" flag).</summary>
    public virtual bool MonstersTouch => false;
    public virtual bool PlayersTouch => true;
    /// <summary>The player can press E on it.</summary>
    public virtual bool Usable => false;

    // ── Brush volume ─────────────────────────────────────────────────────────
    /// <summary>The entity's box in Half-Life world units, now.</summary>
    public bool Bounds(out Vector3 mins, out Vector3 maxs)
    {
        mins = maxs = Vector3.zero;
        var w = World;
        if (w == null || w.collision == null || e.brushModel <= 0 || e.brushModel >= w.collision.models.Length) return false;
        BspCollision.Model m = w.collision.models[e.brushModel];
        Vector3 offset;
        if (e.hasOriginKey) offset = e.HlPosition;
        else offset = Hl.ToHl(e.transform.position) - Hl.ToHl(e.startPosition);
        mins = m.mins + offset;
        maxs = m.maxs + offset;
        if (Quaternion.Dot(e.transform.rotation, e.startRotation) < 0.9999f && Quaternion.Dot(e.transform.rotation, e.startRotation) > -0.9999f)
        {
            // Turned: the box that holds it at any angle.
            Vector3 c = e.hasOriginKey ? e.HlPosition : (mins + maxs) * 0.5f;
            float r = Mathf.Max((m.maxs - m.mins).magnitude * 0.5f, Mathf.Max(m.maxs.magnitude, m.mins.magnitude));
            mins = c - new Vector3(r, r, r);
            maxs = c + new Vector3(r, r, r);
        }
        return true;
    }

    public bool Overlaps(Vector3 mins, Vector3 maxs)
    {
        Vector3 a, b;
        if (!Bounds(out a, out b)) return false;
        return mins.x <= b.x && maxs.x >= a.x && mins.y <= b.y && maxs.y >= a.y && mins.z <= b.z && maxs.z >= a.z;
    }

    /// <summary>Contents this entity gives a point (water, ladder), or empty.</summary>
    public virtual int ContentsAt(Vector3 hl) => BspCollision.ContentsEmpty;

    protected bool Inside(Vector3 hl)
    {
        Vector3 a, b;
        if (!Bounds(out a, out b)) return false;
        return hl.x >= a.x && hl.x <= b.x && hl.y >= a.y && hl.y <= b.y && hl.z >= a.z && hl.z <= b.z;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    protected void Sound(string hlPath, float volume = 1f)
    {
        BspSound.PlayAt(hlPath, e.transform != null ? e.transform.position : Vector3.zero, volume);
    }

    protected void SetVisible(bool on)
    {
        if (e.gameObject != null && e.gameObject.activeSelf != on) e.gameObject.SetActive(on);
    }

    // ── The factory ──────────────────────────────────────────────────────────
    public static BspBehaviour Create(BspEntity e)
    {
        string c = (e.className ?? string.Empty).ToLowerInvariant();
        BspBehaviour b = null;
        switch (c)
        {
            case "trigger_once": b = new BspTrigger(true); break;
            case "trigger_multiple": b = new BspTrigger(false); break;
            case "trigger_counter": b = new BspTriggerCounter(); break;
            case "trigger_changelevel": b = new BspChangeLevel(); break;
            case "trigger_teleport": b = new BspTeleport(); break;
            case "trigger_push": b = new BspPush(); break;
            case "trigger_hurt": b = new BspHurt(); break;
            case "trigger_gravity": b = new BspGravity(); break;
            case "trigger_autosave": case "trigger_endsection": case "trigger_cdaudio": case "trigger_transition":
            case "trigger_monsterjump": case "func_bomb_target": case "func_buyzone": case "func_hostage_rescue":
            case "func_vip_safetyzone": case "func_escapezone": case "func_mortar_field": case "func_tankcontrols":
            case "func_traincontrols":
                b = new BspPassiveVolume(); break;
            case "trigger_relay": b = new BspRelay(); break;
            case "trigger_auto": b = new BspAuto(); break;
            case "multi_manager": b = new BspMultiManager(); break;
            case "multisource": b = new BspMultisource(); break;
            case "env_global": b = new BspGlobal(); break;
            case "trigger_changetarget": b = new BspChangeTarget(); break;
            case "env_render": b = new BspRender(); break;
            case "game_text": case "env_message": b = new BspMessage(); break;
            case "env_shake": b = new BspShake(); break;
            case "env_fade": b = new BspFade(); break;
            case "ambient_generic": b = new BspAmbient(); break;
            case "speaker": b = new BspSpeaker(); break;
            case "light": case "light_spot": b = new BspLightSwitch(); break;

            case "func_door": case "func_water": b = new BspDoor(false); break;
            case "func_door_rotating": b = new BspDoor(true); break;
            case "momentary_door": b = new BspDoor(false, true); break;
            case "func_button": b = new BspButton(false); break;
            case "func_rot_button": b = new BspButton(true); break;
            case "momentary_rot_button": b = new BspMomentaryButton(); break;
            case "func_train": b = new BspTrain(false); break;
            case "func_tracktrain": b = new BspTrain(true); break;
            case "func_plat": case "func_platrot": b = new BspPlat(); break;
            case "func_rotating": b = new BspRotating(); break;
            case "func_pendulum": b = new BspPendulum(); break;
            case "func_breakable": case "func_pushable": b = new BspBreakable(); break;
            case "func_wall_toggle": b = new BspWallToggle(); break;
            case "func_illusionary": b = new BspIllusionary(); break;
            case "func_ladder": b = new BspLadder(); break;
            case "func_conveyor": b = new BspConveyor(); break;
            case "func_healthcharger": case "func_recharge": b = new BspCharger(); break;
            case "func_monsterclip": b = new BspSolid(true); break;
            case "scripted_sequence": case "aiscripted_sequence": b = new BspScriptedSequence(); break;
            case "scripted_sentence": b = new BspScriptedSentence(); break;
            case "monstermaker": b = new BspMonsterMaker(); break;
            case "target_cdaudio": b = new BspPassiveVolume(); break;
            default:
                if (c.StartsWith("monster_") || c == "cycler" || c == "hostage_entity" || c.StartsWith("xen_"))
                    b = new BspNpc();
                else if (c.StartsWith("weapon_") || c.StartsWith("ammo_") || c.StartsWith("item_") || c == "weaponbox" ||
                         c == "world_items" || c == "armoury_entity")
                    b = new BspItem();
                else if (c.StartsWith("env_") || c == "cycler_sprite" || c == "gibshooter" || c == "infodecal")
                    b = new BspEffect();
                else if (c.StartsWith("func_") && e.IsBrush)
                    b = new BspSolid(false);
                break;
        }
        if (b != null) b.e = e;
        return b;
    }
}

/// <summary>A brush that only blocks (func_wall, func_monsterclip...).</summary>
public class BspSolid : BspBehaviour
{
    readonly bool m_monsterOnly;
    public BspSolid(bool monsterOnly) { m_monsterOnly = monsterOnly; }
    public override bool Solid => !e.killed;
}

/// <summary>A volume the runtime does nothing with but keeps (buy zones, autosave...).</summary>
public class BspPassiveVolume : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (string.Equals(e.className, "trigger_endsection", StringComparison.OrdinalIgnoreCase))
            Debug.Log("[BSP] The section ends here (trigger_endsection).");
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  Triggers
// ═════════════════════════════════════════════════════════════════════════════
public class BspTrigger : BspBehaviour
{
    readonly bool m_once;
    float m_nextTime;
    public BspTrigger(bool once) { m_once = once; }

    public override bool Touchable => true;
    public override bool MonstersTouch => e.HasFlag(1);
    public override bool PlayersTouch => !e.HasFlag(2);

    public override void Touch(BspEntity other)
    {
        if (Now < m_nextTime) return;
        if (!e.MasterAllows(other)) return;
        Fire(other);
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        // A trigger with a name can be fired from elsewhere too.
        if (Now < m_nextTime) return;
        Fire(activator);
    }

    void Fire(BspEntity activator)
    {
        string msg = e.Get("message");
        if (!string.IsNullOrEmpty(msg)) Debug.Log("[BSP] " + msg);
        string noise = e.Get("noise");
        if (!string.IsNullOrEmpty(noise)) Sound(noise);
        e.FireTargets(activator);
        if (m_once) { e.Kill(); return; }
        float wait = e.GetFloat("wait", 0.2f);
        m_nextTime = wait < 0f ? float.MaxValue : Now + Math.Max(0.2f, wait);
    }
}

public class BspTriggerCounter : BspBehaviour
{
    int m_left;
    public override void Start() { m_left = Math.Max(1, e.GetInt("count", 2)); }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (--m_left > 0) return;
        e.FireTargets(activator);
        e.Kill();
    }
}

public class BspChangeLevel : BspBehaviour
{
    bool m_done;
    public override bool Touchable => !e.HasFlag(2);           // "USE Only"
    public override void Touch(BspEntity other)
    {
        if (other == null || !(other.className == "player")) return;
        Go();
    }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) => Go();
    void Go()
    {
        if (m_done) return;
        string map = e.Get("map");
        if (string.IsNullOrEmpty(map)) { Debug.LogWarning("[BSP] trigger_changelevel without a map."); return; }
        m_done = true;
        e.FireTargets(World != null && World.player != null ? World.player.Entity : null);
        if (World == null || !World.ChangeLevel(map, e.Get("landmark"))) m_done = false;
    }
}

public class BspTeleport : BspBehaviour
{
    public override bool Touchable => true;
    public override bool MonstersTouch => e.HasFlag(1);
    public override void Touch(BspEntity other)
    {
        if (!e.MasterAllows(other)) return;
        BspEntity dest = BspEntity.Find(e.target);
        if (dest == null) return;
        var p = World != null ? World.player : null;
        if (other != null && other.className == "player" && p != null)
            p.Teleport(dest.origin, dest.angles.y, 0f, Vector3.zero);
        else if (other != null && other.transform != null)
            other.transform.position = Hl.ToScene(dest.origin);
    }
}

public class BspPush : BspBehaviour
{
    bool m_on = true;
    public override bool Touchable => m_on;
    public override void Start() { m_on = !e.HasFlag(2); }       // "Start Off"
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) { m_on = !m_on; }
    public override void Touch(BspEntity other)
    {
        var p = World != null ? World.player : null;
        if (p == null || other == null || other.className != "player") return;
        float speed = e.GetFloat("speed", 100f);
        p.HlVelocity += Hl.MoveDir(e) * speed * Time.deltaTime * 10f;
    }
}

public class BspHurt : BspBehaviour
{
    bool m_on = true;
    float m_next;
    public override bool Touchable => m_on;
    public override void Start() { m_on = !e.HasFlag(2); }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) { m_on = !m_on; }
    public override void Touch(BspEntity other)
    {
        if (Now < m_next || other == null || other.className != "player") return;
        m_next = Now + 0.5f;
        var p = World.player;
        if (p != null) p.TakeDamage(e.GetFloat("dmg", 10f), e);
        if (!string.IsNullOrEmpty(e.target)) e.FireTargets(other);
    }
}

public class BspGravity : BspBehaviour
{
    public override bool Touchable => true;
    public override void Touch(BspEntity other)
    {
        var p = World != null ? World.player : null;
        if (p != null && other != null && other.className == "player") p.GravityScale = e.GetFloat("gravity", 1f);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  Logic
// ═════════════════════════════════════════════════════════════════════════════
public class BspRelay : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        int state = e.GetInt("triggerstate", 2);
        var t = state == 0 ? BspUseType.Off : state == 1 ? BspUseType.On : BspUseType.Toggle;
        e.FireTargets(activator, t);
        if (e.HasFlag(1)) e.Kill();
    }
}

public class BspAuto : BspBehaviour
{
    public override void MapStarted()
    {
        string gs = e.Get("globalstate");
        if (!string.IsNullOrEmpty(gs) && BspWorld.GlobalState(gs) != 1) return;
        int state = e.GetInt("triggerstate", 2);
        var t = state == 0 ? BspUseType.Off : state == 1 ? BspUseType.On : BspUseType.Toggle;
        e.FireTargets(e, t);
        if (e.HasFlag(1)) e.Kill();
    }
}

public class BspMultiManager : BspBehaviour
{
    struct Item { public string target; public float delay; }
    readonly List<Item> m_items = new List<Item>();
    float m_busyUntil;
    static readonly HashSet<string> kCommon = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "classname", "targetname", "origin", "angles", "spawnflags", "wait", "delay", "target", "killtarget", "globalname",
        "zhlt_lightflags", "light_origin", "master", "message" };

    public override void Start()
    {
        foreach (var kv in e.Keys)
        {
            if (kCommon.Contains(kv.Key)) continue;
            string name = kv.Key;
            int hash = name.IndexOf('#');
            if (hash >= 0) name = name.Substring(0, hash);
            m_items.Add(new Item { target = name, delay = Hl.ParseFloat(kv.Value) });
        }
        m_items.Sort((a, b) => a.delay.CompareTo(b.delay));
    }

    /// <summary>The targets and when each fires, for the gizmos and scripts.</summary>
    public IEnumerable<KeyValuePair<string, float>> Timetable()
    {
        foreach (Item i in m_items) yield return new KeyValuePair<string, float>(i.target, i.delay);
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        bool threaded = e.HasFlag(1);
        if (!threaded && Now < m_busyUntil) return;     // still running its last firing
        float max = 0f;
        foreach (Item i in m_items)
        {
            World.ScheduleTargets(i.delay, i.target, activator, e, BspUseType.Toggle);
            max = Math.Max(max, i.delay);
        }
        m_busyUntil = Now + max;
    }
}

public class BspMultisource : BspBehaviour
{
    readonly Dictionary<BspEntity, bool> m_state = new Dictionary<BspEntity, bool>();

    public override void MapStarted()
    {
        foreach (BspEntity src in World.FindTargeting(e.targetName)) m_state[src] = false;
    }

    public bool IsTriggered(BspEntity activator)
    {
        string gs = e.Get("globalstate");
        if (!string.IsNullOrEmpty(gs) && BspWorld.GlobalState(gs) != 1) return false;
        foreach (var kv in m_state) if (!kv.Value) return false;
        return true;
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        BspEntity key = caller ?? activator;
        if (key == null) return;
        bool cur;
        m_state.TryGetValue(key, out cur);
        m_state[key] = type == BspUseType.On ? true : type == BspUseType.Off ? false : !cur;
        e.isOn = IsTriggered(activator);
        if (e.isOn) e.FireTargets(activator, BspUseType.Toggle);
    }
}

public class BspGlobal : BspBehaviour
{
    public override void Start()
    {
        string name = e.Get("globalstate");
        if (string.IsNullOrEmpty(name)) return;
        if (e.HasFlag(1) && !BspWorld.globals.ContainsKey(name))
            BspWorld.globals[name] = e.GetInt("initialstate", 0);
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        string name = e.Get("globalstate");
        if (string.IsNullOrEmpty(name)) return;
        int mode = e.GetInt("triggermode", 0);
        int cur = BspWorld.GlobalState(name);
        int next = mode == 0 ? 0 : mode == 1 ? 1 : mode == 2 ? 2 : (cur == 1 ? 0 : 1);
        BspWorld.globals[name] = next;
    }
}

public class BspChangeTarget : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        foreach (BspEntity t in BspEntity.FindAll(e.target)) t.Set("target", e.Get("m_iszNewTarget"));
    }
}

public class BspRender : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        int mode = e.GetInt("rendermode", 0);
        float amt = e.GetFloat("renderamt", 255f);
        foreach (BspEntity t in BspEntity.FindAll(e.target))
            if (t.gameObject != null) t.gameObject.SetActive(!(mode != 0 && amt <= 0f));
    }
}

public class BspMessage : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        string m = e.Get("message");
        if (!string.IsNullOrEmpty(m)) Debug.Log("[BSP] " + m.Replace("\\n", "\n"));
        BspHud.Show(m, e.GetFloat("holdtime", 3f));
        e.FireTargets(activator);
    }
}

public class BspShake : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        var p = World != null ? World.player : null;
        if (p == null) return;
        float radius = e.GetFloat("radius", 1000f);
        bool everywhere = e.HasFlag(1);
        if (everywhere || (p.HlOrigin - e.origin).magnitude < radius)
            p.Shake(e.GetFloat("amplitude", 4f), e.GetFloat("duration", 1f), e.GetFloat("frequency", 2.5f));
    }
}

public class BspFade : BspBehaviour
{
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        Vector3 c = e.GetVector("rendercolor", Vector3.zero);
        BspHud.Fade(new Color(c.x / 255f, c.y / 255f, c.z / 255f, e.GetFloat("renderamt", 255f) / 255f),
                    e.GetFloat("duration", 2f), e.GetFloat("holdtime", 0f), e.HasFlag(1));
    }
}

/// <summary>A switchable light: its state is kept for scripts. The light it
/// casts is baked into the lightmap, so the surfaces do not change.</summary>
public class BspLightSwitch : BspBehaviour
{
    public override void Start() { e.isOn = !e.HasFlag(1); }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        e.isOn = type == BspUseType.On ? true : type == BspUseType.Off ? false : !e.isOn;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  Sound
// ═════════════════════════════════════════════════════════════════════════════
public static class BspSound
{
    public static void PlayAt(string hlPath, Vector3 scenePos, float volume = 1f)
    {
        if (string.IsNullOrEmpty(hlPath)) return;
        if (hlPath[0] == '!') { BspSentencePlayer.PlayAt(hlPath, scenePos, volume); return; }
        string p = Hl.ContentPath(hlPath, "sound");
        if (p != null) AudioSource.PlayClipAtPoint(p, scenePos, volume);
    }

    /// <summary>A door's or plat's numbered move/stop sound.</summary>
    public static string Numbered(string family, int n)
    {
        if (n <= 0) return null;
        switch (family)
        {
            case "doormove": return n <= 10 ? "doors/doormove" + n + ".wav" : null;
            case "doorstop": return n <= 8 ? "doors/doorstop" + n + ".wav" : null;
            case "button":
                if (n >= 1 && n <= 11) return "buttons/button" + n + ".wav";
                if (n == 12) return "buttons/latchlocked1.wav";
                if (n == 13) return "buttons/latchunlocked1.wav";
                if (n == 14) return "buttons/lightswitch2.wav";
                if (n >= 21 && n <= 26) return "buttons/lever" + (n - 20) + ".wav";
                return null;
            case "locked":
                if (n >= 1 && n <= 11) return "buttons/button" + n + ".wav";
                return null;
            default: return null;
        }
    }
}

/// <summary>Plays a sentence's words one after another.</summary>
public static class BspSentencePlayer
{
    sealed class Running { public BspSentence s; public int word; public float next; public Vector3 pos; public float vol; public GameObject speaker; }
    static readonly List<Running> s_running = new List<Running>();
    static float s_lastTick = -1f;

    /// <summary>Plays |name| (a sentence, or a group to pick from) at a
    /// point; returns its length in seconds (estimated), 0 when unknown.</summary>
    public static float PlayAt(string name, Vector3 pos, float volume, GameObject speaker = null)
    {
        BspSentence s = BspSentence.Find(name);
        if (s == null || s.files.Count == 0)
        {
            Debug.LogWarning("[BSP] Sentence " + name + " is not in sentences.txt.");
            return 0f;
        }
        s_running.Add(new Running { s = s, word = 0, next = Time.time, pos = pos, vol = volume, speaker = speaker });
        return s.files.Count * 0.45f;
    }

    /// <summary>Plays the next word of every sentence whose last word ended.
    /// BspWorld calls this every frame.</summary>
    public static void Tick()
    {
        if (Time.time == s_lastTick) return;
        s_lastTick = Time.time;
        for (int i = s_running.Count - 1; i >= 0; --i)
        {
            Running r = s_running[i];
            if (Time.time < r.next) continue;
            if (r.word >= r.s.files.Count) { s_running.RemoveAt(i); continue; }
            string file = r.s.files[r.word++];
            string p = Hl.ContentPath(file, "sound");
            Vector3 at = r.speaker != null ? r.speaker.transform.position : r.pos;
            float len = 0.35f;
            if (p != null)
            {
                AudioSource src = r.speaker != null ? AudioSource.Get(r.speaker) : null;
                if (src != null)
                {
                    src.PlayOneShot(p, r.vol);
                }
                else AudioSource.PlayClipAtPoint(p, at, r.vol);
                len = WavLength(p);
            }
            r.next = Time.time + len;
        }
    }

    static readonly Dictionary<string, float> s_lengths = new Dictionary<string, float>();
    /// <summary>A .wav's length in seconds, from its header.</summary>
    public static float WavLength(string projectPath)
    {
        float len;
        if (s_lengths.TryGetValue(projectPath, out len)) return len;
        len = 0.4f;
        try
        {
            byte[] h = new byte[64];
            using (var f = System.IO.File.OpenRead(Project.Resolve(projectPath)))
            {
                int n = f.Read(h, 0, h.Length);
                long size = f.Length;
                if (n >= 44)
                {
                    int byteRate = BitConverter.ToInt32(h, 28);
                    // Find the data chunk (usually at 36).
                    int pos = 12;
                    while (pos + 8 <= n)
                    {
                        int csize = BitConverter.ToInt32(h, pos + 4);
                        if (h[pos] == 'd' && h[pos + 1] == 'a' && h[pos + 2] == 't' && h[pos + 3] == 'a')
                        {
                            if (byteRate > 0) len = Math.Min(csize, (int)(size - pos - 8)) / (float)byteRate;
                            break;
                        }
                        pos += 8 + csize + (csize & 1);
                    }
                }
            }
        }
        catch (Exception) { }
        s_lengths[projectPath] = len;
        return len;
    }
}

public class BspAmbient : BspBehaviour
{
    AudioSource m_src;
    bool m_looped, m_playing;
    string m_message;

    public override void Start()
    {
        m_src = AudioSource.Get(e.gameObject);
        m_message = e.Get("message");
        m_looped = !e.HasFlag(32);
        m_playing = m_looped && !e.HasFlag(16) && !string.IsNullOrEmpty(m_message) && m_message[0] != '!';
    }

    public override void MapStarted()
    {
        // A looped sound that starts on and is a sentence plays now.
        if (m_looped && !e.HasFlag(16) && !string.IsNullOrEmpty(m_message) && m_message[0] == '!')
            BspSentencePlayer.PlayAt(m_message, e.transform.position, Volume(), e.gameObject);
    }

    float Volume() => Mathf.Clamp01(e.GetFloat("health", 10f) / 10f);

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (string.IsNullOrEmpty(m_message)) return;
        if (m_message[0] == '!')
        {
            BspSentencePlayer.PlayAt(m_message, e.transform.position, Volume(), e.gameObject);
            return;
        }
        if (m_src == null) { Sound(m_message, Volume()); return; }
        if (!m_looped)
        {
            m_src.Stop();
            m_src.Play();
            return;
        }
        bool on = type == BspUseType.On ? true : type == BspUseType.Off ? false : !m_playing;
        if (on) m_src.Play(); else m_src.Stop();
        m_playing = on;
    }
}

public class BspSpeaker : BspBehaviour
{
    bool m_on;
    float m_next;
    public override void Start() { m_on = !e.HasFlag(1); m_next = Now + Elintria.Random.Range(5f, 15f); }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) { m_on = !m_on; m_next = Now; }
    public override void Update(float dt)
    {
        if (!m_on || Now < m_next) return;
        string group = e.Get("message");
        if (!string.IsNullOrEmpty(group)) BspSentencePlayer.PlayAt(group.StartsWith("!") ? group : "!" + group, e.transform.position, 1f, e.gameObject);
        float d = e.GetFloat("delay", 0f);
        m_next = Now + (d > 0f ? d : Elintria.Random.Range(30f, 90f));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  Items
// ═════════════════════════════════════════════════════════════════════════════
public class BspItem : BspBehaviour
{
    public override bool Touchable => true;
    public override bool PlayersTouch => true;

    public override void Start()
    {
        // Items sit where they were placed; the model plays its idle.
        var sm = e.gameObject.GetComponent<BspStudioModel>();
        if (sm != null && !string.IsNullOrEmpty(sm.sequence)) SkinnedModel.Get(e.gameObject).Play(sm.sequence, 1f, true, 0f);
    }

    public override void Touch(BspEntity other)
    {
        if (other == null || other.className != "player") return;
        string c = e.className.ToLowerInvariant();
        Sound(c.StartsWith("weapon_") || c == "weaponbox" ? "items/gunpickup2.wav" :
              c.StartsWith("ammo_") ? "items/9mmclip1.wav" :
              c == "item_healthkit" ? "items/smallmedkit1.wav" :
              c == "item_battery" ? "items/gunpickup2.wav" : "items/gunpickup2.wav");
        Debug.Log("[BSP] Picked up " + e.className);
        if (World != null && World.player != null) World.player.Pickup(e.className);
        e.FireTargets(other);
        e.Kill();
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) => Touch(activator);
}
