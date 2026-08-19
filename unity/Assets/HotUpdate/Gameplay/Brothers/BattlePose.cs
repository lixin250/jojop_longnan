using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    public enum BattlePoseKind
    {
        Idle,
        Walk,
        Attack,
        Hurt,
        Dead
    }

    public struct BattlePoseSet
    {
        public Sprite Fallback;
        public Sprite Idle;
        public Sprite Walk;
        public Sprite Atk;
        public Sprite Hurt;
        public Sprite Dead;

        public bool HasAny => Fallback != null || Idle != null || Walk != null || Atk != null;

        public Sprite Resolve(BattlePoseKind pose)
        {
            Sprite pick = pose switch
            {
                BattlePoseKind.Idle => Idle,
                BattlePoseKind.Walk => Walk,
                BattlePoseKind.Attack => Atk,
                BattlePoseKind.Hurt => Hurt,
                BattlePoseKind.Dead => Dead,
                _ => Fallback
            };
            return pick != null ? pick : Fallback;
        }
    }

    /// <summary>
    /// 2D 战场切姿：按移动/出手/受击换帧。不做 Spine；待机只做轻微上下浮动。
    /// </summary>
    public sealed class BattlePoseDriver : MonoBehaviour
    {
        const float AttackHold = 0.28f;
        const float HurtHold = 0.22f;

        BattleUnit _unit;
        SpriteRenderer _sr;
        Transform _spriteXf;
        BattlePoseSet _set;
        Vector3 _baseLocal;
        float _atkLeft;
        float _hurtLeft;
        BattlePoseKind _pose;

        public void Bind(BattleUnit unit, SpriteRenderer sr, BattlePoseSet set)
        {
            _unit = unit;
            _sr = sr;
            _spriteXf = sr != null ? sr.transform : null;
            _set = set;
            _baseLocal = _spriteXf != null ? _spriteXf.localPosition : Vector3.zero;
            Apply(BattlePoseKind.Idle);
        }

        public void NotifyAttack() => _atkLeft = AttackHold;

        public void NotifyHurt() => _hurtLeft = HurtHold;

        void LateUpdate()
        {
            if (_sr == null || _unit == null) return;

            float dt = Time.deltaTime;
            if (_atkLeft > 0f) _atkLeft -= dt;
            if (_hurtLeft > 0f) _hurtLeft -= dt;

            BattlePoseKind next;
            if (!_unit.IsAlive) next = BattlePoseKind.Dead;
            else if (_hurtLeft > 0f) next = BattlePoseKind.Hurt;
            else if (_atkLeft > 0f) next = BattlePoseKind.Attack;
            else if (_unit.IsMoving) next = BattlePoseKind.Walk;
            else next = BattlePoseKind.Idle;

            if (next != _pose)
                Apply(next);

            if (_spriteXf == null) return;

            Vector3 local = _baseLocal;
            if (_pose == BattlePoseKind.Idle)
                local.y += Mathf.Sin(Time.time * 3.2f) * 0.035f;
            _spriteXf.localPosition = local;

            float faceX = _unit.FaceDir.x;
            if (Mathf.Abs(faceX) > 0.08f)
                _sr.flipX = faceX < 0f;
        }

        void Apply(BattlePoseKind pose)
        {
            _pose = pose;
            var sprite = _set.Resolve(pose);
            if (sprite != null)
                _sr.sprite = sprite;
        }
    }
}
