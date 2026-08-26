using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    internal static class BrothersUiUtil
    {
        public static readonly Color Parchment = new Color(0.93f, 0.86f, 0.72f, 0.96f);
        public static readonly Color Ink = new Color(0.18f, 0.14f, 0.12f, 1f);
        public static readonly Color PanelDark = new Color(0.10f, 0.11f, 0.14f, 0.92f);
        public static readonly Color AccentGreen = new Color(0.25f, 0.72f, 0.48f, 1f);
        public static readonly Color AccentBlue = new Color(0.36f, 0.62f, 0.88f, 1f);
        public static readonly Color AccentOrange = new Color(0.85f, 0.48f, 0.22f, 1f);
        public static readonly Color PlusOrange = new Color(1f, 0.55f, 0.1f, 1f);
        public static readonly Color BrotherHp = new Color(0.35f, 0.85f, 0.55f, 1f);
        public static readonly Color EnemyHp = new Color(0.92f, 0.35f, 0.32f, 1f);

        public static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Text MakeText(Transform parent, string name, string value, int size, Vector2 anchored, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchored;
            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = color ?? new Color(0.25f, 0.6f, 1f, 1f);

            var t = MakeText(go.transform, "Label", label, 28, Vector2.zero, size);
            Stretch(t.rectTransform);
            return go.GetComponent<Button>();
        }

        public static Image MakePanel(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static ScrollRect MakeHScroll(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.06f, 0.72f);
            root.GetComponent<Mask>().showMaskGraphic = true;

            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(root.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.sizeDelta = new Vector2(size.x, 0f);
            crt.anchoredPosition = Vector2.zero;
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = root.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 24f;
            scroll.content = crt;
            scroll.viewport = rt;
            return scroll;
        }

        public static Image MakePortrait(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color fallback)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = fallback;
            }

            return img;
        }

        public static Image MakeHpFill(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor)
        {
            MakePanel(parent, name + "Back", size, pos, new Color(0.08f, 0.08f, 0.1f, 0.9f));
            var fillGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(parent, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.sizeDelta = new Vector2(size.x - 8f, size.y - 6f);
            frt.anchoredPosition = new Vector2(pos.x - size.x * 0.5f + 4f, pos.y);
            var fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            return fill;
        }

        public static void SetHpFill(Image fill, float ratio, float fullWidth)
        {
            if (fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            fill.rectTransform.sizeDelta = new Vector2((fullWidth - 8f) * ratio, fill.rectTransform.sizeDelta.y);
        }

        public static void SetAffordable(Button btn, bool ok, Color ready)
        {
            if (btn == null) return;
            btn.interactable = ok;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = ok ? ready : new Color(0.32f, 0.32f, 0.34f, 0.85f);
            var t = btn.GetComponentInChildren<Text>();
            if (t != null)
                t.color = ok ? Color.white : new Color(0.62f, 0.62f, 0.64f);
        }

        public static Button MakePlus(Transform parent, string name, Color color)
        {
            var btn = MakeButton(parent, name, "+", Vector2.zero, new Vector2(68, 68), color);
            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 44;
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                label.alignment = TextAnchor.MiddleCenter;
            }

            return btn;
        }
    }
}
