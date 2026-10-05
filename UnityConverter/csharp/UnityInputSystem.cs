// UnityEngine.InputSystem - part of the Unity Converter plugin.
//
// Unity's Input System reads devices through actions: "Player/Move" is a
// Vector2 made of W, A, S and D; "Player/Fire" is the left mouse button. The
// actions live in a .inputactions file (JSON), and InputSystem.actions is the
// project-wide one chosen in Unity's Project Settings.
//
// This reads that same file from the imported project and evaluates its
// bindings against Elintria's keyboard and mouse every frame, so a converted
// script that asks InputSystem.actions.FindAction("Player/Move") gets the
// keys it was built for:
//
//   <Keyboard>/w, <Keyboard>/leftShift, <Keyboard>/1 ...    keys
//   <Mouse>/leftButton, <Mouse>/delta, <Mouse>/scroll/y ... the mouse
//   <Pointer>/delta, <Pointer>/position                      the same, as a pointer
//   */{Submit}, */{Cancel}                                   Enter / Escape
//   2DVector, Dpad, 1DAxis, Axis, ButtonWithOneModifier, OneModifier composites
//   invertVector2, scaleVector2, normalizeVector2, invert, scale, clamp processors
//
// Gamepads and joysticks read as disconnected: Elintria has no gamepad input
// yet, and Gamepad.current is null exactly as it is in Unity with none
// plugged in.
//
// Keyboard.current and Mouse.current work directly as well.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Elintria;

namespace UnityEngine.InputSystem
{
    // =========================================================================
    //  Frame clock
    // =========================================================================
    internal static class InputFrame
    {
        private static float s_lastTime = -1f;
        private static int s_frame;

        // Advances once per engine frame, however many times it is asked.
        internal static int Current
        {
            get
            {
                float t = Elintria.Time.time;
                if (t != s_lastTime) { s_lastTime = t; ++s_frame; }
                return s_frame;
            }
        }
    }

    // =========================================================================
    //  Devices
    // =========================================================================
    public enum Key
    {
        None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash,
        LeftBracket, RightBracket, Minus, Equals, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P,
        Q, R, S, T, U, V, W, X, Y, Z, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8,
        Digit9, Digit0, LeftShift, RightShift, LeftAlt, RightAlt, AltGr = RightAlt, LeftCtrl, RightCtrl,
        LeftMeta, RightMeta, LeftWindows = LeftMeta, RightWindows = RightMeta, LeftApple = LeftMeta,
        RightApple = RightMeta, LeftCommand = LeftMeta, RightCommand = RightMeta, ContextMenu, Escape,
        LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert,
        Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause, NumpadEnter, NumpadDivide,
        NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals, Numpad0, Numpad1,
        Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9, F1, F2, F3, F4, F5,
        F6, F7, F8, F9, F10, F11, F12, OEM1, OEM2, OEM3, OEM4, OEM5, IMESelected
    }

    public abstract class InputControl
    {
        public string name { get; protected set; } = string.Empty;
        public string path => "/" + name;
        public abstract float Magnitude();
    }

    public class AxisControl : InputControl
    {
        private readonly Func<float> m_read;
        internal AxisControl(string name, Func<float> read) { this.name = name; m_read = read; }
        public float ReadValue() => m_read != null ? m_read() : 0f;
        public override float Magnitude() => Math.Abs(ReadValue());
    }

    public class ButtonControl : AxisControl
    {
        private readonly Func<bool> m_held, m_down, m_up;

        internal ButtonControl(string name, Func<bool> held, Func<bool> down, Func<bool> up)
            : base(name, null)
        {
            m_held = held; m_down = down; m_up = up;
        }

        public bool isPressed => m_held != null && m_held();
        public bool wasPressedThisFrame => m_down != null && m_down();
        public bool wasReleasedThisFrame => m_up != null && m_up();
        public new float ReadValue() => isPressed ? 1f : 0f;
        public override float Magnitude() => isPressed ? 1f : 0f;
    }

    public class KeyControl : ButtonControl
    {
        public Key keyCode { get; private set; }

        internal KeyControl(string name, Key key, Elintria.KeyCode vk)
            : base(name,
                   () => vk != Elintria.KeyCode.None && Elintria.Input.GetKey(vk),
                   () => vk != Elintria.KeyCode.None && Elintria.Input.GetKeyDown(vk),
                   () => vk != Elintria.KeyCode.None && Elintria.Input.GetKeyUp(vk))
        {
            keyCode = key;
        }
    }

    public class Vector2Control : InputControl
    {
        private readonly Func<Elintria.Vector2> m_read;
        public AxisControl x { get; private set; }
        public AxisControl y { get; private set; }

        internal Vector2Control(string name, Func<Elintria.Vector2> read)
        {
            this.name = name;
            m_read = read;
            x = new AxisControl(name + "/x", () => ReadValue().x);
            y = new AxisControl(name + "/y", () => ReadValue().y);
        }

        public Elintria.Vector2 ReadValue() => m_read != null ? m_read() : Elintria.Vector2.zero;
        public override float Magnitude() => ReadValue().magnitude;
    }

    public class InputDevice
    {
        public string displayName { get; protected set; } = string.Empty;
        public bool enabled => true;
        public bool added => true;
    }

