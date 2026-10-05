// ─────────────────────────────────────────────────────────────────────────────
//  BspCollision - the map's own collision, read from its .bsp
//
//  A compiled map carries four collision hulls: the BSP tree itself (a point),
//  and clip-node trees already expanded by a standing player's box, a large
//  monster's and a crouching player's. Tracing a box through the map is then
//  tracing a point through the matching hull - exactly what the game does, so
//  stairs, ledges and doorways feel the way the level designer built them.
//
//  Brush entities (doors, trains, platforms) have hulls of their own, placed
//  wherever the entity is now: a trace is carried into the model's frame and
//  its result carried back.
//
//  Everything here is in Half-Life units.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using Elintria;

public struct BspTrace
{
    public float fraction;
    public Vector3 endPos;
    public Vector3 normal;
    public float planeDist;
    public bool startSolid, allSolid, inOpen, inWater;
    public BspEntity entity;           // the brush entity hit, or null for the world
}

public sealed class BspCollision
{
    public const int ContentsEmpty = -1, ContentsSolid = -2, ContentsWater = -3, ContentsSlime = -4,
                     ContentsLava = -5, ContentsSky = -6, ContentsLadder = -16;
    const float DistEpsilon = 0.03125f;

    struct Plane { public Vector3 n; public float d; public int type; }
    struct Clip { public int plane; public int c0, c1; }
    public struct Model { public Vector3 mins, maxs, origin; public int h0, h1, h2, h3; }

    Plane[] m_planes;
    Clip[] m_clip;          // the map's clip nodes (hulls 1-3)
    Clip[] m_hull0;         // the drawing tree as clip nodes (hull 0)
    int m_root;             // the head node of the trace in progress
    public Model[] models;

    public static readonly Vector3[] HullMins = {
        new Vector3(0, 0, 0), new Vector3(-16, -16, -36), new Vector3(-32, -32, -32), new Vector3(-16, -16, -18) };
    public static readonly Vector3[] HullMaxs = {
        new Vector3(0, 0, 0), new Vector3(16, 16, 36), new Vector3(32, 32, 32), new Vector3(16, 16, 18) };

    public bool Loaded { get; private set; }
    public string Error { get; private set; }

