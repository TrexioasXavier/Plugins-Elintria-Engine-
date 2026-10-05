// ─────────────────────────────────────────────────────────────────────────────
//  Half-Life BSP Loader - shared runtime helpers
//
//  Half-Life measures in inches with Z up; a scene is metres with Y up. A
//  point (x, y, z) in the map is (x, z, -y) * scale in the scene. The runtime
//  keeps its physics (hull traces, player movement) in Half-Life units, where
//  the map's numbers - speeds, lips, step heights - mean what the level
//  designer meant, and converts at the edges.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using Elintria;

/// <summary>How an entity is triggered (Half-Life's USE_TYPE).</summary>
public enum BspUseType { Off = 0, On = 1, Set = 2, Toggle = 3 }

public static class Hl
{
    /// <summary>Metres per Half-Life unit, from the world being played.</summary>
    public static float Scale = 0.0254f;

    public static Vector3 ToScene(Vector3 hl) => new Vector3(hl.x * Scale, hl.z * Scale, -hl.y * Scale);
    public static Vector3 ToHl(Vector3 s) => new Vector3(s.x / Scale, -s.z / Scale, s.y / Scale);
    /// <summary>A direction (no scale).</summary>
    public static Vector3 DirToScene(Vector3 hl) => new Vector3(hl.x, hl.z, -hl.y);
    public static Vector3 DirToHl(Vector3 s) => new Vector3(s.x, -s.z, s.y);

    public static float ParseFloat(string s, float def = 0f)
    {
        float v;
        return !string.IsNullOrEmpty(s) && float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
    }

    public static int ParseInt(string s, int def = 0)
    {
        if (string.IsNullOrEmpty(s)) return def;
        int v;
        if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
        float f;
        return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? (int)f : def;
    }

    public static Vector3 ParseVector(string s, Vector3 def)
    {
        if (string.IsNullOrEmpty(s)) return def;
        string[] p = s.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        Vector3 v = def;
        if (p.Length > 0) v.x = ParseFloat(p[0], def.x);
        if (p.Length > 1) v.y = ParseFloat(p[1], def.y);
        if (p.Length > 2) v.z = ParseFloat(p[2], def.z);
        return v;
    }

    /// <summary>Half-Life's AngleVectors forward for (pitch, yaw, roll).</summary>
    public static Vector3 Forward(Vector3 angles)
    {
        float p = angles.x * Mathf.Deg2Rad, y = angles.y * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(p) * Mathf.Cos(y), Mathf.Cos(p) * Mathf.Sin(y), -Mathf.Sin(p));
    }

    public static Vector3 Right(Vector3 angles)
    {
        float y = angles.y * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(y), -Mathf.Cos(y), 0f);
    }

    /// <summary>The direction a door/button moves for its "angles" (a
    /// mover's movedir: angle -1 up, -2 down).</summary>
    public static Vector3 MoveDir(BspEntity e)
    {
        string a = e.Get("angle");
        Vector3 ang = e.angles;
        if (!string.IsNullOrEmpty(a))
        {
            float f = ParseFloat(a);
            if (f == -1f) return new Vector3(0, 0, 1);
            if (f == -2f) return new Vector3(0, 0, -1);
        }
        if (ang.x == -90f && ang.y == 0f) return new Vector3(0, 0, 1);
        if (ang.x == 90f && ang.y == 0f) return new Vector3(0, 0, -1);
        return Forward(ang);
    }

    public static float AngleMod(float a)
    {
        a %= 360f;
        if (a < 0f) a += 360f;
        return a;
    }

    /// <summary>The shortest signed turn from |from| to |to| in degrees.</summary>
    public static float AngleDelta(float from, float to)
    {
        float d = (to - from) % 360f;
        if (d > 180f) d -= 360f;
        if (d < -180f) d += 360f;
        return d;
    }

    public static float YawOf(Vector3 hlDir) => Mathf.Atan2(hlDir.y, hlDir.x) * Mathf.Rad2Deg;

    /// <summary>A scene rotation that turns a model (which faces +X, like
    /// Half-Life's) to |yaw| degrees.</summary>
    public static Quaternion ModelRotation(float yaw) => Quaternion.Euler(0f, yaw, 0f);

    /// <summary>Yaw in Half-Life degrees of a model's scene rotation.</summary>
    public static float ModelYaw(Quaternion q)
    {
        Vector3 f = q * new Vector3(1f, 0f, 0f);      // the model's forward in the scene
        return Mathf.Atan2(-f.z, f.x) * Mathf.Rad2Deg;
    }

    /// <summary>A content file by its Half-Life path ("ambience/drips.wav",
    /// "sound/..." or "models/x.mdl"), as a project-relative path, or null.</summary>
    public static string ContentPath(string hlPath, string kind)
    {
        if (string.IsNullOrEmpty(hlPath)) return null;
        string p = hlPath.Replace('\\', '/').TrimStart('/');
        string root = BspWorld.Instance != null ? BspWorld.Instance.contentRoot : "Assets/BSPContent";
        var candidates = new List<string>();
        if (kind == "sound")
        {
            if (p.StartsWith("sound/", StringComparison.OrdinalIgnoreCase)) p = p.Substring(6);
            candidates.Add(root + "/sounds/" + p);
            candidates.Add(root + "/sound/" + p);
            candidates.Add(root + "/" + p);
        }
        else
        {
            candidates.Add(root + "/" + p);
        }
        foreach (string c in candidates)
        {
            try { if (System.IO.File.Exists(Project.Resolve(c))) return c; }
            catch (Exception) { }
        }
        return null;
    }
}

