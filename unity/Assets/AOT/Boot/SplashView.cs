using UnityEngine;
using UnityEngine.UI;

namespace JojoP.AOT.Boot
{
    /// <summary>闪屏：品牌展示，不承载下载逻辑。</summary>
    public sealed class SplashView : MonoBehaviour
    {
        public static SplashView Create(Transform parent)
        {
            var go = new GameObject("Splash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            go.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 1f);

            var view = go.AddComponent<SplashView>();
            var title = MakeText(go.transform, "JojoP", 72, new Vector2(0, 40));
            MakeText(go.transform, "Stack", 36, new Vector2(0, -40)).color = new Color(1, 1, 1, 0.65f);
            title.fontStyle = FontStyle.Bold;
            return view;
        }

        public void Hide() => gameObject.SetActive(false);

        static Text MakeText(Transform parent, string value, int size, Vector2 pos)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900, 100);
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
