using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// Small UGUI construction helpers, used across the HUD, station panel, and popups (well past rule-of-
    /// three). Placeholder chrome per §12.8 — legacy Text + solid Images, no real art. Real UI swaps in
    /// against ui_inventory mockups later; nothing here leaks a placeholder into the event layer.
    public static class UiFactory
    {
        // Unity 6 ships the old Arial as this builtin; legacy Text needs a font and TMP essentials aren't imported.
        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static readonly Color PanelBg = new Color(0.10f, 0.08f, 0.19f, 0.94f);   // void indigo
        public static readonly Color RowBg = new Color(0.18f, 0.16f, 0.28f, 1f);
        public static readonly Color Accent = new Color(0.545f, 0.361f, 0.965f, 1f);    // #8B5CF6
        public static readonly Color Disabled = new Color(0.30f, 0.30f, 0.34f, 1f);
        public static readonly Color Warning = new Color(0.90f, 0.42f, 0.42f, 1f);
        public static readonly Color Ink = new Color(0.93f, 0.93f, 0.97f, 1f);

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

        public static Image Panel(Transform parent, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int fontSize, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = Ink;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string label, int fontSize, out Text labelText)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Accent;
            var btn = go.GetComponent<Button>();
            labelText = Label(go.transform, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform);
            return btn;
        }

        /// Anchor + fill a rect to its parent, with an optional uniform inset.
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

        public static VerticalLayoutGroup VerticalList(Transform parent, float spacing, RectOffset padding)
        {
            var go = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
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

        public static HorizontalLayoutGroup Row(Transform parent, float spacing)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = RowBg;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(16, 16, 8, 8);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }
    }
}
