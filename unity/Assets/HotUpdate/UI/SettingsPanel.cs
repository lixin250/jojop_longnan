using System;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    /// <summary>
    /// 设置面板（占位）：隐私政策、关闭。
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public const string KeyRoot = "panel_settings";
        public const string KeyBtnPrivacy = "btn_privacy";
        public const string KeyBtnClose = "btn_close";

        UIBinder _binder;
        string _privacyUrl;
        Action _onClose;

        public static SettingsPanel Create(Transform canvasRoot)
        {
            var go = new GameObject("SettingsPanel", typeof(RectTransform), typeof(UIBinder));
            go.transform.SetParent(canvasRoot, false);
            Stretch(go.GetComponent<RectTransform>());
            var panel = go.AddComponent<SettingsPanel>();
            panel.BuildRuntimeSkeleton();
            panel.WireFromBinder();
            go.SetActive(false);
            return panel;
        }

        public void WireFromBinder()
        {
            _binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();
            _binder.Rebuild();

            var btnPrivacy = _binder.Get<Button>(KeyBtnPrivacy);
            if (btnPrivacy != null)
            {
                btnPrivacy.onClick.RemoveAllListeners();
                btnPrivacy.onClick.AddListener(OpenPrivacy);
            }

            var btnClose = _binder.Get<Button>(KeyBtnClose);
            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(() =>
                {
                    Hide();
                    _onClose?.Invoke();
                });
            }
        }

        void BuildRuntimeSkeleton()
        {
            _binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();

            var dim = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.55f);
            _binder.Set(KeyRoot, transform);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(820, 520);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

            MakeText(panel.transform, "Title", "设置", 44, new Vector2(0, 180), new Vector2(700, 70));

            var privacy = MakeButton(panel.transform, KeyBtnPrivacy, "隐私政策", new Vector2(0, 40));
            _binder.Set(KeyBtnPrivacy, privacy);

            var close = MakeButton(panel.transform, KeyBtnClose, "关闭", new Vector2(0, -120));
            _binder.Set(KeyBtnClose, close);
        }

        public void Show(string privacyUrl, Action onClose = null)
        {
            _privacyUrl = privacyUrl;
            _onClose = onClose;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        void OpenPrivacy()
        {
            if (string.IsNullOrEmpty(_privacyUrl)) return;
            Application.OpenURL(_privacyUrl);
        }

        static Text MakeText(Transform parent, string name, string value, int size, Vector2 anchored, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchored;
            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        static Button MakeButton(Transform parent, string name, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480, 90);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.25f, 0.6f, 1f, 1f);

            var t = MakeText(go.transform, "Label", label, 30, Vector2.zero, new Vector2(480, 90));
            Stretch(t.rectTransform);
            return go.GetComponent<Button>();
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
