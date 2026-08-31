using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    /// <summary>五维雷达：蓝=人物底板，绿=培养后外圈。</summary>
    public sealed class StatRadarGraphic : MaskableGraphic
    {
        static readonly Color Grid = new Color(1f, 1f, 0.92f, 0.16f);
        static readonly Color Axis = new Color(1f, 1f, 0.92f, 0.22f);
        static readonly Color InnerFill = new Color(0.36f, 0.66f, 0.91f, 0.48f);
        static readonly Color InnerLine = new Color(0.45f, 0.72f, 0.95f, 0.95f);
        static readonly Color OuterFill = new Color(0.22f, 0.78f, 0.48f, 0.32f);
        static readonly Color OuterLine = new Color(0.32f, 0.86f, 0.55f, 0.95f);

        readonly float[] _inner = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
        readonly float[] _outer = { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f };
        Text[] _labels;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = true;
        }

        public void SetValues(float[] inner, float[] outer)
        {
            int n = _inner.Length;
            for (int i = 0; i < n; i++)
            {
                _inner[i] = inner != null && i < inner.Length ? Mathf.Clamp01(inner[i]) : 0.08f;
                _outer[i] = outer != null && i < outer.Length ? Mathf.Clamp01(outer[i]) : _inner[i];
                if (_outer[i] < _inner[i]) _outer[i] = _inner[i];
            }

            SetVerticesDirty();
        }

        public void EnsureLabels(string[] names)
        {
            if (_labels != null && _labels.Length == names.Length) return;
            _labels = new Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var t = BrothersUiUtil.MakeText(transform, "Axis" + i, names[i], 18, Vector2.zero, new Vector2(48, 28));
                t.color = new Color(0.92f, 0.88f, 0.78f, 0.95f);
                _labels[i] = t;
            }

            LayoutLabels();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            LayoutLabels();
        }

        void LayoutLabels()
        {
            if (_labels == null) return;
            var r = rectTransform.rect;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.42f;
            float labelR = radius + 22f;
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] == null) continue;
                _labels[i].rectTransform.anchoredPosition = c + Dir(i) * labelR;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.42f;
            int n = _inner.Length;

            for (int ring = 1; ring <= 3; ring++)
                DrawPolyLine(vh, c, radius * (ring / 3f), n, 1f, 1f, Grid);
            for (int i = 0; i < n; i++)
                AddLine(vh, c, c + Dir(i) * radius, 1.2f, Axis);

            DrawFill(vh, c, radius, _outer, OuterFill);
            DrawPolyLine(vh, c, radius, n, 1f, 0f, OuterLine, _outer);
            DrawFill(vh, c, radius, _inner, InnerFill);
            DrawPolyLine(vh, c, radius, n, 1f, 0f, InnerLine, _inner);
        }

        public static Vector2 AxisDir(int i)
        {
            float ang = (90f - i * 72f) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }

        public Vector2 VertexLocal(int i, float extra)
        {
            var r = rectTransform.rect;
            float radius = Mathf.Min(r.width, r.height) * 0.42f + extra;
            return r.center + AxisDir(i) * radius;
        }

        static Vector2 Dir(int i) => AxisDir(i);

        static Vector2 Pt(Vector2 c, float radius, int i, float t) => c + Dir(i) * (radius * t);

        static void DrawFill(VertexHelper vh, Vector2 c, float radius, float[] t, Color col)
        {
            int n = t.Length;
            int start = vh.currentVertCount;
            AddVert(vh, c, col);
            for (int i = 0; i < n; i++)
                AddVert(vh, Pt(c, radius, i, t[i]), col);
            for (int i = 0; i < n; i++)
            {
                int a = start;
                int b = start + 1 + i;
                int d = start + 1 + (i + 1) % n;
                vh.AddTriangle(a, b, d);
            }
        }

        static void DrawPolyLine(VertexHelper vh, Vector2 c, float radius, int n, float width, float tAll, Color col, float[] t = null)
        {
            for (int i = 0; i < n; i++)
            {
                float ta = t != null ? t[i] : tAll;
                float tb = t != null ? t[(i + 1) % n] : tAll;
                AddLine(vh, Pt(c, radius, i, ta), Pt(c, radius, (i + 1) % n, tb), width, col);
            }
        }

        static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float w, Color col)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 0.0001f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
            AddQuad(vh, a + n, b + n, b - n, a - n, col);
        }

        static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = vh.currentVertCount;
            AddVert(vh, a, col);
            AddVert(vh, b, col);
            AddVert(vh, c, col);
            AddVert(vh, d, col);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void AddVert(VertexHelper vh, Vector2 p, Color col)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = col;
            v.uv0 = Vector2.zero;
            vh.AddVert(v);
        }
    }
}
