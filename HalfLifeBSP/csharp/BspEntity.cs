// ─────────────────────────────────────────────────────────────────────────────
//  BspEntity - one entity of a Half-Life map
//
//  Every object the BSP Loader makes from an entity carries one. It holds all
//  of the entity's keys exactly as the map wrote them, says what the entity is
//  (classname, targetname, where it points), and is how the map's logic runs:
//
//      BspEntity.Find("door1").Use(player)        // what a button does
//      e.FireTargets(activator)                   // what a trigger does
//      e.onUse += (activator, type) => { ... }    // react from your own script
//
//  The class's behaviour (a door's movement, a trigger's touch, a scientist's
//  idle) is created from its classname when Play starts; see BspBehaviours.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Elintria;

public class BspEntity : ElintriaScript
{
    // ── What the map says (written by the importer) ─────────────────────────
    public string className = "";
    public string targetName = "";
    public string target = "";
    public string killTarget = "";
    /// <summary>Where the map placed it, in Half-Life units.</summary>
    public Vector3 origin;
    /// <summary>Pitch, yaw, roll in Half-Life degrees.</summary>
    public Vector3 angles;
    public int spawnFlags;
    public int entityIndex;
    /// <summary>"*12" for a brush entity, a model path, or "".</summary>
    public string model = "";
    public float delay;
    /// <summary>Every key=value, in the map's order (keys can repeat).</summary>
    public List<string> keyValues = new List<string>();

    // ── Runtime ──────────────────────────────────────────────────────────────
    /// <summary>Raised whenever the entity is used (triggered), before its
    /// own behaviour runs.</summary>
    public event Action<BspEntity, BspUseType> onUse;

    /// <summary>What the class does in Play mode (null for a class the
    /// runtime has nothing for).</summary>
    public BspBehaviour behaviour { get; private set; }
    /// <summary>On/off for the classes that have a state (a toggled wall,
    /// a multisource, a light).</summary>
    [HideInInspector]
    public bool isOn;
    /// <summary>Removed by a killtarget or a fired-once trigger.</summary>
    public bool killed { get; private set; }

    List<KeyValuePair<string, string>> m_kv;
    [HideInInspector]
    public int brushModel = -1;
    [HideInInspector]
    public Vector3 startPosition;           // scene position when Play started
    [HideInInspector]
    public Quaternion startRotation;
    [HideInInspector]
    public bool hasOriginKey;
    [HideInInspector]
    public Vector3 originKey;               // Half-Life

