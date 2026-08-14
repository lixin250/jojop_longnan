using System;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.Privacy
{
    /// <summary>首次启动隐私弹窗（代码搭 UI，少依赖美术）。</summary>
    public sealed class PrivacyConsentView : MonoBehaviour
    {
        Action _onAccepted;

        public static PrivacyConsentView Show(Transform canvasRoot, string privacyUrl, Action onAccepted)
        {
            var go = new GameObject("PrivacyConsent", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            Stretch(go.GetComponent<RectTransform>());

            go.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.96f);

            var panel = CreatePanel(go.transform);
            CreateText(panel, "隐私说明", 42, FontStyle.Bold, new Vector2(0, 220), new Vector2(900, 80));
            CreateText(
                panel,
                "JojoP Stack 免费游玩，会展示广告。我们可能使用设备标识用于广告与可选云存档。" +
                "点「同意」即表示你接受隐私政策。",
                28,
                FontStyle.Normal,
                new Vector2(0, 40),
                new Vector2(920, 280));

            var view = go.AddComponent<PrivacyConsentView>();
            view._onAccepted = onAccepted;

            CreateButton(panel, "查看政策", new Vector2(-220, -220), () =>
            {
                if (!string.IsNullOrEmpty(privacyUrl))
                    Application.OpenURL(privacyUrl);
            });
            CreateButton(panel, "同意", new Vector2(220, -220), () =>
            {
                PrivacyConsent.Accept();
                view._onAccepted?.Invoke();
                Destroy(go);
            });

            return view;
        }

        static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000, 700);
            go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
            return rt;
        }

        static void CreateText(RectTransform parent, string content, int size, FontStyle style, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static void CreateButton(RectTransform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320, 96);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.95f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var text = textGo.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = label;
            text.fontSize = 34;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
