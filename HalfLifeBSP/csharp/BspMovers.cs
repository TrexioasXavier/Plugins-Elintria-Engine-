// ─────────────────────────────────────────────────────────────────────────────
//  Movers - doors, buttons, trains, platforms, things that spin, swing and
//  break. Each moves its object; the world traces the player against the
//  brush wherever it is now, so riding a lift or being pushed by a door works.
//
//  Half-Life keys, Half-Life units: "speed" is units (or degrees) per second,
//  "lip" is how much of a door stays in view when open, "wait" is how long it
//  stays open (-1: until triggered again).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Elintria;

/// <summary>Shared by everything that slides or turns between two positions.</summary>
public abstract class BspMover : BspBehaviour
{
    protected enum State { AtStart, Moving1, AtEnd, Moving2 }
    protected State state = State.AtStart;
    protected Vector3 pos1, pos2;          // offsets from where it was placed, Half-Life units
    protected float ang1, ang2;            // degrees about |axis| (rotating movers)
    protected Vector3 axisScene = Vector3.up;
    protected bool rotating;
    protected float speed = 100f;
    protected float current;               // 0 at start, 1 at end
    protected string moveSound, stopSound;
    float m_length = 1f;

    public override bool Solid => !e.killed && !e.HasFlag(8);

    protected void SetupLinear(float lip, Vector3 dir)
    {
        Vector3 mins, maxs;
        Vector3 size = Bounds(out mins, out maxs) ? maxs - mins : new Vector3(64, 64, 64);
        float dist = Mathf.Abs(dir.x * (size.x - 2f)) + Mathf.Abs(dir.y * (size.y - 2f)) + Mathf.Abs(dir.z * (size.z - 2f)) - lip;
        pos1 = Vector3.zero;
        pos2 = dir * dist;
        m_length = Mathf.Max(0.001f, dist);
    }

    protected void SetupAngular(float distance, Vector3 axis)
    {
        rotating = true;
        axisScene = axis;
        ang1 = 0f;
        ang2 = distance;
        m_length = Mathf.Max(0.001f, Mathf.Abs(distance));
    }

    /// <summary>Puts the object at |t| (0..1) between its two positions.</summary>
    protected void Place(float t)
    {
        current = Mathf.Clamp01(t);
        if (rotating)
        {
            float a = Mathf.Lerp(ang1, ang2, current);
            e.transform.rotation = Quaternion.AngleAxis(a, axisScene) * e.startRotation;
        }
        else
        {
            Vector3 hl = Vector3.Lerp(pos1, pos2, current);
            e.transform.position = e.startPosition + Hl.ToScene(hl);
        }
    }

    /// <summary>Moves toward |target| (0 or 1) at speed; true on arrival.</summary>
    protected bool Step(float target, float dt)
    {
        float s = speed / m_length * dt;
        float next = Mathf.MoveTowards(current, target, s);
        // Something in the way (the player under a lift) stops it, as the
        // game's movers stop on a blocker.
        Place(next);
        return Mathf.Approximately(next, target);
    }

    protected void StartSound() { if (!string.IsNullOrEmpty(moveSound)) Sound(moveSound); }
    protected void StopSound() { if (!string.IsNullOrEmpty(stopSound)) Sound(stopSound); }
}

/// <summary>func_door, func_door_rotating, func_water, momentary_door.</summary>
public class BspDoor : BspMover
{
    readonly bool m_rot, m_momentary;
    float m_wait, m_closeAt;
    BspEntity m_activator;
    public BspDoor(bool rotating, bool momentary = false) { m_rot = rotating; m_momentary = momentary; }

    public bool IsOpen => state == State.AtEnd || state == State.Moving1;
    public bool MonstersCanOpen => !e.HasFlag(512) && string.IsNullOrEmpty(e.targetName);
    public override bool Touchable => !e.HasFlag(256) && string.IsNullOrEmpty(e.targetName) && !m_momentary;
    public override bool MonstersTouch => !e.HasFlag(512);
    public override bool Usable => !m_momentary && (e.HasFlag(256) || string.IsNullOrEmpty(e.targetName));

    public override int ContentsAt(Vector3 hl)
    {
        if (!string.Equals(e.className, "func_water", StringComparison.OrdinalIgnoreCase)) return BspCollision.ContentsEmpty;
        return Inside(hl) ? BspCollision.ContentsWater : BspCollision.ContentsEmpty;
    }

    public override bool Solid => base.Solid && !string.Equals(e.className, "func_water", StringComparison.OrdinalIgnoreCase);

