// UnityEngine compatibility - what Elintria genuinely does not have.
//
// Part of the Unity Converter plugin.
//
// ============================================================================
//  This file used to be three times this size
// ============================================================================
//
// It once declared its own Vector3, Quaternion, Mathf, Color, Transform,
// GameObject, Rigidbody, Physics and MonoBehaviour, because the engine had
// none of them in C#. They are all in the Elintria namespace now - real ones,
// backed by the engine rather than approximated here - so this file declares
// none of them, and a converted script says "using Elintria;" and gets the
// genuine article.
//
// What is left is the set of things Unity has and Elintria does not:
// renderers and materials, the UI, the animation system, editor-only drawing,
// and asset loading. These compile and they say so once when called, because
// a converted project should explain itself rather than misbehave quietly.
//
// MonoBehaviour is still here, deriving from ElintriaScript, for Unity code
// pasted in by hand. The converter no longer emits it.

using System;
using System.Collections;
using System.Collections.Generic;
using Elintria;

namespace UnityEngine
{
    // =========================================================================
    //  Diagnostics
    // =========================================================================
    internal static class Shim
    {
        private static readonly HashSet<string> s_said = new HashSet<string>();

        internal static void NotSupported(string api, string detail)
        {
            if (!s_said.Add(api)) return;
            Elintria.Debug.LogWarning(
                "[Unity compat] " + api + " does nothing in Elintria" +
                (string.IsNullOrEmpty(detail) ? "." : " - " + detail));
        }

        internal static void NotSupported(string api) { NotSupported(api, null); }
    }

    // =========================================================================
    //  MonoBehaviour
    //
    //  Only for Unity code brought across by hand. Converted scripts derive
    //  from ElintriaScript directly, which is the same lifecycle without the
    //  extra name in the way.
    // =========================================================================
    public abstract class MonoBehaviour : Elintria.ElintriaScript
    {
    }

    /// <summary>Unity's common base. Elintria has no equivalent - nothing
    /// needs one - so this exists to carry the static lifetime helpers that
    /// code sometimes calls through it.</summary>
    public class Object
    {
        public string name { get; set; } = string.Empty;

        /// <summary>An asset (a clip, a sprite, a material) is "true" while
        /// it exists, as every Unity object is.</summary>
        public static implicit operator bool(Object o) => (object)o != null;

        public int GetInstanceID() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public HideFlags hideFlags { get; set; }

        public static void Destroy(object o) => ElintriaScript.Destroy(o);
        public static void Destroy(object o, float delay) => ElintriaScript.Destroy(o, delay);
        public static void DestroyImmediate(object o) => ElintriaScript.Destroy(o);

        public static void DontDestroyOnLoad(object target)
            => Shim.NotSupported("Object.DontDestroyOnLoad",
                                 "Elintria does not unload a scene out from "
                                 + "under an object, so nothing is lost");

        public static T Instantiate<T>(T original) where T : class
        {
            Elintria.GameObject g = original as Elintria.GameObject;
            return g != null ? ElintriaScript.Instantiate(g) as T : null;
        }

        public static T Instantiate<T>(T original, Vector3 position,
                                       Quaternion rotation) where T : class
        {
            Elintria.GameObject g = original as Elintria.GameObject;
            return g != null ? ElintriaScript.Instantiate(g, position, rotation) as T : null;
        }

        public static T Instantiate<T>(T original, Elintria.Transform parent)
            where T : class
        {
            Elintria.GameObject g = original as Elintria.GameObject;
            return g != null ? ElintriaScript.Instantiate(g, parent) as T : null;
        }

        public static T Instantiate<T>(T original, Vector3 position,
                                       Quaternion rotation,
                                       Elintria.Transform parent) where T : class
        {
            Elintria.GameObject g = original as Elintria.GameObject;
            return g != null
                 ? ElintriaScript.Instantiate(g, position, rotation, parent) as T : null;
        }

