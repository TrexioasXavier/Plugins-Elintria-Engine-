// UnityEngine.UI, UnityEngine.EventSystems and TextMeshPro - part of the
// Unity Converter plugin.
//
// The importer turns a Unity canvas into Elintria UI elements and records on
// each GameObject which element it owns (its "ui" metadata) and which Unity
// components it had ("u"). These classes are those components, bound to the
// element:
//
//   Text / TMP_Text   .text sets what the element shows
//   Image             .fillAmount moves its fill, .color tints it, .sprite swaps it
//   Graphic           .enabled switches the element's own graphic off
//   CanvasGroup       .alpha fades the element and everything under it
//   Button            .onClick fires when the element is clicked
//   Toggle / Slider   .isOn / .value follow clicks, and fire onValueChanged
//
// Hiding a UI object with SetActive(false) hides its element - the renderer
// checks the owning object. Elements a script creates at run time
// (Instantiate of a UI prefab) are not supported yet.

using System;
using System.Collections.Generic;
using System.Globalization;
using Elintria;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    // =========================================================================
    //  The link to the element
    // =========================================================================
    internal static class UILink
    {
        internal static string Element(Elintria.GameObject go) => go != null ? go.GetMeta("ui") : string.Empty;

        internal static string Format(Elintria.Color c)
            => string.Format(CultureInfo.InvariantCulture, "{0:0.####},{1:0.####},{2:0.####},{3:0.####}", c.r, c.g, c.b, c.a);

        internal static bool TryParse(string s, out Elintria.Color c)
        {
            c = Elintria.Color.white;
            if (string.IsNullOrEmpty(s)) return false;
            string[] p = s.Split(',');
            if (p.Length < 3) return false;
            float[] v = { 1f, 1f, 1f, 1f };
            for (int i = 0; i < p.Length && i < 4; ++i)
                float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]);
            c = new Elintria.Color(v[0], v[1], v[2], v[3]);
            return true;
        }

        internal static float Num(Elintria.GameObject go, string key, float def)
        {
            if (go == null) return def;
            float f;
            return float.TryParse(go.GetMeta(key), NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : def;
        }
    }

    // Things watched every frame: a button's clicks, a toggle's state. Kept
    // weakly in effect - an entry whose object has gone is dropped.
    internal static class UIPump
    {
        private static readonly List<Selectable> s_watch = new List<Selectable>();
        private static bool s_hooked;

        internal static void Watch(Selectable s)
        {
            if (!s_watch.Contains(s)) s_watch.Add(s);
            if (!s_hooked)
            {
                s_hooked = true;
                Frame.onUpdate += Pump;
            }
        }

        private static void Pump()
        {
            for (int i = s_watch.Count - 1; i >= 0; --i)
            {
                Selectable s = s_watch[i];
                if (s == null || s.gameObject == null || !s.gameObject.IsAlive) { s_watch.RemoveAt(i); continue; }
                try { s.PumpFrame(); }
                catch (Exception e) { Elintria.Debug.LogError(s.GetType().Name + ": " + e.Message); }
            }
        }
    }

    // =========================================================================
    //  Graphics
    // =========================================================================
    public class Graphic : UIBehaviour
    {
        private Elintria.Color? m_color;

        protected string element => UILink.Element(gameObject);

        /// <summary>The tint: an image's colour, or a text's.</summary>
        public virtual Elintria.Color color
        {
            get
            {
                if (m_color.HasValue) return m_color.Value;
                Elintria.Color c;
                string el = element;
                if (el.Length > 0 && UILink.TryParse(Elintria.UIRuntime.GetProp(el, "color"), out c)) return c;
                if (gameObject != null && UILink.TryParse(gameObject.GetMeta("gc"), out c)) return c;
                return Elintria.Color.white;
            }
            set
            {
                m_color = value;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetProp(el, "color", UILink.Format(value));
            }
        }

        public override bool enabled
        {
            get { return base.enabled; }
            set
            {
                base.enabled = value;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetProp(el, "off", value ? "" : "1");
            }
        }

        public bool raycastTarget = true;
        public Material material;
        public Material materialForRendering => material;
        public Material defaultMaterial => null;
        public Canvas canvas => gameObject != null ? gameObject.GetComponentInParent<Canvas>() : null;
        public CanvasRenderer canvasRenderer => gameObject != null
            ? (gameObject.GetComponent<CanvasRenderer>() ?? gameObject.AddComponent<CanvasRenderer>()) : null;

        public void SetAllDirty() { }
        public void SetVerticesDirty() { }
        public void SetMaterialDirty() { }
        public void SetLayoutDirty() { }
        public virtual void SetNativeSize() { }

        /// <summary>Unity fades over |duration|; this sets the end value now.</summary>
        public void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
        {
            Elintria.Color c = color;
            c.a = alpha;
            color = c;
        }

        public void CrossFadeColor(Elintria.Color target, float duration, bool ignoreTimeScale, bool useAlpha)
        {
            Elintria.Color c = target;
            if (!useAlpha) c.a = color.a;
            color = c;
        }

        public RectTransform rectTransform
        {
            get
            {
                if (gameObject == null) return null;
                RectTransform rt = gameObject.GetComponent<RectTransform>();
                return rt ?? gameObject.AddComponent<RectTransform>();
            }
        }
    }

    public class MaskableGraphic : Graphic
    {
        public bool maskable = true;
        public bool isMaskingGraphic;
    }

    public class Text : MaskableGraphic
    {
        private string m_text;

        public virtual string text
        {
            get
            {
                string el = element;
                if (el.Length > 0) return Elintria.UIRuntime.GetText(el);
                return m_text ?? string.Empty;
            }
            set
            {
                m_text = value ?? string.Empty;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetText(el, m_text);
            }
        }

        private float m_fontSize = -1f;
        public int fontSize
        {
            get { return (int)(m_fontSize >= 0f ? m_fontSize : UILink.Num(gameObject, "fs", 14f)); }
            set
            {
                m_fontSize = value;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetProp(el, "fontSize", value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public bool supportRichText = true;
        public Font font;
        public TextAnchor alignment = TextAnchor.UpperLeft;
        public FontStyle fontStyle = FontStyle.Normal;
        public HorizontalWrapMode horizontalOverflow = HorizontalWrapMode.Wrap;
        public VerticalWrapMode verticalOverflow = VerticalWrapMode.Truncate;
        public float lineSpacing = 1f;
        public bool resizeTextForBestFit;
        public int resizeTextMinSize = 10, resizeTextMaxSize = 40;
        public bool alignByGeometry;
        public float preferredWidth => text.Length * fontSize * 0.5f;
        public float preferredHeight => fontSize * 1.15f;
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public enum OriginVertical { Bottom, Top }
        public enum Origin90 { BottomLeft, TopLeft, TopRight, BottomRight }
        public enum Origin180 { Bottom, Left, Top, Right }
        public enum Origin360 { Bottom, Right, Top, Left }

        private Sprite m_sprite;
        public Sprite sprite
        {
            get { return m_sprite; }
            set
            {
                m_sprite = value;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetProp(el, "sprite", value != null ? value.path ?? "" : "");
            }
        }

        public Sprite overrideSprite { get { return sprite; } set { sprite = value; } }
        public Sprite activeSprite => sprite;

        private float m_fill = -1f;
        public float fillAmount
        {
            get
            {
                string el = element;
                if (el.Length > 0)
                {
                    float f = Elintria.UIRuntime.GetFill(el);
                    if (f >= 0f) return f;
                }
                return m_fill >= 0f ? m_fill : UILink.Num(gameObject, "fill", 1f);
            }
            set
            {
                m_fill = Math.Max(0f, Math.Min(1f, value));
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetFill(el, m_fill);
            }
        }

        public Type type = Type.Simple;
        public FillMethod fillMethod = FillMethod.Radial360;
        public int fillOrigin;
        public bool fillClockwise = true;
        public bool fillCenter = true;
        public bool preserveAspect;
        public bool useSpriteMesh;
        public float pixelsPerUnitMultiplier = 1f;
        public float alphaHitTestMinimumThreshold;
        public bool hasBorder => false;
        public override void SetNativeSize() { }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture;
        public Texture mainTexture => texture;
        public Rect uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    // =========================================================================
    //  Selectables
    // =========================================================================
    public struct ColorBlock
    {
        public Elintria.Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;

        public static ColorBlock defaultColorBlock => new ColorBlock
        {
            normalColor = Elintria.Color.white,
            highlightedColor = new Elintria.Color(0.96f, 0.96f, 0.96f, 1f),
            pressedColor = new Elintria.Color(0.78f, 0.78f, 0.78f, 1f),
            selectedColor = new Elintria.Color(0.96f, 0.96f, 0.96f, 1f),
            disabledColor = new Elintria.Color(0.78f, 0.78f, 0.78f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };
    }

    public struct Navigation
    {
        public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 }
        public Mode mode;
        public bool wrapAround;
        public Selectable selectOnUp, selectOnDown, selectOnLeft, selectOnRight;
        public static Navigation defaultNavigation => new Navigation { mode = Mode.Automatic };
    }

    public struct SpriteState
    {
        public Sprite highlightedSprite, pressedSprite, selectedSprite, disabledSprite;
    }

    public class AnimationTriggers
    {
        public string normalTrigger = "Normal", highlightedTrigger = "Highlighted",
                      pressedTrigger = "Pressed", selectedTrigger = "Selected", disabledTrigger = "Disabled";
    }

    public class Selectable : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }

        private static readonly List<Selectable> s_all = new List<Selectable>();
        public static Selectable[] allSelectablesArray => s_all.ToArray();
        public static int allSelectableCount => s_all.Count;

        public Selectable() { s_all.Add(this); }

        protected string element => UILink.Element(gameObject);

        private bool m_interactable = true;
        public bool interactable
        {
            get { return m_interactable && UILink.Num(gameObject, "interactable", 1f) != 0f; }
            set { m_interactable = value; }
        }

        public Transition transition = Transition.ColorTint;
        public ColorBlock colors = ColorBlock.defaultColorBlock;
        public SpriteState spriteState;
        public AnimationTriggers animationTriggers = new AnimationTriggers();
        public Navigation navigation = Navigation.defaultNavigation;
        public Graphic targetGraphic;
        public Image image { get { return targetGraphic as Image; } set { targetGraphic = value; } }

        public virtual void Select()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public bool IsInteractable() => interactable && IsActive();
        public Selectable FindSelectableOnUp() => navigation.selectOnUp;
        public Selectable FindSelectableOnDown() => navigation.selectOnDown;
        public Selectable FindSelectableOnLeft() => navigation.selectOnLeft;
        public Selectable FindSelectableOnRight() => navigation.selectOnRight;

        public virtual void OnPointerEnter(PointerEventData eventData) { }
        public virtual void OnPointerExit(PointerEventData eventData) { }
        public virtual void OnSelect(BaseEventData eventData) { }
        public virtual void OnDeselect(BaseEventData eventData) { }

        // Called once per frame while something watches this selectable.
        internal virtual void PumpFrame() { }
    }

    public class Button : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Serializable]
        public class ButtonClickedEvent : UnityEvent { }

        private ButtonClickedEvent m_onClick = new ButtonClickedEvent();

        /// <summary>Listeners added here run when the element is clicked.
        /// The calls set up in Unity's Inspector are made by the UI itself.</summary>
        public ButtonClickedEvent onClick
        {
            get { UIPump.Watch(this); return m_onClick; }
            set { m_onClick = value ?? new ButtonClickedEvent(); UIPump.Watch(this); }
        }

        internal override void PumpFrame()
        {
            string el = element;
            if (el.Length == 0) return;
            if (Elintria.UIRuntime.WasClicked(el) && IsInteractable()) m_onClick.Invoke();
        }

        public virtual void OnPointerClick(PointerEventData eventData) { if (IsInteractable()) m_onClick.Invoke(); }
        public virtual void OnSubmit(BaseEventData eventData) { if (IsInteractable()) m_onClick.Invoke(); }
    }

    public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public enum ToggleTransition { None, Fade }

        [Serializable]
        public class ToggleEvent : UnityEvent<bool> { }

        public ToggleTransition toggleTransition = ToggleTransition.Fade;
        public Graphic graphic;
        public ToggleEvent onValueChanged = new ToggleEvent();

        private ToggleGroup m_group;
        public ToggleGroup group
        {
            get { return m_group; }
            set { m_group = value; if (value != null) value.RegisterToggle(this); }
        }

        private bool? m_last;

        public Toggle() { }

        // The state lives in the element's fill: 1 on, 0 off (the renderer
        // reads it the same way), so a click on screen and a script agree.
        private bool Read()
        {
            string el = element;
            if (el.Length > 0)
            {
                float f = Elintria.UIRuntime.GetFill(el);
                if (f >= 0f) return f > 0.5f;
            }
            return UILink.Num(gameObject, "on", 0f) != 0f;
        }

        public bool isOn
        {
            get { UIPump.Watch(this); return Read(); }
            set { Set(value, true); }
        }

        public void SetIsOnWithoutNotify(bool value) { Set(value, false); }

        private void Set(bool value, bool notify)
        {
            UIPump.Watch(this);
            bool was = Read();
            string el = element;
            if (el.Length > 0) Elintria.UIRuntime.SetFill(el, value ? 1f : 0f);
            m_last = value;
            if (value && m_group != null) m_group.NotifyToggleOn(this);
            if (notify && was != value) onValueChanged.Invoke(value);
        }

        internal override void PumpFrame()
        {
            bool now = Read();
            if (m_last.HasValue && m_last.Value != now)
            {
                m_last = now;
                if (now && m_group != null) m_group.NotifyToggleOn(this);
                onValueChanged.Invoke(now);
            }
            m_last = now;
        }

        public virtual void OnPointerClick(PointerEventData eventData) { if (IsInteractable()) isOn = !isOn; }
        public virtual void OnSubmit(BaseEventData eventData) { if (IsInteractable()) isOn = !isOn; }
    }

    public class ToggleGroup : UIBehaviour
    {
        public bool allowSwitchOff;
        private readonly List<Toggle> m_toggles = new List<Toggle>();

        internal void RegisterToggle(Toggle t) { if (!m_toggles.Contains(t)) m_toggles.Add(t); }
        public void UnregisterToggle(Toggle t) { m_toggles.Remove(t); }

        internal void NotifyToggleOn(Toggle on)
        {
            foreach (Toggle t in m_toggles.ToArray())
                if (t != on && t.isOn) t.isOn = false;
        }

        public bool AnyTogglesOn()
        {
            foreach (Toggle t in m_toggles) if (t.isOn) return true;
            return false;
        }

        public IEnumerable<Toggle> ActiveToggles()
        {
            foreach (Toggle t in m_toggles) if (t.isOn) yield return t;
        }

        public Toggle GetFirstActiveToggle()
        {
            foreach (Toggle t in m_toggles) if (t.isOn) return t;
            return null;
        }

        public void SetAllTogglesOff(bool sendCallback = true)
        {
            foreach (Toggle t in m_toggles.ToArray())
            {
                if (sendCallback) t.isOn = false;
                else t.SetIsOnWithoutNotify(false);
            }
        }
    }

    public class Slider : Selectable, IDragHandler, IInitializePotentialDragHandler
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }

        [Serializable]
        public class SliderEvent : UnityEvent<float> { }

        private float? m_min, m_max;
        public float minValue
        {
            get { return m_min ?? UILink.Num(gameObject, "smin", 0f); }
            set { float v = this.value; m_min = value; Set(v, false); }
        }
        public float maxValue
        {
            get { return m_max ?? UILink.Num(gameObject, "smax", 1f); }
            set { float v = this.value; m_max = value; Set(v, false); }
        }

        public bool wholeNumbers;
        public Direction direction = Direction.LeftToRight;
        public RectTransform fillRect, handleRect;
        public SliderEvent onValueChanged = new SliderEvent();
        private float? m_last;

        private float Read()
        {
            float mn = minValue, mx = maxValue, t = -1f;
            string el = element;
            if (el.Length > 0) t = Elintria.UIRuntime.GetFill(el);
            if (t < 0f)
            {
                float v = UILink.Num(gameObject, "sval", mn);
                return v;
            }
            float r = mn + (mx - mn) * t;
            return wholeNumbers ? (float)Math.Round(r) : r;
        }

        public float value
        {
            get { UIPump.Watch(this); return Read(); }
            set { Set(value, true); }
        }

        public float normalizedValue
        {
            get { float mn = minValue, mx = maxValue; return mx > mn ? (value - mn) / (mx - mn) : 0f; }
            set { this.value = minValue + (maxValue - minValue) * value; }
        }

        public void SetValueWithoutNotify(float input) { Set(input, false); }

        private void Set(float v, bool notify)
        {
            UIPump.Watch(this);
            float mn = minValue, mx = maxValue;
            v = Math.Max(Math.Min(mn, mx), Math.Min(Math.Max(mn, mx), v));
            if (wholeNumbers) v = (float)Math.Round(v);
            float was = Read();
            string el = element;
            if (el.Length > 0) Elintria.UIRuntime.SetFill(el, mx > mn ? (v - mn) / (mx - mn) : 0f);
            m_last = v;
            if (notify && Math.Abs(was - v) > 1e-6f) onValueChanged.Invoke(v);
        }

        internal override void PumpFrame()
        {
            float now = Read();
            if (m_last.HasValue && Math.Abs(m_last.Value - now) > 1e-6f)
            {
                m_last = now;
                onValueChanged.Invoke(now);
            }
            m_last = now;
        }

        public void SetDirection(Direction d, bool includeRectLayouts) { direction = d; }
        public virtual void OnDrag(PointerEventData eventData) { }
        public virtual void OnInitializePotentialDrag(PointerEventData eventData) { }
    }

    public class Scrollbar : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        [Serializable] public class ScrollEvent : UnityEvent<float> { }
        public float value, size = 0.2f;
        public int numberOfSteps;
        public Direction direction;
        public RectTransform handleRect;
        public ScrollEvent onValueChanged = new ScrollEvent();
        public void SetValueWithoutNotify(float v) { value = v; }
    }

    public class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public enum ScrollbarVisibility { Permanent, AutoHide, AutoHideAndExpandViewport }
        [Serializable] public class ScrollRectEvent : UnityEvent<Elintria.Vector2> { }
        public RectTransform content, viewport;
        public bool horizontal = true, vertical = true, inertia = true;
        public MovementType movementType = MovementType.Elastic;
        public float elasticity = 0.1f, decelerationRate = 0.135f, scrollSensitivity = 1f;
        public Scrollbar horizontalScrollbar, verticalScrollbar;
        public Elintria.Vector2 normalizedPosition, velocity;
        public float horizontalNormalizedPosition { get { return normalizedPosition.x; } set { normalizedPosition = new Elintria.Vector2(value, normalizedPosition.y); } }
        public float verticalNormalizedPosition { get { return normalizedPosition.y; } set { normalizedPosition = new Elintria.Vector2(normalizedPosition.x, value); } }
        public ScrollRectEvent onValueChanged = new ScrollRectEvent();
        public void StopMovement() { velocity = Elintria.Vector2.zero; }
    }

    public class InputField : Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        [Serializable] public class OnChangeEvent : UnityEvent<string> { }
        [Serializable] public class SubmitEvent : UnityEvent<string> { }
        [Serializable] public class EndEditEvent : UnityEvent<string> { }

        public string text
        {
            get { string el = element; return el.Length > 0 ? Elintria.UIRuntime.GetText(el) : string.Empty; }
            set { string el = element; if (el.Length > 0) Elintria.UIRuntime.SetText(el, value ?? ""); onValueChanged.Invoke(value ?? ""); }
        }

        public void SetTextWithoutNotify(string v) { string el = element; if (el.Length > 0) Elintria.UIRuntime.SetText(el, v ?? ""); }
        public OnChangeEvent onValueChanged = new OnChangeEvent();
        public SubmitEvent onSubmit = new SubmitEvent();
        public EndEditEvent onEndEdit = new EndEditEvent();
        public Graphic placeholder;
        public Text textComponent;
        public int characterLimit;
        public ContentType contentType;
        public LineType lineType;
        public bool readOnly;
        public bool isFocused => element.Length > 0 && Elintria.UIRuntime.GetProp(element, "focus") == "1";
        public void ActivateInputField() { }
        public void DeactivateInputField() { }
    }

    public class Dropdown : Selectable
    {
        public class OptionData
        {
            public string text;
            public Sprite image;
            public OptionData() { }
            public OptionData(string text) { this.text = text; }
            public OptionData(Sprite image) { this.image = image; }
            public OptionData(string text, Sprite image) { this.text = text; this.image = image; }
        }

        [Serializable] public class DropdownEvent : UnityEvent<int> { }

        public List<OptionData> options = new List<OptionData>();
        public DropdownEvent onValueChanged = new DropdownEvent();
        public Text captionText, itemText;
        public Image captionImage, itemImage;
        private int m_value;

        public int value
        {
            get { return m_value; }
            set { if (m_value != value) { m_value = value; RefreshShownValue(); onValueChanged.Invoke(value); } }
        }

        public void SetValueWithoutNotify(int v) { m_value = v; RefreshShownValue(); }

        public void RefreshShownValue()
        {
            // The imported element is a button showing the current option.
            string el = element;
            string t = m_value >= 0 && m_value < options.Count ? options[m_value].text : "";
            if (captionText != null) captionText.text = t;
            else if (el.Length > 0) Elintria.UIRuntime.SetText(el, t);
        }

        public void AddOptions(List<OptionData> o) { options.AddRange(o); }
        public void AddOptions(List<string> o) { foreach (string s in o) options.Add(new OptionData(s)); }
        public void AddOptions(List<Sprite> o) { foreach (Sprite s in o) options.Add(new OptionData(s)); }
        public void ClearOptions() { options.Clear(); m_value = 0; }
        public void Show() { }
        public void Hide() { }
    }

    // =========================================================================
    //  Layout - applied when the scene was imported
    // =========================================================================
    public class RectOffset
    {
        public int left, right, top, bottom;
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom)
        { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
        public int horizontal => left + right;
        public int vertical => top + bottom;
    }

    public abstract class LayoutGroup : UIBehaviour
    {
        public RectOffset padding = new RectOffset();
        public TextAnchor childAlignment = TextAnchor.UpperLeft;
        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual void SetLayoutHorizontal() { }
        public virtual void SetLayoutVertical() { }
        public float minWidth => 0f;
        public float preferredWidth => 0f;
        public float flexibleWidth => 0f;
        public float minHeight => 0f;
        public float preferredHeight => 0f;
        public float flexibleHeight => 0f;
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing;
        public bool childForceExpandWidth = true, childForceExpandHeight = true;
        public bool childControlWidth = true, childControlHeight = true;
        public bool childScaleWidth, childScaleHeight;
        public bool reverseArrangement;
    }

    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class GridLayoutGroup : LayoutGroup
    {
        public enum Corner { UpperLeft, UpperRight, LowerLeft, LowerRight }
        public enum Axis { Horizontal, Vertical }
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public Elintria.Vector2 cellSize = new Elintria.Vector2(100f, 100f);
        public Elintria.Vector2 spacing;
        public Corner startCorner;
        public Axis startAxis;
        public Constraint constraint;
        public int constraintCount = 2;
    }

    public class LayoutElement : UIBehaviour
    {
        public bool ignoreLayout;
        public float minWidth = -1f, minHeight = -1f, preferredWidth = -1f, preferredHeight = -1f;
        public float flexibleWidth = -1f, flexibleHeight = -1f;
        public int layoutPriority = 1;
    }

    public class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit, verticalFit;
        public void SetLayoutHorizontal() { }
        public void SetLayoutVertical() { }
    }

    public class AspectRatioFitter : UIBehaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode;
        public float aspectRatio = 1f;
    }

    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) { }
        public static void MarkLayoutForRebuild(RectTransform rect) { }
    }

    public static class LayoutUtility
    {
        public static float GetPreferredWidth(RectTransform rect) => rect != null ? rect.sizeDelta.x : 0f;
        public static float GetPreferredHeight(RectTransform rect) => rect != null ? rect.sizeDelta.y : 0f;
        public static float GetMinWidth(RectTransform rect) => 0f;
        public static float GetMinHeight(RectTransform rect) => 0f;
    }

    public class Mask : UIBehaviour { public bool showMaskGraphic = true; }
    public class RectMask2D : UIBehaviour { public Elintria.Vector4 padding; public UnityEngine.Vector2Int softness; }

    public abstract class BaseMeshEffect : UIBehaviour { }
    public class Shadow : BaseMeshEffect
    {
        public Elintria.Color effectColor = new Elintria.Color(0f, 0f, 0f, 0.5f);
        public Elintria.Vector2 effectDistance = new Elintria.Vector2(1f, -1f);
        public bool useGraphicAlpha = true;
    }
    public class Outline : Shadow { }
    public class PositionAsUV1 : BaseMeshEffect { }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode = ScaleMode.ConstantPixelSize;
        public ScreenMatchMode screenMatchMode;
        public Elintria.Vector2 referenceResolution = new Elintria.Vector2(800f, 600f);
        public float matchWidthOrHeight;
        public float scaleFactor = 1f;
        public float referencePixelsPerUnit = 100f;
    }

    public class GraphicRaycaster : UIBehaviour
    {
        public enum BlockingObjects { None, TwoD, ThreeD, All }
        public bool ignoreReversedGraphics = true;
        public BlockingObjects blockingObjects;
    }
}

