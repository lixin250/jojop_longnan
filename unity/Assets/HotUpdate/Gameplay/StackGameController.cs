using System;
using System.Collections.Generic;
using UnityEngine;

namespace JojoP.Gameplay
{
    /// <summary>
    /// 超休闲 Stack：左右晃动的块，点击落下；对不齐就失败。
    /// Hyper-casual stack: tap to drop; miss = fail.
    /// </summary>
    public sealed class StackGameController : MonoBehaviour
    {
        public event Action<int> ScoreChanged;
        public event Action<int> RoundFailed;

        Camera _cam;
        readonly List<Transform> _stack = new List<Transform>();
        Transform _moving;
        float _blockHeight = 0.45f;
        float _moveSpeed = 2.4f;
        float _speedRamp = 0.04f;
        float _minOverlap = 0.18f;
        int _dir = 1;
        int _score;
        bool _playing;
        bool _revivedThisRound; // 每局只能复活一次
        Material _matA;
        Material _matB;

        public int Score => _score;
        public bool IsPlaying => _playing;
        public bool CanRevive => !_revivedThisRound;

        public void Configure(float moveSpeed, float speedRamp, float minOverlap)
        {
            _moveSpeed = moveSpeed;
            _speedRamp = speedRamp;
            _minOverlap = minOverlap;
        }

        public void Bootstrap(Camera cam)
        {
            _cam = cam;
            _matA = CreateColorMat(new Color(0.95f, 0.45f, 0.35f));
            _matB = CreateColorMat(new Color(0.35f, 0.75f, 0.95f));
            EnsureLights();
        }

        public void StartRound()
        {
            ClearStack();
            _score = 0;
            _revivedThisRound = false;
            _playing = true;
            ScoreChanged?.Invoke(_score);

            // 底座
            _stack.Add(CreateBlock(Vector3.zero, 3.2f, true));
            SpawnMoving();
        }

        public void HandleTap()
        {
            if (!_playing || _moving == null) return;
            DropCurrent();
        }

        public bool TryRevive()
        {
            if (_revivedThisRound || _playing) return false;

            _revivedThisRound = true;
            _playing = true;
            SpawnMoving();
            return true;
        }

        public void ApplyDoubleScore()
        {
            _score *= 2;
            ScoreChanged?.Invoke(_score);
        }

        void Update()
        {
            if (!_playing || _moving == null) return;

            float speed = _moveSpeed + _score * _speedRamp;
            var pos = _moving.position;
            pos.x += _dir * speed * Time.deltaTime;

            const float maxX = 2.8f;
            if (pos.x > maxX) { pos.x = maxX; _dir = -1; }
            else if (pos.x < -maxX) { pos.x = -maxX; _dir = 1; }

            _moving.position = pos;
        }

        void DropCurrent()
        {
            var prev = _stack[_stack.Count - 1];
            float prevHalf = prev.localScale.x * 0.5f;
            float curHalf = _moving.localScale.x * 0.5f;
            float left = Mathf.Max(prev.position.x - prevHalf, _moving.position.x - curHalf);
            float right = Mathf.Min(prev.position.x + prevHalf, _moving.position.x + curHalf);
            float overlap = right - left;

            // 重叠太少 = 失败
            if (overlap <= _moving.localScale.x * _minOverlap)
            {
                Destroy(_moving.gameObject);
                _moving = null;
                _playing = false;
                RoundFailed?.Invoke(_score);
                return;
            }

            float newWidth = overlap;
            float newX = (left + right) * 0.5f;
            var settled = _moving;
            settled.position = new Vector3(newX, settled.position.y, 0f);
            settled.localScale = new Vector3(newWidth, _blockHeight, 1.2f);
            _stack.Add(settled);
            _moving = null;

            _score++;
            ScoreChanged?.Invoke(_score);
            SpawnMoving();
            FollowCamera();
        }

        void SpawnMoving()
        {
            var prev = _stack[_stack.Count - 1];
            float y = prev.position.y + _blockHeight;
            _dir = (_score % 2 == 0) ? 1 : -1;
            float startX = _dir > 0 ? -2.6f : 2.6f;
            _moving = CreateBlock(new Vector3(startX, y, 0f), prev.localScale.x, _score % 2 == 0);
        }

        Transform CreateBlock(Vector3 pos, float width, bool useA)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Block";
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(width, _blockHeight, 1.2f);
            go.GetComponent<Renderer>().sharedMaterial = useA ? _matA : _matB;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go.transform;
        }

        void FollowCamera()
        {
            if (_cam == null || _stack.Count == 0) return;
            float topY = _stack[_stack.Count - 1].position.y;
            var target = new Vector3(0f, Mathf.Max(0f, topY - 1.5f), -10f);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, target, 0.35f);
        }

        void ClearStack()
        {
            if (_moving != null)
            {
                Destroy(_moving.gameObject);
                _moving = null;
            }

            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i] != null) Destroy(_stack[i].gameObject);
            }

            _stack.Clear();
            if (_cam != null)
                _cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        void EnsureLights()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static Material CreateColorMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}