    public override void Start()
    {
        speed = e.GetFloat("speed", 100f);
        if (speed <= 0f) speed = 100f;
        m_wait = e.GetFloat("wait", 3f);
        moveSound = BspSound.Numbered("doormove", e.GetInt("movesnd", 0));
        stopSound = BspSound.Numbered("doorstop", e.GetInt("stopsnd", 0));
        if (m_rot)
        {
            // Turns about Z (yaw) unless told X or Y; "distance" degrees.
            Vector3 axis = e.HasFlag(64) ? new Vector3(1, 0, 0) : e.HasFlag(128) ? new Vector3(0, 0, -1) : new Vector3(0, 1, 0);
            float dist = e.GetFloat("distance", 90f);
            if (e.HasFlag(2)) dist = -dist;
            SetupAngular(dist, axis);
        }
        else SetupLinear(e.GetFloat("lip", 0f), Hl.MoveDir(e));
        if (e.HasFlag(1))
        {
            // Starts open: the open position is where it was placed.
            if (rotating) { float t = ang1; ang1 = ang2; ang2 = t; }
            else { Vector3 t = pos1; pos1 = pos2; pos2 = t; }
            Place(0f);
        }
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (m_momentary)
        {
            Place(Mathf.Clamp01(value));
            return;
        }
        if (!e.MasterAllows(activator))
        {
            BspSoundLocked();
            return;
        }
        m_activator = activator;
        bool toggle = e.HasFlag(32);
        if (state == State.AtStart || state == State.Moving2)
        {
            if (type == BspUseType.Off) return;
            Open();
        }
        else if (toggle || m_wait < 0f || type == BspUseType.Off)
            Close();
        else if (state == State.AtEnd)
            m_closeAt = Now + Math.Max(0f, m_wait);       // re-triggered: stay open longer
    }

    public override void Touch(BspEntity other)
    {
        if (state != State.AtStart) return;
        if (!e.MasterAllows(other)) return;
        m_activator = other;
        Open();
    }

    void BspSoundLocked()
    {
        string s = BspSound.Numbered("locked", e.GetInt("locked_sound", 0));
        if (s != null) Sound(s);
    }

    void Open()
    {
        if (state == State.Moving1 || state == State.AtEnd) return;
        state = State.Moving1;
        StartSound();
        e.FireTargets(m_activator);
    }

    void Close()
    {
        if (state == State.Moving2 || state == State.AtStart) return;
        state = State.Moving2;
        StartSound();
    }

    public override void Update(float dt)
    {
        switch (state)
        {
            case State.Moving1:
                if (Step(1f, dt))
                {
                    state = State.AtEnd;
                    StopSound();
                    m_closeAt = m_wait < 0f || e.HasFlag(32) ? float.MaxValue : Now + m_wait;
                }
                break;
            case State.AtEnd:
                if (Now >= m_closeAt) Close();
                break;
            case State.Moving2:
                if (Step(0f, dt))
                {
                    state = State.AtStart;
                    StopSound();
                    string fire = e.Get("fireonclose");
                    if (!string.IsNullOrEmpty(fire)) World.FireTargets(fire, m_activator, e);
                }
                break;
        }
    }
}

/// <summary>func_button and func_rot_button.</summary>
public class BspButton : BspMover
{
    readonly bool m_rot;
    float m_wait, m_returnAt;
    BspEntity m_activator;
    public BspButton(bool rotating) { m_rot = rotating; }

    public override bool Usable => true;
    public override bool Touchable => e.HasFlag(256);

    public override void Start()
    {
        speed = e.GetFloat("speed", m_rot ? 40f : 40f);
        if (speed <= 0f) speed = 40f;
        m_wait = e.GetFloat("wait", 1f);
        moveSound = BspSound.Numbered("button", e.GetInt("sounds", 0));
        if (e.HasFlag(1)) { speed = 1e6f; SetupLinear(1e6f, Vector3.zero); }     // "Don't move"
        else if (m_rot)
        {
            Vector3 axis = e.HasFlag(64) ? new Vector3(1, 0, 0) : e.HasFlag(128) ? new Vector3(0, 0, -1) : new Vector3(0, 1, 0);
            float dist = e.GetFloat("distance", 90f);
            if (e.HasFlag(2)) dist = -dist;
            SetupAngular(dist, axis);
        }
        else SetupLinear(e.GetFloat("lip", 4f), Hl.MoveDir(e));
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) => Press(activator);
    public override void Touch(BspEntity other) => Press(other);