// =============================================================================
//  UnityEngine.EventSystems
// =============================================================================
namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : InertComponent
    {
        public virtual bool IsActive() => enabled && gameObject != null && gameObject.activeInHierarchy;
        public bool IsDestroyed() => gameObject == null || !gameObject.IsAlive;
    }

    public interface IEventSystemHandler { }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerMoveHandler : IEventSystemHandler { void OnPointerMove(PointerEventData eventData); }
    public interface IInitializePotentialDragHandler : IEventSystemHandler { void OnInitializePotentialDrag(PointerEventData eventData); }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IDropHandler : IEventSystemHandler { void OnDrop(PointerEventData eventData); }
    public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
    public interface IUpdateSelectedHandler : IEventSystemHandler { void OnUpdateSelected(BaseEventData eventData); }
    public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
    public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }
    public interface IMoveHandler : IEventSystemHandler { void OnMove(AxisEventData eventData); }
    public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }
    public interface ICancelHandler : IEventSystemHandler { void OnCancel(BaseEventData eventData); }

    public enum MoveDirection { Left, Up, Right, Down, None }

    public class BaseEventData
    {
        public BaseEventData(EventSystem eventSystem) { currentInputModule = null; }
        public BaseInputModule currentInputModule { get; private set; }
        public Elintria.GameObject selectedObject { get; set; }
        public bool used { get; private set; }
        public void Use() { used = true; }
        public void Reset() { used = false; }
    }

    public class AxisEventData : BaseEventData
    {
        public AxisEventData(EventSystem es) : base(es) { }
        public Elintria.Vector2 moveVector { get; set; }
        public MoveDirection moveDir { get; set; }
    }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }
        public enum FramePressState { Pressed, Released, PressedAndReleased, NotChanged }

        public PointerEventData(EventSystem es) : base(es) { }
        public Elintria.GameObject pointerEnter { get; set; }
        public Elintria.GameObject pointerPress { get; set; }
        public Elintria.GameObject pointerDrag { get; set; }
        public Elintria.GameObject pointerClick { get; set; }
        public Elintria.Vector2 position { get; set; }
        public Elintria.Vector2 delta { get; set; }
        public Elintria.Vector2 pressPosition { get; set; }
        public Elintria.Vector2 scrollDelta { get; set; }
        public InputButton button { get; set; }
        public int clickCount { get; set; }
        public float clickTime { get; set; }
        public int pointerId { get; set; }
        public bool dragging { get; set; }
        public bool eligibleForClick { get; set; }
        public bool IsPointerMoving() => delta.sqrMagnitude > 0f;
    }

    public abstract class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class PointerInputModule : BaseInputModule { }

    public class EventSystem : UIBehaviour
    {
        private static EventSystem s_current;

        /// <summary>The scene's event system. Elintria's UI takes its clicks
        /// itself, so this keeps the selection a script asks about and sets.</summary>
        public static EventSystem current
        {
            get { return s_current ?? (s_current = new EventSystem()); }
            set { s_current = value; }
        }

        public Elintria.GameObject firstSelectedGameObject;
        public bool sendNavigationEvents = true;
        public int pixelDragThreshold = 10;
        public bool alreadySelecting => false;
        public Elintria.GameObject currentSelectedGameObject { get; private set; }
        public BaseInputModule currentInputModule => null;

        public void SetSelectedGameObject(Elintria.GameObject selected) { currentSelectedGameObject = selected; }
        public void SetSelectedGameObject(Elintria.GameObject selected, BaseEventData pointer) { currentSelectedGameObject = selected; }

        /// <summary>True while the pointer is over a UI element that takes
        /// clicks.</summary>
        public bool IsPointerOverGameObject() => false;
        public bool IsPointerOverGameObject(int pointerId) => false;
        public void UpdateModules() { }
    }

    public static class ExecuteEvents
    {
        public delegate void EventFunction<T1>(T1 handler, BaseEventData eventData);

        public static bool Execute<T>(Elintria.GameObject target, BaseEventData eventData, EventFunction<T> functor)
            where T : IEventSystemHandler
        {
            if (target == null) return false;
            T[] hs = target.GetComponents<T>();
            foreach (T h in hs) functor(h, eventData);
            return hs.Length > 0;
        }

        public static EventFunction<ISubmitHandler> submitHandler => (h, d) => h.OnSubmit(d);
        public static EventFunction<ICancelHandler> cancelHandler => (h, d) => h.OnCancel(d);
        public static EventFunction<ISelectHandler> selectHandler => (h, d) => h.OnSelect(d);
        public static EventFunction<IDeselectHandler> deselectHandler => (h, d) => h.OnDeselect(d);
        public static EventFunction<IPointerClickHandler> pointerClickHandler => (h, d) => h.OnPointerClick((PointerEventData)d);
    }
}

