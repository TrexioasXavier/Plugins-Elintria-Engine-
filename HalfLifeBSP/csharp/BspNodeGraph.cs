// ─────────────────────────────────────────────────────────────────────────────
//  BspNodeGraph - the AI nodes monsters walk between
//
//  The importer writes the map's node graph (from its .nod, or worked out from
//  the map when there is none) as <map>.hlgraph next to its geometry: node
//  positions in scene units and the links between them, with the hull sizes
//  each link allows. Monsters find routes through it with A*.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using Elintria;

public sealed class BspNodeGraph
{
    public const int LinkSmall = 1, LinkHuman = 2, LinkLarge = 4, LinkFly = 8, LinkDisabled = 16;
    public const int NodeLand = 1, NodeAir = 2, NodeWater = 4;

    public struct Node { public Vector3 position; public int info; public int hint; public float hintYaw; }
    public struct Link { public int a, b; public int info; public string blocker; }

    public readonly List<Node> nodes = new List<Node>();
    public readonly List<Link> links = new List<Link>();
    public string source = "";
    readonly List<List<int>> m_adjacent = new List<List<int>>();   // link indices per node

    public static BspNodeGraph Load(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath)) return null;
        string abs;
        try { abs = Project.Resolve(projectPath); } catch (Exception) { return null; }
        if (!System.IO.File.Exists(abs)) return null;
        var g = new BspNodeGraph();
        foreach (string raw in System.IO.File.ReadAllLines(abs))
        {
            string[] p = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) continue;
            if (p[0] == "source" && p.Length > 1) g.source = p[1];
            else if (p[0] == "node" && p.Length >= 6)
            {
                var n = new Node
                {
                    position = new Vector3(F(p[2]), F(p[3]), F(p[4])),
                    info = (int)F(p[5]),
                    hint = p.Length > 6 ? (int)F(p[6]) : 0,
                    hintYaw = p.Length > 7 ? F(p[7]) : 0f,
                };
                g.nodes.Add(n);
            }
            else if (p[0] == "link" && p.Length >= 4)
                g.links.Add(new Link { a = (int)F(p[1]), b = (int)F(p[2]), info = (int)F(p[3]), blocker = p.Length > 4 && p[4] != "-" ? p[4] : null });
        }
        for (int i = 0; i < g.nodes.Count; ++i) g.m_adjacent.Add(new List<int>());
        for (int i = 0; i < g.links.Count; ++i)
        {
            Link l = g.links[i];
            if (l.a < 0 || l.b < 0 || l.a >= g.nodes.Count || l.b >= g.nodes.Count) continue;
            g.m_adjacent[l.a].Add(i);
            g.m_adjacent[l.b].Add(i);
        }
        return g;
    }

    static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>The ground node nearest |scenePos| (|air| for flyers), or -1.</summary>
    public int Nearest(Vector3 scenePos, bool air = false)
    {
        int best = -1;
        float bd = float.MaxValue;
        for (int i = 0; i < nodes.Count; ++i)
        {
            bool isAir = (nodes[i].info & NodeAir) != 0;
            if (isAir != air) continue;
            float d = (nodes[i].position - scenePos).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>The nodes from |from| to |to| (scene positions, the end
    /// position last), or null when no route joins them. |hullBit| is the
    /// link kind the walker needs (LinkHuman for a person).</summary>
    public List<Vector3> Route(Vector3 from, Vector3 to, int hullBit = LinkHuman, bool air = false)
    {
        int a = Nearest(from, air), b = Nearest(to, air);
        if (a < 0 || b < 0) return null;
        var route = new List<Vector3>();
        if (a == b) { route.Add(nodes[a].position); route.Add(to); return route; }

        int n = nodes.Count;
        var g = new float[n];
        var prev = new int[n];
        var closed = new bool[n];
        for (int i = 0; i < n; ++i) { g[i] = float.MaxValue; prev[i] = -1; }
        g[a] = 0f;
        var open = new List<int> { a };
        while (open.Count > 0)
        {
            int bi = 0;
            float bf = float.MaxValue;
            for (int i = 0; i < open.Count; ++i)
            {
                float f = g[open[i]] + Vector3.Distance(nodes[open[i]].position, nodes[b].position);
                if (f < bf) { bf = f; bi = i; }
            }
            int cur = open[bi];
            open.RemoveAt(bi);
            if (cur == b) break;
            if (closed[cur]) continue;
            closed[cur] = true;
            foreach (int li in m_adjacent[cur])
            {
                Link l = links[li];
                if ((l.info & LinkDisabled) != 0) continue;
                if (hullBit != 0 && (l.info & hullBit) == 0 && (l.info & (LinkSmall | LinkHuman | LinkLarge | LinkFly)) != 0) continue;
                if (!string.IsNullOrEmpty(l.blocker) && BlockedBy(l.blocker)) continue;
                int nb = l.a == cur ? l.b : l.a;
                if (closed[nb]) continue;
                float ng = g[cur] + Vector3.Distance(nodes[cur].position, nodes[nb].position);
                if (ng < g[nb]) { g[nb] = ng; prev[nb] = cur; if (!open.Contains(nb)) open.Add(nb); }
            }
        }
        if (prev[b] < 0) return null;
        var stack = new List<int>();
        for (int c = b; c >= 0; c = prev[c]) stack.Add(c);
        stack.Reverse();
        foreach (int i in stack) route.Add(nodes[i].position);
        route.Add(to);
        return route;
    }

    // A link through a door is blocked while that door is closed.
    static bool BlockedBy(string model)
    {
        var w = BspWorld.Instance;
        if (w == null) return false;
        foreach (BspEntity e in w.All)
        {
            if (e.model != model) continue;
            var door = e.behaviour as BspDoor;
            return door != null && !door.IsOpen && !door.MonstersCanOpen;
        }
        return false;
    }
}
