using UnityEngine;
using UnityEngine.UI;

namespace JojoP.AOT.Boot
{
    /// <summary>Loading：展示版本/差量/资源/HybridCLR 进度，玩家不可进主界面交互。</summary>
    public sealed class LoadingView : MonoBehaviour
    {
        Text _status;
        Text _detail;
        Image _barFill;
        RectTransform _barFillRt;
        float _barWidth = 720f;

        public static LoadingView Create(Transform parent)
        {
            var go = new GameObject("Loading", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 1f);

            var view = go.AddComponent<LoadingView>();
            view.Build();
            go.SetActive(false);
            return view;
        }

        void Build()
        {
            MakeText(transform, "LoadingTitle", "正在更新", 40, new Vector2(0, 120));
            _status = MakeText(transform, "Status", "准备中…", 28, new Vector2(0, 40));
            _detail = MakeText(transform, "Detail", string.Empty, 22, new Vector2(0, -10));
            _detail.color = new Color(1, 1, 1, 0.55f);

            var barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(transform, false);
            var bgRt = barBg.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(_barWidth, 18);
            bgRt.anchoredPosition = new Vector2(0, -80);
            barBg.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);

            var barFill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFill.transform.SetParent(barBg.transform, false);
            _barFillRt = barFill.GetComponent<RectTransform>();
            _barFillRt.anchorMin = new Vector2(0, 0.5f);
            _barFillRt.anchorMax = new Vector2(0, 0.5f);
            _barFillRt.pivot = new Vector2(0, 0.5f);
            _barFillRt.sizeDelta = new Vector2(0, 18);
            _barFillRt.anchoredPosition = Vector2.zero;
            _barFill = barFill.GetComponent<Image>();
            _barFill.color = new Color(0.3f, 0.7f, 1f, 1f);
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        public void SetProgress(float progress01, string status, string detail = null)
        {
            progress01 = Mathf.Clamp01(progress01);
            if (_status != null) _status.text = status ?? string.Empty;
            if (_detail != null) _detail.text = detail ?? string.Empty;
            if (_barFillRt != null)
                _barFillRt.sizeDelta = new Vector2(_barWidth * progress01, 18);
        }

        static Text MakeText(Transform parent, string name, string value, int size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900, 60);
            rt.anchoredPosition = pos;
            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
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
