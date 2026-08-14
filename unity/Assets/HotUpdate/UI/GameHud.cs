using System;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    /// <summary>
    /// 局内 HUD + 结算。通过 UIBinder 取控件，方便以后换成 Prefab。
    /// 当前仍可代码搭一层骨架，但绑定 key 已经定死，别再到处 Find。
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public const string KeyScore = "txt_score";
        public const string KeyHint = "txt_hint";
        public const string KeyGameOverRoot = "panel_gameover";
        public const string KeyFinalScore = "txt_final_score";
        public const string KeyBtnRevive = "btn_revive";
        public const string KeyBtnDouble = "btn_double";
        public const string KeyBtnRetry = "btn_retry";
        public const string KeyBtnHome = "btn_home";

        UIBinder _binder;
        Text _scoreText;
        Text _hintText;
        GameObject _gameOverRoot;
        Text _gameOverScore;
        Action _onRevive;
        Action _onDouble;
        Action _onRetry;
        Action _onHome;

        public static GameHud Create(Transform canvasRoot)
        {
            var go = new GameObject("GameHud", typeof(RectTransform), typeof(UIBinder));
            go.transform.SetParent(canvasRoot, false);
            Stretch(go.GetComponent<RectTransform>());
            var hud = go.AddComponent<GameHud>();
            hud.BuildRuntimeSkeleton();
            hud.WireFromBinder();
            return hud;
        }

        /// <summary>若已有 Prefab 实例，直接从 UIBinder 接线。</summary>
        public void WireFromBinder()
        {
            _binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();
            _binder.Rebuild();

            _scoreText = _binder.Get<Text>(KeyScore);
            _hintText = _binder.Get<Text>(KeyHint);
            var goRoot = _binder.Get<Transform>(KeyGameOverRoot);
            _gameOverRoot = goRoot != null ? goRoot.gameObject : null;
            _gameOverScore = _binder.Get<Text>(KeyFinalScore);

            var btnRevive = _binder.Get<Button>(KeyBtnRevive);
            var btnDouble = _binder.Get<Button>(KeyBtnDouble);
            var btnRetry = _binder.Get<Button>(KeyBtnRetry);
            var btnHome = _binder.Get<Button>(KeyBtnHome);

            if (btnRevive != null)
            {
                btnRevive.onClick.RemoveAllListeners();
                btnRevive.onClick.AddListener(() => _onRevive?.Invoke());
            }

            if (btnDouble != null)
            {
                btnDouble.onClick.RemoveAllListeners();
                btnDouble.onClick.AddListener(() => _onDouble?.Invoke());
            }

            if (btnRetry != null)
            {
                btnRetry.onClick.RemoveAllListeners();
                btnRetry.onClick.AddListener(() => _onRetry?.Invoke());
            }

            if (btnHome != null)
            {
                btnHome.onClick.RemoveAllListeners();
                btnHome.onClick.AddListener(() => _onHome?.Invoke());
            }

            if (_gameOverRoot != null)
                _gameOverRoot.SetActive(false);
        }

        /// <summary>开发期无 Prefab 时，代码搭一份并写入 UIBinder。</summary>
        void BuildRuntimeSkeleton()
        {
            _binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();

            var score = MakeText(transform, KeyScore, "0", 56, new Vector2(0, -80), new Vector2(600, 100), TextAnchor.UpperCenter);
            var hint = MakeText(transform, KeyHint, "点击落块", 32, new Vector2(0, 120), new Vector2(700, 60), TextAnchor.LowerCenter);
            _binder.Set(KeyScore, score);
            _binder.Set(KeyHint, hint);

            var gameOver = new GameObject(KeyGameOverRoot, typeof(RectTransform), typeof(Image));
            gameOver.transform.SetParent(transform, false);
            Stretch(gameOver.GetComponent<RectTransform>());
            gameOver.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);
            gameOver.SetActive(false);
            _binder.Set(KeyGameOverRoot, gameOver.transform);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(gameOver.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 740);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

            MakeText(panel.transform, "Title", "游戏结束", 48, new Vector2(0, 280), new Vector2(800, 80), TextAnchor.MiddleCenter);
            var finalScore = MakeText(panel.transform, KeyFinalScore, "分数 0", 36, new Vector2(0, 180), new Vector2(800, 60), TextAnchor.MiddleCenter);
            _binder.Set(KeyFinalScore, finalScore);

            var b1 = MakeButton(panel.transform, KeyBtnRevive, "看广告复活", new Vector2(0, 70));
            var b2 = MakeButton(panel.transform, KeyBtnDouble, "看广告双倍分", new Vector2(0, -50));
            var b3 = MakeButton(panel.transform, KeyBtnRetry, "再来一局", new Vector2(0, -170));
            var b4 = MakeButton(panel.transform, KeyBtnHome, "回主界面", new Vector2(0, -290));
            _binder.Set(KeyBtnRevive, b1);
            _binder.Set(KeyBtnDouble, b2);
            _binder.Set(KeyBtnRetry, b3);
            _binder.Set(KeyBtnHome, b4);
        }

        public void SetScore(int score)
        {
            if (_scoreText != null) _scoreText.text = score.ToString();
        }

        public void SetHint(string hint)
        {
            if (_hintText != null) _hintText.text = hint ?? string.Empty;
        }

        public void ShowGameOver(int score, Action onRevive, Action onDouble, Action onRetry, Action onHome)
        {
            _onRevive = onRevive;
            _onDouble = onDouble;
            _onRetry = onRetry;
            _onHome = onHome;
            if (_gameOverScore != null) _gameOverScore.text = $"分数 {score}";
            if (_gameOverRoot != null) _gameOverRoot.SetActive(true);
            SetHint(string.Empty);
        }

        public void HideGameOver()
        {
            if (_gameOverRoot != null) _gameOverRoot.SetActive(false);
        }

        static Text MakeText(Transform parent, string name, string value, int size, Vector2 anchored, Vector2 sizeDelta, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchor(rt, anchor);
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
            rt.sizeDelta = new Vector2(520, 90);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.25f, 0.6f, 1f, 1f);

            var t = MakeText(go.transform, "Label", label, 30, Vector2.zero, new Vector2(520, 90), TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
            return go.GetComponent<Button>();
        }

        static Font BuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static void SetAnchor(RectTransform rt, TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    break;
                case TextAnchor.LowerCenter:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    break;
                default:
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }
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
