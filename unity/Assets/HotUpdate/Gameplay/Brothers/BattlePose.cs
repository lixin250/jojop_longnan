using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    public enum BattlePoseKind
    {
        Idle,
        Walk,
        Attack,
        Skill,
        Hurt,
        Dead
    }

    public readonly struct BattleClip
    {
        public readonly Sprite[] Frames;
        public readonly float Fps;

        public BattleClip(Sprite[] frames, float fps)
        {
            Frames = frames;
            Fps = fps;
        }

        public static BattleClip Of(Sprite sprite, float fps = 0f)
        {
            return sprite == null
                ? default
                : new BattleClip(new[] { sprite }, fps);
        }

        public int Count => Frames == null ? 0 : Frames.Length;
        public bool HasMany => Count > 1;
        public Sprite First => Count > 0 ? Frames[0] : null;

        public Sprite Sample(float t, bool loop)
        {
            if (Count == 0) return null;
            if (Count == 1) return Frames[0];
            float fps = Fps > 0.05f ? Fps : 6f;
            int i = Mathf.FloorToInt(Mathf.Max(0f, t) * fps);
            if (loop)
            {
                i %= Count;
                if (i < 0) i += Count;
            }
            else if (i >= Count)
                i = Count - 1;
            return Frames[i];
        }

        public float Duration
        {
            get
            {
                if (Count <= 1) return 0f;
                float fps = Fps > 0.05f ? Fps : 6f;
                return Count / fps;
            }
        }
    }

    public struct BattlePoseSet
    {
        public Sprite Fallback;
        public BattleClip Idle;
        public BattleClip Walk;
        public BattleClip Atk;
        public BattleClip Skill;
        public BattleClip Hurt;
        public BattleClip Dead;

        public bool HasAny =>
            Fallback != null || Idle.Count > 0 || Walk.Count > 0 || Atk.Count > 0;

        public static BattlePoseSet FromSingle(Sprite sprite)
        {
            var clip = BattleClip.Of(sprite);
            return new BattlePoseSet
            {
                Fallback = sprite,
                Idle = clip,
                Walk = clip,
                Atk = clip,
                Skill = clip,
                Hurt = clip,
                Dead = clip
            };
        }

        public BattleClip Clip(BattlePoseKind pose)
        {
            return pose switch
            {
                BattlePoseKind.Idle => Idle,
                BattlePoseKind.Walk => Walk,
                BattlePoseKind.Attack => Atk,
                BattlePoseKind.Skill => Skill.Count > 0 ? Skill : Atk,
                BattlePoseKind.Hurt => Hurt,
                BattlePoseKind.Dead => Dead,
                _ => default
            };
        }

        public Sprite Resolve(BattlePoseKind pose)
        {
            var sp = Clip(pose).First;
            return sp != null ? sp : Fallback;
        }
    }

    /// <summary>
    /// 2D 战场切帧：idle_1 / walk_2 / atk_1。有多帧就播序列；单帧待机才轻微上下浮动。
    /// </summary>
    public sealed class BattlePoseDriver : MonoBehaviour
    {
        const float AttackHold = 0.28f;
        const float SkillHold = 0.32f;
        const float HurtHold = 0.22f;

        BattleUnit _unit;
        SpriteRenderer _sr;
        Transform _spriteXf;
        BattlePoseSet _set;
        Vector3 _baseLocal;
        float _atkLeft;
        float _skillLeft;
        float _hurtLeft;
        float _poseTime;
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

        public void NotifyAttack()
        {
            float hold = _set.Atk.Duration;
            _atkLeft = hold > AttackHold ? hold : AttackHold;
        }

        public void NotifySkill()
        {
            var clip = _set.Skill.Count > 0 ? _set.Skill : _set.Atk;
            float hold = clip.Duration;
            _skillLeft = hold > SkillHold ? hold : SkillHold;
        }

        public void NotifyHurt()
        {
            float hold = _set.Hurt.Duration;
            _hurtLeft = hold > HurtHold ? hold : HurtHold;
        }

        void LateUpdate()
        {
            if (_sr == null || _unit == null) return;

            float dt = Time.deltaTime;
            if (_atkLeft > 0f) _atkLeft -= dt;
            if (_skillLeft > 0f) _skillLeft -= dt;
            if (_hurtLeft > 0f) _hurtLeft -= dt;

            BattlePoseKind next;
            if (!_unit.IsAlive) next = BattlePoseKind.Dead;
            else if (_hurtLeft > 0f) next = BattlePoseKind.Hurt;
            else if (_skillLeft > 0f) next = BattlePoseKind.Skill;
            else if (_atkLeft > 0f) next = BattlePoseKind.Attack;
            else if (_unit.IsMoving) next = BattlePoseKind.Walk;
            else next = BattlePoseKind.Idle;

            if (next != _pose)
                Apply(next);
            else
                _poseTime += dt;

            ShowFrame();

            if (_spriteXf == null) return;

            Vector3 local = _baseLocal;
            if (_pose == BattlePoseKind.Idle && !_set.Idle.HasMany)
                local.y += Mathf.Sin(Time.time * 3.2f) * 0.035f;
            _spriteXf.localPosition = local;

            float faceX = _unit.FaceDir.x;
            if (Mathf.Abs(faceX) > 0.08f)
                _sr.flipX = faceX < 0f;
        }

        void Apply(BattlePoseKind pose)
        {
            _pose = pose;
            _poseTime = 0f;
            ShowFrame();
        }

        void ShowFrame()
        {
            var clip = _set.Clip(_pose);
            bool loop = _pose == BattlePoseKind.Idle || _pose == BattlePoseKind.Walk;
            var sprite = clip.Sample(_poseTime, loop) ?? _set.Fallback;
            if (sprite != null)
                _sr.sprite = sprite;
        }
    }
}