    public class Keyboard : InputDevice
    {
        private static Keyboard s_current;
        public static Keyboard current => s_current ?? (s_current = new Keyboard());

        private readonly Dictionary<Key, KeyControl> m_keys = new Dictionary<Key, KeyControl>();

        internal static Elintria.KeyCode ToVk(Key k)
        {
            if (k >= Key.A && k <= Key.Z) return (Elintria.KeyCode)((int)Elintria.KeyCode.A + (k - Key.A));
            if (k >= Key.Digit1 && k <= Key.Digit9) return (Elintria.KeyCode)((int)Elintria.KeyCode.Alpha1 + (k - Key.Digit1));
            if (k >= Key.F1 && k <= Key.F12) return (Elintria.KeyCode)((int)Elintria.KeyCode.F1 + (k - Key.F1));
            if (k >= Key.Numpad0 && k <= Key.Numpad9) return (Elintria.KeyCode)(96 + (k - Key.Numpad0));
            switch (k)
            {
            case Key.Digit0: return Elintria.KeyCode.Alpha0;
            case Key.Space: return Elintria.KeyCode.Space;
            case Key.Enter: case Key.NumpadEnter: return Elintria.KeyCode.Return;
            case Key.Tab: return Elintria.KeyCode.Tab;
            case Key.Escape: return Elintria.KeyCode.Escape;
            case Key.Backspace: return Elintria.KeyCode.Backspace;
            // The host reports the generic modifiers, not the sided ones.
            case Key.LeftShift: case Key.RightShift: return Elintria.KeyCode.Shift;
            case Key.LeftCtrl: case Key.RightCtrl: return Elintria.KeyCode.Control;
            case Key.LeftAlt: case Key.RightAlt: return Elintria.KeyCode.Alt;
            case Key.LeftArrow: return Elintria.KeyCode.LeftArrow;
            case Key.RightArrow: return Elintria.KeyCode.RightArrow;
            case Key.UpArrow: return Elintria.KeyCode.UpArrow;
            case Key.DownArrow: return Elintria.KeyCode.DownArrow;
            case Key.PageUp: return Elintria.KeyCode.PageUp;
            case Key.PageDown: return Elintria.KeyCode.PageDown;
            case Key.Home: return Elintria.KeyCode.Home;
            case Key.End: return Elintria.KeyCode.End;
            case Key.Insert: return Elintria.KeyCode.Insert;
            case Key.Delete: return Elintria.KeyCode.Delete;
            case Key.CapsLock: return Elintria.KeyCode.CapsLock;
            case Key.Minus: return (Elintria.KeyCode)189;
            case Key.Equals: return (Elintria.KeyCode)187;
            case Key.Comma: return (Elintria.KeyCode)188;
            case Key.Period: return (Elintria.KeyCode)190;
            case Key.Slash: return (Elintria.KeyCode)191;
            case Key.Backquote: return (Elintria.KeyCode)192;
            case Key.LeftBracket: return (Elintria.KeyCode)219;
            case Key.Backslash: return (Elintria.KeyCode)220;
            case Key.RightBracket: return (Elintria.KeyCode)221;
            case Key.Quote: return (Elintria.KeyCode)222;
            case Key.Semicolon: return (Elintria.KeyCode)186;
            default: return Elintria.KeyCode.None;
            }
        }

        public Keyboard() { displayName = "Keyboard"; }

        public KeyControl this[Key key]
        {
            get
            {
                KeyControl c;
                if (!m_keys.TryGetValue(key, out c))
                {
                    string n = key.ToString();
                    c = new KeyControl(char.ToLowerInvariant(n[0]) + n.Substring(1), key, ToVk(key));
                    m_keys[key] = c;
                }
                return c;
            }
        }

        private ButtonControl m_any;
        public ButtonControl anyKey => m_any ?? (m_any = new ButtonControl("anyKey",
            () => AnyKey(false), () => AnyKey(true), () => false));

        private static bool AnyKey(bool down)
        {
            for (int vk = 8; vk < 223; ++vk)
            {
                if (down ? Elintria.Input.GetKeyDown((Elintria.KeyCode)vk)
                         : Elintria.Input.GetKey((Elintria.KeyCode)vk)) return true;
            }
            return false;
        }

