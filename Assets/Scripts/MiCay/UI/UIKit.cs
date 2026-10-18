using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace MiCay.UI
{
    public static class Pal
    {
        public static readonly Color Bg = new Color(0.98f, 0.93f, 0.84f);
        public static readonly Color Wood = new Color(0.55f, 0.36f, 0.22f);
        public static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.13f);
        public static readonly Color Paper = new Color(1f, 0.98f, 0.93f);
        public static readonly Color Red = new Color(0.84f, 0.2f, 0.15f);
        public static readonly Color Orange = new Color(0.95f, 0.55f, 0.15f);
        public static readonly Color Green = new Color(0.2f, 0.62f, 0.3f);
        public static readonly Color Blue = new Color(0.2f, 0.45f, 0.78f);
        public static readonly Color Teal = new Color(0.1f, 0.55f, 0.55f);
        public static readonly Color Gray = new Color(0.6f, 0.6f, 0.6f);
        public static readonly Color Dark = new Color(0.15f, 0.12f, 0.1f);
        public static readonly Color Text = new Color(0.18f, 0.12f, 0.08f);
        public static readonly Color Kitchen = new Color(0.86f, 0.88f, 0.88f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
    }

    public static class UIKit
    {
        static Font font;
        static bool pixelFont;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>("Fonts/VT323-Regular");
                    pixelFont = font != null;
                    if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Roboto", "Noto Sans", "Segoe UI" }, 32);
                }
                return font;
            }
        }

        public static float FontScale
        {
            get
            {
                var _ = Font;
                return pixelFont ? 1.3f : 1f;
            }
        }

        public static int Fs(float size) => Mathf.RoundToInt(size * FontScale);

        static readonly Regex SizeTag = new Regex(@"<size=(\d+)>");

        public static string Px(string s) =>
            string.IsNullOrEmpty(s) || s.IndexOf("<size=", StringComparison.Ordinal) < 0
                ? s
                : SizeTag.Replace(s, m => $"<size={Fs(int.Parse(m.Groups[1].Value))}>");

        public static void Set(this Text t, string s) => t.text = Px(s);

        public static Text Flow(this Text t)
        {
            t.resizeTextForBestFit = false;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Fit(this Text t, float max, float min = -1)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = Fs(max);
            t.resizeTextMinSize = Fs(min > 0 ? min : Mathf.Max(9, max * 0.55f));
            t.fontSize = Fs(max);
            return t;
        }

        static Sprite rounded, circle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            rounded = null;
            circle = null;
            font = null;
        }

        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeRounded(64, 18);
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = MakeRounded(64, 32);
                return circle;
            }
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = Mathf.Min(radius + 1, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Anchors(this RectTransform rt, float x0, float y0, float x1, float y1, float pad = 0)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float pad = 0) => rt.Anchors(0, 0, 1, 1, pad);

        public static T Fill<T>(this T c, float pad = 0) where T : Component
        {
            ((RectTransform)c.transform).Fill(pad);
            return c;
        }

        public static Image Box(Transform parent, Color color, bool rounded = true, string name = "Box")
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Art.BoxFrame ?? Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size = 26, Color? color = null,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = Px(text);
            t.color = color ?? Pal.Text;
            t.alignment = anchor;
            t.fontStyle = pixelFont && style == FontStyle.Bold ? FontStyle.Normal : style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.lineSpacing = pixelFont ? 0.9f : 1f;
            t.Fit(size);
            return t;
        }

        public static Button Button(Transform parent, string text, Action onClick, Color? bg = null, int size = 26, Color? fg = null)
        {
            var img = Box(parent, bg ?? Pal.Orange, true, "Btn_" + text);
            if (Art.Btn != null) img.sprite = Art.Btn;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.6f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("click"); onClick(); });
            var t = Label(img.transform, text, size, fg ?? Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchors(0, 0, 1, 1).offsetMin = new Vector2(8, 8);
            t.rectTransform.offsetMax = new Vector2(-8, -6);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.06f, 0.03f, 0.65f);
            sh.effectDistance = new Vector2(2, -2);
            return b;
        }

        public static Text Caption(this Button b) => b.GetComponentInChildren<Text>();

        public static LayoutElement Size(this Component c, float h = -1, float w = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static VerticalLayoutGroup VList(Component c, float spacing = 8, int pad = 8, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HList(Component c, float spacing = 8, int pad = 8, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return h;
        }

        public static RectTransform ScrollList(Transform parent, float spacing = 8, int pad = 8)
        {
            var root = NewRect("Scroll", parent);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40;
            var viewport = NewRect("Viewport", root).Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1, 1, 1, 0.001f);
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VList(content, spacing, pad);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport;
            sr.content = content;
            return content;
        }

        public static GridLayoutGroup Grid(Component c, Vector2 cell, Vector2 spacing, int pad = 6)
        {
            var g = c.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.padding = new RectOffset(pad, pad, pad, pad);
            return g;
        }

        public static RectTransform Bar(Transform parent, Color bg, Color fill, out Image fillImg)
        {
            var back = Box(parent, bg, true, "Bar");
            fillImg = Box(back.transform, fill, true, "Fill");
            var frt = fillImg.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            return back.rectTransform;
        }

        public static void SetBar(Image fill, float v)
        {
            if (fill == null) return;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    public class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastRes;

        void Update()
        {
            var sa = Screen.safeArea;
            var res = new Vector2Int(Screen.width, Screen.height);
            if (sa == last && res == lastRes) return;
            last = sa;
            lastRes = res;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
