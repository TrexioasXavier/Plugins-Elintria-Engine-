// ─────────────────────────────────────────────────────────────────────────────
//  BspWorld - a Half-Life map while it plays
//
//  One per imported map, on the scene's root. It is the map's
//  "function execution master": every entity registers here by targetname,
//  every trigger fires through here (now, or after a delay - multi_manager,
//  trigger_relay, "delay" keys), masters and global states are kept here, and
//  so is the map's collision (its own clip hulls) and AI node graph.
//
//  With showActivity on it draws each firing as a line from what fired to
//  what it fired, fading over two seconds - the runtime activity gizmo.
//
//  A level change (trigger_changelevel) loads the next map's scene and puts
//  the player at the same place relative to the shared info_landmark.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using Elintria;

public class BspWorld : ElintriaScript
{
    public string mapName = "";
    public string bspPath = "";
    public string contentRoot = "Assets/BSPContent";
    public float unitScale = 0.0254f;
    public string skyName = "";
    public string graphPath = "";
    public bool showActivity = true;
    public int bspVersion = 30;
    public List<string> worldKeys = new List<string>();

    public static BspWorld Instance { get; private set; }

    /// <summary>The map's collision; null until loaded (or when the .bsp is gone).</summary>
    public BspCollision collision { get; private set; }
    public BspPlayer player { get; internal set; }
    public BspNodeGraph graph { get; private set; }

    /// <summary>Global states (env_global), kept across level changes.</summary>
    public static readonly Dictionary<string, int> globals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    readonly List<BspEntity> m_all = new List<BspEntity>();
    readonly Dictionary<string, List<BspEntity>> m_byName = new Dictionary<string, List<BspEntity>>(StringComparer.OrdinalIgnoreCase);

    struct Pending
    {
        public float due;
        public BspEntity source;      // FireTargets of this entity (delayed SUB_UseTargets)...
        public string targetName;     // ...or Use on everything named this
        public BspEntity activator, caller;
        public BspUseType type;
        public float value;
    }
    readonly List<Pending> m_pending = new List<Pending>();

    struct Activity { public Vector3 a, b; public float time; public Color color; }
    readonly List<Activity> m_activity = new List<Activity>();
    public int firedCount { get; private set; }

    bool m_started;
    float m_startTime;

    // ── Registration ─────────────────────────────────────────────────────────
    internal void Register(BspEntity e)
    {
        if (m_all.Contains(e)) return;
        m_all.Add(e);
        if (string.IsNullOrEmpty(e.targetName)) return;
        List<BspEntity> l;
        if (!m_byName.TryGetValue(e.targetName, out l)) m_byName[e.targetName] = l = new List<BspEntity>();
        l.Add(e);
    }

    public IReadOnlyList<BspEntity> All => m_all;

    public BspEntity FindFirst(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        List<BspEntity> l;
        if (m_byName.TryGetValue(name, out l))
            foreach (var e in l) if (!e.killed) return e;
        return null;
    }

    public List<BspEntity> FindAll(string name)
    {
        var o = new List<BspEntity>();
        if (string.IsNullOrEmpty(name)) return o;
        List<BspEntity> l;
        if (m_byName.TryGetValue(name, out l)) foreach (var e in l) if (!e.killed) o.Add(e);
        return o;
    }

    public List<BspEntity> FindByClass(string cls)
    {
        var o = new List<BspEntity>();
        foreach (var e in m_all) if (!e.killed && string.Equals(e.className, cls, StringComparison.OrdinalIgnoreCase)) o.Add(e);
        return o;
    }

    /// <summary>Who targets |name| - the entities whose target is it.</summary>
    public List<BspEntity> FindTargeting(string name)
    {
        var o = new List<BspEntity>();
        foreach (var e in m_all) if (string.Equals(e.target, name, StringComparison.OrdinalIgnoreCase)) o.Add(e);
        return o;
    }

    // ── Firing ───────────────────────────────────────────────────────────────
    /// <summary>Uses every entity named |targetName| (FireTargets).</summary>
    public void FireTargets(string targetName, BspEntity activator, BspEntity caller, BspUseType type = BspUseType.Toggle, float value = 0f)
    {
        if (string.IsNullOrEmpty(targetName)) return;
        foreach (BspEntity e in FindAll(targetName))
            e.Use(activator, caller, type, value);
    }

    /// <summary>|source|'s FireTargets after |delay| seconds.</summary>
    public void Schedule(float delay, BspEntity source, BspEntity activator, BspUseType type, float value, bool sourceTargets)
    {
        m_pending.Add(new Pending { due = Time.time + delay, source = source, activator = activator, caller = source, type = type, value = value });
    }

    /// <summary>Uses everything named |targetName| after |delay| seconds.</summary>
    public void ScheduleTargets(float delay, string targetName, BspEntity activator, BspEntity caller, BspUseType type = BspUseType.Toggle, float value = 0f)
    {
        m_pending.Add(new Pending { due = Time.time + Math.Max(0f, delay), targetName = targetName, activator = activator, caller = caller, type = type, value = value });
    }

