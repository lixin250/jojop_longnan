using System;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    /// <summary>
    /// 主界面：最高分、开始游戏、右上角设置。
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        public const string KeyBest = "txt_best";
        public const string KeyTitle = "txt_title";
        public const string KeyBtnStart = "btn_start";
        public const string KeyBtnBrothers = "btn_brothers";
        public const string KeyBtnSettings = "btn_settings";

        UIBinder _binder;
        Text _bestText;
        Action _onStart;
        Action _onBrothers;
        Action _onSettings;

        public static MainMenuView Create(Transform canvasRoot)
        {
            var go = new GameObject("MainMenu", typeof(RectTransform), typeof(UIBinder));
            go.transform.SetParent(canvasRoot, false);
            Stretch(go.GetComponent<RectTransform>());
            var view = go.AddComponent<MainMenuView>();
            view.BuildRuntimeSkeleton();
            view.WireFromBinder();
            return view;
        }

        public void WireFromBinder()
        {
            _binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();
            _binder.Rebuild();

            _bestText = _binder.Get<Text>(KeyBest);

            var btnStart = _binder.Get<Button>(KeyBtnStart);
            if (btnStart != null)
            {
                btnStart.onClick.RemoveAllListeners();
                btnStart.onClick.AddListener(() => _onStart?.Invoke());
            }

            var btnBrothers = _binder.Get<Button>(KeyBtnBrothers);
            if (btnBrothers != null)
            {
                btnBrothers.onClick.RemoveAllListeners();
                btnBrothers.onClick.AddListener(() => _onBrothers?.Invoke());
            }

            var btnSettings = _binder.Get<Button>(KeyBtnSettings);
            if (btnSettings != null)
            {
                btnSettings.onClick.RemoveAllListeners();
                btnSettings.onClick.AddListener(() => _onSettings?.Invoke());
            }
        }

        void BuildRuntimeSkeleton()
        {
            _binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();

            var title = MakeText(transform, KeyTitle, "JojoP · 龙南", 64, new Vector2(0, 320), new Vector2(900, 100), TextAnchor.MiddleCenter);
            _binder.Set(KeyTitle, title);

            var best = MakeText(transform, KeyBest, "最高分 0", 36, new Vector2(0, 200), new Vector2(700, 60), TextAnchor.MiddleCenter);
            _binder.Set(KeyBest, best);

            var brothers = MakeButton(transform, KeyBtnBrothers, "我和我的龙兄南弟", new Vector2(0, 40), new Vector2(520, 110));
            brothers.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.45f, 1f);
            _binder.Set(KeyBtnBrothers, brothers);

            var start = MakeButton(transform, KeyBtnStart, "叠叠乐", new Vector2(0, -100), new Vector2(520, 90));
            _binder.Set(KeyBtnStart, start);

            var settings = MakeButton(transform, KeyBtnSettings, "设置", new Vector2(-40, -40), new Vector2(160, 70));
            var srt = settings.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-40, -40);
            _binder.Set(KeyBtnSettings, settings);
        }

        public void Show(int bestScore, Action onStart, Action onSettings, Action onBrothers = null)
        {
            _onStart = onStart;
            _onSettings = onSettings;
            _onBrothers = onBrothers;
            SetBest(bestScore);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetBest(int bestScore)
        {
            if (_bestText != null)
                _bestText.text = $"最高分 {bestScore}";
        }

        static Text MakeText(Transform parent, string name, string value, int size, Vector2 anchored, Vector2 sizeDelta, TextAnchor anchor)
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
            return text;
        }

        static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.25f, 0.6f, 1f, 1f);

            var t = MakeText(go.transform, "Label", label, 32, Vector2.zero, size, TextAnchor.MiddleCenter);
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
