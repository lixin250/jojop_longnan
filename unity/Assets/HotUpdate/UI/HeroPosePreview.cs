using JojoP.Gameplay.Brothers;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.UI
{
    /// <summary>选角预览：idle / atk 轮换，待机轻微浮动。</summary>
    public sealed class HeroPosePreview : MonoBehaviour
    {
        const float Cycle = 1.35f;

        Image _image;
        BattlePoseSet _set;
        float _baseY;
        bool _hasBase;
        bool _locked;

        public void Bind(Image image)
        {
            _image = image;
            _hasBase = false;
        }

        public void Show(BattlePoseSet set, bool locked)
        {
            _set = set;
            _locked = locked;
            Apply(false, 0f);
        }

        void LateUpdate()
        {
            if (_image == null) return;
            if (!_hasBase)
            {
                _baseY = _image.rectTransform.anchoredPosition.y;
                _hasBase = true;
            }

            bool atk = Mathf.Repeat(Time.unscaledTime, Cycle * 2f) >= Cycle;
            Apply(atk, Time.unscaledTime);

            var rt = _image.rectTransform;
            var p = rt.anchoredPosition;
            p.y = _baseY + Mathf.Sin(Time.unscaledTime * 3.1f) * 6f;
            rt.anchoredPosition = p;
        }

        void Apply(bool atk, float t)
        {
            if (_image == null) return;
            var clip = atk ? _set.Atk : _set.Idle;
            var sp = clip.Sample(t, true);
            if (sp == null) sp = _set.Fallback;
            if (sp != null)
            {
                _image.sprite = sp;
                _image.preserveAspect = true;
                _image.color = _locked ? new Color(0.45f, 0.45f, 0.5f, 1f) : Color.white;
            }
            else
            {
                _image.sprite = null;
                _image.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            }
        }
    }
}
