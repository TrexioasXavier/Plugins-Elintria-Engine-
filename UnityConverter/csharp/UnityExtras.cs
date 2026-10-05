// More of UnityEngine - part of the Unity Converter plugin.
//
// ScriptableObject, the integer vectors, Gradient, MaterialPropertyBlock,
// the particle system, the audio mixer, NavMeshAgent, and the attributes
// scripts are decorated with. What each one really does in Elintria is said
// on the class.

using System;
using System.Collections.Generic;
using System.Globalization;
using Elintria;

namespace UnityEngine
{
    // =========================================================================
    //  ScriptableObject
    //
    //  A plain object here. Scripts that make one with CreateInstance and use
    //  it as data work; .asset files a Unity project saved them into are not
    //  loaded (the importer reports which).
    // =========================================================================
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            T t = new T();
            t.name = typeof(T).Name;
            t.CallAwake();
            return t;
        }

        public static ScriptableObject CreateInstance(Type type)
        {
            if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type)) return null;
            var t = (ScriptableObject)Activator.CreateInstance(type);
            t.name = type.Name;
            t.CallAwake();
            return t;
        }

        public static ScriptableObject CreateInstance(string className)
        {
            foreach (Type t in typeof(ScriptableObject).Assembly.GetTypes())
                if (t.Name == className && typeof(ScriptableObject).IsAssignableFrom(t)) return CreateInstance(t);
            return null;
        }

        private void CallAwake()
        {
            var m = GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (m == null) m = GetType().GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            try { m?.Invoke(this, null); } catch (Exception e) { Elintria.Debug.LogError(GetType().Name + ".Awake threw: " + e.Message); }
        }

        public void SetDirty() { }
    }

    // =========================================================================
    //  Attributes - kept so the intent survives; the Inspector does not read them
    // =========================================================================
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HelpURLAttribute : Attribute { public HelpURLAttribute(string url) { URL = url; } public string URL { get; } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ColorUsageAttribute : Attribute
    {
        public ColorUsageAttribute(bool showAlpha) { this.showAlpha = showAlpha; }
        public ColorUsageAttribute(bool showAlpha, bool hdr) { this.showAlpha = showAlpha; this.hdr = hdr; }
        public bool showAlpha { get; }
        public bool hdr { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class GradientUsageAttribute : Attribute
    {
        public GradientUsageAttribute(bool hdr) { this.hdr = hdr; }
        public GradientUsageAttribute(bool hdr, ColorSpace colorSpace) { this.hdr = hdr; }
        public bool hdr { get; }
    }

    public enum ColorSpace { Uninitialized = -1, Gamma = 0, Linear = 1 }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : Attribute { public MinAttribute(float min) { this.min = min; } public float min { get; } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MultilineAttribute : Attribute { public MultilineAttribute() { } public MultilineAttribute(int lines) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DelayedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NonReorderableAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeReferenceAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InspectorNameAttribute : Attribute { public InspectorNameAttribute(string displayName) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SelectionBaseAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { this.order = order; } public int order { get; } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PreferBinarySerialization : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ContextMenuItemAttribute : Attribute { public ContextMenuItemAttribute(string name, string function) { } }

    // =========================================================================
    //  Integer vectors
    // =========================================================================
    [Serializable]
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public static Vector2Int up => new Vector2Int(0, 1);
        public static Vector2Int down => new Vector2Int(0, -1);
        public static Vector2Int left => new Vector2Int(-1, 0);
        public static Vector2Int right => new Vector2Int(1, 0);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public int sqrMagnitude => x * x + y * y;
        public int this[int i] { get { return i == 0 ? x : y; } set { if (i == 0) x = value; else y = value; } }
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public static Vector2Int operator *(Vector2Int a, int d) => new Vector2Int(a.x * d, a.y * d);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public static implicit operator Vector2(Vector2Int v) => new Vector2(v.x, v.y);
        public static Vector2Int RoundToInt(Vector2 v) => new Vector2Int((int)Math.Round(v.x), (int)Math.Round(v.y));
        public static Vector2Int FloorToInt(Vector2 v) => new Vector2Int((int)Math.Floor(v.x), (int)Math.Floor(v.y));
        public static Vector2Int CeilToInt(Vector2 v) => new Vector2Int((int)Math.Ceiling(v.x), (int)Math.Ceiling(v.y));
        public bool Equals(Vector2Int o) => this == o;
        public override bool Equals(object o) => o is Vector2Int && this == (Vector2Int)o;
        public override int GetHashCode() => x * 73856093 ^ y * 19349663;
        public override string ToString() => "(" + x + ", " + y + ")";
    }

    [Serializable]
    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public Vector3Int(int x, int y) { this.x = x; this.y = y; z = 0; }
        public static Vector3Int zero => new Vector3Int(0, 0, 0);
        public static Vector3Int one => new Vector3Int(1, 1, 1);
        public static Vector3Int up => new Vector3Int(0, 1, 0);
        public static Vector3Int down => new Vector3Int(0, -1, 0);
        public static Vector3Int left => new Vector3Int(-1, 0, 0);
        public static Vector3Int right => new Vector3Int(1, 0, 0);
        public static Vector3Int forward => new Vector3Int(0, 0, 1);
        public static Vector3Int back => new Vector3Int(0, 0, -1);
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public int sqrMagnitude => x * x + y * y + z * z;
        public int this[int i]
        {
            get { return i == 0 ? x : i == 1 ? y : z; }
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new Vector3Int(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3Int operator *(Vector3Int a, int d) => new Vector3Int(a.x * d, a.y * d, a.z * d);
        public static bool operator ==(Vector3Int a, Vector3Int b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);
        public static implicit operator Vector3(Vector3Int v) => new Vector3(v.x, v.y, v.z);
        public static Vector3Int RoundToInt(Vector3 v) => new Vector3Int((int)Math.Round(v.x), (int)Math.Round(v.y), (int)Math.Round(v.z));
        public static Vector3Int FloorToInt(Vector3 v) => new Vector3Int((int)Math.Floor(v.x), (int)Math.Floor(v.y), (int)Math.Floor(v.z));
        public static Vector3Int CeilToInt(Vector3 v) => new Vector3Int((int)Math.Ceiling(v.x), (int)Math.Ceiling(v.y), (int)Math.Ceiling(v.z));
        public bool Equals(Vector3Int o) => this == o;
        public override bool Equals(object o) => o is Vector3Int && this == (Vector3Int)o;
        public override int GetHashCode() => x * 73856093 ^ y * 19349663 ^ z * 83492791;
        public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
    }

    // =========================================================================
    //  Gradient - complete; it is only arithmetic
    // =========================================================================
    public enum GradientMode { Blend, Fixed, PerceptualBlend }

    [Serializable]
    public struct GradientColorKey
    {
        public Color color; public float time;
        public GradientColorKey(Color col, float time) { color = col; this.time = time; }
    }

    [Serializable]
    public struct GradientAlphaKey
    {
        public float alpha; public float time;
        public GradientAlphaKey(float alpha, float time) { this.alpha = alpha; this.time = time; }
    }

    [Serializable]
    public class Gradient
    {
        public GradientColorKey[] colorKeys = { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        public GradientAlphaKey[] alphaKeys = { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        public GradientMode mode = GradientMode.Blend;

        public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a)
        {
            colorKeys = c ?? new GradientColorKey[0];
            alphaKeys = a ?? new GradientAlphaKey[0];
            Array.Sort(colorKeys, (p, q) => p.time.CompareTo(q.time));
            Array.Sort(alphaKeys, (p, q) => p.time.CompareTo(q.time));
        }

        public Color Evaluate(float time)
        {
            Color c = Color.white;
            if (colorKeys != null && colorKeys.Length > 0)
            {
                int i = 0;
                while (i < colorKeys.Length && colorKeys[i].time < time) ++i;
                if (i == 0) c = colorKeys[0].color;
                else if (i >= colorKeys.Length) c = colorKeys[colorKeys.Length - 1].color;
                else if (mode == GradientMode.Fixed) c = colorKeys[i].color;
                else
                {
                    GradientColorKey a = colorKeys[i - 1], b = colorKeys[i];
                    float t = b.time > a.time ? (time - a.time) / (b.time - a.time) : 0f;
                    c = new Color(a.color.r + (b.color.r - a.color.r) * t, a.color.g + (b.color.g - a.color.g) * t,
                                  a.color.b + (b.color.b - a.color.b) * t, 1f);
                }
            }
            float alpha = 1f;
            if (alphaKeys != null && alphaKeys.Length > 0)
            {
                int i = 0;
                while (i < alphaKeys.Length && alphaKeys[i].time < time) ++i;
                if (i == 0) alpha = alphaKeys[0].alpha;
                else if (i >= alphaKeys.Length) alpha = alphaKeys[alphaKeys.Length - 1].alpha;
                else if (mode == GradientMode.Fixed) alpha = alphaKeys[i].alpha;
                else
                {
                    GradientAlphaKey a = alphaKeys[i - 1], b = alphaKeys[i];
                    float t = b.time > a.time ? (time - a.time) / (b.time - a.time) : 0f;
                    alpha = a.alpha + (b.alpha - a.alpha) * t;
                }
            }
            c.a = alpha;
            return c;
        }
    }

    // =========================================================================
    //  MaterialPropertyBlock - per-renderer overrides. Stored; a renderer
    //  given one keeps it, and the colour it sets is not drawn yet.
    // =========================================================================
    public class MaterialPropertyBlock
    {
        private readonly Dictionary<string, object> m_values = new Dictionary<string, object>();
        public bool isEmpty => m_values.Count == 0;
        public void Clear() { m_values.Clear(); }
        public void SetColor(string name, Color value) { m_values[name] = value; }
        public void SetColor(int nameID, Color value) { m_values["#" + nameID] = value; }
        public void SetFloat(string name, float value) { m_values[name] = value; }
        public void SetFloat(int nameID, float value) { m_values["#" + nameID] = value; }
        public void SetInt(string name, int value) { m_values[name] = value; }
        public void SetVector(string name, Vector4 value) { m_values[name] = value; }
        public void SetTexture(string name, Texture value) { m_values[name] = value; }
        public void SetMatrix(string name, Matrix4x4 value) { m_values[name] = value; }
        public Color GetColor(string name) { object v; return m_values.TryGetValue(name, out v) && v is Color ? (Color)v : Color.white; }
        public float GetFloat(string name) { object v; return m_values.TryGetValue(name, out v) && v is float ? (float)v : 0f; }
        public Vector4 GetVector(string name) { object v; return m_values.TryGetValue(name, out v) && v is Vector4 ? (Vector4)v : new Vector4(); }
        public bool HasProperty(string name) => m_values.ContainsKey(name);
    }

    public enum MotionVectorGenerationMode { Camera, Object, ForceNoMotion }
    public enum ShadowQuality { Disable, HardOnly, All }
    public enum ShadowResolution { Low, Medium, High, VeryHigh }
    public enum AnisotropicFiltering { Disable, Enable, ForceEnable }

    // =========================================================================
    //  Line and trail renderers - data only
    // =========================================================================
    public class LineRenderer : Renderer
    {
        private Vector3[] m_points = new Vector3[0];
        public int positionCount { get { return m_points.Length; } set { Array.Resize(ref m_points, Math.Max(0, value)); } }
        public void SetPosition(int i, Vector3 p) { if (i >= 0 && i < m_points.Length) m_points[i] = p; }
        public Vector3 GetPosition(int i) => i >= 0 && i < m_points.Length ? m_points[i] : Vector3.zero;
        public void SetPositions(Vector3[] p) { m_points = p != null ? (Vector3[])p.Clone() : new Vector3[0]; }
        public int GetPositions(Vector3[] p) { int n = Math.Min(p.Length, m_points.Length); Array.Copy(m_points, p, n); return n; }
        public float startWidth = 1f, endWidth = 1f, widthMultiplier = 1f;
        public Color startColor = Color.white, endColor = Color.white;
        public bool useWorldSpace = true, loop;
        public Gradient colorGradient = new Gradient();
        public AnimationCurve widthCurve;
    }

    public class TrailRenderer : Renderer
    {
        public float time = 5f, startWidth = 1f, endWidth = 1f, widthMultiplier = 1f, minVertexDistance = 0.1f;
        public Color startColor = Color.white, endColor = Color.white;
        public bool emitting = true, autodestruct;
        public Gradient colorGradient = new Gradient();
        public void Clear() { }
    }

    // =========================================================================
    //  ParticleSystem
    //
    //  Elintria has no particle renderer yet, so nothing is drawn. The object
    //  is complete in every other respect - modules keep what is set on them,
    //  Play/Stop/isPlaying behave, time advances while playing - so effect
    //  code runs as written.
    // =========================================================================
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemScalingMode { Hierarchy, Local, Shape }
    public enum ParticleSystemCurveMode { Constant, Curve, TwoCurves, TwoConstants }
    public enum ParticleSystemGradientMode { Color, Gradient, TwoColors, TwoGradients, RandomColor }
    public enum ParticleSystemStopAction { None, Disable, Destroy, Callback }
    public enum ParticleSystemShapeType { Sphere, Hemisphere = 2, Cone = 4, Box = 5, Mesh = 6, Circle = 10, Edge = 12, Rectangle = 18 }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None }

    public class ParticleSystem : InertComponent
    {
        [Serializable]
        public struct MinMaxCurve
        {
            public ParticleSystemCurveMode mode;
            public float constant, constantMin, constantMax, curveMultiplier;
            public AnimationCurve curve, curveMin, curveMax;
            public MinMaxCurve(float c) { mode = ParticleSystemCurveMode.Constant; constant = constantMin = constantMax = c; curveMultiplier = 1f; curve = curveMin = curveMax = null; }
            public MinMaxCurve(float min, float max) { mode = ParticleSystemCurveMode.TwoConstants; constant = max; constantMin = min; constantMax = max; curveMultiplier = 1f; curve = curveMin = curveMax = null; }
            public MinMaxCurve(float multiplier, AnimationCurve c) { mode = ParticleSystemCurveMode.Curve; constant = constantMin = constantMax = 0f; curveMultiplier = multiplier; curve = c; curveMin = curveMax = null; }
            public static implicit operator MinMaxCurve(float c) => new MinMaxCurve(c);
            public float Evaluate(float time) => mode == ParticleSystemCurveMode.TwoConstants ? (constantMin + constantMax) * 0.5f
                                               : mode == ParticleSystemCurveMode.Curve && curve != null ? curve.Evaluate(time) * curveMultiplier : constant;
            public float Evaluate(float time, float lerp) => mode == ParticleSystemCurveMode.TwoConstants
                ? constantMin + (constantMax - constantMin) * lerp : Evaluate(time);
        }

        [Serializable]
        public struct MinMaxGradient
        {
            public ParticleSystemGradientMode mode;
            public Color color, colorMin, colorMax;
            public Gradient gradient, gradientMin, gradientMax;
            public MinMaxGradient(Color c) { mode = ParticleSystemGradientMode.Color; color = colorMin = colorMax = c; gradient = gradientMin = gradientMax = null; }
            public MinMaxGradient(Color min, Color max) { mode = ParticleSystemGradientMode.TwoColors; color = max; colorMin = min; colorMax = max; gradient = gradientMin = gradientMax = null; }
            public MinMaxGradient(Gradient g) { mode = ParticleSystemGradientMode.Gradient; color = colorMin = colorMax = Color.white; gradient = g; gradientMin = gradientMax = null; }
            public static implicit operator MinMaxGradient(Color c) => new MinMaxGradient(c);
            public static implicit operator MinMaxGradient(Gradient g) => new MinMaxGradient(g);
            public Color Evaluate(float time) => gradient != null ? gradient.Evaluate(time) : color;
        }

        public struct Burst
        {
            public float time; public MinMaxCurve count; public int cycleCount; public float repeatInterval; public float probability;
            public Burst(float time, short count) { this.time = time; this.count = count; cycleCount = 1; repeatInterval = 0.01f; probability = 1f; }
            public Burst(float time, MinMaxCurve count) { this.time = time; this.count = count; cycleCount = 1; repeatInterval = 0.01f; probability = 1f; }
        }

        public struct Particle
        {
            public Vector3 position, velocity, angularVelocity3D, rotation3D, startSize3D;
            public float startLifetime, remainingLifetime, startSize, rotation, angularVelocity;
            public Color32 startColor;
            public uint randomSeed;
        }

        public struct EmitParams
        {
            public Vector3 position, velocity;
            public float startLifetime, startSize;
            public Color32 startColor;
            public bool applyShapeToPosition;
        }

        // Modules are views onto the system, as in Unity: a copy taken from
        // .emission and changed changes the system.
        public class MainModule
        {
            public float duration = 5f; public bool loop = true; public bool prewarm;
            public MinMaxCurve startDelay, startLifetime = 5f, startSpeed = 5f, startSize = 1f, startRotation, gravityModifier;
            public float startDelayMultiplier = 1f, startLifetimeMultiplier = 5f, startSpeedMultiplier = 5f, startSizeMultiplier = 1f;
            public float gravityModifierMultiplier = 1f, startRotationMultiplier = 1f, simulationSpeed = 1f;
            public bool startSize3D, startRotation3D, playOnAwake = true, useUnscaledTime;
            public MinMaxCurve startSizeX = 1f, startSizeY = 1f, startSizeZ = 1f;
            public MinMaxGradient startColor = new MinMaxGradient(Color.white);
            public ParticleSystemSimulationSpace simulationSpace;
            public Transform customSimulationSpace;
            public ParticleSystemScalingMode scalingMode = ParticleSystemScalingMode.Local;
            public int maxParticles = 1000;
            public ParticleSystemStopAction stopAction;
            public float flipRotation;
        }

        public class EmissionModule
        {
            public bool enabled = true;
            public MinMaxCurve rateOverTime = 10f, rateOverDistance;
            public float rateOverTimeMultiplier = 10f, rateOverDistanceMultiplier;
            private readonly List<Burst> m_bursts = new List<Burst>();
            public int burstCount { get { return m_bursts.Count; } set { while (m_bursts.Count > value) m_bursts.RemoveAt(m_bursts.Count - 1); while (m_bursts.Count < value) m_bursts.Add(new Burst()); } }
            public void SetBursts(Burst[] bursts) { m_bursts.Clear(); if (bursts != null) m_bursts.AddRange(bursts); }
            public void SetBursts(Burst[] bursts, int size) { m_bursts.Clear(); for (int i = 0; bursts != null && i < size && i < bursts.Length; ++i) m_bursts.Add(bursts[i]); }
            public int GetBursts(Burst[] bursts) { int n = Math.Min(bursts.Length, m_bursts.Count); m_bursts.CopyTo(0, bursts, 0, n); return n; }
            public void SetBurst(int i, Burst b) { if (i >= 0 && i < m_bursts.Count) m_bursts[i] = b; }
            public Burst GetBurst(int i) => i >= 0 && i < m_bursts.Count ? m_bursts[i] : new Burst();
        }

        public class ShapeModule
        {
            public bool enabled = true;
            public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
            public float radius = 1f, angle = 25f, arc = 360f, length = 5f, radiusThickness = 1f, randomDirectionAmount, sphericalDirectionAmount;
            public Vector3 position, rotation, scale = Vector3.one;
            public Mesh mesh;
            public MeshRenderer meshRenderer;
            public SkinnedMeshRenderer skinnedMeshRenderer;
        }

        public class VelocityOverLifetimeModule
        {
            public bool enabled;
            public MinMaxCurve x, y, z, orbitalX, orbitalY, orbitalZ, orbitalOffsetX, orbitalOffsetY, orbitalOffsetZ, radial, speedModifier = 1f;
            public float xMultiplier, yMultiplier, zMultiplier, orbitalXMultiplier, orbitalYMultiplier, orbitalZMultiplier, radialMultiplier, speedModifierMultiplier = 1f;
            public ParticleSystemSimulationSpace space;
        }

        public class LimitVelocityOverLifetimeModule { public bool enabled; public MinMaxCurve limit = 1f; public float limitMultiplier = 1f, dampen; public bool separateAxes; }
        public class InheritVelocityModule { public bool enabled; public MinMaxCurve curve; public float curveMultiplier; }
        public class ForceOverLifetimeModule { public bool enabled; public MinMaxCurve x, y, z; public float xMultiplier, yMultiplier, zMultiplier; public ParticleSystemSimulationSpace space; public bool randomized; }
        public class ColorOverLifetimeModule { public bool enabled; public MinMaxGradient color = new MinMaxGradient(Color.white); }
        public class ColorBySpeedModule { public bool enabled; public MinMaxGradient color = new MinMaxGradient(Color.white); public Vector2 range = new Vector2(0f, 1f); }
        public class SizeOverLifetimeModule { public bool enabled; public MinMaxCurve size = 1f, x = 1f, y = 1f, z = 1f; public float sizeMultiplier = 1f; public bool separateAxes; }
        public class SizeBySpeedModule { public bool enabled; public MinMaxCurve size = 1f; public float sizeMultiplier = 1f; public Vector2 range = new Vector2(0f, 1f); }
        public class RotationOverLifetimeModule { public bool enabled; public MinMaxCurve x, y, z; public float xMultiplier, yMultiplier, zMultiplier; public bool separateAxes; }
        public class RotationBySpeedModule { public bool enabled; public MinMaxCurve x, y, z; public bool separateAxes; }
        public class NoiseModule { public bool enabled; public MinMaxCurve strength = 1f; public float strengthMultiplier = 1f, frequency = 0.5f, scrollSpeedMultiplier; public int octaveCount = 1; public bool damping = true, separateAxes; }
        public class CollisionModule { public bool enabled; public MinMaxCurve dampen, bounce = 1f, lifetimeLoss; public float radiusScale = 1f; public bool sendCollisionMessages; public LayerMask collidesWith; }
        public class TriggerModule { public bool enabled; }
        public class SubEmittersModule { public bool enabled; public int subEmittersCount => 0; }
        public class TextureSheetAnimationModule { public bool enabled; public int numTilesX = 1, numTilesY = 1; public MinMaxCurve frameOverTime, startFrame; public int cycleCount = 1; }
        public class LightsModule { public bool enabled; public float ratio, intensityMultiplier = 1f, rangeMultiplier = 1f; public Light light; public int maxLights = 20; }
        public class TrailModule { public bool enabled; public float ratio = 1f, minVertexDistance = 0.2f; public MinMaxCurve lifetime = 1f, widthOverTrail = 1f; public MinMaxGradient colorOverLifetime = new MinMaxGradient(Color.white), colorOverTrail = new MinMaxGradient(Color.white); public bool worldSpace, dieWithParticles = true, inheritParticleColor = true; }
        public class ExternalForcesModule { public bool enabled; public float multiplier = 1f; }
        public class CustomDataModule { public bool enabled; }

        public MainModule main { get; } = new MainModule();
        public EmissionModule emission { get; } = new EmissionModule();
        public ShapeModule shape { get; } = new ShapeModule();
        public VelocityOverLifetimeModule velocityOverLifetime { get; } = new VelocityOverLifetimeModule();
        public LimitVelocityOverLifetimeModule limitVelocityOverLifetime { get; } = new LimitVelocityOverLifetimeModule();
        public InheritVelocityModule inheritVelocity { get; } = new InheritVelocityModule();
        public ForceOverLifetimeModule forceOverLifetime { get; } = new ForceOverLifetimeModule();
        public ColorOverLifetimeModule colorOverLifetime { get; } = new ColorOverLifetimeModule();
        public ColorBySpeedModule colorBySpeed { get; } = new ColorBySpeedModule();
        public SizeOverLifetimeModule sizeOverLifetime { get; } = new SizeOverLifetimeModule();
        public SizeBySpeedModule sizeBySpeed { get; } = new SizeBySpeedModule();
        public RotationOverLifetimeModule rotationOverLifetime { get; } = new RotationOverLifetimeModule();
        public RotationBySpeedModule rotationBySpeed { get; } = new RotationBySpeedModule();
        public NoiseModule noise { get; } = new NoiseModule();
        public CollisionModule collision { get; } = new CollisionModule();
        public TriggerModule trigger { get; } = new TriggerModule();
        public SubEmittersModule subEmitters { get; } = new SubEmittersModule();
        public TextureSheetAnimationModule textureSheetAnimation { get; } = new TextureSheetAnimationModule();
        public LightsModule lights { get; } = new LightsModule();
        public TrailModule trails { get; } = new TrailModule();
        public ExternalForcesModule externalForces { get; } = new ExternalForcesModule();
        public CustomDataModule customData { get; } = new CustomDataModule();

        private bool m_playing, m_paused, m_everPlayed;
        private float m_startTime;

        public bool isPlaying { get { AutoPlay(); return m_playing && !m_paused && (main.loop || time < main.duration); } }
        public bool isEmitting => isPlaying && emission.enabled;
        public bool isStopped { get { AutoPlay(); return !m_playing; } }
        public bool isPaused => m_paused;
        public bool IsAlive() => isPlaying;
        public bool IsAlive(bool withChildren) => isPlaying;
        public int particleCount => 0;
        public float totalTime => m_playing ? Elintria.Time.time - m_startTime : 0f;
        public float time
        {
            get { float t = totalTime; return main.loop && main.duration > 0f ? t % main.duration : Math.Min(t, main.duration); }
            set { m_startTime = Elintria.Time.time - value; }
        }
        public uint randomSeed;
        public bool useAutoRandomSeed = true;
        public bool proceduralSimulationSupported => true;

        // Unity starts a play-on-awake system by itself.
        private void AutoPlay()
        {
            if (!m_everPlayed && main.playOnAwake) { m_everPlayed = true; m_playing = true; m_startTime = Elintria.Time.time; }
        }

        private static bool s_said;
        private static void Say()
        {
            if (s_said) return;
            s_said = true;
            Elintria.Debug.Log("[Unity compat] particle systems run but are not drawn yet - Elintria has no particle renderer");
        }

        public void Play() { Play(true); }
        public void Play(bool withChildren)
        {
            Say();
            m_everPlayed = true;
            if (!m_playing || m_paused) { if (!m_paused) m_startTime = Elintria.Time.time; m_playing = true; m_paused = false; }
            if (withChildren) ForChildren(p => p.Play(false));
        }

        public void Stop() { Stop(true); }
        public void Stop(bool withChildren) { Stop(withChildren, ParticleSystemStopBehavior.StopEmitting); }
        public void Stop(bool withChildren, ParticleSystemStopBehavior behavior)
        {
            m_everPlayed = true;
            m_playing = false;
            m_paused = false;
            if (withChildren) ForChildren(p => p.Stop(false, behavior));
        }

        public void Pause() { Pause(true); }
        public void Pause(bool withChildren) { if (m_playing) m_paused = true; if (withChildren) ForChildren(p => p.Pause(false)); }
        public void Clear() { }
        public void Clear(bool withChildren) { }
        public void Simulate(float t) { }
        public void Simulate(float t, bool withChildren, bool restart = true, bool fixedTimeStep = true) { }
        public void Emit(int count) { Say(); }
        public void Emit(EmitParams emitParams, int count) { Say(); }
        public int GetParticles(Particle[] particles) => 0;
        public int GetParticles(Particle[] particles, int size) => 0;
        public void SetParticles(Particle[] particles) { }
        public void SetParticles(Particle[] particles, int size) { }

        private void ForChildren(Action<ParticleSystem> a)
        {
            if (gameObject == null) return;
            foreach (ParticleSystem p in gameObject.GetComponentsInChildren<ParticleSystem>())
                if (p != null && p != this) a(p);
        }
    }

    public class ParticleSystemRenderer : Renderer
    {
        public ParticleSystemRenderMode renderMode;
        public float lengthScale = 2f, velocityScale, minParticleSize, maxParticleSize = 0.5f;
        public Mesh mesh;
        public Material trailMaterial;
    }
}

// =============================================================================
//  UnityEngine.Audio
//
//  Elintria mixes every source straight to the output, so a mixer is a
//  store of the parameters a script sets and gets back. Groups exist so
//  "route this source to the SFX group" compiles and is remembered.
// =============================================================================
namespace UnityEngine.Audio
{
    public class AudioMixer : UnityEngine.Object
    {
        private readonly Dictionary<string, float> m_params = new Dictionary<string, float>();
        private readonly List<AudioMixerGroup> m_groups = new List<AudioMixerGroup>();
        public AudioMixerGroup outputAudioMixerGroup;
        public AudioMixerUpdateMode updateMode;

        public bool SetFloat(string name, float value) { m_params[name ?? ""] = value; return true; }
        public bool GetFloat(string name, out float value) { return m_params.TryGetValue(name ?? "", out value); }
        public bool ClearFloat(string name) => m_params.Remove(name ?? "");

        public AudioMixerGroup[] FindMatchingGroups(string subPath)
        {
            // Unity answers with the groups whose path contains |subPath|;
            // here a group is made the first time it is asked for, so the
            // same name always gives the same group.
            string key = subPath ?? "";
            foreach (AudioMixerGroup g in m_groups)
                if (g.name == key) return new[] { g };
            var made = new AudioMixerGroup { name = key, audioMixer = this };
            m_groups.Add(made);
            return new[] { made };
        }

        public AudioMixerSnapshot FindSnapshot(string name) => new AudioMixerSnapshot { name = name, audioMixer = this };
        public void TransitionToSnapshots(AudioMixerSnapshot[] snapshots, float[] weights, float timeToReach) { }

        // A script's mixer field holds the asset's path; every field naming
        // the same asset gets the same mixer, as in Unity.
        private static readonly Dictionary<string, AudioMixer> s_byPath = new Dictionary<string, AudioMixer>();

        public static implicit operator AudioMixer(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            AudioMixer m;
            if (!s_byPath.TryGetValue(path, out m))
            {
                m = new AudioMixer { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                s_byPath[path] = m;
            }
            return m;
        }
    }

    public enum AudioMixerUpdateMode { Normal, UnscaledTime }

    public class AudioMixerGroup : UnityEngine.Object
    {
        public AudioMixer audioMixer { get; internal set; }

        // A group is inside its mixer's asset, so the path is the mixer's:
        // the group is that mixer's Master.
        public static implicit operator AudioMixerGroup(string path)
        {
            AudioMixer m = path;
            return m != null ? m.FindMatchingGroups("Master")[0] : null;
        }
    }

    public class AudioMixerSnapshot : UnityEngine.Object
    {
        public AudioMixer audioMixer { get; internal set; }
        public void TransitionTo(float timeToReach) { }
    }
}

// =============================================================================
//  UnityEngine.AI - on Elintria's own navigation
//
//  An agent imported from Unity became an Elintria nav agent (Window > AI >
//  Navigation bakes the mesh). NavMeshAgent drives that agent; NavMesh
//  queries run against the baked mesh.
// =============================================================================
namespace UnityEngine.AI
{
    public enum NavMeshPathStatus { PathComplete, PathPartial, PathInvalid }
    public enum ObstacleAvoidanceType { NoObstacleAvoidance, LowQualityObstacleAvoidance, MedQualityObstacleAvoidance, GoodQualityObstacleAvoidance, HighQualityObstacleAvoidance }
    public enum NavMeshObstacleShape { Capsule, Box }

    public struct NavMeshHit
    {
        public Vector3 position, normal;
        public float distance;
        public int mask;
        public bool hit;
    }

    public class NavMeshPath
    {
        internal Vector3[] m_corners = new Vector3[0];
        public Vector3[] corners => m_corners;
        public NavMeshPathStatus status { get; internal set; } = NavMeshPathStatus.PathInvalid;
        public int GetCornersNonAlloc(Vector3[] results) { int n = Math.Min(results.Length, m_corners.Length); Array.Copy(m_corners, results, n); return n; }
        public void ClearCorners() { m_corners = new Vector3[0]; status = NavMeshPathStatus.PathInvalid; }
    }

    public static class NavMesh
    {
        public const int AllAreas = -1;

        public static bool SamplePosition(Vector3 sourcePosition, out NavMeshHit hit, float maxDistance, int areaMask)
        {
            Vector3 p;
            bool ok = Elintria.NavAgent.SamplePosition(sourcePosition, maxDistance, out p);
            hit = new NavMeshHit { position = p, hit = ok, distance = ok ? Vector3.Distance(sourcePosition, p) : float.PositiveInfinity, mask = areaMask };
            return ok;
        }

        public static bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit, int areaMask)
        {
            Vector3 p;
            bool blocked = Elintria.NavAgent.Raycast(sourcePosition, targetPosition, out p);
            hit = new NavMeshHit { position = p, hit = blocked, distance = Vector3.Distance(sourcePosition, p), mask = areaMask };
            return blocked;
        }

        public static bool CalculatePath(Vector3 sourcePosition, Vector3 targetPosition, int areaMask, NavMeshPath path)
        {
            Vector3[] corners;
            bool ok = Elintria.NavAgent.CalculatePath(sourcePosition, targetPosition, out corners);
            if (path != null)
            {
                path.m_corners = corners ?? new Vector3[0];
                path.status = ok ? NavMeshPathStatus.PathComplete : NavMeshPathStatus.PathInvalid;
            }
            return ok;
        }

        public static int GetAreaFromName(string areaName) => areaName == "Walkable" ? 0 : areaName == "Not Walkable" ? 1 : areaName == "Jump" ? 2 : -1;
        public static float GetAreaCost(int areaIndex) => 1f;
        public static void SetAreaCost(int areaIndex, float cost) { }
        public static void RemoveAllNavMeshData() { }
    }

    public class NavMeshAgent : InertComponent
    {
        private Elintria.NavAgent Agent => Elintria.NavAgent.Of(gameObject);

        public bool SetDestination(Vector3 target) => Agent.SetDestination(target);
        public Vector3 destination { get { return Agent.destination; } set { Agent.SetDestination(value); } }
        public Vector3 velocity { get { return Agent.velocity; } set { } }
        public Vector3 desiredVelocity => Agent.velocity;
        public Vector3 nextPosition { get { return transform != null ? transform.position : Vector3.zero; } set { } }
        public Vector3 steeringTarget => Agent.destination;
        public float speed { get { return Agent.speed; } set { Agent.speed = value; } }
        public float angularSpeed = 120f, acceleration = 8f, stoppingDistance, radius = 0.5f, height = 2f, baseOffset;
        public bool autoBraking = true, autoRepath = true, autoTraverseOffMeshLink = true, updatePosition = true, updateRotation = true, updateUpAxis = true;
        public int avoidancePriority = 50, areaMask = -1, agentTypeID;
        public ObstacleAvoidanceType obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        public bool isStopped { get { return Agent.isStopped; } set { Agent.isStopped = value; } }
        public float remainingDistance => Agent.remainingDistance;
        public bool hasPath => Agent.hasPath;
        public bool pathPending => false;
        public bool isOnNavMesh => Agent.isOnNavMesh;
        public bool isOnOffMeshLink => Agent.isOnOffMeshLink;
        public bool isPathStale => false;
        public NavMeshPathStatus pathStatus => (NavMeshPathStatus)(2 - Agent.pathStatus);
        public NavMeshPath path
        {
            get
            {
                var p = new NavMeshPath();
                if (transform != null) NavMesh.CalculatePath(transform.position, destination, -1, p);
                return p;
            }
            set { }
        }

        public bool Warp(Vector3 newPosition) => Agent.Warp(newPosition);
        public void ResetPath() { Agent.ResetPath(); }
        public void Stop() { isStopped = true; }
        public void Resume() { isStopped = false; }
        public void Move(Vector3 offset) { if (transform != null) Warp(transform.position + offset); }
        public bool CalculatePath(Vector3 targetPosition, NavMeshPath p)
            => transform != null && NavMesh.CalculatePath(transform.position, targetPosition, -1, p);
        public bool SetPath(NavMeshPath p) => p != null && p.corners.Length > 0 && SetDestination(p.corners[p.corners.Length - 1]);
        public void CompleteOffMeshLink() { }
    }

    public class NavMeshObstacle : InertComponent
    {
        public NavMeshObstacleShape shape = NavMeshObstacleShape.Box;
        public Vector3 center, size = Vector3.one;
        public float radius = 0.5f, height = 2f;
        public bool carving, carveOnlyStationary = true;
    }
}

// =============================================================================
//  Unity.Play / ProBuilder - not available
// =============================================================================
namespace UnityEngine.ProBuilder
{
    /// <summary>ProBuilder's editable mesh. The importer turned every
    /// ProBuilder object into an ordinary mesh, so there is none of these in
    /// a scene and GetComponent finds nothing - which is how code written to
    /// handle both kinds of mesh already copes.</summary>
    public class ProBuilderMesh : InertComponent
    {
        public void ToMesh() { }
        public void Refresh() { }
        public int vertexCount => 0;
        public int faceCount => 0;
    }
}
