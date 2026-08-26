using UnityEngine;
using UnityEngine.UI;

namespace JojoP.AOT.Boot
{
    /// <summary>Loading：展示版本/差量/资源/HybridCLR 进度，玩家不可进主界面交互。</summary>
    public sealed class LoadingView : MonoBehaviour
    {
        Text _meta;
        Text _status;
        Text _detail;
        Text _percent;
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
            MakeText(transform, "LoadingTitle", "正在更新", 36, new Vector2(0, 280), 48);
            _meta = MakeText(transform, "Meta", "", 20, new Vector2(0, 160), 160);
            _meta.color = new Color(0.75f, 0.88f, 1f, 1f);
            _status = MakeText(transform, "Status", "准备中…", 28, new Vector2(0, 40), 56);
            _detail = MakeText(transform, "Detail", string.Empty, 20, new Vector2(0, -70), 140);
            _detail.color = new Color(1, 1, 1, 0.6f);
            _percent = MakeText(transform, "Percent", "0%", 22, new Vector2(0, -200), 40);

            var barBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(transform, false);
            var bgRt = barBg.GetComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(_barWidth, 18);
            bgRt.anchoredPosition = new Vector2(0, -160);
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

        public void SetVersionBoard(string mode, string localVer, string remoteVer, string cdnUrl)
        {
            if (_meta == null) return;
            _meta.text =
                $"{mode}\n" +
                $"GetPackageVersion 本地: {Blank(localVer)}\n" +
                $"RequestPackageVersion 远端: {Blank(remoteVer)}\n" +
                $"{Blank(cdnUrl)}";
        }

        public void SetProgress(float progress01, string status, string detail = null)
        {
            progress01 = Mathf.Clamp01(progress01);
            if (_status != null) _status.text = status ?? string.Empty;
            if (_detail != null) _detail.text = detail ?? string.Empty;
            if (_percent != null) _percent.text = $"{Mathf.RoundToInt(progress01 * 100f)}%";
            if (_barFillRt != null)
                _barFillRt.sizeDelta = new Vector2(_barWidth * progress01, 18);
        }

        static string Blank(string v) => string.IsNullOrEmpty(v) ? "—" : v;

        static Text MakeText(Transform parent, string name, string value, int size, Vector2 pos, float height = 60f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(980, height);
            rt.anchoredPosition = pos;
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