// =============================================================================
//  TextMeshPro
// =============================================================================
namespace TMPro
{
    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, TopFlush = 272, TopGeoAligned = 288,
        Left = 513, Center = 514, Right = 516, Justified = 520, Flush = 528, CenterGeoAligned = 544,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, BottomFlush = 1040, BottomGeoAligned = 1056,
        BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, BaselineJustified = 2056, BaselineFlush = 2064, BaselineGeoAligned = 2080,
        MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, MidlineFlush = 4112, MidlineGeoAligned = 4128,
        CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, CaplineJustified = 8200, CaplineFlush = 8208, CaplineGeoAligned = 8224,
        Converted = 65535,
    }

    public enum HorizontalAlignmentOptions { Left = 1, Center = 2, Right = 4, Justified = 8, Flush = 16, Geometry = 32 }
    public enum VerticalAlignmentOptions { Top = 256, Middle = 512, Bottom = 1024, Baseline = 2048, Geometry = 4096, Capline = 8192 }

    [Flags]
    public enum FontStyles
    {
        Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32,
        Strikethrough = 64, Superscript = 128, Subscript = 256, Highlight = 512,
    }

    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    public enum TextWrappingModes { NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap }

    public class TMP_FontAsset : UnityEngine.Object { }
    public class TMP_SpriteAsset : UnityEngine.Object { }
    public class TMP_StyleSheet : UnityEngine.Object { }

    public class TMP_TextInfo
    {
        public int characterCount, wordCount, lineCount, pageCount;
    }

    public class TMP_Text : UnityEngine.UI.MaskableGraphic
    {
        private string m_text;

        public virtual string text
        {
            get
            {
                string el = element;
                if (el.Length > 0) return Elintria.UIRuntime.GetText(el);
                return m_text ?? string.Empty;
            }
            set
            {
                m_text = value ?? string.Empty;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetText(el, m_text);
            }
        }

        public void SetText(string sourceText) { text = sourceText; }
        public void SetText(string sourceText, bool syncTextInputBox) { text = sourceText; }
        public void SetText(System.Text.StringBuilder sb) { text = sb != null ? sb.ToString() : ""; }
        public void SetText(char[] chars) { text = chars != null ? new string(chars) : ""; }
        public void SetText(string format, float arg0) { text = Format(format, arg0, 0f, 0f); }
        public void SetText(string format, float arg0, float arg1) { text = Format(format, arg0, arg1, 0f); }
        public void SetText(string format, float arg0, float arg1, float arg2) { text = Format(format, arg0, arg1, arg2); }

        // TMP's own mini format: {0}, {1}, {2}, with an optional ":n" for the
        // number of decimal places - "{0:2}" - rather than .NET's.
        private static string Format(string f, float a, float b, float c)
        {
            if (f == null) return string.Empty;
            float[] args = { a, b, c };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < f.Length; ++i)
            {
                if (f[i] == '{')
                {
                    int close = f.IndexOf('}', i);
                    if (close > i)
                    {
                        string inner = f.Substring(i + 1, close - i - 1);
                        string[] parts = inner.Split(':');
                        int idx;
                        if (int.TryParse(parts[0], out idx) && idx >= 0 && idx < 3)
                        {
                            int dec = 0;
                            if (parts.Length > 1) int.TryParse(parts[1], out dec);
                            sb.Append(args[idx].ToString("F" + dec, CultureInfo.InvariantCulture));
                            i = close;
                            continue;
                        }
                    }
                }
                sb.Append(f[i]);
            }
            return sb.ToString();
        }

        private float m_size = -1f;
        public float fontSize
        {
            get { return m_size >= 0f ? m_size : UnityEngine.UI.UILink.Num(gameObject, "fs", 36f); }
            set
            {
                m_size = value;
                string el = element;
                if (el.Length > 0) Elintria.UIRuntime.SetProp(el, "fontSize", value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public float fontSizeMin = 18f, fontSizeMax = 72f;
        public bool enableAutoSizing;
        public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        public HorizontalAlignmentOptions horizontalAlignment = HorizontalAlignmentOptions.Left;
        public VerticalAlignmentOptions verticalAlignment = VerticalAlignmentOptions.Top;
        public FontStyles fontStyle = FontStyles.Normal;
        public TMP_FontAsset font;
        public UnityEngine.Material fontSharedMaterial, fontMaterial;
        public bool richText = true;
        public bool enableWordWrapping = true;
        public TextWrappingModes textWrappingMode = TextWrappingModes.Normal;
        public TextOverflowModes overflowMode = TextOverflowModes.Overflow;
        public int maxVisibleCharacters = 99999, maxVisibleWords = 99999, maxVisibleLines = 99999;
        public int firstVisibleCharacter;
        public float characterSpacing, wordSpacing, lineSpacing, paragraphSpacing;
        public Elintria.Vector4 margin;
        public bool isRightToLeftText, parseCtrlCharacters = true, isOrthographic;
        public float outlineWidth;
        public Elintria.Color32 faceColor = new Elintria.Color32(255, 255, 255, 255);
        public Elintria.Color32 outlineColor = new Elintria.Color32(0, 0, 0, 255);

        public float alpha
        {
            get { return color.a; }
            set { Elintria.Color c = color; c.a = value; color = c; }
        }

        public bool isTextOverflowing => false;
        public TMP_TextInfo textInfo => new TMP_TextInfo { characterCount = text.Length };
        public float preferredWidth => text.Length * fontSize * 0.5f;
        public float preferredHeight => fontSize * 1.2f;
        public Elintria.Vector2 GetPreferredValues() => new Elintria.Vector2(preferredWidth, preferredHeight);
        public Elintria.Vector2 GetPreferredValues(string t) => new Elintria.Vector2((t ?? "").Length * fontSize * 0.5f, preferredHeight);
        public Elintria.Vector2 GetRenderedValues() => GetPreferredValues();
        public void ForceMeshUpdate() { }
        public void ForceMeshUpdate(bool ignoreActiveState, bool forceTextReparsing = false) { }
        public string GetParsedText() => text;
    }

    public class TextMeshProUGUI : TMP_Text { }

    /// <summary>World-space text. The UI draws on the screen only, so this
    /// keeps its text for scripts to read back and is not drawn.</summary>
    public class TextMeshPro : TMP_Text { }

    public class TMP_InputField : UnityEngine.UI.Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        [Serializable] public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
        [Serializable] public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
        [Serializable] public class SelectionEvent : UnityEngine.Events.UnityEvent<string> { }

        public string text
        {
            get { string el = element; return el.Length > 0 ? Elintria.UIRuntime.GetText(el) : string.Empty; }
            set { string el = element; if (el.Length > 0) Elintria.UIRuntime.SetText(el, value ?? ""); onValueChanged.Invoke(value ?? ""); }
        }

        public void SetTextWithoutNotify(string v) { string el = element; if (el.Length > 0) Elintria.UIRuntime.SetText(el, v ?? ""); }
        public OnChangeEvent onValueChanged = new OnChangeEvent();
        public SubmitEvent onEndEdit = new SubmitEvent();
        public SubmitEvent onSubmit = new SubmitEvent();
        public SelectionEvent onSelect = new SelectionEvent();
        public SelectionEvent onDeselect = new SelectionEvent();
        public TMP_Text textComponent;
        public UnityEngine.UI.Graphic placeholder;
        public int characterLimit;
        public ContentType contentType;
        public LineType lineType;
        public bool readOnly;
        public bool isFocused => false;
        public int caretPosition { get; set; }
        public void ActivateInputField() { }
        public void DeactivateInputField(bool clearSelection = false) { }
    }

    public class TMP_Dropdown : UnityEngine.UI.Selectable
    {
        public class OptionData
        {
            public string text;
            public UnityEngine.Sprite image;
            public OptionData() { }
            public OptionData(string text) { this.text = text; }
            public OptionData(UnityEngine.Sprite image) { this.image = image; }
            public OptionData(string text, UnityEngine.Sprite image) { this.text = text; this.image = image; }
        }

        [Serializable] public class DropdownEvent : UnityEngine.Events.UnityEvent<int> { }

        public List<OptionData> options = new List<OptionData>();
        public DropdownEvent onValueChanged = new DropdownEvent();
        public TMP_Text captionText, itemText;
        private int m_value;

        public int value
        {
            get { return m_value; }
            set { if (m_value != value) { m_value = value; RefreshShownValue(); onValueChanged.Invoke(value); } }
        }

        public void SetValueWithoutNotify(int v) { m_value = v; RefreshShownValue(); }

        public void RefreshShownValue()
        {
            string t = m_value >= 0 && m_value < options.Count ? options[m_value].text : "";
            if (captionText != null) captionText.text = t;
            else if (element.Length > 0) Elintria.UIRuntime.SetText(element, t);
        }

        public void AddOptions(List<OptionData> o) { options.AddRange(o); }
        public void AddOptions(List<string> o) { foreach (string s in o) options.Add(new OptionData(s)); }
        public void AddOptions(List<UnityEngine.Sprite> o) { foreach (UnityEngine.Sprite s in o) options.Add(new OptionData(s)); }
        public void ClearOptions() { options.Clear(); m_value = 0; }
        public void Show() { }
        public void Hide() { }
    }
}