/// <summary>A sound/sentences.txt line: the words of a sentence.</summary>
public sealed class BspSentence
{
    public string name;
    public readonly List<string> files = new List<string>();     // "scientist/hello.wav" (below sound/)
    public readonly List<float> pitches = new List<float>();

    static Dictionary<string, BspSentence> s_table;

    /// <summary>"!SC_HELLO0" or "SC_HELLO0" - or a group, "SC_HELLO", from
    /// which one is picked at random.</summary>
    public static BspSentence Find(string name)
    {
        Load();
        if (string.IsNullOrEmpty(name)) return null;
        string n = name.TrimStart('!').ToUpperInvariant();
        BspSentence s;
        if (s_table.TryGetValue(n, out s)) return s;
        var group = new List<BspSentence>();
        foreach (var kv in s_table)
        {
            if (!kv.Key.StartsWith(n, StringComparison.Ordinal) || kv.Key.Length == n.Length) continue;
            bool digits = true;
            for (int i = n.Length; i < kv.Key.Length; ++i) digits &= char.IsDigit(kv.Key[i]);
            if (digits) group.Add(kv.Value);
        }
        return group.Count == 0 ? null : group[UnityEngineRandom(group.Count)];
    }

    static int UnityEngineRandom(int n) => (int)(Elintria.Random.value * n) % Math.Max(1, n);

    static void Load()
    {
        if (s_table != null) return;
        s_table = new Dictionary<string, BspSentence>();
        string path = Hl.ContentPath("sentences.txt", "sound");
        if (path == null) return;
        string[] lines;
        try { lines = System.IO.File.ReadAllLines(Project.Resolve(path)); }
        catch (Exception) { return; }
        foreach (string raw in lines)
        {
            string line = raw;
            int cm = line.IndexOf("//", StringComparison.Ordinal);
            if (cm >= 0) line = line.Substring(0, cm);
            line = line.Trim();
            if (line.Length == 0) continue;
            int sp = line.IndexOfAny(new[] { ' ', '\t' });
            if (sp < 0) continue;
            var s = new BspSentence { name = line.Substring(0, sp) };
            string folder = "vox";
            bool first = true;
            float pitch = 1f;
            foreach (string tok0 in line.Substring(sp + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tok = tok0;
                int paren = tok.IndexOf('(');
                string parms = null;
                if (paren >= 0) { parms = tok.Substring(paren + 1).TrimEnd(')'); tok = tok.Substring(0, paren); }
                if (parms != null)
                    foreach (string pr in parms.Split(' '))
                        if (pr.Length > 1 && (pr[0] == 'p' || pr[0] == 'P')) pitch = Hl.ParseFloat(pr.Substring(1), 100f) / 100f;
                if (tok.Length == 0) continue;
                if (first)
                {
                    int sl = tok.IndexOf('/');
                    if (sl >= 0) { folder = tok.Substring(0, sl); tok = tok.Substring(sl + 1); }
                    first = false;
                }
                foreach (string part in tok.Split(','))
                {
                    string w = part.TrimEnd('.');
                    if (w.Length == 0) continue;
                    s.files.Add(folder + "/" + w + ".wav");
                    s.pitches.Add(pitch);
                }
            }
            s_table[s.name.ToUpperInvariant()] = s;
        }
    }
}
