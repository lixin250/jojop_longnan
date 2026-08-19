using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>竖屏战场导播：正交远近 + 轻跟随小队中心。</summary>
    public sealed class BattleCamera : MonoBehaviour
    {
        public const float SizeMin = 4.2f;
        public const float SizeMax = 8.4f;
        public const float SizeDefault = 6.2f;

        Camera _cam;
        float _size = SizeDefault;
        Vector3 _shake;
        float _shakeLeft;
        Transform _follow;

        public static BattleCamera Ensure(Camera cam)
        {
            if (cam == null) return null;
            var dir = cam.GetComponent<BattleCamera>();
            if (dir == null) dir = cam.gameObject.AddComponent<BattleCamera>();
            dir._cam = cam;
            dir._size = cam.orthographicSize;
            return dir;
        }

        public void SetFollow(Transform t) => _follow = t;

        public void ZoomBy(float delta)
        {
            _size = Mathf.Clamp(_size + delta, SizeMin, SizeMax);
        }

        public void Shake(float amp = 0.12f, float dur = 0.12f)
        {
            _shake = Random.insideUnitCircle * amp;
            _shakeLeft = dur;
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            Vector3 pos = new Vector3(0f, 0f, -10f);
            if (_follow != null)
            {
                var p = _follow.position;
                pos.x = Mathf.Clamp(p.x * 0.22f, -0.8f, 0.8f);
                pos.y = Mathf.Clamp(p.y * 0.22f, -1.1f, 1.1f);
            }

            if (_shakeLeft > 0f)
            {
                _shakeLeft -= Time.unscaledDeltaTime;
                pos.x += _shake.x;
                pos.y += _shake.y;
                _shake *= 0.72f;
            }

            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _size, Time.unscaledDeltaTime * 8f);
            _cam.transform.position = pos;
        }
    }

    /// <summary>受击顿帧 + 占位音效（无资源时程序生成短 beep）。</summary>
    public static class BattleFeel
    {
        static AudioSource _src;
        static AudioClip _hit;
        static float _stopUntil;
        static float _lastHit;

        public static void Hit(BattleUnit target)
        {
            if (Time.unscaledTime - _lastHit < 0.07f) return;
            _lastHit = Time.unscaledTime;
            BattleCamera.Ensure(Camera.main)?.Shake(
                target != null && target.Side == UnitSide.Brother ? 0.18f : 0.1f,
                0.1f);
            _stopUntil = Time.unscaledTime + 0.04f;
            Time.timeScale = 0.22f;
            PlayHitSfx();
        }

        public static void Tick()
        {
            if (Time.unscaledTime >= _stopUntil && Time.timeScale < 0.99f)
                Time.timeScale = 1f;
        }

        public static void Reset()
        {
            Time.timeScale = 1f;
            _stopUntil = 0f;
        }

        static void PlayHitSfx()
        {
            if (_src == null)
            {
                var go = GameObject.Find("BattleSfx");
                if (go == null)
                {
                    go = new GameObject("BattleSfx");
                    Object.DontDestroyOnLoad(go);
                }

                _src = go.GetComponent<AudioSource>();
                if (_src == null) _src = go.AddComponent<AudioSource>();
                _src.playOnAwake = false;
                _src.spatialBlend = 0f;
            }

            if (_hit == null)
                _hit = MakeBeep(880f, 0.04f);
            _src.pitch = Random.Range(0.92f, 1.08f);
            _src.PlayOneShot(_hit, 0.35f);
        }

        static AudioClip MakeBeep(float freq, float seconds)
        {
            int hz = 22050;
            int n = Mathf.CeilToInt(hz * seconds);
            var clip = AudioClip.Create("beep", n, 1, hz, false);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)hz;
                float env = 1f - t / seconds;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
            }

            clip.SetData(data, 0);
            return clip;
        }
    }
}