    public bool Load(string absPath)
    {
        byte[] d;
        try { d = System.IO.File.ReadAllBytes(absPath); }
        catch (Exception e) { Error = "Could not read " + absPath + ": " + e.Message; return false; }
        if (d.Length < 124) { Error = "Not a map."; return false; }
        int version = BitConverter.ToInt32(d, 0);
        if (version != 30 && version != 29) { Error = "BSP version " + version + " is not Half-Life's."; return false; }
        int[] ofs = new int[15], len = new int[15];
        for (int l = 0; l < 15; ++l) { ofs[l] = BitConverter.ToInt32(d, 4 + l * 8); len[l] = BitConverter.ToInt32(d, 8 + l * 8); }
        // Blue Shift keeps planes in lump 0 and entities in lump 1.
        if (len[0] % 20 == 0 && len[0] > 0 && d[ofs[0]] != (byte)'{' && len[1] > 0 && d[ofs[1]] == (byte)'{')
        {
            int t = ofs[0]; ofs[0] = ofs[1]; ofs[1] = t;
            t = len[0]; len[0] = len[1]; len[1] = t;
        }
        for (int l = 0; l < 15; ++l)
            if (ofs[l] < 0 || len[l] < 0 || (long)ofs[l] + len[l] > d.Length) { Error = "The map is truncated."; return false; }

        int np = len[1] / 20;
        m_planes = new Plane[np];
        for (int i = 0; i < np; ++i)
        {
            int o = ofs[1] + i * 20;
            m_planes[i].n = new Vector3(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), BitConverter.ToSingle(d, o + 8));
            m_planes[i].d = BitConverter.ToSingle(d, o + 12);
            m_planes[i].type = BitConverter.ToInt32(d, o + 16);
        }
        int nc = len[9] / 8;
        m_clip = new Clip[nc];
        for (int i = 0; i < nc; ++i)
        {
            int o = ofs[9] + i * 8;
            m_clip[i].plane = BitConverter.ToInt32(d, o);
            m_clip[i].c0 = BitConverter.ToInt16(d, o + 4);
            m_clip[i].c1 = BitConverter.ToInt16(d, o + 6);
        }
        // Hull 0: every node becomes a clip node whose leaf children are the
        // leaf's contents (the engine's Mod_MakeHull0).
        int nn = len[5] / 24, nl = len[10] / 28;
        int[] leafContents = new int[nl];
        for (int i = 0; i < nl; ++i) leafContents[i] = BitConverter.ToInt32(d, ofs[10] + i * 28);
        m_hull0 = new Clip[nn];
        for (int i = 0; i < nn; ++i)
        {
            int o = ofs[5] + i * 24;
            m_hull0[i].plane = BitConverter.ToInt32(d, o);
            int a = BitConverter.ToInt16(d, o + 4), b = BitConverter.ToInt16(d, o + 6);
            m_hull0[i].c0 = a >= 0 ? a : (-1 - a < nl ? leafContents[-1 - a] : ContentsSolid);
            m_hull0[i].c1 = b >= 0 ? b : (-1 - b < nl ? leafContents[-1 - b] : ContentsSolid);
        }
        int nm = len[14] / 64;
        models = new Model[nm];
        for (int i = 0; i < nm; ++i)
        {
            int o = ofs[14] + i * 64;
            models[i].mins = new Vector3(BitConverter.ToSingle(d, o), BitConverter.ToSingle(d, o + 4), BitConverter.ToSingle(d, o + 8));
            models[i].maxs = new Vector3(BitConverter.ToSingle(d, o + 12), BitConverter.ToSingle(d, o + 16), BitConverter.ToSingle(d, o + 20));
            models[i].origin = new Vector3(BitConverter.ToSingle(d, o + 24), BitConverter.ToSingle(d, o + 28), BitConverter.ToSingle(d, o + 32));
            models[i].h0 = BitConverter.ToInt32(d, o + 36);
            models[i].h1 = BitConverter.ToInt32(d, o + 40);
            models[i].h2 = BitConverter.ToInt32(d, o + 44);
            models[i].h3 = BitConverter.ToInt32(d, o + 48);
        }
        Loaded = nm > 0;
        if (!Loaded) Error = "The map has no models.";
        return Loaded;
    }

    int HeadNode(int model, int hull)
    {
        Model m = models[model];
        return hull == 0 ? m.h0 : hull == 1 ? m.h1 : hull == 2 ? m.h2 : m.h3;
    }

    Clip[] Nodes(int hull) => hull == 0 ? m_hull0 : m_clip;

    /// <summary>The contents at |p| in |hull| of |model| (model space).</summary>
    public int HullPointContents(int model, int hull, Vector3 p)
    {
        if (!Loaded || model < 0 || model >= models.Length) return ContentsEmpty;
        return PointContents(Nodes(hull), HeadNode(model, hull), p);
    }

    int PointContents(Clip[] nodes, int num, Vector3 p)
    {
        int guard = 0;
        while (num >= 0)
        {
            if (num >= nodes.Length || ++guard > 4096) return ContentsSolid;
            Clip c = nodes[num];
            Plane pl = m_planes[c.plane];
            float dd = pl.type < 3 ? p[pl.type] - pl.d : Vector3.Dot(pl.n, p) - pl.d;
            num = dd < 0f ? c.c1 : c.c0;
        }
        return num;
    }

    /// <summary>Traces a point through |hull| of |model| from |a| to |b|,
    /// both in the model's own space.</summary>
    public BspTrace TraceModel(int model, int hull, Vector3 a, Vector3 b)
    {
        var tr = new BspTrace { fraction = 1f, allSolid = true, endPos = b };
        if (!Loaded || model < 0 || model >= models.Length) { tr.allSolid = false; return tr; }
        Clip[] nodes = Nodes(hull);
        m_root = HeadNode(model, hull);
        RecursiveHullCheck(nodes, m_root, 0f, 1f, a, b, ref tr);
        if (tr.allSolid) tr.startSolid = true;
        if (tr.startSolid) tr.fraction = 0f;
        if (tr.fraction < 1f) tr.endPos = a + (b - a) * tr.fraction;
        return tr;
    }

    bool RecursiveHullCheck(Clip[] nodes, int num, float p1f, float p2f, Vector3 p1, Vector3 p2, ref BspTrace tr)
    {
        if (num < 0)
        {
            if (num != ContentsSolid)
            {
                tr.allSolid = false;
                if (num == ContentsEmpty) tr.inOpen = true;
                else tr.inWater = true;
            }
            else tr.startSolid = true;
            return true;
        }
        if (num >= nodes.Length) return true;
        Clip node = nodes[num];
        Plane plane = m_planes[node.plane];
        float t1, t2;
        if (plane.type < 3) { t1 = p1[plane.type] - plane.d; t2 = p2[plane.type] - plane.d; }
        else { t1 = Vector3.Dot(plane.n, p1) - plane.d; t2 = Vector3.Dot(plane.n, p2) - plane.d; }

        if (t1 >= 0f && t2 >= 0f) return RecursiveHullCheck(nodes, node.c0, p1f, p2f, p1, p2, ref tr);
        if (t1 < 0f && t2 < 0f) return RecursiveHullCheck(nodes, node.c1, p1f, p2f, p1, p2, ref tr);

        // Put the cross point DIST_EPSILON on the near side.
        float frac = t1 < 0f ? (t1 + DistEpsilon) / (t1 - t2) : (t1 - DistEpsilon) / (t1 - t2);
        if (frac < 0f) frac = 0f;
        if (frac > 1f) frac = 1f;
        float midf = p1f + (p2f - p1f) * frac;
        Vector3 mid = p1 + (p2 - p1) * frac;
        int side = t1 < 0f ? 1 : 0;
        int near = side == 0 ? node.c0 : node.c1, far = side == 0 ? node.c1 : node.c0;

        if (!RecursiveHullCheck(nodes, near, p1f, midf, p1, mid, ref tr)) return false;
        if (PointContents(nodes, far, mid) != ContentsSolid)
            return RecursiveHullCheck(nodes, far, midf, p2f, mid, p2, ref tr);
        if (tr.allSolid) return false;

        // The far side is solid: this is where it hit.
        if (side == 0) { tr.normal = plane.n; tr.planeDist = plane.d; }
        else { tr.normal = -plane.n; tr.planeDist = -plane.d; }
        int guard = 0;
        while (PointContents(nodes, m_root, mid) == ContentsSolid && guard++ < 20)
        {
            // Should not happen, but does now and then: back off.
            frac -= 0.1f;
            if (frac < 0f) { tr.fraction = midf; tr.endPos = mid; return false; }
            midf = p1f + (p2f - p1f) * frac;
            mid = p1 + (p2 - p1) * frac;
        }
        tr.fraction = midf;
        tr.endPos = mid;
        return false;
    }
}
