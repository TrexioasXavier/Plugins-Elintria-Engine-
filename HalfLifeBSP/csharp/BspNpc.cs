// ─────────────────────────────────────────────────────────────────────────────
//  Monsters and the sequences that direct them
//
//  A monster stands in its idle, walks its path_corner route when it has one,
//  follows the player when used (scientists and guards), and is taken over by
//  scripted_sequence / aiscripted_sequence entities: walked or run to the
//  sequence's spot (through the AI node graph when there is one), turned to
//  face its angle, played its idle until triggered and then its action, after
//  which the sequence fires its target. scripted_sentence makes it talk.
//
//  The game moves a monster by its animation's ground speed; so does this.
//  Positions are kept on the floor with the map's own hull.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Elintria;

public class BspNpc : BspBehaviour
{
    public enum Mode { Idle, WalkPath, Follow, MoveTo, Scripted, Dead }
    public Mode mode = Mode.Idle;

    BspStudioModel m_model;
    float m_yaw, m_yawTarget;
    BspEntity m_corner;               // path_corner being walked to
    List<Vector3> m_route;            // scene positions
    int m_routeIndex;
    float m_moveSpeed;                // Half-Life units/s
    string m_moveAnim;
    Action m_onArrive;
    BspScriptedSequence m_script;
    bool m_dead, m_flyer;
    float m_nextThink;

    public bool IsBusy => mode == Mode.Scripted || mode == Mode.MoveTo;
    public BspStudioModel Model => m_model;
    public override bool Usable => !m_dead && (IsTalker || mode == Mode.Follow);
    bool IsTalker
    {
        get
        {
            string c = e.className.ToLowerInvariant();
            return c == "monster_scientist" || c == "monster_barney" || c == "monster_sitting_scientist" || c == "hostage_entity";
        }
    }

    public override void Start()
    {
        m_model = e.gameObject.GetComponent<BspStudioModel>();
        m_yaw = m_yawTarget = Hl.ModelYaw(e.transform.rotation);
        string c = e.className.ToLowerInvariant();
        m_dead = c.EndsWith("_dead") || c == "monster_hevsuit_dead";
        m_flyer = c == "monster_alien_controller" || c == "monster_flyer_flock" || c == "monster_apache" ||
                  c == "monster_osprey" || c == "monster_ichthyosaur" || c == "monster_leech" || c == "monster_nihilanth";
        if (m_dead) mode = Mode.Dead;
        // "WaitForScript": stays put until a sequence wants it.
    }

    public override void MapStarted()
    {
        if (m_dead || mode == Mode.Scripted) return;
        if (!string.IsNullOrEmpty(e.target) && !e.HasFlag(128))
        {
            BspEntity corner = BspEntity.Find(e.target);
            if (corner != null && string.Equals(corner.className, "path_corner", StringComparison.OrdinalIgnoreCase))
                WalkPath(corner);
        }
    }

    // ── Animation ────────────────────────────────────────────────────────────
    public bool PlayAnim(string seq, bool loop = true, float blend = 0.2f)
    {
        if (m_model == null || string.IsNullOrEmpty(seq)) return false;
        return m_model.Play(seq, loop, 1f, blend);
    }