        public KeyControl aKey => this[Key.A]; public KeyControl bKey => this[Key.B];
        public KeyControl cKey => this[Key.C]; public KeyControl dKey => this[Key.D];
        public KeyControl eKey => this[Key.E]; public KeyControl fKey => this[Key.F];
        public KeyControl gKey => this[Key.G]; public KeyControl hKey => this[Key.H];
        public KeyControl iKey => this[Key.I]; public KeyControl jKey => this[Key.J];
        public KeyControl kKey => this[Key.K]; public KeyControl lKey => this[Key.L];
        public KeyControl mKey => this[Key.M]; public KeyControl nKey => this[Key.N];
        public KeyControl oKey => this[Key.O]; public KeyControl pKey => this[Key.P];
        public KeyControl qKey => this[Key.Q]; public KeyControl rKey => this[Key.R];
        public KeyControl sKey => this[Key.S]; public KeyControl tKey => this[Key.T];
        public KeyControl uKey => this[Key.U]; public KeyControl vKey => this[Key.V];
        public KeyControl wKey => this[Key.W]; public KeyControl xKey => this[Key.X];
        public KeyControl yKey => this[Key.Y]; public KeyControl zKey => this[Key.Z];
        public KeyControl digit0Key => this[Key.Digit0]; public KeyControl digit1Key => this[Key.Digit1];
        public KeyControl digit2Key => this[Key.Digit2]; public KeyControl digit3Key => this[Key.Digit3];
        public KeyControl digit4Key => this[Key.Digit4]; public KeyControl digit5Key => this[Key.Digit5];
        public KeyControl digit6Key => this[Key.Digit6]; public KeyControl digit7Key => this[Key.Digit7];
        public KeyControl digit8Key => this[Key.Digit8]; public KeyControl digit9Key => this[Key.Digit9];
        public KeyControl spaceKey => this[Key.Space]; public KeyControl enterKey => this[Key.Enter];
        public KeyControl tabKey => this[Key.Tab]; public KeyControl escapeKey => this[Key.Escape];
        public KeyControl backspaceKey => this[Key.Backspace]; public KeyControl deleteKey => this[Key.Delete];
        public KeyControl leftShiftKey => this[Key.LeftShift]; public KeyControl rightShiftKey => this[Key.RightShift];
        public KeyControl leftCtrlKey => this[Key.LeftCtrl]; public KeyControl rightCtrlKey => this[Key.RightCtrl];
        public KeyControl leftAltKey => this[Key.LeftAlt]; public KeyControl rightAltKey => this[Key.RightAlt];
        public KeyControl shiftKey => this[Key.LeftShift]; public KeyControl ctrlKey => this[Key.LeftCtrl];
        public KeyControl altKey => this[Key.LeftAlt];
        public KeyControl upArrowKey => this[Key.UpArrow]; public KeyControl downArrowKey => this[Key.DownArrow];
        public KeyControl leftArrowKey => this[Key.LeftArrow]; public KeyControl rightArrowKey => this[Key.RightArrow];
        public KeyControl pageUpKey => this[Key.PageUp]; public KeyControl pageDownKey => this[Key.PageDown];
        public KeyControl homeKey => this[Key.Home]; public KeyControl endKey => this[Key.End];
        public KeyControl insertKey => this[Key.Insert]; public KeyControl capsLockKey => this[Key.CapsLock];
        public KeyControl minusKey => this[Key.Minus]; public KeyControl equalsKey => this[Key.Equals];
        public KeyControl commaKey => this[Key.Comma]; public KeyControl periodKey => this[Key.Period];
        public KeyControl slashKey => this[Key.Slash]; public KeyControl backquoteKey => this[Key.Backquote];
        public KeyControl f1Key => this[Key.F1]; public KeyControl f2Key => this[Key.F2];
        public KeyControl f3Key => this[Key.F3]; public KeyControl f4Key => this[Key.F4];
        public KeyControl f5Key => this[Key.F5]; public KeyControl f6Key => this[Key.F6];
        public KeyControl f7Key => this[Key.F7]; public KeyControl f8Key => this[Key.F8];
        public KeyControl f9Key => this[Key.F9]; public KeyControl f10Key => this[Key.F10];
        public KeyControl f11Key => this[Key.F11]; public KeyControl f12Key => this[Key.F12];
        public KeyControl numpadEnterKey => this[Key.NumpadEnter];
    }

    public class Pointer : InputDevice
    {
        public Vector2Control position { get; protected set; }
        public Vector2Control delta { get; protected set; }
        public ButtonControl press { get; protected set; }
    }

    public class Mouse : Pointer
    {
        private static Mouse s_current;
        public static Mouse current => s_current ?? (s_current = new Mouse());

        public ButtonControl leftButton { get; private set; }
        public ButtonControl rightButton { get; private set; }
        public ButtonControl middleButton { get; private set; }
        public Vector2Control scroll { get; private set; }

        private int m_deltaFrame = -1;
        private Elintria.Vector2 m_delta;

        public Mouse()
        {
            displayName = "Mouse";
            leftButton = Button("leftButton", 0);
            rightButton = Button("rightButton", 1);
            middleButton = Button("middleButton", 2);
            press = leftButton;
            // Unity's pointer origin is the bottom left; the window's is the top.
            position = new Vector2Control("position", () =>
            {
                Elintria.Vector2 p = Elintria.Input.mousePosition;
                return new Elintria.Vector2(p.x, Screen.height - p.y);
            });
            delta = new Vector2Control("delta", ReadDelta);
            scroll = new Vector2Control("scroll",
                () => new Elintria.Vector2(0f, Elintria.Input.mouseScrollDelta * 120f));
        }

        private static ButtonControl Button(string name, int b)
            => new ButtonControl(name, () => Elintria.Input.GetMouseButton(b),
                                 () => Elintria.Input.GetMouseButtonDown(b),
                                 () => Elintria.Input.GetMouseButtonUp(b));

        // Once per frame, however often it is read. While the cursor is
        // locked the pointer is pulled back to the middle of the window every
        // frame, which is how an unbounded delta is had from a window API;
        // otherwise it is the plain difference in position. Up is positive,
        // as it is in Unity.
        private Elintria.Vector2 ReadDelta()
        {
            int f = InputFrame.Current;
            if (f != m_deltaFrame)
            {
                m_deltaFrame = f;
                Elintria.Vector2 d = Cursor.lockState == CursorLockMode.Locked
                                   ? Elintria.Mouse.RecenterAndGetDelta()
                                   : Elintria.Input.mouseDelta;
                m_delta = new Elintria.Vector2(d.x, -d.y);
            }
            return m_delta;
        }
    }