    void Press(BspEntity activator)
    {
        if (!e.MasterAllows(activator))
        {
            string ls = BspSound.Numbered("locked", e.GetInt("locked_sound", 0));
            if (ls != null) Sound(ls);
            return;
        }
        bool toggle = e.HasFlag(32);
        if (state == State.Moving1 || (state == State.AtEnd && !toggle)) return;
        m_activator = activator;
        StartSound();
        if (state == State.AtEnd && toggle) { state = State.Moving2; e.FireTargets(activator, BspUseType.Off); return; }
        state = State.Moving1;
        e.FireTargets(activator, toggle ? BspUseType.On : BspUseType.Toggle);
    }

    public override void Update(float dt)
    {
        switch (state)
        {
            case State.Moving1:
                if (Step(1f, dt) || e.HasFlag(1))
                {
                    state = State.AtEnd;
                    m_returnAt = e.HasFlag(32) || m_wait < 0f ? float.MaxValue : Now + m_wait;
                }
                break;
            case State.AtEnd:
                if (Now >= m_returnAt) state = State.Moving2;
                break;
            case State.Moving2:
                if (Step(0f, dt) || e.HasFlag(1)) state = State.AtStart;
                break;
        }
    }
}

/// <summary>A valve wheel: holding use turns it and drives its target
/// (a momentary_door) by how far it has turned.</summary>
public class BspMomentaryButton : BspMover
{
    float m_value;
    float m_lastUse = -10f;
    public override bool Usable => true;

    public override void Start()
    {
        speed = e.GetFloat("speed", 50f);
        if (speed <= 0f) speed = 50f;
        Vector3 axis = e.HasFlag(64) ? new Vector3(1, 0, 0) : e.HasFlag(128) ? new Vector3(0, 0, -1) : new Vector3(0, 1, 0);
        SetupAngular(e.GetFloat("distance", 90f), axis);
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        m_lastUse = Now;
    }

    public override void Update(float dt)
    {
        bool held = Now - m_lastUse < 0.25f;
        float target = held ? 1f : (e.HasFlag(1) ? m_value : 0f);    // "Door Hold" keeps it where it is
        float before = m_value;
        m_value = Mathf.MoveTowards(m_value, target, speed / 90f * dt * 0.5f);
        Place(m_value);
        if (!Mathf.Approximately(before, m_value))
            World.FireTargets(e.target, e, e, BspUseType.Set, m_value);
    }
}

/// <summary>func_train (path_corners) and func_tracktrain (path_tracks).</summary>
public class BspTrain : BspBehaviour
{
    readonly bool m_track;
    BspEntity m_from, m_to;
    float m_speed, m_waitUntil;
    bool m_moving, m_started;
    BspEntity m_activator;
    public BspTrain(bool track) { m_track = track; }

    public override bool Solid => !e.killed && !e.HasFlag(8);

    public override void MapStarted()
    {
        m_speed = e.GetFloat(m_track ? "startspeed" : "speed", 100f);
        if (m_speed <= 0f) m_speed = m_track ? e.GetFloat("speed", 100f) : 100f;
        m_to = BspEntity.Find(e.target);
        if (m_to == null) return;
        // Placed at its first corner.
        e.transform.position = Hl.ToScene(CornerPoint(m_to));
        m_from = m_to;
        m_to = BspEntity.Find(m_from.target);
        // A train nobody triggers starts on its own; a track train starts
        // when told unless it has a start speed.
        if (string.IsNullOrEmpty(e.targetName) || (m_track && e.GetFloat("startspeed", 0f) > 0f)) Go();
    }

    Vector3 CornerPoint(BspEntity corner)
    {
        Vector3 p = corner.origin;
        // A track train runs its origin at the track's height plus its own.
        if (m_track) p.z += e.GetFloat("height", 4f);
        return p;
    }

    void Go()
    {
        if (m_to == null) return;
        m_moving = true;
        m_started = true;
        string noise = e.Get("noise");
        if (!string.IsNullOrEmpty(noise)) Sound(noise);
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        m_activator = activator;
        if (m_moving && type != BspUseType.On) { m_moving = false; return; }
        if (type == BspUseType.Off) return;
        if (m_to == null && m_from != null) m_to = BspEntity.Find(m_from.target);
        Go();
    }

    public override void Update(float dt)
    {
        if (!m_moving || m_to == null || Now < m_waitUntil) return;
        Vector3 cur = Hl.ToHl(e.transform.position);
        Vector3 dst = CornerPoint(m_to);
        Vector3 d = dst - cur;
        float dist = d.magnitude;
        float step = m_speed * dt;
        if (m_track && dist > 1f)
        {
            // Face along the track.
            float yaw = Hl.YawOf(d);
            e.transform.rotation = Quaternion.AngleAxis(yaw - Hl.YawOf(Hl.MoveDir(e)), Vector3.up) * e.startRotation;
        }
        if (dist <= step || dist < 0.01f)
        {
            e.transform.position = Hl.ToScene(dst);
            Arrive();
            return;
        }
        e.transform.position = Hl.ToScene(cur + d * (step / dist));
    }