        public static T FindObjectOfType<T>() where T : class
            => ElintriaScript.FindObjectOfType<T>();
        public static T[] FindObjectsOfType<T>() where T : class
            => ElintriaScript.FindObjectsOfType<T>();
        public static T FindFirstObjectByType<T>() where T : class
            => ElintriaScript.FindObjectOfType<T>();
        public static T FindAnyObjectByType<T>() where T : class
            => ElintriaScript.FindObjectOfType<T>();
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sort) where T : class
            => ElintriaScript.FindObjectsOfType<T>();
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) where T : class
            => ElintriaScript.FindObjectsOfType<T>();

        public override string ToString() => name;
    }

    // =========================================================================
    //  Attributes Elintria does not read
    //
    //  SerializeField and HideInInspector are Elintria's own and really work;
    //  these are the rest, kept so the intent survives in the file.
    // =========================================================================
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    { public HeaderAttribute(string header) { } }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TooltipAttribute : Attribute
    { public TooltipAttribute(string tooltip) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    { public RangeAttribute(float min, float max) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SpaceAttribute : Attribute
    {
        public SpaceAttribute() { }
        public SpaceAttribute(float height) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TextAreaAttribute : Attribute
    {
        public TextAreaAttribute() { }
        public TextAreaAttribute(int minLines, int maxLines) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponentAttribute : Attribute
    {
        public RequireComponentAttribute(Type a) { }
        public RequireComponentAttribute(Type a, Type b) { }
        public RequireComponentAttribute(Type a, Type b, Type c) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponentAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExecuteInEditModeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExecuteAlwaysAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenuAttribute : Attribute
    {
        public AddComponentMenuAttribute(string menuName) { }
        public AddComponentMenuAttribute(string menuName, int order) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ContextMenuAttribute : Attribute
    { public ContextMenuAttribute(string name) { } }

    // =========================================================================
    //  Enums Unity has and Elintria does not
    // =========================================================================
    public enum SendMessageOptions { RequireReceiver, DontRequireReceiver }
    public enum RuntimePlatform { WindowsPlayer, WindowsEditor }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5,
                                BGRA32 = 14, DXT1 = 10, DXT5 = 12 }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum MeshTopology { Triangles = 0, Quads = 2, Lines = 3, LineStrip = 4, Points = 5 }
    public enum HideFlags { None = 0, HideInHierarchy = 1, HideAndDontSave = 61 }
    public enum LightShadows { None, Hard, Soft }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum WrapMode { Once = 1, Loop = 2, PingPong = 4, ClampForever = 8 }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }

    [Flags]
    public enum MeshColliderCookingOptions
    {
        None = 0, InflateConvexMesh = 1, CookForFasterSimulation = 2,
        EnableMeshCleaning = 4, WeldColocatedVertices = 8, UseFastMidphase = 16
    }

    // CollisionFlags is Elintria's, beside its CharacterController.

    // =========================================================================
    //  InertComponent
    //
    //  The base for everything below. It is deliberately NOT Elintria's
    //  Component: that one wraps a real engine object, and these have nothing
    //  behind them. GetComponent returns null for all of them, exactly as it
    //  did before - which is honest, and is what the import report warns
    //  about for every call site.
    // =========================================================================
    public abstract class InertComponent : Elintria.ManagedComponent
    {
        // "Inert" means the engine does not act on it - it is not drawn, not
        // simulated, not raycast against. It is still a real component:
        // AddComponent builds it, GetComponent finds it again, and it knows
        // which object it is on, because gameObject and transform come from
        // ManagedComponent now. They used to be hard-coded null here, which
        // turned every "canvasGo.AddComponent<Canvas>().enabled = false"
        // into a NullReferenceException.
        public virtual bool enabled { get; set; } = true;
    }

    /// <summary>Unity batches static geometry ahead of time. Elintria does
    /// not, so this is accepted and ignored.</summary>
    public static class StaticBatchingUtility
    {
        public static void Combine(Elintria.GameObject staticBatchRoot) { }
        public static void Combine(Elintria.GameObject[] gos,
                                   Elintria.GameObject staticBatchRoot) { }
    }

    // =========================================================================
    //  Rendering - data only
    // =========================================================================
    public class Renderer : InertComponent
    {
        private Material m_material;

        public bool isVisible => true;
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        public bool receiveShadows = true;
        public Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On;

        public Material material
        {
            get { return m_material ?? (m_material = new Material(Shader.Find("Standard"))); }
            set
            {
                m_material = value;
                Shim.NotSupported("Renderer.material",
                                  "a material set from a script is stored but "
                                  + "not drawn; assign it on the object in the "
                                  + "Elintria editor");
            }
        }

        public Material sharedMaterial { get { return material; } set { material = value; } }
        public Material[] materials { get; set; } = new Material[0];
        public Material[] sharedMaterials { get; set; } = new Material[0];

        // Per-renderer overrides, kept per material slot.
        private readonly Dictionary<int, MaterialPropertyBlock> m_blocks = new Dictionary<int, MaterialPropertyBlock>();
        public void SetPropertyBlock(MaterialPropertyBlock block) { SetPropertyBlock(block, -1); }
        public void SetPropertyBlock(MaterialPropertyBlock block, int materialIndex)
        {
            if (block == null) m_blocks.Remove(materialIndex);
            else m_blocks[materialIndex] = block;
        }
        public void GetPropertyBlock(MaterialPropertyBlock dest) { GetPropertyBlock(dest, -1); }
        public void GetPropertyBlock(MaterialPropertyBlock dest, int materialIndex) { }
        public bool HasPropertyBlock() => m_blocks.Count > 0;

        public bool forceRenderingOff;
        public int sortingOrder, sortingLayerID, rendererPriority;
        public string sortingLayerName = "Default";
        public uint renderingLayerMask = 1;
        public MotionVectorGenerationMode motionVectorGenerationMode = MotionVectorGenerationMode.Object;
        public Rendering.LightProbeUsage lightProbeUsage = Rendering.LightProbeUsage.BlendProbes;
        public Rendering.ReflectionProbeUsage reflectionProbeUsage = Rendering.ReflectionProbeUsage.BlendProbes;
        public int lightmapIndex = -1;
        public Vector4 lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
        public bool allowOcclusionWhenDynamic = true;
        public bool staticShadowCaster;
        public Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
    }

    public class MeshRenderer : Renderer { }

    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh;
        public Elintria.Transform[] bones = new Elintria.Transform[0];
        public Elintria.Transform rootBone;
    }

    public class MeshFilter : InertComponent
    {
        private Mesh m_mesh;

        public Mesh mesh
        {
            get { return m_mesh ?? (m_mesh = new Mesh()); }
            set
            {
                m_mesh = value;

                // This used to be the end of the road for procedural
                // geometry: the arrays were kept and nothing drew them. The
                // engine takes a run-time mesh now, so assigning one here
                // hands the vertices over and the object is drawn with them.
                if (gameObject == null) return;
                if (m_mesh == null)
                {
                    Elintria.RuntimeMesh.Assign(gameObject, null);
                    return;
                }
                Elintria.RuntimeMesh.Assign(gameObject, m_mesh.Upload());
            }
        }

        public Mesh sharedMesh { get { return mesh; } set { mesh = value; } }

        /// <summary>Pushes the current contents of the mesh over again, for
        /// code that fills the arrays after assigning rather than before.
        /// </summary>
        public void Refresh()
        {
            if (gameObject != null && m_mesh != null)
                Elintria.RuntimeMesh.Assign(gameObject, m_mesh.Upload());
        }
    }

    /// <summary>Real storage, no rendering. A script that builds geometry
    /// really builds it - the arrays are kept and RecalculateNormals does the
    /// arithmetic - but there is no way to put the result on screen.</summary>
    public class Mesh
    {
        public string name { get; set; } = "Mesh";

        // The engine-side copy, made the first time this mesh is put on an
        // object and refreshed whenever it is put on one again. Building a
        // mesh is usually "fill the arrays, then assign", so assignment is
        // the natural moment to hand the data over.
        private Elintria.RuntimeMesh m_runtime;

        internal Elintria.RuntimeMesh Upload()
        {
            if (m_runtime == null) m_runtime = new Elintria.RuntimeMesh();

            m_runtime.SetVertices(vertices);
            m_runtime.SetTriangles(triangles);
            if (normals != null && normals.Length == (vertices == null ? 0 : vertices.Length))
                m_runtime.SetNormals(normals);
            if (uv != null && uv.Length == (vertices == null ? 0 : vertices.Length))
                m_runtime.SetUVs(uv);

            return m_runtime;
        }

        /// <summary>A number that identifies this mesh. Code uses it as a
        /// cache key, which works whether or not anything draws it.</summary>
        public int GetInstanceID() => base.GetHashCode();

        public Vector3[] vertices  = new Vector3[0];
        public int[]     triangles = new int[0];
        public Vector2[] uv        = new Vector2[0];
        public Vector2[] uv2       = new Vector2[0];
        public Vector3[] normals   = new Vector3[0];
        public Vector4[] tangents  = new Vector4[0];
        public Color[]   colors    = new Color[0];
        public Color32[] colors32  = new Color32[0];
        public BoneWeight[] boneWeights = new BoneWeight[0];
        public Matrix4x4[]  bindposes   = new Matrix4x4[0];

        public int vertexCount => vertices != null ? vertices.Length : 0;
        public int subMeshCount { get; set; } = 1;
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        public Rendering.IndexFormat indexFormat { get; set; }

        public void Clear()
        {
            vertices = new Vector3[0]; triangles = new int[0];
            uv = new Vector2[0]; normals = new Vector3[0]; colors = new Color[0];
        }

        public void SetVertices(List<Vector3> v) { vertices = v?.ToArray() ?? new Vector3[0]; }
        public void SetTriangles(List<int> t, int submesh) { triangles = t?.ToArray() ?? new int[0]; }
        public void SetTriangles(int[] t, int submesh) { triangles = t ?? new int[0]; }
        public void SetUVs(int channel, List<Vector2> v)
        {
            if (channel == 0) uv = v?.ToArray() ?? new Vector2[0];
            else uv2 = v?.ToArray() ?? new Vector2[0];
        }
        public void SetNormals(List<Vector3> v) { normals = v?.ToArray() ?? new Vector3[0]; }
        public void SetColors(List<Color> v) { colors = v?.ToArray() ?? new Color[0]; }
        public void SetColors(List<Color32> v) { colors32 = v?.ToArray() ?? new Color32[0]; }

        public int[] GetTriangles(int submesh) => triangles;

        /// <summary>Area-weighted vertex normals - the same result Unity
        /// gives, because the algorithm is not a matter of opinion.</summary>
        public void RecalculateNormals()
        {
            if (vertices == null || triangles == null) return;

            Vector3[] n = new Vector3[vertices.Length];
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 ||
                    a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                    continue;

                // Not normalised: the cross product's length is twice the
                // triangle's area, which is exactly the weighting wanted.
                Vector3 face = Vector3.Cross(vertices[b] - vertices[a],
                                             vertices[c] - vertices[a]);
                n[a] = n[a] + face;
                n[b] = n[b] + face;
                n[c] = n[c] + face;
            }

            for (int i = 0; i < n.Length; ++i) n[i] = n[i].normalized;
            normals = n;
        }

        public void RecalculateBounds()
        {
            if (vertices == null || vertices.Length == 0)
            { bounds = new Bounds(Vector3.zero, Vector3.zero); return; }

            Vector3 mn = vertices[0], mx = vertices[0];
            for (int i = 1; i < vertices.Length; ++i)
            {
                mn = Vector3.Min(mn, vertices[i]);
                mx = Vector3.Max(mx, vertices[i]);
            }
            Bounds b = new Bounds();
            b.SetMinMax(mn, mx);
            bounds = b;
        }

        /// <summary>Joins the meshes into this one, each moved by its
        /// transform. With mergeSubMeshes false every input keeps its own
        /// submesh range, which the triangle list records in order.</summary>
        public void CombineMeshes(CombineInstance[] combine, bool mergeSubMeshes = true, bool useMatrices = true,
                                  bool hasLightmapData = false)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<Vector2>();
            var tri = new List<int>();
            foreach (CombineInstance c in combine ?? new CombineInstance[0])
            {
                if (c.mesh == null || c.mesh.vertices == null) continue;
                int b = v.Count;
                Mesh m = c.mesh;
                for (int i = 0; i < m.vertices.Length; ++i)
                {
                    v.Add(useMatrices ? c.transform.MultiplyPoint3x4(m.vertices[i]) : m.vertices[i]);
                    if (m.normals != null && i < m.normals.Length)
                        n.Add(useMatrices ? c.transform.MultiplyVector(m.normals[i]).normalized : m.normals[i]);
                    if (m.uv != null && i < m.uv.Length) t.Add(m.uv[i]);
                }
                foreach (int k in m.triangles ?? new int[0]) tri.Add(b + k);
            }
            vertices = v.ToArray();
            normals = n.Count == v.Count ? n.ToArray() : new Vector3[0];
            uv = t.Count == v.Count ? t.ToArray() : new Vector2[0];
            triangles = tri.ToArray();
            subMeshCount = mergeSubMeshes ? 1 : Math.Max(1, (combine ?? new CombineInstance[0]).Length);
            RecalculateBounds();
        }

        public void RecalculateTangents() { }
        public void Optimize() { }
        public void UploadMeshData(bool markNoLongerReadable) { }
        public void MarkDynamic() { }
    }

    [Serializable]
    public struct BoneWeight
    {
        public int   boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }

    public class Material
    {
        private readonly Dictionary<string, object> m_props = new Dictionary<string, object>();

        public string name { get; set; } = "Material";
        public Shader shader;
        public Color color
        { get { return GetColor("_Color"); } set { SetColor("_Color", value); } }
        public Texture mainTexture
        { get { return GetTexture("_MainTex"); } set { SetTexture("_MainTex", value); } }
        public Vector2 mainTextureScale { get; set; } = new Vector2(1f, 1f);
        public Vector2 mainTextureOffset { get; set; } = new Vector2(0f, 0f);
        public int renderQueue { get; set; }

        public Material(Shader s) { shader = s; name = s != null ? s.name : "Material"; }
        public Material(Material src)
        {
            if (src == null) return;
            shader = src.shader;
            name = src.name;
            foreach (KeyValuePair<string, object> kv in src.m_props) m_props[kv.Key] = kv.Value;
        }

        public void SetFloat(string n, float v)     { m_props[n] = v; }
        public void SetInt(string n, int v)         { m_props[n] = v; }
        public void SetColor(string n, Color v)     { m_props[n] = v; }
        public void SetVector(string n, Vector4 v)  { m_props[n] = v; }
        public void SetTexture(string n, Texture v) { m_props[n] = v; }
        public void SetMatrix(string n, Matrix4x4 v){ m_props[n] = v; }

        public float GetFloat(string n)
        { object o; return m_props.TryGetValue(n, out o) && o is float ? (float)o : 0f; }
        public int GetInt(string n)
        { object o; return m_props.TryGetValue(n, out o) && o is int ? (int)o : 0; }
        public Color GetColor(string n)
        { object o; return m_props.TryGetValue(n, out o) && o is Color ? (Color)o : Color.white; }
        public Vector4 GetVector(string n)
        { object o; return m_props.TryGetValue(n, out o) && o is Vector4 ? (Vector4)o
                                                                         : new Vector4(0f,0f,0f,0f); }
        public Texture GetTexture(string n)
        { object o; return m_props.TryGetValue(n, out o) ? o as Texture : null; }

        public bool HasProperty(string n) => m_props.ContainsKey(n);
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
        public void SetOverrideTag(string tag, string val) { }
    }

    public class Shader
    {
        private static readonly Dictionary<string, Shader> s_known = new Dictionary<string, Shader>();

        public string name { get; private set; }
        private Shader(string n) { name = n; }

        /// <summary>A named placeholder rather than null: code very often
        /// does "new Material(Shader.Find(...))", and a null here would throw
        /// somewhere far from the cause. Elintria has its own shader graph;
        /// a converted material has to be rebuilt there.</summary>
        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "Standard";
            Shader s;
            if (s_known.TryGetValue(name, out s)) return s;

            Shim.NotSupported("Shader.Find",
                              "Unity shaders are not converted; build the "
                              + "material in Elintria's shader graph instead");
            s = new Shader(name);
            s_known[name] = s;
            return s;
        }

        public static void SetGlobalFloat(string n, float v) { }
        public static void SetGlobalColor(string n, Color v) { }
        public static void SetGlobalVector(string n, Vector4 v) { }
        public static void SetGlobalTexture(string n, Texture v) { }
        public static int PropertyToID(string n) => n != null ? n.GetHashCode() : 0;
        public static void WarmupAllShaders() { }
    }

    public class Texture
    {
        public string name { get; set; } = "Texture";
        public int width  { get; protected set; }
        public int height { get; protected set; }
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
        public int anisoLevel { get; set; }
    }

    /// <summary>Pixel storage is real: a script that decodes an image into a
    /// Texture2D, reads it back and writes it out gets the right bytes. What
    /// it cannot do is put it on a surface.</summary>
    public class Texture2D : Texture
    {
        private Color32[] m_pixels;
        public TextureFormat format { get; private set; }

        public Texture2D(int w, int h) : this(w, h, TextureFormat.RGBA32, false) { }
        public Texture2D(int w, int h, TextureFormat fmt, bool mipChain, bool linear)
            : this(w, h, fmt, mipChain) { }
        public Texture2D(int w, int h, TextureFormat fmt, int mipCount, bool linear)
            : this(w, h, fmt, mipCount > 1) { }

        public Texture2D(int w, int h, TextureFormat fmt, bool mipChain)
        {
            width = w < 0 ? 0 : w;
            height = h < 0 ? 0 : h;
            format = fmt;
            m_pixels = new Color32[width * height];
        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return Color.clear;
            return m_pixels[y * width + x];
        }

        public void SetPixel(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            m_pixels[y * width + x] = c;
        }

        public Color[] GetPixels()
        {
            Color[] o = new Color[m_pixels.Length];
            for (int i = 0; i < m_pixels.Length; ++i) o[i] = m_pixels[i];
            return o;
        }

        public void SetPixels(Color[] colors)
        {
            if (colors == null) return;
            int n = Math.Min(colors.Length, m_pixels.Length);
            for (int i = 0; i < n; ++i) m_pixels[i] = colors[i];
        }

        public void SetPixels(int x, int y, int bw, int bh, Color[] colors)
        {
            if (colors == null) return;
            for (int row = 0; row < bh; ++row)
                for (int col = 0; col < bw; ++col)
                {
                    int src = row * bw + col;
                    if (src >= colors.Length) return;
                    SetPixel(x + col, y + row, colors[src]);
                }
        }

        public Color[] GetPixels(int x, int y, int bw, int bh)
        {
            Color[] o = new Color[bw * bh];
            for (int row = 0; row < bh; ++row)
                for (int col = 0; col < bw; ++col)
                    o[row * bw + col] = GetPixel(x + col, y + row);
            return o;
        }

        public Color32[] GetPixels32()
        {
            Color32[] o = new Color32[m_pixels.Length];
            Array.Copy(m_pixels, o, m_pixels.Length);
            return o;
        }

        public void SetPixels32(Color32[] colors)
        {
            if (colors == null) return;
            Array.Copy(colors, m_pixels, Math.Min(colors.Length, m_pixels.Length));
        }

        public void SetPixels32(int x, int y, int bw, int bh, Color32[] colors)
        {
            if (colors == null) return;
            for (int row = 0; row < bh; ++row)
                for (int col = 0; col < bw; ++col)
                {
                    int src = row * bw + col;
                    if (src >= colors.Length) return;
                    SetPixel(x + col, y + row, colors[src]);
                }
        }

        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }

        public bool LoadImage(byte[] data)
        {
            Shim.NotSupported("Texture2D.LoadImage",
                              "decoding PNG or JPEG in a script is not "
                              + "supported; import the image as an asset");
            return false;
        }

        public byte[] EncodeToPNG()
        { Shim.NotSupported("Texture2D.EncodeToPNG"); return new byte[0]; }

        public static Texture2D whiteTexture => new Texture2D(1, 1);
        public static Texture2D blackTexture => new Texture2D(1, 1);
    }

    public class Cubemap : Texture { }
    public class RenderTexture : Texture { }

    public class Sprite : Object
    {
        /// <summary>The image file (project-relative, or "builtin:...") the
        /// UI draws for this sprite.</summary>
        public string path;
        public Texture2D texture;
        public Rect rect;
        public Rect textureRect => rect;
        public Vector2 pivot;
        public float pixelsPerUnit = 100f;
        public Vector4 border;
        public Bounds bounds;
        public Sprite() { name = "Sprite"; }

        /// <summary>A sprite from its image file, as a scene hands one to a
        /// script's field.</summary>
        public static implicit operator Sprite(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return null;
            return new Sprite { path = imagePath, name = System.IO.Path.GetFileNameWithoutExtension(imagePath) };
        }
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot)
            => new Sprite { texture = tex, rect = rect };
    }

    /// <summary>A typeface. Elintria draws text with its own UI, so this
    /// carries a name and nothing else.</summary>
    public class Font
    {
        public string name { get; set; } = "Font";
        public int fontSize { get; set; }
        public Font() { }
        public Font(string n) { name = n; }
        public static Font CreateDynamicFontFromOSFont(string fontname, int size)
        {
            Shim.NotSupported("Font.CreateDynamicFontFromOSFont",
                              "Elintria has its own UI and text rendering");
            return new Font(fontname);
        }
    }

    // =========================================================================
    //  Colliders Elintria does not have
    //
    //  It has box and sphere colliders, and those are the real ones in the
    //  Elintria namespace. A capsule, a mesh collider and a character
    //  controller have no counterpart, so they are here and inert.
    // =========================================================================
    public class CapsuleCollider : InertComponent
    {
        public Vector3 center = Vector3.zero;
        public float   radius = 0.5f;
        public float   height = 2f;
        public int     direction = 1;
        public bool    isTrigger;
        public PhysicsMaterial material;
        public PhysicsMaterial sharedMaterial;
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
    }

    public class MeshCollider : InertComponent
    {
        public Mesh sharedMesh;
        public bool convex;
        public bool isTrigger;
        public MeshColliderCookingOptions cookingOptions =
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices;
    }

    // CharacterController is Elintria's own component now (Elintria.
    // CharacterController): the engine moves the capsule, and it is a
    // collider rays and triggers see.

    /// <summary>Surface friction and bounce. Unity 6 renamed PhysicMaterial
    /// to PhysicsMaterial; both spellings are here.</summary>
    public class PhysicsMaterial
    {
        public string name { get; set; } = "PhysicsMaterial";
        public float dynamicFriction = 0.6f;
        public float staticFriction = 0.6f;
        public float bounciness;
        public PhysicsMaterial() { }
        public PhysicsMaterial(string n) { name = n; }
    }

    public class PhysicMaterial : PhysicsMaterial
    {
        public PhysicMaterial() { }
        public PhysicMaterial(string n) : base(n) { }
    }

    // =========================================================================
    //  Camera
    //
    //  Elintria has a camera, but it is the viewport's rather than a
    //  component a script can reach. This is a detached stand-in: what is
    //  written to it can be read back, and nothing on screen changes.
    // =========================================================================
    public class Camera : InertComponent
    {
        private static Camera s_main;
        private static Camera s_standIn;

        /// <summary>Unity's rule: the camera on the object tagged
        /// MainCamera. Remembered while that object is alive and active -
        /// scripts ask every frame - and looked for again otherwise. With no
        /// such object, a stand-in camera on an object of its own, so
        /// "Camera.main.transform" still has a transform.</summary>
        public static Camera main
        {
            get
            {
                if (s_main != null && (object)s_main.gameObject != null && s_main.gameObject.activeInHierarchy)
                    return s_main;
                s_main = null;

                Elintria.GameObject g = Elintria.GameObject.FindWithTag("MainCamera");
                if (g != null)
                {
                    Camera c = g.GetComponent<Camera>() ?? g.AddComponent<Camera>();
                    if (c != null) { s_main = c; return c; }
                }

                if (s_standIn == null || (object)s_standIn.gameObject == null)
                {
                    Shim.NotSupported("Camera.main",
                                      "no object is tagged MainCamera; a stand-in camera is used");
                    Elintria.GameObject go = new Elintria.GameObject("Main Camera (stand-in)");
                    s_standIn = go.AddComponent<Camera>();
                }
                return s_standIn;
            }
        }

        public static Camera current => main;
        public static int allCamerasCount => 1;

        public float fieldOfView = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
        public float orthographicSize = 5f;
        public bool  orthographic;
        public int   cullingMask = ~0;
        public int   depth;
        public Color backgroundColor = Color.black;
        public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
        public Rect  rect = new Rect(0f, 0f, 1f, 1f);
        public float aspect = 16f / 9f;

        public Vector3 WorldToScreenPoint(Vector3 p)
        { Shim.NotSupported("Camera.WorldToScreenPoint"); return Vector3.zero; }
        public Vector3 ScreenToWorldPoint(Vector3 p)
        { Shim.NotSupported("Camera.ScreenToWorldPoint"); return Vector3.zero; }
        public Vector3 WorldToViewportPoint(Vector3 p)
        { Shim.NotSupported("Camera.WorldToViewportPoint"); return Vector3.zero; }
        public Ray ScreenPointToRay(Vector3 p)
        {
            Shim.NotSupported("Camera.ScreenPointToRay");
            return new Ray(Vector3.zero, Vector3.forward);
        }
    }

    public static class RenderSettings
    {
        public static Color ambientLight = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static Color fogColor = Color.gray;
        public static bool  fog;
        public static float fogDensity = 0.01f;
        public static float fogStartDistance;
        public static float fogEndDistance = 300f;
        public static Material skybox;
        public static float ambientIntensity = 1f;
    }

    // The project's quality levels (Project Settings > Quality), through
    // Elintria.Quality: a converted graphics menu switches them for real.
    // Elintria draws no shadows, so those settings are kept but do nothing.
    public static class QualitySettings
    {
        public static int vSyncCount
        { get { return Elintria.Quality.vSyncCount; } set { Elintria.Quality.vSyncCount = value; } }
        public static int pixelLightCount
        { get { return Elintria.Quality.pixelLightCount; } set { Elintria.Quality.pixelLightCount = value; } }
        public static int antiAliasing
        { get { return Elintria.Quality.antiAliasing; } set { Elintria.Quality.antiAliasing = value; } }
        public static AnisotropicFiltering anisotropicFiltering
        {
            get
            {
                int a = Elintria.Quality.anisotropicFiltering;
                return a == 0 ? AnisotropicFiltering.Disable : a >= 16 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            }
            set
            {
                Elintria.Quality.anisotropicFiltering = value == AnisotropicFiltering.Disable ? 0
                                                      : value == AnisotropicFiltering.ForceEnable ? 16 : 4;
            }
        }
        public static int masterTextureLimit
        { get { return Elintria.Quality.textureQuality; } set { Elintria.Quality.textureQuality = value; } }
        public static int globalTextureMipmapLimit
        { get { return Elintria.Quality.textureQuality; } set { Elintria.Quality.textureQuality = value; } }
        public static int shadowDistance { get; set; } = 150;
        public static ShadowQuality shadows { get; set; } = ShadowQuality.All;
        public static ShadowResolution shadowResolution { get; set; } = ShadowResolution.High;
        public static float lodBias { get; set; } = 1f;
        public static string[] names => Elintria.Quality.names;
        public static int count => Elintria.Quality.count;
        public static void SetQualityLevel(int index) => Elintria.Quality.SetLevel(index);
        public static void SetQualityLevel(int index, bool applyExpensiveChanges) => Elintria.Quality.SetLevel(index);
        public static int GetQualityLevel() => Elintria.Quality.GetLevel();
        public static void IncreaseLevel(bool applyExpensiveChanges = false) => Elintria.Quality.IncreaseLevel();
        public static void DecreaseLevel(bool applyExpensiveChanges = false) => Elintria.Quality.DecreaseLevel();
    }

    // =========================================================================
    //  Animation
    // =========================================================================
    public class AnimationClip
    {
        public string name { get; set; } = "Clip";
        public float length = 1f;
        public float frameRate = 30f;
        public bool  legacy = true;
        public WrapMode wrapMode = WrapMode.Once;

        private readonly Dictionary<string, AnimationCurve> m_curves =
            new Dictionary<string, AnimationCurve>();

        public void SetCurve(string relativePath, Type type, string propertyName,
                             AnimationCurve curve)
        { m_curves[relativePath + "/" + propertyName] = curve; }

        public void ClearCurves() { m_curves.Clear(); }
        public void EnsureQuaternionContinuity() { }
        public int CurveCount => m_curves.Count;
    }

    /// <summary>A real curve: keys are stored and evaluated, because that is
    /// arithmetic and there is no reason to fake it.</summary>
    public class AnimationCurve
    {
        private readonly List<Keyframe> m_keys = new List<Keyframe>();

        public AnimationCurve() { }
        public AnimationCurve(params Keyframe[] keys)
        { if (keys != null) m_keys.AddRange(keys); Sort(); }

        public Keyframe[] keys
        {
            get { return m_keys.ToArray(); }
            set { m_keys.Clear(); if (value != null) m_keys.AddRange(value); Sort(); }
        }

        public int length => m_keys.Count;
        public WrapMode preWrapMode  { get; set; }
        public WrapMode postWrapMode { get; set; }

        public Keyframe this[int i] => m_keys[i];

        public int AddKey(float time, float value)
        {
            m_keys.Add(new Keyframe(time, value));
            Sort();
            for (int i = 0; i < m_keys.Count; ++i)
                if (m_keys[i].time == time) return i;
            return m_keys.Count - 1;
        }

        public int AddKey(Keyframe key) { m_keys.Add(key); Sort(); return m_keys.Count - 1; }
        public void RemoveKey(int index)
        { if (index >= 0 && index < m_keys.Count) m_keys.RemoveAt(index); }

        /// <summary>Sets a key's tangents to the slope through its
        /// neighbours, blended towards flat by |weight|.</summary>
        public void SmoothTangents(int index, float weight)
        {
            if (index < 0 || index >= m_keys.Count) return;

            Keyframe k = m_keys[index];
            if (m_keys.Count == 1) { k.inTangent = 0f; k.outTangent = 0f; }
            else
            {
                int a = index > 0 ? index - 1 : index;
                int b = index + 1 < m_keys.Count ? index + 1 : index;
                float dt = m_keys[b].time - m_keys[a].time;
                float slope = dt > 1e-8f ? (m_keys[b].value - m_keys[a].value) / dt : 0f;
                slope *= (1f - Mathf.Clamp01(weight));
                k.inTangent = slope;
                k.outTangent = slope;
            }
            m_keys[index] = k;
        }

        private void Sort()
        {
            // Insertion sort: key lists are short and usually already ordered.
            for (int i = 1; i < m_keys.Count; ++i)
            {
                Keyframe k = m_keys[i];
                int j = i;
                while (j > 0 && m_keys[j - 1].time > k.time) { m_keys[j] = m_keys[j - 1]; --j; }
                m_keys[j] = k;
            }
        }

        public float Evaluate(float time)
        {
            if (m_keys.Count == 0) return 0f;
            if (m_keys.Count == 1) return m_keys[0].value;
            if (time <= m_keys[0].time) return m_keys[0].value;
            if (time >= m_keys[m_keys.Count - 1].time) return m_keys[m_keys.Count - 1].value;

            for (int i = 0; i + 1 < m_keys.Count; ++i)
            {
                Keyframe a = m_keys[i], b = m_keys[i + 1];
                if (time < a.time || time > b.time) continue;

                float dt = b.time - a.time;
                if (dt < 1e-8f) return b.value;
                float t = (time - a.time) / dt;

                // Cubic Hermite with the keys' own tangents.
                float t2 = t * t, t3 = t2 * t;
                float h00 =  2f * t3 - 3f * t2 + 1f;
                float h10 =       t3 - 2f * t2 + t;
                float h01 = -2f * t3 + 3f * t2;
                float h11 =       t3 -      t2;
                return h00 * a.value + h10 * dt * a.outTangent
                     + h01 * b.value + h11 * dt * b.inTangent;
            }
            return m_keys[m_keys.Count - 1].value;
        }

        public static AnimationCurve Linear(float t0, float v0, float t1, float v1)
        {
            float slope = (t1 - t0) > 1e-8f ? (v1 - v0) / (t1 - t0) : 0f;
            return new AnimationCurve(new Keyframe(t0, v0, slope, slope),
                                      new Keyframe(t1, v1, slope, slope));
        }

        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1)
            => new AnimationCurve(new Keyframe(t0, v0, 0f, 0f), new Keyframe(t1, v1, 0f, 0f));

        public static AnimationCurve Constant(float t0, float t1, float value)
            => new AnimationCurve(new Keyframe(t0, value, 0f, 0f),
                                  new Keyframe(t1, value, 0f, 0f));
    }

    [Serializable]
    public struct Keyframe
    {
        public float time, value, inTangent, outTangent;

        public Keyframe(float time, float value)
        { this.time = time; this.value = value; inTangent = 0f; outTangent = 0f; }

        public Keyframe(float time, float value, float inTangent, float outTangent)
        {
            this.time = time; this.value = value;
            this.inTangent = inTangent; this.outTangent = outTangent;
        }
    }

    public class AnimationState
    {
        public string name = string.Empty;
        public float  speed = 1f;
        public float  time;
        public float  length = 1f;
        public float  weight = 1f;
        public WrapMode wrapMode = WrapMode.Once;
        public bool   enabled;
    }

    public class Animation : InertComponent, IEnumerable
    {
        private readonly Dictionary<string, AnimationState> m_states =
            new Dictionary<string, AnimationState>();

        public AnimationClip clip;
        public bool playAutomatically = true;
        public WrapMode wrapMode = WrapMode.Once;
        public bool isPlaying { get; private set; }

        public AnimationState this[string name]
        {
            get
            {
                AnimationState s;
                if (!m_states.TryGetValue(name, out s))
                { s = new AnimationState { name = name }; m_states[name] = s; }
                return s;
            }
        }

        private void Inert(string what)
            => Shim.NotSupported("Animation." + what,
                                 "Elintria plays animation through its own "
                                 + "animation system, not from scripts");

        public bool Play() { Inert("Play"); isPlaying = true; return true; }
        public bool Play(string name) { Inert("Play"); isPlaying = true; return true; }
        public void Stop() { isPlaying = false; }
        public void Stop(string name) { isPlaying = false; }
        public void CrossFade(string name) { Inert("CrossFade"); }
        public void CrossFade(string name, float fadeLength) { Inert("CrossFade"); }
        public void AddClip(AnimationClip c, string newName)
        { this[newName].length = c != null ? c.length : 1f; }
        public bool IsPlaying(string name) => isPlaying;
        public void Sample() { }

        public IEnumerator<AnimationState> GetEnumerator() => m_states.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => m_states.Values.GetEnumerator();
    }

    // Animator and AnimatorStateInfo are Elintria's and real: the engine has
    // an Animator Controller state machine. Declaring them here as well would
    // make every converted script's "Animator" ambiguous between the two
    // namespaces it imports.

    // =========================================================================
    //  Audio
    //
    //  AudioSource is Elintria's and real. What Unity has and Elintria does
    //  not is a clip object: Elintria names its clips. AudioClip therefore
    //  carries a name and converts to one implicitly, so
    //  "source.clip = myClip" works against the engine's string property.
    // =========================================================================
    public class AudioClip : Object
    {

        public float length { get; internal set; }
        public int frequency => 44100;
        public int channels => 2;

        public AudioClip() { }
        public AudioClip(string n) { name = n; }

        public static implicit operator string(AudioClip c) => c != null ? c.name : string.Empty;
        public static implicit operator AudioClip(string n) => string.IsNullOrEmpty(n) ? null : new AudioClip(n);

        public override string ToString() => name;
    }

    public class AudioListener : InertComponent
    {
        public static float volume
        {
            get { return Elintria.Audio.masterVolume; }
            set { Elintria.Audio.masterVolume = value; }
        }

        public static bool pause
        {
            get { return Elintria.Audio.paused; }
            set { Elintria.Audio.paused = value; }
        }
    }

    // =========================================================================
    //  Editor-only drawing and asset loading
    // =========================================================================
    public static class Gizmos
    {
        public static Color color = Color.white;
        public static Matrix4x4 matrix = Matrix4x4.identity;

        public static void DrawLine(Vector3 from, Vector3 to) { }
        public static void DrawRay(Vector3 from, Vector3 direction) { }
        public static void DrawRay(Ray r) { }
        public static void DrawWireSphere(Vector3 center, float radius) { }
        public static void DrawSphere(Vector3 center, float radius) { }
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawCube(Vector3 center, Vector3 size) { }
        public static void DrawMesh(Mesh mesh, Vector3 position) { }
        public static void DrawIcon(Vector3 center, string name) { }
        public static void DrawFrustum(Vector3 c, float fov, float far, float near, float aspect) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : class
        {
            Shim.NotSupported("Resources.Load",
                              "Elintria has no Resources folder; the importer "
                              + "copied those files into Assets, so load them "
                              + "by path instead");
            return null;
        }

        public static object Load(string path) => Load<object>(path);
        public static object[] LoadAll(string path) { Load<object>(path); return new object[0]; }
        public static T[] LoadAll<T>(string path) where T : class
        { Load<T>(path); return new T[0]; }

        public static T GetBuiltinResource<T>(string path) where T : class
        {
            Shim.NotSupported("Resources.GetBuiltinResource",
                              "Unity's built-in assets are not available");
            return null;
        }

        public static object GetBuiltinResource(Type type, string path)
            => GetBuiltinResource<object>(path);

        public static void UnloadUnusedAssets() { }
        public static void UnloadAsset(object o) { }
    }

    public static class Application
    {
        public static bool isPlaying => true;
        public static bool isEditor  => false;

        // The project's own folders, not the process's working directory -
        // which is wherever the editor was launched from and has nothing to
        // do with the project. The importer copies a Unity project's
        // Assets/ across as it was, so these land in the same places
        // relative to the content as they did in Unity.
        public static string dataPath => Elintria.Project.assetsPath;
        public static string streamingAssetsPath
            => System.IO.Path.Combine(dataPath, "StreamingAssets");
        public static string persistentDataPath
            => System.IO.Path.Combine(
                   Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                   "Elintria");
        public static string temporaryCachePath => System.IO.Path.GetTempPath();

        public static string productName  => "Elintria Game";
        public static string companyName  => "Elintria";
        public static string version      => "1.0";
        public static string unityVersion => "converted";

        // Unity's -1 is "the platform default": unlimited here.
        public static int targetFrameRate
        {
            get { int f = Elintria.Quality.targetFrameRate; return f > 0 ? f : -1; }
            set { Elintria.Quality.targetFrameRate = value > 0 ? value : 0; }
        }

        public static bool runInBackground { get { return true; } set { } }

        public static void Quit()
            => Shim.NotSupported("Application.Quit", "close the game window instead");

        public static void OpenURL(string url) => Shim.NotSupported("Application.OpenURL");
    }

    public static class Screen
    {
        // The game view (or the built game's window), as it is this frame.
        public static int width  { get { int w = Elintria.GameWindow.width;  return w > 0 ? w : 1280; } }
        public static int height { get { int h = Elintria.GameWindow.height; return h > 0 ? h : 720; } }
        public static float dpi => 96f;
        public static bool fullScreen { get; set; }
        public static Resolution currentResolution
            => new Resolution { width = width, height = height, refreshRate = 60 };

        public static void SetResolution(int w, int h, bool fs)
            => Shim.NotSupported("Screen.SetResolution");
    }

    public struct Resolution
    {
        public int width, height, refreshRate;
        public override string ToString() => width + " x " + height + " @ " + refreshRate + "Hz";
    }

    public enum CursorLockMode { None, Locked, Confined }

    public static class Cursor
    {
        private static bool s_visible = true;

        public static bool visible
        {
            get { return s_visible; }
            set
            {
                s_visible = value;
                if (value) Elintria.Mouse.ShowCursor(); else Elintria.Mouse.HideCursor();
            }
        }

        private static CursorLockMode s_lock = CursorLockMode.None;

        /// <summary>Locked captures the mouse the way an FPS wants it: the
        /// pointer is held in the window and read as movement (Mouse.current.
        /// delta, Input.GetAxis("Mouse X")).</summary>
        public static CursorLockMode lockState
        {
            get { return s_lock; }
            set
            {
                s_lock = value;
                if (value == CursorLockMode.Locked) Elintria.Mouse.DisableCursor();
                else Elintria.Mouse.EnableCursor();
            }
        }

        public static void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode mode) { }
    }

    public enum CursorMode { Auto, ForceSoftware }

    public class Canvas : InertComponent
    {
        public RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        public int sortingOrder;
        public Camera worldCamera;
        public float scaleFactor = 1f;
        public float referencePixelsPerUnit = 100f;
        public bool overrideSorting, pixelPerfect;
        public Canvas rootCanvas => this;
        public bool isRootCanvas => true;

        /// <summary>Layout is worked out as the frame is drawn, so there is
        /// nothing waiting to be forced.</summary>
        public static void ForceUpdateCanvases() { }
    }

    public struct CombineInstance
    {
        public Mesh mesh;
        public int subMeshIndex;
        public Matrix4x4 transform;
        public Vector4 lightmapScaleOffset, realtimeLightmapScaleOffset;
    }

    /// <summary>The audio clock. Elintria starts sounds when asked, so the
    /// DSP time is the game clock.</summary>
    public static class AudioSettings
    {
        public static double dspTime => Elintria.Time.time;
        public static int outputSampleRate => 48000;
        public static AudioSpeakerMode speakerMode => AudioSpeakerMode.Stereo;
    }

    public enum AudioSpeakerMode { Mono = 1, Stereo = 2, Quad = 3, Surround = 4, Mode5point1 = 5, Mode7point1 = 6 }

    public static class ScreenCapture
    {
        public enum StereoScreenCaptureMode { LeftEye = 1, RightEye = 2, BothEyes = 3 }

        public static void CaptureScreenshot(string filename) => CaptureScreenshot(filename, 1);
        public static void CaptureScreenshot(string filename, int superSize)
            => Shim.NotSupported("ScreenCapture.CaptureScreenshot",
                                 "use the editor's Game view screenshot instead");
        public static Texture2D CaptureScreenshotAsTexture() => new Texture2D(Screen.width, Screen.height);
        public static Texture2D CaptureScreenshotAsTexture(int superSize) => CaptureScreenshotAsTexture();
    }

    public class RectTransform : InertComponent
    {
        /// <summary>In Unity a RectTransform is the object's Transform, so
        /// code hands one to anything wanting a Transform - a parent for
        /// Instantiate, SetParent. This is that object's transform.</summary>
        public static implicit operator Elintria.Transform(RectTransform r) => r?.transform;

        public enum Edge { Left, Right, Top, Bottom }
        public enum Axis { Horizontal, Vertical }

        public Vector2 anchoredPosition;
        public Vector2 sizeDelta = new Vector2(100f, 100f);
        public Vector2 anchorMin;
        public Vector2 anchorMax = new Vector2(1f, 1f);
        public Vector2 pivot = new Vector2(0.5f, 0.5f);
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public Rect rect => new Rect(0f, 0f, sizeDelta.x, sizeDelta.y);

        public void SetSizeWithCurrentAnchors(int axis, float size)
        {
            if (axis == 0) sizeDelta = new Vector2(size, sizeDelta.y);
            else sizeDelta = new Vector2(sizeDelta.x, size);
        }

        public void SetInsetAndSizeFromParentEdge(int edge, float inset, float size) { }
    }

    public class CanvasGroup : InertComponent
    {
        private float? m_alpha;

        /// <summary>Fades the element and everything under it.</summary>
        public float alpha
        {
            get
            {
                if (m_alpha.HasValue) return m_alpha.Value;
                float f;
                return float.TryParse(gameObject != null ? gameObject.GetMeta("galpha") : "",
                                      System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out f) ? f : 1f;
            }
            set
            {
                m_alpha = value;
                string el = gameObject != null ? gameObject.GetMeta("ui") : "";
                if (el.Length > 0)
                    Elintria.UIRuntime.SetProp(el, "galpha",
                        value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        public bool interactable = true;
        public bool blocksRaycasts = true;
        public bool ignoreParentGroups;
    }

    /// <summary>The per-element renderer state uGUI keeps. Its alpha is the
    /// same fade a CanvasGroup gives, for one graphic.</summary>
    public class CanvasRenderer : InertComponent
    {
        private float m_alpha = 1f;
        public float GetAlpha() => m_alpha;
        public void SetAlpha(float a)
        {
            m_alpha = a;
            var g = gameObject != null ? gameObject.GetComponent<UnityEngine.UI.Graphic>() : null;
            if (g != null) g.CrossFadeAlpha(a, 0f, true);
        }
        public Color GetColor() => Color.white;
        public void SetColor(Color c) { }
        public bool cull { get; set; }
        public void Clear() { }
    }
}

// =============================================================================
//  UnityEngine.Rendering
// =============================================================================
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum CompareFunction { Disabled, Never, Less, Equal, LessEqual, Greater,
                                  NotEqual, GreaterEqual, Always }
    public enum CullMode { Off, Front, Back }
    public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor,
                            SrcAlpha, OneMinusSrcColor, DstAlpha, OneMinusDstAlpha,
                            SrcAlphaSaturate, OneMinusSrcAlpha }
    public enum IndexFormat { UInt16, UInt32 }
    public enum LightProbeUsage { Off = 0, BlendProbes = 1, UseProxyVolume = 2, CustomProvided = 4 }
    public enum ReflectionProbeUsage { Off, BlendProbes, BlendProbesAndSkybox, Simple }

    /// <summary>Draw-order buckets. Elintria sorts its own way, so this is
    /// carried but not consulted.</summary>
    public enum RenderQueue
    {
        Background = 1000, Geometry = 2000, AlphaTest = 2450,
        GeometryLast = 2500, Transparent = 3000, Overlay = 4000
    }
}