    /// <summary>No gamepad support yet - reads as none connected.</summary>
    public class Gamepad : InputDevice
    {
        public static Gamepad current => null;
        public static Gamepad[] all => new Gamepad[0];
    }

    public class Touchscreen : Pointer
    {
        public static Touchscreen current => null;
    }

    // =========================================================================
    //  Actions
    // =========================================================================
    public enum InputActionType { Value, Button, PassThrough }
    public enum InputActionPhase { Disabled, Waiting, Started, Performed, Canceled }

    public struct InputBinding
    {
        public string name;
        public string path;
        public string groups;
        public string processors;
        public string interactions;
        public string action;
        public bool isComposite;
        public bool isPartOfComposite;
        public string effectivePath => path;
    }

    public class InputAction
    {
        public struct CallbackContext
        {
            internal InputAction m_action;
            internal InputActionPhase m_phase;

            public InputAction action => m_action;
            public InputActionPhase phase => m_phase;
            public bool started => m_phase == InputActionPhase.Started;
            public bool performed => m_phase == InputActionPhase.Performed;
            public bool canceled => m_phase == InputActionPhase.Canceled;
            public double time => Elintria.Time.time;
            public TValue ReadValue<TValue>() where TValue : struct
                => m_action != null ? m_action.ReadValue<TValue>() : default(TValue);
            public object ReadValueAsObject() => m_action?.ReadValueAsObject();
            public bool ReadValueAsButton() => m_action != null && m_action.IsPressed();
        }

        public string name { get; private set; }
        public InputActionType type { get; private set; }
        public string expectedControlType { get; private set; }
        public InputActionMap actionMap { get; internal set; }
        public bool enabled { get; private set; } = true;
        public Guid id { get; } = Guid.NewGuid();

        internal readonly List<InputBinding> m_bindings = new List<InputBinding>();
        public IReadOnlyList<InputBinding> bindings => m_bindings;

        public event Action<CallbackContext> started;
        public event Action<CallbackContext> performed;
        public event Action<CallbackContext> canceled;

        private int m_frame = -1;
        private bool m_pressed, m_wasPressed;
        private bool m_down, m_up;
        private float m_axis;
        private Elintria.Vector2 m_vec;
        private float m_magnitude;
        private int m_performedFrame = -1;

        public InputAction(string name = null, InputActionType type = InputActionType.Value,
                           string binding = null, string interactions = null,
                           string processors = null, string expectedControlType = null)
        {
            this.name = name ?? string.Empty;
            this.type = type;
            this.expectedControlType = expectedControlType ?? string.Empty;
            if (!string.IsNullOrEmpty(binding))
                m_bindings.Add(new InputBinding { path = binding, processors = processors, interactions = interactions });
        }

        internal InputAction(string name, InputActionType type, string expected, bool dummy)
        {
            this.name = name;
            this.type = type;
            expectedControlType = expected ?? string.Empty;
        }

        public void Enable() { enabled = true; }
        public void Disable() { enabled = false; }
        public void Dispose() { }

        public InputAction AddBinding(string path, string interactions = null, string processors = null, string groups = null)
        {
            m_bindings.Add(new InputBinding { path = path, interactions = interactions, processors = processors, groups = groups });
            return this;
        }

        // ── Reading ──────────────────────────────────────────────────────────
        public TValue ReadValue<TValue>() where TValue : struct
        {
            Tick();
            object v;
            Type t = typeof(TValue);
            if (t == typeof(Elintria.Vector2)) v = m_vec;
            else if (t == typeof(float)) v = m_axis;
            else if (t == typeof(double)) v = (double)m_axis;
            else if (t == typeof(int)) v = (int)Math.Round(m_axis);
            else if (t == typeof(bool)) v = m_pressed;
            else if (t == typeof(Elintria.Vector3)) v = new Elintria.Vector3(m_vec.x, m_vec.y, 0f);
            else return default(TValue);
            return (TValue)v;
        }

        public object ReadValueAsObject()
        {
            Tick();
            if (IsVector) return m_vec;
            return m_axis;
        }

        public bool IsPressed() { Tick(); return m_pressed; }
        public bool WasPressedThisFrame() { Tick(); return m_down; }
        public bool WasReleasedThisFrame() { Tick(); return m_up; }
        public bool WasPerformedThisFrame() { Tick(); return m_performedFrame == m_frame; }
        public bool IsInProgress() { Tick(); return m_magnitude > 0f; }
        public bool triggered => WasPerformedThisFrame();
        public InputActionPhase phase
        {
            get
            {
                Tick();
                if (!enabled) return InputActionPhase.Disabled;
                return m_pressed ? InputActionPhase.Performed : InputActionPhase.Waiting;
            }
        }

        public float GetControlMagnitude() { Tick(); return m_magnitude; }

        private bool IsVector
            => expectedControlType == "Vector2" || expectedControlType == "Stick" ||
               expectedControlType == "Dpad" || expectedControlType == "Vector3";