    /// <summary>Drops |caller|'s pending firings (a multi_manager restarted).</summary>
    public void CancelScheduled(BspEntity caller)
    {
        m_pending.RemoveAll(p => p.caller == caller && p.source == null);
    }

    void RunPending()
    {
        float now = Time.time;
        for (int i = 0; i < m_pending.Count; )
        {
            if (m_pending[i].due > now) { ++i; continue; }
            Pending p = m_pending[i];
            m_pending.RemoveAt(i);
            if (p.source != null) p.source.FireTargetsNow(p.activator, p.type, p.value);
            else FireTargets(p.targetName, p.activator, p.caller, p.type, p.value);
        }
    }

    // ── Masters and globals ──────────────────────────────────────────────────
    public bool IsMasterOn(string master, BspEntity activator)
    {
        foreach (BspEntity m in FindAll(master))
        {
            var ms = m.behaviour as BspMultisource;
            if (ms != null) return ms.IsTriggered(activator);
            return m.isOn;
        }
        return true;     // a master that does not exist does not lock anything
    }

    /// <summary>0 off, 1 on, 2 dead; -1 when never set.</summary>
    public static int GlobalState(string name)
    {
        int v;
        return !string.IsNullOrEmpty(name) && globals.TryGetValue(name, out v) ? v : -1;
    }

    // ── Activity (the runtime gizmo) ─────────────────────────────────────────
    internal void NoteUse(BspEntity from, BspEntity to, BspUseType type)
    {
        ++firedCount;
        if (!showActivity || from == null || to == null || from.transform == null || to.transform == null) return;
        Color c = type == BspUseType.Off ? new Color(1f, 0.35f, 0.3f, 1f) : type == BspUseType.On ? new Color(0.4f, 1f, 0.45f, 1f)
                                                                         : new Color(1f, 0.85f, 0.25f, 1f);
        m_activity.Add(new Activity { a = from.transform.position, b = to.transform.position, time = Time.time, color = c });
        if (m_activity.Count > 256) m_activity.RemoveAt(0);
    }

    internal void NoteKill(BspEntity from, BspEntity to)
    {
        if (!showActivity || from == null || to == null || from.transform == null || to.transform == null) return;
        m_activity.Add(new Activity { a = from.transform.position, b = to.transform.position, time = Time.time, color = new Color(1f, 0.2f, 0.2f, 1f) });
    }

    void DrawActivity()
    {
        float now = Time.time;
        m_activity.RemoveAll(a => now - a.time > 2f);
        foreach (Activity a in m_activity)
        {
            float age = (now - a.time) / 2f;
            Color c = a.color;
            c.a = 1f - age;
            Debug.DrawLine(a.a, a.b, c, 0f, false);
            // A pulse travelling along the line in the first half second.
            float t = Mathf.Clamp01((now - a.time) / 0.5f);
            Vector3 p = Vector3.Lerp(a.a, a.b, t);
            float r = 0.12f;
            Debug.DrawLine(p - Vector3.up * r, p + Vector3.up * r, c, 0f, false);
            Debug.DrawLine(p - Vector3.right * r, p + Vector3.right * r, c, 0f, false);
            Debug.DrawLine(p - Vector3.forward * r, p + Vector3.forward * r, c, 0f, false);
        }
    }

    // ── Collision ────────────────────────────────────────────────────────────
    /// <summary>Traces a box (hull 0 point, 1 standing, 2 large, 3 crouching)
    /// from |a| to |b| through the world and every solid brush entity.
    /// |ignore| is skipped (the entity doing the moving).</summary>
    public BspTrace Trace(Vector3 a, Vector3 b, int hull, BspEntity ignore = null, bool monsterClip = false)
    {
        var best = new BspTrace { fraction = 1f, endPos = b };
        if (collision == null) return best;
        best = collision.TraceModel(0, hull, a, b);
        Vector3 mins = Vector3.Min(a, b) + BspCollision.HullMins[hull], maxs = Vector3.Max(a, b) + BspCollision.HullMaxs[hull];
        foreach (BspEntity e in m_all)
        {
            if (e == ignore || e.killed || !e.IsBrush || e.behaviour == null || !e.behaviour.Solid) continue;
            if (!monsterClip && string.Equals(e.className, "func_monsterclip", StringComparison.OrdinalIgnoreCase)) continue;
            if (e.brushModel >= collision.models.Length) continue;
            if (!e.behaviour.Overlaps(mins, maxs)) continue;
            Vector3 la = e.ToModelSpace(a), lb = e.ToModelSpace(b);
            BspTrace t = collision.TraceModel(e.brushModel, hull, la, lb);
            if (t.allSolid || t.startSolid || t.fraction < best.fraction)
            {
                if (t.fraction >= best.fraction && !t.startSolid) continue;
                t.endPos = a + (b - a) * t.fraction;
                t.normal = e.FromModelDir(t.normal);
                t.entity = e;
                if (t.startSolid && !best.startSolid) best = t;
                else if (t.fraction < best.fraction) best = t;
            }
        }
        return best;
    }

