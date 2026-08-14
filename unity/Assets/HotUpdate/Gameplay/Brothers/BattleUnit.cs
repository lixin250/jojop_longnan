using System.Collections.Generic;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    public sealed class BattleUnit : MonoBehaviour
    {
        public UnitSide Side;
        public string ThemeId;
        public string DisplayName;
        public float MaxHp;
        public float Hp;
        public float Atk;
        public float Move;
        public float AttackRange = 0.55f;
        public float AttackCooldown = 0.55f;
        public float Defense;
        public float CritRate;
        public float CritDamage = 1.5f;
        public bool HighArmor;
        public bool IsElite;
        public BrotherRuntime BoundBrother;
        public float DamageTakenMul = 1f;
        public float BaseDamageTakenMul = 1f;
        public float AtkMul = 1f;
        public float BaseAtkMul = 1f;
        public float MoveMul = 1f;
        public float Shield;
        public float MechBonusVsArmor;
        public readonly List<SkillSlot> Skills = new List<SkillSlot>();
        public readonly List<RuntimeBuff> Buffs = new List<RuntimeBuff>();
        public float SummonLifeLeft; // >0 为召唤物，到期销毁

        float _cd;
        Renderer _renderer;
        Color _baseColor;

        public bool IsAlive => Hp > 0f && gameObject.activeInHierarchy;

        public void SetupVisual(Color color, float scale)
        {
            transform.localScale = Vector3.one * scale;
            _renderer = GetComponent<Renderer>();
            _baseColor = color;
            if (_renderer != null)
                ApplyColor(color);
        }

        public void TickCombat(float dt, BattleUnit target, System.Action<BattleUnit, BattleUnit, float> onHit)
        {
            if (!IsAlive || target == null || !target.IsAlive) return;

            Vector3 to = target.transform.position - transform.position;
            to.z = 0f;
            float dist = to.magnitude;
            if (dist > AttackRange)
            {
                if (dist > 0.01f)
                    transform.position += to.normalized * (Move * MoveMul * dt);
                return;
            }

            _cd -= dt;
            if (_cd > 0f) return;
            _cd = AttackCooldown;

            float dmg = Atk * AtkMul;
            if (Side == UnitSide.Brother && CritRate > 0f && Random.value < CritRate)
                dmg *= Mathf.Max(1f, CritDamage);
            if (Side == UnitSide.Brother && target.HighArmor)
                dmg *= 1f + MechBonusVsArmor;

            onHit?.Invoke(this, target, dmg);
        }

        public void ApplyDamage(float raw)
        {
            float defenseMul = 100f / (100f + Mathf.Max(0f, Defense));
            float dmg = raw * defenseMul * DamageTakenMul;
            if (Shield > 0f)
            {
                float absorb = Mathf.Min(Shield, dmg);
                Shield -= absorb;
                dmg -= absorb;
            }

            Hp -= dmg;
            if (Hp < 0f) Hp = 0f;
            Flash();
        }

        public void Heal(float amount)
        {
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        void Flash()
        {
            if (_renderer == null) return;
            ApplyColor(Color.white);
            CancelInvoke(nameof(RestoreColor));
            Invoke(nameof(RestoreColor), 0.08f);
        }

        void RestoreColor() => ApplyColor(_baseColor);

        void ApplyColor(Color c)
        {
            if (_renderer == null) return;
            if (_renderer.material.HasProperty("_Color"))
                _renderer.material.color = c;
            else if (_renderer.material.HasProperty("_BaseColor"))
                _renderer.material.SetColor("_BaseColor", c);
        }
    }
}