        // ── One frame's state ────────────────────────────────────────────────
        internal void Tick()
        {
            int f = InputFrame.Current;
            if (f == m_frame) return;
            m_frame = f;

            m_wasPressed = m_pressed;
            if (!enabled || (actionMap != null && !actionMap.enabled))
            {
                m_pressed = false; m_axis = 0f; m_vec = Elintria.Vector2.zero; m_magnitude = 0f;
                m_down = false; m_up = m_wasPressed;
                return;
            }

            bool anyDownEdge;
            BindingEval.Evaluate(this, out m_vec, out m_axis, out m_magnitude, out anyDownEdge);

            // A button counts as pressed past half-way, as Unity's default
            // press point is. Value actions are "pressed" while non-zero.
            float press = type == InputActionType.Button ? 0.5f : 0.0001f;
            m_pressed = m_magnitude >= press;
            m_down = (m_pressed && !m_wasPressed) || (anyDownEdge && m_pressed);
            m_up = !m_pressed && m_wasPressed;

            if (type == InputActionType.Button ? m_down : (m_pressed || m_up))
                m_performedFrame = f;

            Fire();
        }

        private void Fire()
        {
            if (started == null && performed == null && canceled == null) return;
            try
            {
                if (m_down && started != null)
                    started(new CallbackContext { m_action = this, m_phase = InputActionPhase.Started });
                if (m_performedFrame == m_frame && performed != null)
                    performed(new CallbackContext { m_action = this, m_phase = InputActionPhase.Performed });
                if (m_up && canceled != null)
                    canceled(new CallbackContext { m_action = this, m_phase = InputActionPhase.Canceled });
            }
            catch (Exception e)
            {
                Elintria.Debug.LogError("InputAction '" + name + "' callback threw: " + e.Message);
            }
        }

        public override string ToString() => (actionMap != null ? actionMap.name + "/" : "") + name;
    }

    public class InputActionMap
    {
        public string name { get; private set; }
        public InputActionAsset asset { get; internal set; }
        public bool enabled { get; private set; } = true;
        internal readonly List<InputAction> m_actions = new List<InputAction>();
        public IReadOnlyList<InputAction> actions => m_actions;

        public InputActionMap(string name = null) { this.name = name ?? string.Empty; }

        public InputAction FindAction(string actionName, bool throwIfNotFound = false)
        {
            for (int i = 0; i < m_actions.Count; ++i)
                if (string.Equals(m_actions[i].name, actionName, StringComparison.OrdinalIgnoreCase))
                    return m_actions[i];
            if (throwIfNotFound) throw new ArgumentException("No action '" + actionName + "' in map '" + name + "'");
            return null;
        }

        public InputAction this[string actionName] => FindAction(actionName, true);

        public InputAction AddAction(string actionName, InputActionType type = InputActionType.Value,
                                     string binding = null)
        {
            var a = new InputAction(actionName, type, binding);
            a.actionMap = this;
            m_actions.Add(a);
            return a;
        }

        public void Enable() { enabled = true; }
        public void Disable() { enabled = false; }
    }

    public class InputActionAsset : ScriptableObject
    {
        internal readonly List<InputActionMap> m_maps = new List<InputActionMap>();
        public IReadOnlyList<InputActionMap> actionMaps => m_maps;
        public bool enabled { get; private set; } = true;

        public InputActionMap FindActionMap(string mapName, bool throwIfNotFound = false)
        {
            for (int i = 0; i < m_maps.Count; ++i)
                if (string.Equals(m_maps[i].name, mapName, StringComparison.OrdinalIgnoreCase))
                    return m_maps[i];
            if (throwIfNotFound) throw new ArgumentException("No action map '" + mapName + "'");
            return null;
        }

        /// <summary>"Map/Action", or just "Action" searched through every map.</summary>
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        {
            if (string.IsNullOrEmpty(actionNameOrId)) return null;
            int slash = actionNameOrId.IndexOf('/');
            if (slash > 0)
            {
                InputActionMap m = FindActionMap(actionNameOrId.Substring(0, slash));
                InputAction a = m != null ? m.FindAction(actionNameOrId.Substring(slash + 1)) : null;
                if (a != null) return a;
            }
            else
            {
                for (int i = 0; i < m_maps.Count; ++i)
                {
                    InputAction a = m_maps[i].FindAction(actionNameOrId);
                    if (a != null) return a;
                }
            }
            if (throwIfNotFound) throw new ArgumentException("No action '" + actionNameOrId + "'");
            Elintria.Debug.LogWarning("InputSystem: no action called '" + actionNameOrId + "' in " + name);
            return null;
        }

        public InputAction this[string actionNameOrId] => FindAction(actionNameOrId, true);

        public void Enable() { enabled = true; for (int i = 0; i < m_maps.Count; ++i) m_maps[i].Enable(); }
        public void Disable() { enabled = false; for (int i = 0; i < m_maps.Count; ++i) m_maps[i].Disable(); }