    /// <summary>The contents at a point: solid, water, ladder...</summary>
    public int PointContents(Vector3 p)
    {
        if (collision == null) return BspCollision.ContentsEmpty;
        int c = collision.HullPointContents(0, 0, p);
        if (c != BspCollision.ContentsEmpty) return c;
        foreach (BspEntity e in m_all)
        {
            if (e.killed || !e.IsBrush || e.behaviour == null) continue;
            int bc = e.behaviour.ContentsAt(p);
            if (bc != BspCollision.ContentsEmpty) return bc;
        }
        return c;
    }

    // ── Level changes ────────────────────────────────────────────────────────
    /// <summary>Loads |map|'s scene (imported by the BSP Loader) with the
    /// player placed relative to |landmark|, as the game's changelevel does.</summary>
    public bool ChangeLevel(string map, string landmark)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("hl.changelevel=1\n");
        sb.Append("hl.from=").Append(mapName).Append('\n');
        sb.Append("hl.landmark=").Append(landmark ?? "").Append('\n');
        if (player != null)
        {
            BspEntity lm = FindLandmark(landmark);
            Vector3 lmPos = lm != null ? lm.origin : Vector3.zero;
            Vector3 rel = player.HlOrigin - lmPos;
            sb.Append("hl.offset=").Append(V(rel)).Append('\n');
            sb.Append("hl.velocity=").Append(V(player.HlVelocity)).Append('\n');
            sb.Append("hl.yaw=").Append(player.Yaw.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("hl.pitch=").Append(player.Pitch.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        foreach (var g in globals) sb.Append("hl.global.").Append(g.Key).Append('=').Append(g.Value).Append('\n');
        string scene = "Scenes/HalfLife/" + map + ".elscene";
        Debug.Log("[BSP] Changing level: " + mapName + " -> " + map + " at landmark '" + landmark + "'");
        if (SceneManager.LoadScene(scene, sb.ToString())) return true;
        Debug.LogWarning("[BSP] Could not load " + scene + ": import " + map + ".bsp with the BSPLoader window first.");
        return false;
    }

    static string V(Vector3 v) => v.x.ToString(CultureInfo.InvariantCulture) + " " + v.y.ToString(CultureInfo.InvariantCulture) + " " +
                                  v.z.ToString(CultureInfo.InvariantCulture);

    public BspEntity FindLandmark(string name)
    {
        foreach (BspEntity e in FindAll(name))
            if (string.Equals(e.className, "info_landmark", StringComparison.OrdinalIgnoreCase)) return e;
        return FindFirst(name);
    }

    void ArriveFromLevelChange()
    {
        string data = SceneManager.sceneData;
        if (string.IsNullOrEmpty(data) || data.IndexOf("hl.changelevel=1", StringComparison.Ordinal) < 0) return;
        var kv = new Dictionary<string, string>();
        foreach (string line in data.Split('\n'))
        {
            int eq = line.IndexOf('=');
            if (eq > 0) kv[line.Substring(0, eq)] = line.Substring(eq + 1);
        }
        foreach (var p in kv)
            if (p.Key.StartsWith("hl.global.", StringComparison.Ordinal)) globals[p.Key.Substring(10)] = Hl.ParseInt(p.Value);
        string landmark;
        kv.TryGetValue("hl.landmark", out landmark);
        BspEntity lm = FindLandmark(landmark);
        if (player == null) return;
        if (lm == null)
        {
            Debug.LogWarning("[BSP] Landmark '" + landmark + "' is not in " + mapName + "; the player starts at the map's start.");
            return;
        }
        string s;
        Vector3 offset = kv.TryGetValue("hl.offset", out s) ? Hl.ParseVector(s, Vector3.zero) : Vector3.zero;
        Vector3 vel = kv.TryGetValue("hl.velocity", out s) ? Hl.ParseVector(s, Vector3.zero) : Vector3.zero;
        float yaw = kv.TryGetValue("hl.yaw", out s) ? Hl.ParseFloat(s) : player.Yaw;
        float pitch = kv.TryGetValue("hl.pitch", out s) ? Hl.ParseFloat(s) : 0f;
        player.Teleport(lm.origin + offset, yaw, pitch, vel);
        Debug.Log("[BSP] Arrived in " + mapName + " at landmark '" + landmark + "'.");
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public override void Awake()
    {
        Instance = this;
        Hl.Scale = unitScale > 0f ? unitScale : 0.0254f;
        var c = new BspCollision();
        string abs = null;
        try { abs = Project.Resolve(bspPath); } catch (Exception) { }
        if (abs != null && c.Load(abs)) collision = c;
        else Debug.LogWarning("[BSP] No collision for " + mapName + ": " + (c.Error ?? "the map file is missing") +
                              " (" + bspPath + "). The player will fall through the world.");
        graph = BspNodeGraph.Load(graphPath);
    }

    public override void Start()
    {
        m_startTime = Time.time;
    }

    public override void Update()
    {
        if (!m_started && Time.time - m_startTime >= 0.05f)
        {
            // Once every entity has registered (their Start has run).
            m_started = true;
            ArriveFromLevelChange();
            foreach (BspEntity e in m_all.ToArray())
                if (e.behaviour != null) e.behaviour.MapStarted();
        }
        RunPending();
        if (showActivity) DrawActivity();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