// =============================================================================
//  UnityEngine.SceneManagement
// =============================================================================
namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get; internal set; }
        public string path { get; internal set; }
        public int buildIndex => 0;
        public bool isLoaded => true;
        public bool IsValid() => !string.IsNullOrEmpty(name);
    }

    public enum LoadSceneMode { Single, Additive }

    public static class SceneManager
    {
        public static Scene GetActiveScene() => new Scene();
        public static int sceneCount => 1;
        public static int sceneCountInBuildSettings => 1;

        public static void LoadScene(string sceneName)
            => Shim.NotSupported("SceneManager.LoadScene",
                                 "changing scene from a script is not "
                                 + "supported yet; load it in the editor");

        public static void LoadScene(int buildIndex) => LoadScene(buildIndex.ToString());
        public static void LoadScene(string sceneName, LoadSceneMode mode) => LoadScene(sceneName);
        public static void LoadSceneAsync(string sceneName) => LoadScene(sceneName);
    }
}

// =============================================================================
//  UnityEngine.Serialization
// =============================================================================
namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FormerlySerializedAsAttribute : Attribute
    {
        public FormerlySerializedAsAttribute(string oldName) { }
    }
}

// =============================================================================
//  UnityEngine.Networking
//
//  Only enough to compile and to fail honestly. Fetching an asset over HTTP
//  at run time is not something Elintria does, so a request completes
//  immediately with an error and says so once.
// =============================================================================
namespace UnityEngine.Networking
{
    public enum AudioType { UNKNOWN = 0, WAV = 20, OGGVORBIS = 14, MPEG = 13 }