        public static InputActionAsset FromJson(string json)
        {
            var asset = CreateInstance<InputActionAsset>();
            object root;
            try { root = MiniJson.Parse(json); }
            catch (Exception e)
            {
                Elintria.Debug.LogError("InputSystem: the actions file could not be read - " + e.Message);
                return asset;
            }
            var d = root as Dictionary<string, object>;
            if (d == null) return asset;
            object nm;
            if (d.TryGetValue("name", out nm)) asset.name = nm as string ?? "";
            object maps;
            if (!d.TryGetValue("maps", out maps)) return asset;
            foreach (object mo in (maps as List<object>) ?? new List<object>())
            {
                var md = mo as Dictionary<string, object>;
                if (md == null) continue;
                var map = new InputActionMap(Str(md, "name"));
                map.asset = asset;
                foreach (object ao in (Get(md, "actions") as List<object>) ?? new List<object>())
                {
                    var ad = ao as Dictionary<string, object>;
                    if (ad == null) continue;
                    InputActionType t = InputActionType.Value;
                    string ts = Str(ad, "type");
                    if (ts == "Button") t = InputActionType.Button;
                    else if (ts == "PassThrough") t = InputActionType.PassThrough;
                    var a = new InputAction(Str(ad, "name"), t, Str(ad, "expectedControlType"), true);
                    a.actionMap = map;
                    map.m_actions.Add(a);
                }
                foreach (object bo in (Get(md, "bindings") as List<object>) ?? new List<object>())
                {
                    var bd = bo as Dictionary<string, object>;
                    if (bd == null) continue;
                    var b = new InputBinding
                    {
                        name = Str(bd, "name"),
                        path = Str(bd, "path"),
                        groups = Str(bd, "groups"),
                        processors = Str(bd, "processors"),
                        interactions = Str(bd, "interactions"),
                        action = Str(bd, "action"),
                        isComposite = Get(bd, "isComposite") is bool && (bool)Get(bd, "isComposite"),
                        isPartOfComposite = Get(bd, "isPartOfComposite") is bool && (bool)Get(bd, "isPartOfComposite"),
                    };
                    InputAction target = map.FindAction(b.action);
                    if (target != null) target.m_bindings.Add(b);
                }
                asset.m_maps.Add(map);
            }
            return asset;
        }

        private static object Get(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? v : null;
        }

        private static string Str(Dictionary<string, object> d, string k) => Get(d, k) as string ?? string.Empty;
    }

    public class InputActionReference : ScriptableObject
    {
        public InputAction action { get; private set; }
        public InputActionAsset asset => action?.actionMap?.asset;
        public void Set(InputAction a) { action = a; }
        public static InputActionReference Create(InputAction a)
        {
            var r = CreateInstance<InputActionReference>();
            r.action = a;
            return r;
        }
        public static implicit operator InputAction(InputActionReference r) => r?.action;
    }

    public static class InputSystem
    {
        private static InputActionAsset s_actions;
        private static bool s_looked;

        /// <summary>The project-wide actions: the .inputactions file the Unity
        /// project named in Project Settings, which the importer records in
        /// Settings/UnityImport.ini; failing that, the first one in Assets.</summary>
        public static InputActionAsset actions
        {
            get
            {
                if (s_actions == null && !s_looked)
                {
                    s_looked = true;
                    s_actions = LoadProjectWide();
                }
                return s_actions;
            }
            set { s_actions = value; s_looked = true; }
        }

        public static string version => "1.0 (Elintria)";
        public static void Update() { }

        public static IReadOnlyList<InputDevice> devices
            => new List<InputDevice> { Keyboard.current, Mouse.current };

        private static InputActionAsset LoadProjectWide()
        {
            string file = null;
            try
            {
                string ini = Elintria.Project.Resolve(Path.Combine("Settings", "UnityImport.ini"));
                if (File.Exists(ini))
                    foreach (string line in File.ReadAllLines(ini))
                        if (line.StartsWith("inputActions=", StringComparison.Ordinal))
                            file = Elintria.Project.Resolve(line.Substring(13).Trim());
                if (file == null || !File.Exists(file))
                {
                    string[] all = Directory.GetFiles(Elintria.Project.assetsPath, "*.inputactions",
                                                      SearchOption.AllDirectories);
                    file = all.Length > 0 ? all[0] : null;
                }
            }
            catch (Exception) { file = null; }

            if (file == null || !File.Exists(file))
            {
                Elintria.Debug.LogWarning("InputSystem.actions: the project has no .inputactions file, so no " +
                                          "actions are defined. Keyboard.current and Mouse.current still work.");
                return CreateEmpty();
            }
            InputActionAsset a = InputActionAsset.FromJson(File.ReadAllText(file));
            if (string.IsNullOrEmpty(a.name)) a.name = Path.GetFileNameWithoutExtension(file);
            return a;
        }

        private static InputActionAsset CreateEmpty() => ScriptableObject.CreateInstance<InputActionAsset>();
    }

    // =========================================================================
    //  Binding evaluation
    // =========================================================================
    internal static class BindingEval
    {
        // A single control a path names.
        private static InputControl Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string p = path.Trim();

            // Usages: */{Submit}, <Keyboard>/{Cancel} and so on.
            int brace = p.IndexOf('{');
            if (brace >= 0)
            {
                int end = p.IndexOf('}', brace);
                string usage = end > brace ? p.Substring(brace + 1, end - brace - 1) : "";
                switch (usage)
                {
                case "Submit": return Keyboard.current.enterKey;
                case "Cancel": return Keyboard.current.escapeKey;
                case "Point": return Mouse.current.position;
                case "PrimaryAction": return Mouse.current.leftButton;
                case "SecondaryAction": return Mouse.current.rightButton;
                case "ScrollWheel": return Mouse.current.scroll;
                default: return null;
                }
            }

