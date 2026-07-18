using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Data;

namespace VoidDay.View
{
    /// UGUI construction helpers shared by the HUD and the station popup. Every colour, size, and radius comes
    /// from the UiThemeSO (set once at boot) — nothing here inlines a mockup value (CLAUDE.md rule 1). Corner
    /// rounding is produced at runtime from a 9-slice rounded-rect sprite so the chrome reads "chunky rounded"
    /// (StyleGuide) without importing a sprite atlas; real UI art still swaps into the same slots later (§12.8).
    public static class UiFactory
    {
        static UiThemeSO _theme;
        public static UiThemeSO Theme => _theme;
        public static void SetTheme(UiThemeSO theme) => _theme = theme;

        // Unity 6 ships the old Arial here; legacy Text needs a font and TMP essentials aren't imported.
        static Font _builtinFont;
        public static Font Font =>
            _theme != null && _theme.font != null
                ? _theme.font
                : (_builtinFont != null ? _builtinFont : (_builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")));

        // ---- Rounded-rect sprites (cached per radius) ----

        static readonly Dictionary<int, Sprite> _rounded = new();

        /// A white 9-slice rounded-rect sprite of the given corner radius (reference px). Tint via Image.color.
        public static Sprite Rounded(int radius)
        {
            if (radius <= 0) radius = 1;
            if (_rounded.TryGetValue(radius, out var s)) return s;

            int n = radius * 2 + 4; // 4px flat centre so the slice has something to stretch
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, n - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float cov = Mathf.Clamp01(radius + 0.5f - d); // 1px antialiased edge
                px[y * n + x] = new Color(1f, 1f, 1f, cov);
            }
            tex.SetPixels(px);
            tex.Apply();

            // pixelsPerUnit 100 → the 9-slice border renders at `radius` reference px (multiplier 1).
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _rounded[radius] = sprite;
            return sprite;
        }

        // ---- Canvas ----

        public static Canvas Canvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // portrait phone (§2)
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        // ---- Images ----

        /// A rounded, tinted panel/box. Shadowless; soft shadows are faked by a darker sibling where needed.
        public static Image RoundedBox(Transform parent, Color color, int radius, string name = "Box")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = color;
            return img;
        }

        /// A rounded outline (transparent centre, coloured border) — used for empty slots.
        public static Image RoundedOutline(Transform parent, Color color, int radius, float thickness, string name = "Outline")
        {
            var box = RoundedBox(parent, color, radius, name);
            box.type = Image.Type.Sliced;
            box.fillCenter = false;
            box.pixelsPerUnitMultiplier = Mathf.Max(1f, radius / Mathf.Max(1f, thickness));
            return box;
        }

        // ---- Text ----

        public static Text Label(Transform parent, string text, int fontSize, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---- Buttons ----

        /// A rounded, filled text button. Returns the Button; out param is its label (for state re-tinting).
        public static Button TextButton(Transform parent, string label, int fontSize, Color bg, Color fg,
            int radius, out Text labelText)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = bg;
            var btn = go.GetComponent<Button>();
            labelText = Label(go.transform, label, fontSize, fg, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform);
            return btn;
        }

        /// A round icon button (circle) — e.g. the popup close ✕.
        public static Button CircleButton(Transform parent, string glyph, int fontSize, Color bg, Color fg,
            float diameter, out Text labelText)
        {
            var btn = TextButton(parent, glyph, fontSize, bg, fg, Mathf.RoundToInt(diameter * 0.5f), out labelText);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = diameter;
            le.preferredHeight = le.minHeight = diameter;
            return btn;
        }

        // ---- Layout ----

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static RectTransform Rect(Component c) => (RectTransform)c.transform;

        public static LayoutElement Sized(Component c, float height)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return le;
        }

        public static LayoutElement Flexible(Component c)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            return le;
        }

        public static LayoutElement Width(Component c, float width)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            return le;
        }

        public static VerticalLayoutGroup VerticalList(Transform parent, float spacing, RectOffset padding)
        {
            var go = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float spacing, Color? bg = null, int radius = 0)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            if (bg.HasValue)
            {
                var img = go.AddComponent<Image>();
                if (radius > 0) { img.sprite = Rounded(radius); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
                img.color = bg.Value;
            }
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(16, 16, 8, 8);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }
    }
}