    string Pick(params string[] names)
    {
        if (m_model == null) return null;
        string[] all = m_model.Sequences;
        foreach (string n in names)
            foreach (string s in all)
                if (string.Equals(s, n, StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    public string IdleAnim => Pick(m_model != null ? m_model.sequence : "idle", "idle1", "idle", "idle2", "look_idle", "deep_idle") ?? (m_model != null ? m_model.sequence : null);
    public string WalkAnim => Pick("walk", "walk1", "walk2", "walk_scared", "crawl") ?? IdleAnim;
    public string RunAnim => Pick("run", "run1", "run2", "runscared") ?? WalkAnim;

    // ── Movement ─────────────────────────────────────────────────────────────
    /// <summary>Walks (or runs) to |hlTarget|, through the node graph when the
    /// straight line is blocked, then calls |onArrive|.</summary>
    public void MoveTo(Vector3 hlTarget, bool run, Action onArrive, Mode asMode = Mode.MoveTo)
    {
        m_onArrive = onArrive;
        m_moveAnim = run ? RunAnim : WalkAnim;
        mode = asMode;
        m_route = null;
        Vector3 here = e.HlPosition;
        if (World != null && World.graph != null && !Clear(here, hlTarget))
        {
            m_route = World.graph.Route(e.transform.position, Hl.ToScene(hlTarget), BspNodeGraph.LinkHuman, m_flyer);
        }
        if (m_route == null) m_route = new List<Vector3> { Hl.ToScene(hlTarget) };
        m_routeIndex = 0;
        PlayAnim(m_moveAnim, true);
        m_moveSpeed = 0f;
    }

    bool Clear(Vector3 a, Vector3 b)
    {
        if (World == null || World.collision == null) return true;
        BspTrace t = World.Trace(a + new Vector3(0, 0, 8), b + new Vector3(0, 0, 8), 0, e);
        return t.fraction >= 0.99f;
    }

    public void WalkPath(BspEntity corner)
    {
        m_corner = corner;
        if (corner == null) { Idle(); return; }
        MoveTo(corner.origin, false, OnCorner, Mode.WalkPath);
    }

    void OnCorner()
    {
        BspEntity c = m_corner;
        if (c == null) { Idle(); return; }
        string msg = c.Get("message");
        if (!string.IsNullOrEmpty(msg)) World.FireTargets(msg, e, c);
        float wait = c.GetFloat("wait", 0f);
        BspEntity next = BspEntity.Find(c.target);
        if (next == null || c.HasFlag(1)) { Idle(); return; }
        if (wait > 0f)
        {
            Idle();
            m_nextThink = Now + wait;
            m_corner = next;
            mode = Mode.WalkPath;
            m_route = null;
            return;
        }
        WalkPath(next);
    }

    public void Idle()
    {
        mode = Mode.Idle;
        m_route = null;
        PlayAnim(IdleAnim, true);
    }

    public void Follow(bool on)
    {
        if (on) { mode = Mode.Follow; Sound(e.className.Contains("barney") ? "barney/ba_ok.wav" : "scientist/okgetaway.wav"); }
        else Idle();
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (m_dead || mode == Mode.Scripted) return;
        if (activator != null && activator.className == "player" && IsTalker)
        {
            Follow(mode != Mode.Follow);
            return;
        }
        // A monster with a TriggerTarget fires it when used by a trigger.
    }

    public override void Update(float dt)
    {
        if (m_dead) return;
        switch (mode)
        {
            case Mode.WalkPath:
                if (m_route == null && m_corner != null && Now >= m_nextThink) WalkPath(m_corner);
                else FollowRoute(dt);
                break;
            case Mode.MoveTo:
                FollowRoute(dt);
                break;
            case Mode.Follow:
                FollowPlayer(dt);
                break;
            case Mode.Scripted:
                if (m_route != null) FollowRoute(dt);
                break;
        }
        // Turn smoothly toward where it wants to face.
        m_yaw += Mathf.Clamp(Hl.AngleDelta(m_yaw, m_yawTarget), -270f * dt, 270f * dt);
        e.transform.rotation = Hl.ModelRotation(m_yaw);
    }

    public void FaceYaw(float yaw, bool instant = false)
    {
        m_yawTarget = yaw;
        if (instant) { m_yaw = yaw; e.transform.rotation = Hl.ModelRotation(m_yaw); }
    }

    void FollowRoute(float dt)
    {
        if (m_route == null) return;
        if (m_routeIndex >= m_route.Count)
        {
            m_route = null;
            Action a = m_onArrive;
            m_onArrive = null;
            if (a != null) a(); else Idle();
            return;
        }
        Vector3 here = e.HlPosition;
        Vector3 goal = Hl.ToHl(m_route[m_routeIndex]);
        Vector3 flat = goal - here;
        if (!m_flyer) flat.z = 0f;
        float dist = flat.magnitude;
        if (m_moveSpeed <= 0f)
        {
            float gs = m_model != null ? m_model.GroundSpeed / Hl.Scale : 0f;
            m_moveSpeed = gs > 1f ? gs : (m_moveAnim == RunAnim ? 220f : 70f);
        }
        float step = m_moveSpeed * dt;
        if (dist <= Math.Max(step, 4f))
        {
            ++m_routeIndex;
            return;
        }
        FaceYaw(Hl.YawOf(flat));
        Vector3 next = here + flat * (step / dist);
        if (!m_flyer) next = OnFloor(here, next);
        e.transform.position = Hl.ToScene(next);
    }

    // Steps up and down stairs, as the hull lets a walker.
    Vector3 OnFloor(Vector3 from, Vector3 to)
    {
        if (World == null || World.collision == null) return to;
        BspTrace t = World.Trace(to + new Vector3(0, 0, 18), to - new Vector3(0, 0, 48), 1, e, true);
        if (t.startSolid) return to;
        if (t.fraction < 1f) to.z = t.endPos.z;
        return to;
    }

    void FollowPlayer(float dt)
    {
        var p = World != null ? World.player : null;
        if (p == null) return;
        Vector3 here = e.HlPosition, there = p.HlOrigin;
        Vector3 d = there - here;
        d.z = 0f;
        float dist = d.magnitude;
        FaceYaw(Hl.YawOf(d));
        if (dist < 96f)
        {
            if (m_route != null || m_model == null || m_model.sequence != IdleAnim) { m_route = null; PlayAnim(IdleAnim, true); }
            return;
        }
        if (m_route == null || Now >= m_nextThink)
        {
            m_nextThink = Now + 1f;
            bool run = dist > 300f;
            Action back = () => { mode = Mode.Follow; };
            MoveTo(there - d.normalized * 64f, run, back, Mode.Follow);
        }
        FollowRoute(dt);
    }

    // ── Scripts ──────────────────────────────────────────────────────────────
    internal bool Possess(BspScriptedSequence s)
    {
        if (m_dead || (m_script != null && m_script != s && m_script.NoInterruptions)) return false;
        if (m_script != null && m_script != s) m_script.Release(false);
        m_script = s;
        mode = Mode.Scripted;
        m_route = null;
        return true;
    }

    internal void Release(BspScriptedSequence s)
    {
        if (m_script != s) return;
        m_script = null;
        Idle();
    }

    internal void ScriptMove(Vector3 hlTarget, bool run, Action arrived)
    {
        MoveTo(hlTarget, run, arrived, Mode.Scripted);
    }

    internal void ScriptStop() { m_route = null; }

    public override void OnKilled() { if (m_script != null) m_script.Release(false); }
}

/// <summary>scripted_sequence / aiscripted_sequence: the map directing a monster.</summary>
public class BspScriptedSequence : BspBehaviour
{
    enum Stage { Waiting, Moving, Idling, Playing, Done }
    Stage m_stage = Stage.Waiting;
    BspNpc m_npc;
    BspEntity m_activator;
    bool m_triggered;
    float m_playUntil;

    public bool NoInterruptions => e.HasFlag(32);
    public BspNpc Monster => m_npc;

    public override void MapStarted()
    {
        // With an idle animation (or nothing to wait for), the monster is
        // taken at once and waits in it until the sequence is triggered.
        if (!string.IsNullOrEmpty(e.Get("m_iszIdle")) || string.IsNullOrEmpty(e.targetName))
            Begin(null, string.IsNullOrEmpty(e.targetName));
    }

    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        m_activator = activator;
        if (m_stage == Stage.Done && !e.HasFlag(4)) return;          // not repeatable
        if (m_stage == Stage.Waiting || m_stage == Stage.Done) Begin(activator, true);
        else { m_triggered = true; if (m_stage == Stage.Idling) Play(); }
    }

    BspNpc FindMonster()
    {
        string name = e.Get("m_iszEntity");
        if (string.IsNullOrEmpty(name)) return null;
        BspNpc best = null;
        float bestD = float.MaxValue;
        float radius = e.GetFloat("m_flRadius", 512f);
        foreach (BspEntity m in World.All)
        {
            if (m.killed) continue;
            var npc = m.behaviour as BspNpc;
            if (npc == null) continue;
            bool byName = string.Equals(m.targetName, name, StringComparison.OrdinalIgnoreCase);
            bool byClass = string.Equals(m.className, name, StringComparison.OrdinalIgnoreCase);
            if (!byName && !byClass) continue;
            float d = (m.HlPosition - e.origin).magnitude;
            if (byClass && d > radius) continue;
            if (d < bestD) { bestD = d; best = npc; }
        }
        return best;
    }

    void Begin(BspEntity activator, bool triggered)
    {
        m_npc = FindMonster();
        if (m_npc == null)
        {
            if (triggered) Debug.LogWarning("[BSP] " + e + ": no monster '" + e.Get("m_iszEntity") + "' to direct.");
            return;
        }
        if (!m_npc.Possess(this)) return;
        m_triggered = triggered;
        // m_fMoveTo: 0 no, 1 walk, 2 run, 4 instant, 5 turn to face.
        int moveTo = e.GetInt("m_fMoveTo", 0);
        if (moveTo == 1 || moveTo == 2)
        {
            m_stage = Stage.Moving;
            m_npc.ScriptMove(e.origin, moveTo == 2, Arrived);
        }
        else
        {
            if (moveTo == 4) m_npc.e.transform.position = Hl.ToScene(e.origin);
            Arrived();
        }
    }

    void Arrived()
    {
        if (m_npc == null) return;
        int moveTo = e.GetInt("m_fMoveTo", 0);
        if (moveTo != 0) m_npc.FaceYaw(e.angles.y, moveTo == 4);
        string idle = e.Get("m_iszIdle");
        if (!m_triggered && !string.IsNullOrEmpty(idle))
        {
            m_stage = Stage.Idling;
            m_npc.PlayAnim(idle, true);
            return;
        }
        Play();
    }

    void Play()
    {
        string play = e.Get("m_iszPlay");
        if (string.IsNullOrEmpty(play)) { Finish(); return; }
        m_stage = Stage.Playing;
        if (!m_npc.PlayAnim(play, false, 0.1f))
        {
            Debug.LogWarning("[BSP] " + e + ": the monster has no sequence '" + play + "'.");
            Finish();
            return;
        }
        m_playUntil = Now + 30f;     // a guard; the clip's end ends it
    }

    public override void Update(float dt)
    {
        if (m_stage != Stage.Playing || m_npc == null) return;
        if (m_npc.Model == null || m_npc.Model.Finished || Now > m_playUntil) Finish();
    }

    void Finish()
    {
        m_stage = Stage.Done;
        BspNpc npc = m_npc;
        // The animation walked the monster somewhere; the game leaves it there.
        if (npc != null && npc.Model != null && !e.HasFlag(128))
        {
            float gs = npc.Model.GroundSpeed;
            if (gs > 0.01f)
            {
                float len = npc.Model.Model.length;
                Vector3 fwd = Hl.Forward(new Vector3(0, Hl.ModelYaw(npc.e.transform.rotation), 0));
                npc.e.transform.position += Hl.DirToScene(fwd) * gs * len;
            }
        }
        e.FireTargets(m_activator);
        if (npc != null)
        {
            if (e.HasFlag(8)) { /* leave corpse: stays on its last frame */ }
            else npc.Release(this);
        }
    }

    internal void Release(bool fire)
    {
        if (fire) e.FireTargets(m_activator);
        m_stage = Stage.Done;
        m_npc = null;
    }
}

/// <summary>scripted_sentence: a monster says a line.</summary>
public class BspScriptedSentence : BspBehaviour
{
    float m_nextAllowed;
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        if (Now < m_nextAllowed) return;
        string who = e.Get("entity");
        BspEntity speaker = null;
        float bestD = float.MaxValue;
        foreach (BspEntity m in World.All)
        {
            if (m.killed || !(m.behaviour is BspNpc)) continue;
            if (!string.Equals(m.targetName, who, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(m.className, who, StringComparison.OrdinalIgnoreCase)) continue;
            float d = (m.HlPosition - e.origin).magnitude;
            if (d < bestD) { bestD = d; speaker = m; }
        }
        string sentence = e.Get("sentence");
        Vector3 at = speaker != null ? speaker.transform.position : e.transform.position;
        float len = BspSentencePlayer.PlayAt(sentence, at, Mathf.Clamp01(e.GetFloat("volume", 10f) / 10f), speaker != null ? speaker.gameObject : null);
        float refire = e.GetFloat("refire", 3f);
        m_nextAllowed = Now + Math.Max(refire, len);
        float dur = e.GetFloat("duration", len);
        if (!string.IsNullOrEmpty(e.target)) World.ScheduleTargets(Math.Max(0f, dur), e.target, activator, e);
        if (!e.HasFlag(1)) { }      // "Fire Once" is flag 1
        else e.Kill();
    }
}

/// <summary>monstermaker: shows a copy of the monster it makes when triggered.</summary>
public class BspMonsterMaker : BspBehaviour
{
    int m_made;
    public override void Use(BspEntity activator, BspEntity caller, BspUseType type, float value)
    {
        int max = e.GetInt("monstercount", 1);
        if (max > 0 && m_made >= max) return;
        ++m_made;
        Debug.Log("[BSP] " + e + " makes a " + e.Get("monstertype") + " (" + m_made + ").");
        // Spawned monsters share the map's existing model objects when one of
        // that class waits hidden under this maker's name.
        string child = e.Get("netname");
        if (!string.IsNullOrEmpty(child))
            foreach (BspEntity m in BspEntity.FindAll(child))
                if (m.gameObject != null && !m.gameObject.activeSelf) { m.gameObject.SetActive(true); break; }
        e.FireTargets(activator);
    }
}