    // ── Keys ─────────────────────────────────────────────────────────────────
    public List<KeyValuePair<string, string>> Keys
    {
        get
        {
            if (m_kv == null)
            {
                m_kv = new List<KeyValuePair<string, string>>();
                if (keyValues != null)
                    foreach (string kv in keyValues)
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 0) continue;
                        m_kv.Add(new KeyValuePair<string, string>(kv.Substring(0, eq), kv.Substring(eq + 1)));
                    }
            }
            return m_kv;
        }
    }

    public bool Has(string key)
    {
        foreach (var kv in Keys) if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public string Get(string key, string def = "")
    {
        foreach (var kv in Keys) if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return def;
    }

    public float GetFloat(string key, float def = 0f) => Has(key) ? Hl.ParseFloat(Get(key), def) : def;
    public int GetInt(string key, int def = 0) => Has(key) ? Hl.ParseInt(Get(key), def) : def;
    public Vector3 GetVector(string key, Vector3 def) => Hl.ParseVector(Get(key), def);
    public bool HasFlag(int bit) => (spawnFlags & bit) != 0;

    /// <summary>Changes a key at run time (trigger_changetarget, scripts).</summary>
    public void Set(string key, string value)
    {
        var keys = Keys;
        for (int i = 0; i < keys.Count; ++i)
            if (string.Equals(keys[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                keys[i] = new KeyValuePair<string, string>(keys[i].Key, value);
                if (key == "target") target = value;
                return;
            }
        keys.Add(new KeyValuePair<string, string>(key, value));
        if (key == "target") target = value;
    }

    // ── Finding entities ─────────────────────────────────────────────────────
    /// <summary>The first entity named |targetName|, or null.</summary>
    public static BspEntity Find(string targetName)
    {
        var w = BspWorld.Instance;
        return w != null ? w.FindFirst(targetName) : null;
    }

    /// <summary>Every entity named |targetName|.</summary>
    public static List<BspEntity> FindAll(string targetName)
    {
        var w = BspWorld.Instance;
        return w != null ? w.FindAll(targetName) : new List<BspEntity>();
    }

    /// <summary>Every entity of a class ("monster_scientist").</summary>
    public static List<BspEntity> FindByClass(string className)
    {
        var w = BspWorld.Instance;
        return w != null ? w.FindByClass(className) : new List<BspEntity>();
    }

    // ── Use and fire ─────────────────────────────────────────────────────────
    /// <summary>Triggers this entity, as a button, trigger or relay does.</summary>
    public void Use(BspEntity activator, BspEntity caller = null, BspUseType type = BspUseType.Toggle, float value = 0f)
    {
        if (killed) return;
        var w = BspWorld.Instance;
        if (w != null) w.NoteUse(caller ?? activator, this, type);
        try { onUse?.Invoke(activator, type); }
        catch (Exception ex) { Debug.LogException(ex); }
        if (behaviour != null) behaviour.Use(activator, caller, type, value);
    }

    /// <summary>Fires this entity's target (after its delay) and killtarget,
    /// as everything does when its job is done: SUB_UseTargets.</summary>
    public void FireTargets(BspEntity activator, BspUseType type = BspUseType.Toggle, float value = 0f)
    {
        var w = BspWorld.Instance;
        if (w == null) return;
        if (delay > 0f)
        {
            w.Schedule(delay, this, activator, type, value, true);
            return;
        }
        FireTargetsNow(activator, type, value);
    }

    internal void FireTargetsNow(BspEntity activator, BspUseType type, float value)
    {
        var w = BspWorld.Instance;
        if (w == null) return;
        if (!string.IsNullOrEmpty(killTarget))
            foreach (BspEntity k in w.FindAll(killTarget)) { w.NoteKill(this, k); k.Kill(); }
        if (!string.IsNullOrEmpty(target)) w.FireTargets(target, activator, this, type, value);
    }

    /// <summary>Removes the entity from the game (killtarget).</summary>
    public void Kill()
    {
        if (killed) return;
        killed = true;
        if (behaviour != null) behaviour.OnKilled();
        if (gameObject != null) gameObject.SetActive(false);
    }

    /// <summary>Whether its "master" (a multisource) lets it act.</summary>
    public bool MasterAllows(BspEntity activator)
    {
        string master = Get("master");
        if (string.IsNullOrEmpty(master)) return true;
        var w = BspWorld.Instance;
        return w == null || w.IsMasterOn(master, activator);
    }

    // ── Where it is, in Half-Life terms ──────────────────────────────────────
    /// <summary>Its position now in Half-Life units (the hull centre of a
    /// monster or player, the pivot of a brush).</summary>
    public Vector3 HlPosition => transform != null ? Hl.ToHl(transform.position) : origin;

    /// <summary>A brush entity's model-space point for |hlWorld| (how far the
    /// entity has moved or turned since the map placed it).</summary>
    public Vector3 ToModelSpace(Vector3 hlWorld)
    {
        Vector3 s = Hl.ToScene(hlWorld);
        Vector3 p = startRotation * (Quaternion.Inverse(transform.rotation) * (s - transform.position)) + startPosition;
        Vector3 hl = Hl.ToHl(p);
        return hasOriginKey ? hl - originKey : hl;
    }

    /// <summary>A model-space direction back in the world.</summary>
    public Vector3 FromModelDir(Vector3 hlDir)
    {
        Vector3 s = Hl.DirToScene(hlDir);
        Vector3 w = transform.rotation * (Quaternion.Inverse(startRotation) * s);
        return Hl.DirToHl(w);
    }

    public bool IsBrush => brushModel > 0;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    public override void Awake()
    {
        if (!string.IsNullOrEmpty(model) && model[0] == '*') brushModel = Hl.ParseInt(model.Substring(1), -1);
        hasOriginKey = Has("origin");
        originKey = origin;
    }

    public override void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        var w = BspWorld.Instance ?? FindObjectOfType<BspWorld>();
        if (w != null) w.Register(this);
        behaviour = BspBehaviour.Create(this);
        if (behaviour != null)
        {
            try { behaviour.Start(); }
            catch (Exception ex) { Debug.LogError(className + " (" + targetName + "): " + ex.Message); }
        }
    }

    public override void Update()
    {
        if (killed || behaviour == null) return;
        try { behaviour.Update(Time.deltaTime); }
        catch (Exception ex)
        {
            Debug.LogError(className + " (" + targetName + "): " + ex.Message);
            behaviour = null;
        }
    }

    public override string ToString() => string.IsNullOrEmpty(targetName) ? className : className + " (" + targetName + ")";
}