            string device = "", rest = p;
            if (p.StartsWith("<", StringComparison.Ordinal))
            {
                int gt = p.IndexOf('>');
                if (gt < 0) return null;
                device = p.Substring(1, gt - 1);
                rest = p.Substring(gt + 1).TrimStart('/');
            }
            else if (p.StartsWith("*/", StringComparison.Ordinal))
            {
                rest = p.Substring(2);
            }

            string lower = rest.ToLowerInvariant();

            if (device == "Keyboard" || (device == "" && !lower.Contains("button") && !lower.Contains("delta")))
            {
                Key k;
                if (TryKey(rest, out k)) return Keyboard.current[k];
                if (lower == "anykey") return Keyboard.current.anyKey;
                return null;
            }

            if (device == "Mouse" || device == "Pointer" || device == "")
            {
                Mouse m = Mouse.current;
                switch (lower)
                {
                case "leftbutton": case "press": return m.leftButton;
                case "rightbutton": return m.rightButton;
                case "middlebutton": return m.middleButton;
                case "delta": return m.delta;
                case "delta/x": return m.delta.x;
                case "delta/y": return m.delta.y;
                case "position": return m.position;
                case "position/x": return m.position.x;
                case "position/y": return m.position.y;
                case "scroll": return m.scroll;
                case "scroll/y": case "scroll/up": return m.scroll.y;
                case "scroll/x": return m.scroll.x;
                }
            }
            // Gamepads, joysticks, touch, XR: not connected.
            return null;
        }

        private static readonly Dictionary<string, Key> s_keyNames = BuildKeyNames();