    public class DownloadHandler
    {
        public byte[] data => new byte[0];
        public string text => string.Empty;
        public virtual void Dispose() { }
    }

    public class DownloadHandlerAudioClip : DownloadHandler
    {
        public AudioClip audioClip => null;
        public bool streamAudio { get; set; }
        public static AudioClip GetContent(UnityWebRequest www) => null;
    }

    public class UploadHandler { public virtual void Dispose() { } }

    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError,
                             DataProcessingError }

        public string url { get; set; }
        public string method { get; set; }
        public long responseCode => 0;
        public bool isDone => true;
        public float downloadProgress => 1f;

        /// <summary>Always a connection error: nothing here can reach the
        /// network, and reporting success with no data would send callers
        /// down a path that then fails somewhere less obvious.</summary>
        public Result result => Result.ConnectionError;

        public string error => "Elintria does not support web requests from scripts";
        public DownloadHandler downloadHandler { get; set; }
        public UploadHandler uploadHandler { get; set; }

        public UnityWebRequest() { Warn(); }
        public UnityWebRequest(string url) { this.url = url; Warn(); }
        public UnityWebRequest(string url, string method)
        { this.url = url; this.method = method; Warn(); }

        private static void Warn()
            => Shim.NotSupported("UnityWebRequest",
                                 "Elintria cannot fetch assets over the network "
                                 + "at run time; every request reports a "
                                 + "connection error");

        public UnityWebRequestAsyncOperation SendWebRequest()
            => new UnityWebRequestAsyncOperation();

        public void Abort() { }
        public void Dispose() { }
        public void SetRequestHeader(string name, string value) { }
        public string GetRequestHeader(string name) => null;
        public string GetResponseHeader(string name) => null;

        public static UnityWebRequest Get(string uri) => new UnityWebRequest(uri, "GET");
        public static UnityWebRequest Post(string uri, string postData)
            => new UnityWebRequest(uri, "POST");
        public static UnityWebRequest Put(string uri, byte[] body)
            => new UnityWebRequest(uri, "PUT");
        public static UnityWebRequest Delete(string uri) => new UnityWebRequest(uri, "DELETE");
        public static UnityWebRequest Head(string uri) => new UnityWebRequest(uri, "HEAD");
    }

    /// <summary>Already finished the moment it is made, so a coroutine that
    /// yields on one resumes next frame instead of waiting forever.</summary>
    public class UnityWebRequestAsyncOperation : IEnumerator
    {
        public bool isDone => true;
        public float progress => 1f;
        public object Current => null;
        public bool MoveNext() => false;
        public void Reset() { }
    }

    public static class UnityWebRequestMultimedia
    {
        public static UnityWebRequest GetAudioClip(string uri, AudioType audioType)
        {
            UnityWebRequest r = new UnityWebRequest(uri, "GET");
            r.downloadHandler = new DownloadHandlerAudioClip();
            return r;
        }
    }

    public static class UnityWebRequestTexture
    {
        public static UnityWebRequest GetTexture(string uri)
            => new UnityWebRequest(uri, "GET");
    }
}