    void Arrive()
    {
        BspEntity corner = m_to;
        // The corner's message fires when the train gets there; its speed
        // and wait apply from here.
        string msg = corner.Get("message");
        if (!string.IsNullOrEmpty(msg)) World.FireTargets(msg, m_activator ?? e, corner);
        if (m_track && !string.IsNullOrEmpty(corner.Get("netname"))) World.FireTargets(corner.Get("netname"), m_activator ?? e, corner);
        float sp = corner.GetFloat("speed", 0f);
        if (sp > 0f) m_speed = sp;
        float wait = corner.GetFloat("wait", 0f);
        m_from = corner;
        BspEntity next = BspEntity.Find(corner.target);
        if (next != null && next.HasFlag(2) && !m_track)
        {
            // "Teleport": jump to the next corner.
            e.transform.position = Hl.ToScene(CornerPoint(next));
            m_from = next;
            next = BspEntity.Find(next.target);
        }
        m_to = next;
        if (corner.HasFlag(1) && !m_track) { m_moving = false; return; }   // wait for retrigger
        if (wait < 0f) { m_moving = false; return; }
        if (wait > 0f) m_waitUntil = Now + wait;
        if (m_to == null) m_moving = false;
    }
}

/// <summary>func_plat: rises when stood on (or toggles when triggered).</summary>
public class BspPlat : BspMover
{
    float m_downAt;
    public override bool Touchable => string.IsNullOrEmpty(e.targetName);

    public override void Start()
    {
        speed = e.GetFloat("speed", 150f);
        if (speed <= 0f) speed = 150f;
        Vector3 mins, maxs;
        float h = e.GetFloat("height", 0f);
        if (h <= 0f) h = Bounds(out mins, out maxs) ? (maxs.z - mins.z - 8f) : 64f;
        // pos1 = top (where it was placed), pos2 = bottom.
        pos1 = Vector3.zero;
        pos2 = new Vector3(0, 0, -h);
        moveSound = e.GetInt("movesnd", 0) > 0 ? "plats/bigmove1.wav" : null;
        stopSound = e.GetInt("stopsnd", 0) > 0 ? "plats/bigstop1.wav" : null;
        SetupLinearOnly(h);
        if (string.IsNullOrEmpty(e.targetName)) { Place(1f); state = State.AtEnd; }    // waits at the bottom
    }

    void SetupLinearOnly(float h) { speedLength = h; }
    float speedLength = 1f;

    public override void Touch(BspEntity other)
    {
        // Standing on it at the bottom sends it up.
        if (state == State.AtEnd) { state = State.Moving2; StartSound(); }
        m_downAt = Now + 3f;
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (state == State.AtStart || state == State.Moving2) { state = State.Moving1; StartSound(); }
        else { state = State.Moving2; StartSound(); }
    }

    public override void Update(float dt)
    {
        float s = speed / Mathf.Max(1f, speedLength) * dt;
        switch (state)
        {
            case State.Moving1:
                Place(Mathf.MoveTowards(current, 1f, s));
                if (current >= 1f) { state = State.AtEnd; StopSound(); }
                break;
            case State.Moving2:
                Place(Mathf.MoveTowards(current, 0f, s));
                if (current <= 0f) { state = State.AtStart; StopSound(); m_downAt = Now + 3f; }
                break;
            case State.AtStart:
                if (string.IsNullOrEmpty(e.targetName) && Now > m_downAt) { state = State.Moving1; StartSound(); }
                break;
        }
    }
}

/// <summary>func_rotating: fans, gears.</summary>
public class BspRotating : BspBehaviour
{
    float m_speed, m_cur, m_angle;
    bool m_on;
    Vector3 m_axis;
    public override bool Solid => !e.HasFlag(64);

    public override void Start()
    {
        m_speed = e.GetFloat("speed", 0f);
        if (m_speed == 0f) m_speed = 15f;
        if (e.HasFlag(2)) m_speed = -m_speed;
        m_axis = e.HasFlag(4) ? new Vector3(1, 0, 0) : e.HasFlag(8) ? new Vector3(0, 0, -1) : new Vector3(0, 1, 0);
        m_on = e.HasFlag(1);
        m_cur = m_on ? m_speed : 0f;
        if (m_on) Sound(e.Get("message"));
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        m_on = type == BspUseType.On ? true : type == BspUseType.Off ? false : !m_on;
        if (m_on) Sound(e.Get("message"));
    }