        private static Dictionary<string, Key> BuildKeyNames()
        {
            var d = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
            foreach (Key k in Enum.GetValues(typeof(Key))) d[k.ToString()] = k;
            for (int i = 0; i <= 9; ++i) d[i.ToString(CultureInfo.InvariantCulture)] = i == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + i - 1);
            d["enter"] = Key.Enter; d["return"] = Key.Enter; d["ctrl"] = Key.LeftCtrl; d["shift"] = Key.LeftShift;
            d["alt"] = Key.LeftAlt; d["leftCtrl"] = Key.LeftCtrl; d["rightCtrl"] = Key.RightCtrl;
            d["escape"] = Key.Escape; d["esc"] = Key.Escape;
            return d;
        }

        private static bool TryKey(string name, out Key k) => s_keyNames.TryGetValue(name.Trim(), out k);

        // |processors| as written in the file: "invertVector2(invertX=false),scaleVector2(x=0.5,y=0.5)".
        private static void Process(string processors, ref Elintria.Vector2 v, ref float a)
        {
            if (string.IsNullOrEmpty(processors)) return;
            foreach (string raw in SplitTop(processors))
            {
                string p = raw.Trim();
                string nm = p, args = "";
                int open = p.IndexOf('(');
                if (open >= 0) { nm = p.Substring(0, open); args = p.Substring(open + 1).TrimEnd(')'); }
                var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string part in args.Split(','))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0) kv[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
                }
                Func<string, float, float> num = (key, def) =>
                {
                    string s; float f;
                    return kv.TryGetValue(key, out s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : def;
                };
                Func<string, bool, bool> flag = (key, def) =>
                {
                    string s;
                    return kv.TryGetValue(key, out s) ? s == "true" || s == "True" || s == "1" : def;
                };
                switch (nm)
                {
                case "invertVector2":
                    v = new Elintria.Vector2(flag("invertX", true) ? -v.x : v.x, flag("invertY", true) ? -v.y : v.y);
                    break;
                case "scaleVector2": v = new Elintria.Vector2(v.x * num("x", 1f), v.y * num("y", 1f)); break;
                case "normalizeVector2": if (v.magnitude > 1e-6f) v = v.normalized; break;
                case "invert": a = -a; break;
                case "scale": a *= num("factor", 1f); break;
                case "clamp": a = Math.Max(num("min", 0f), Math.Min(num("max", 1f), a)); break;
                case "normalize":
                    {
                        float mn = num("min", 0f), mx = num("max", 1f);
                        a = mx > mn ? (a - mn) / (mx - mn) : a;
                        break;
                    }
                }
            }
        }

        private static IEnumerable<string> SplitTop(string s)
        {
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; ++i)
            {
                if (s[i] == '(') ++depth;
                else if (s[i] == ')') --depth;
                else if (s[i] == ',' && depth == 0) { yield return s.Substring(start, i - start); start = i + 1; }
            }
            if (start < s.Length) yield return s.Substring(start);
        }

        private static float Scalar(InputControl c)
        {
            if (c is ButtonControl) return ((ButtonControl)c).isPressed ? 1f : 0f;
            if (c is AxisControl) return ((AxisControl)c).ReadValue();
            if (c is Vector2Control) return ((Vector2Control)c).ReadValue().magnitude;
            return 0f;
        }

        private static bool Down(InputControl c) => c is ButtonControl && ((ButtonControl)c).wasPressedThisFrame;

        // Every binding of the action, composites included; the one with the
        // largest magnitude wins, which is Unity's rule for a Value action
        // with several controls behind it.
        internal static void Evaluate(InputAction action, out Elintria.Vector2 vec, out float axis,
                                      out float magnitude, out bool downEdge)
        {
            vec = Elintria.Vector2.zero;
            axis = 0f;
            magnitude = 0f;
            downEdge = false;
            List<InputBinding> bs = action.m_bindings;

            for (int i = 0; i < bs.Count; ++i)
            {
                InputBinding b = bs[i];
                if (b.isPartOfComposite) continue;

                Elintria.Vector2 v = Elintria.Vector2.zero;
                float a = 0f;
                bool down = false;

                if (b.isComposite)
                {
                    // Its parts follow it.
                    var parts = new Dictionary<string, List<InputControl>>(StringComparer.OrdinalIgnoreCase);
                    int j = i + 1;
                    for (; j < bs.Count && bs[j].isPartOfComposite; ++j)
                    {
                        InputControl c = Resolve(bs[j].path);
                        if (c == null) continue;
                        List<InputControl> l;
                        if (!parts.TryGetValue(bs[j].name, out l)) parts[bs[j].name] = l = new List<InputControl>();
                        l.Add(c);
                    }
                    Func<string, float> part = pn =>
                    {
                        List<InputControl> l;
                        if (!parts.TryGetValue(pn, out l)) return 0f;
                        float best = 0f;
                        foreach (InputControl c in l) best = Math.Max(best, Scalar(c));
                        return best;
                    };
                    Func<string, bool> partDown = pn =>
                    {
                        List<InputControl> l;
                        if (!parts.TryGetValue(pn, out l)) return false;
                        foreach (InputControl c in l) if (Down(c)) return true;
                        return false;
                    };

                    string kind = (b.path ?? "").Split('(')[0].Trim();
                    switch (kind)
                    {
                    case "2DVector":
                    case "Dpad":
                        v = new Elintria.Vector2(part("right") - part("left"), part("up") - part("down"));
                        // Unity's default mode normalises a diagonal.
                        if ((b.path ?? "").IndexOf("mode=2", StringComparison.Ordinal) < 0 && v.magnitude > 1f)
                            v = v.normalized;
                        a = v.magnitude;
                        down = partDown("up") || partDown("down") || partDown("left") || partDown("right");
                        break;
                    case "1DAxis":
                    case "Axis":
                        a = part("positive") - part("negative");
                        v = new Elintria.Vector2(a, 0f);
                        down = partDown("positive") || partDown("negative");
                        break;
                    case "ButtonWithOneModifier":
                    case "OneModifier":
                        a = part("modifier") > 0.5f ? Math.Max(part("button"), part("binding")) : 0f;
                        v = new Elintria.Vector2(a, 0f);
                        down = a > 0.5f && (partDown("button") || partDown("binding") || partDown("modifier"));
                        break;
                    case "ButtonWithTwoModifiers":
                    case "TwoModifiers":
                        a = part("modifier1") > 0.5f && part("modifier2") > 0.5f
                          ? Math.Max(part("button"), part("binding")) : 0f;
                        v = new Elintria.Vector2(a, 0f);
                        down = a > 0.5f && (partDown("button") || partDown("binding"));
                        break;
                    }
                    Process(b.processors, ref v, ref a);
                    i = j - 1;
                }
                else
                {
                    InputControl c = Resolve(b.path);
                    if (c == null) continue;
                    if (c is Vector2Control)
                    {
                        v = ((Vector2Control)c).ReadValue();
                        a = v.magnitude;
                    }
                    else
                    {
                        a = Scalar(c);
                        v = new Elintria.Vector2(a, 0f);
                        down = Down(c);
                    }
                    Process(b.processors, ref v, ref a);
                }

                float mag = Math.Max(Math.Abs(a), v.magnitude);
                if (down) downEdge = true;
                if (mag > magnitude)
                {
                    magnitude = mag;
                    vec = v;
                    axis = a;
                }
            }
        }
    }

    // =========================================================================
    //  A small JSON reader - the actions file is JSON, and the runtime has no
    //  JSON library to lean on.
    // =========================================================================
    internal static class MiniJson
    {
        internal static object Parse(string s)
        {
            int i = 0;
            object v = Value(s, ref i);
            return v;
        }

        private static void Ws(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿') { ++i; continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n') ++i;
                    continue;
                }
                break;
            }
        }

        private static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            if (c == '{')
            {
                ++i;
                var d = new Dictionary<string, object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { ++i; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("':' expected at " + i);
                    ++i;
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { ++i; continue; }
                    if (i < s.Length && s[i] == '}') { ++i; return d; }
                    throw new FormatException("',' or '}' expected at " + i);
                }
            }
            if (c == '[')
            {
                ++i;
                var l = new List<object>();
                Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { ++i; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { ++i; continue; }
                    if (i < s.Length && s[i] == ']') { ++i; return l; }
                    throw new FormatException("',' or ']' expected at " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) ++i;
            if (i == start) throw new FormatException("unexpected '" + c + "' at " + i);
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("string expected at " + i);
            ++i;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            ++i;
            return sb.ToString();
        }
    }
}
