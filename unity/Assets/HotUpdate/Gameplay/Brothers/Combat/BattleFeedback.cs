using UnityEngine;
using UnityEngine.UI;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>战斗反馈：伤害/治疗飘字 + 世界空间血条。</summary>
    public static class BattleFeedback
    {
        static Font _font;

        public static void EnsureOn(BattleUnit unit)
        {
            if (unit == null) return;
            if (unit.GetComponent<UnitHpBar>() == null)
                unit.gameObject.AddComponent<UnitHpBar>().Bind(unit);
        }

        public static void Damage(BattleUnit target, float amount, bool crit = false)
        {
            if (target == null || amount <= 0.05f) return;
            SpawnFloater(FloaterPos(target),
                Mathf.RoundToInt(amount).ToString(),
                crit ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.35f, 0.3f),
                crit ? 1.15f : 0.95f);
            target.GetComponent<UnitHpBar>()?.RefreshImmediate();
        }

        public static void Heal(BattleUnit target, float amount)
        {
            if (target == null || amount <= 0.05f) return;
            SpawnFloater(FloaterPos(target),
                "+" + Mathf.RoundToInt(amount),
                new Color(0.45f, 0.95f, 0.55f),
                1.0f);
            target.GetComponent<UnitHpBar>()?.RefreshImmediate();
        }

        static Vector3 FloaterPos(BattleUnit target)
        {
            return new Vector3(target.transform.position.x, target.WorldTopY() + 0.22f, 0f);
        }

        static void SpawnFloater(Vector3 world, string text, Color color, float scale)
        {
            var go = new GameObject("DmgFloat");
            go.transform.position = world;
            var floater = go.AddComponent<DamageFloater>();
            floater.Play(text, color, scale, BuiltinFont());
        }

        static Font BuiltinFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }

    public sealed class DamageFloater : MonoBehaviour
    {
        Text _text;
        float _life = 0.85f;
        Vector3 _vel;

        public void Play(string value, Color color, float scale, Font font)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 80;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 60);
            canvasGo.transform.localScale = Vector3.one * (0.018f * scale);

            var textGo = new GameObject("T", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGo.transform.SetParent(canvasGo.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _text = textGo.GetComponent<Text>();
            _text.font = font;
            _text.text = value;
            _text.fontSize = 42;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = color;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = textGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _vel = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(1.2f, 1.8f), 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            transform.position += _vel * dt;
            _vel.y -= 1.6f * dt;
            if (_text != null)
            {
                var c = _text.color;
                c.a = Mathf.Clamp01(_life / 0.35f);
                _text.color = c;
            }

            // 始终朝向相机（正交俯视时保持正向）
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;

            if (_life <= 0f)
                Destroy(gameObject);
        }
    }

    public sealed class UnitHpBar : MonoBehaviour
    {
        BattleUnit _unit;
        Image _fill;
        Image _back;
        Canvas _canvas;

        public void Bind(BattleUnit unit)
        {
            _unit = unit;
            Build();
            RefreshImmediate();
        }

        const float BarWidth = 120f;
        const float FillPad = 6f;

        void Build()
        {
            var root = new GameObject("HpBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 60;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BarWidth, 16f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            _back = MakeBar(root.transform, "Back", new Color(0.08f, 0.08f, 0.1f, 0.9f));
            _fill = MakeBar(root.transform, "Fill",
                _unit != null && _unit.Side == UnitSide.Brother
                    ? new Color(0.35f, 0.85f, 0.55f, 1f)
                    : new Color(0.95f, 0.35f, 0.32f, 1f));
            var frt = _fill.rectTransform;
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchorMin = new Vector2(0f, 0.18f);
            frt.anchorMax = new Vector2(0f, 0.82f);
            frt.anchoredPosition = new Vector2(FillPad, 0f);
            frt.sizeDelta = new Vector2(BarWidth - FillPad * 2f, 0f);
            PlaceAboveHead();
        }

        static Image MakeBar(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        void PlaceAboveHead()
        {
            if (_canvas == null) return;
            float sx = Mathf.Max(0.01f, transform.lossyScale.x);
            float sy = Mathf.Max(0.01f, transform.lossyScale.y);
            const float worldScale = 0.014f;
            _canvas.transform.localScale = new Vector3(worldScale / sx, worldScale / sy, 1f);
            float localTop = 0.7f;
            if (_unit != null)
                localTop = (_unit.WorldTopY() - transform.position.y) / sy + 0.18f;
            _canvas.transform.localPosition = new Vector3(0f, localTop, 0f);
            _canvas.transform.localRotation = Quaternion.identity;
        }

        public void RefreshImmediate()
        {
            if (_unit == null || _fill == null) return;
            float t = _unit.MaxHp > 0.01f ? Mathf.Clamp01(_unit.Hp / _unit.MaxHp) : 0f;
            _fill.rectTransform.sizeDelta = new Vector2((BarWidth - FillPad * 2f) * t, 0f);
            if (_unit.Side == UnitSide.Brother)
                _fill.color = Color.Lerp(new Color(0.95f, 0.55f, 0.2f), new Color(0.35f, 0.85f, 0.55f), t);
            else
                _fill.color = Color.Lerp(new Color(0.45f, 0.1f, 0.1f), new Color(0.95f, 0.35f, 0.32f), t);
        }

        void LateUpdate()
        {
            if (_unit == null) return;
            RefreshImmediate();
            PlaceAboveHead();
        }
    }
}