    public override void Update(float dt)
    {
        float target = m_on ? m_speed : 0f;
        // Acc/Dcc spins up and down; otherwise it is at speed at once.
        m_cur = e.HasFlag(16) ? Mathf.MoveTowards(m_cur, target, Mathf.Abs(m_speed) * 0.5f * dt) : target;
        if (m_cur == 0f) return;
        m_angle = (m_angle + m_cur * dt) % 360f;
        e.transform.rotation = Quaternion.AngleAxis(m_angle, m_axis) * e.startRotation;
    }
}

/// <summary>func_pendulum: swings back and forth through "distance" degrees.</summary>
public class BspPendulum : BspBehaviour
{
    float m_t;
    bool m_on;
    Vector3 m_axis;
    public override bool Solid => !e.HasFlag(8);
    public override void Start()
    {
        m_axis = e.HasFlag(64) ? new Vector3(1, 0, 0) : e.HasFlag(128) ? new Vector3(0, 0, -1) : new Vector3(0, 1, 0);
        m_on = e.HasFlag(1);
    }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) => m_on = !m_on;
    public override void Update(float dt)
    {
        if (!m_on) return;
        float dist = e.GetFloat("distance", 90f), speed = Mathf.Max(1f, e.GetFloat("speed", 100f));
        m_t += dt * speed / Mathf.Max(1f, dist);
        float a = Mathf.Sin(m_t * Mathf.PI) * dist * 0.5f;
        e.transform.rotation = Quaternion.AngleAxis(a, m_axis) * e.startRotation;
    }
}

/// <summary>func_breakable: glass, crates, vents. Breaks when triggered, or
/// when the player hits it (left mouse button, at arm's length).</summary>
public class BspBreakable : BspBehaviour
{
    bool m_broken;
    public override bool Solid => !m_broken && !e.killed;

    public bool CanBeHit => !m_broken && !e.HasFlag(1);

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value) => Break(activator);

    public void Hit(BspEntity activator, float damage)
    {
        if (!CanBeHit) return;
        float hp = e.GetFloat("health", 1f);
        if (hp <= 0f) return;                 // unbreakable
        Break(activator);
    }

    public void Break(BspEntity activator)
    {
        if (m_broken) return;
        m_broken = true;
        int material = e.GetInt("material", 0);
        string snd;
        switch (material)
        {
            case 0: case 7: snd = "debris/bustglass" + (1 + (int)(Elintria.Random.value * 2.99f)) + ".wav"; break;
            case 1: snd = "debris/bustcrate" + (1 + (int)(Elintria.Random.value * 2.99f)) + ".wav"; break;
            case 2: case 6: snd = "debris/bustmetal" + (1 + (int)(Elintria.Random.value * 1.99f)) + ".wav"; break;
            case 3: snd = "debris/bustflesh1.wav"; break;
            default: snd = "debris/bustconcrete" + (1 + (int)(Elintria.Random.value * 1.99f)) + ".wav"; break;
        }
        Sound(snd);
        BspEffects.Debris(e, material);
        e.FireTargets(activator);
        SetVisible(false);
    }
}

public class BspWallToggle : BspBehaviour
{
    bool m_on = true;
    public override bool Solid => m_on && !e.killed;
    public override void Start() { m_on = !e.HasFlag(1); SetVisible(m_on); }
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        m_on = type == BspUseType.On ? true : type == BspUseType.Off ? false : !m_on;
        SetVisible(m_on);
    }
}

public class BspIllusionary : BspBehaviour
{
    public override bool Solid => false;
}

public class BspLadder : BspBehaviour
{
    public override bool Solid => false;
    public override int ContentsAt(Vector3 hl) => Inside(hl) ? BspCollision.ContentsLadder : BspCollision.ContentsEmpty;
}

public class BspConveyor : BspBehaviour
{
    public override bool Solid => !e.HasFlag(2);
    /// <summary>How fast it carries what stands on it, Half-Life units/s.</summary>
    public Vector3 Push => Hl.MoveDir(e) * e.GetFloat("speed", 100f);
}

public class BspCharger : BspBehaviour
{
    float m_next;
    public override bool Solid => true;
    public override bool Usable => true;
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (Now < m_next) return;
        m_next = Now + 0.5f;
        Sound(string.Equals(e.className, "func_healthcharger", StringComparison.OrdinalIgnoreCase) ? "items/medshot4.wav" : "items/suitcharge1.wav");
        if (World.player != null) World.player.Charge(string.Equals(e.className, "func_healthcharger", StringComparison.OrdinalIgnoreCase));
    }
}
