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
        SpriteRenderer _sprite;
        Color _baseColor;

        public bool IsAlive => Hp > 0f && gameObject.activeInHierarchy;

        public void SetupVisual(Color color, float scale)
        {
            transform.localScale = Vector3.one * scale;
            _renderer = GetComponent<Renderer>();
            _sprite = GetComponent<SpriteRenderer>();
            _baseColor = color;
            ApplyColor(color);
        }

        /// <summary>立绘世界空间顶部（血条 / 飘字用）。</summary>
        public float WorldTopY()
        {
            var sr = _sprite != null ? _sprite : GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.enabled && sr.sprite != null)
                return sr.bounds.max.y;
            var r = _renderer != null ? _renderer : GetComponent<Renderer>();
            if (r != null && r.enabled)
                return r.bounds.max.y;
            return transform.position.y + 0.65f;
        }

        /// <summary>挂战斗立绘（可选）；失败则保持色块。Sprite 挂子物体，避免和胶囊 MeshRenderer 互斥。</summary>
        public void TryApplyBattleSprite(Sprite sprite)
        {
            if (sprite == null) return;

            var mesh = GetComponent<MeshRenderer>();
            if (mesh != null)
            {
                mesh.enabled = false;
                Destroy(mesh);
            }
            var filter = GetComponent<MeshFilter>();
            if (filter != null)
            {
                filter.sharedMesh = null;
                Destroy(filter);
            }

            var child = transform.Find("BattleSprite");
            if (child == null)
            {
                var go = new GameObject("BattleSprite");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                go.transform.localRotation = Quaternion.identity;
                child = go.transform;
            }

            _sprite = child.GetComponent<SpriteRenderer>();
            if (_sprite == null) _sprite = child.gameObject.AddComponent<SpriteRenderer>();
            if (_sprite == null) return;

            _sprite.sprite = sprite;
            _sprite.sortingOrder = Side == UnitSide.Brother ? 20 : 10;
            // 缩放只打在立绘子物体上，血条不跟着被拉歪。
            transform.localScale = Vector3.one;
            float h = sprite.bounds.size.y;
            float target = Side == UnitSide.Brother ? 2.35f : 1.55f;
            float s = h > 0.01f ? target / h : 1f;
            child.localScale = Vector3.one * s;
            _renderer = _sprite;
            _baseColor = Color.white;
            ApplyColor(Color.white);
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
                    transform.position = BattleField.ClampToArena(
                        transform.position + to.normalized * (Move * MoveMul * dt));
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

        public float ApplyDamage(float raw, bool showFloater = true)
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
            if (showFloater && dmg > 0.05f)
                BattleFeedback.Damage(this, dmg);
            return dmg;
        }

        public void Heal(float amount, bool showFloater = true)
        {
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            float gained = Hp - before;
            if (showFloater && gained > 0.05f)
                BattleFeedback.Heal(this, gained);
        }

        void Flash()
        {
            ApplyColor(Color.white);
            CancelInvoke(nameof(RestoreColor));
            Invoke(nameof(RestoreColor), 0.08f);
        }

        void RestoreColor() => ApplyColor(_baseColor);

        void ApplyColor(Color c)
        {
            if (_sprite != null)
            {
                _sprite.color = c;
                return;
            }

            if (_renderer == null) return;
            if (_renderer.material.HasProperty("_Color"))
                _renderer.material.color = c;
            else if (_renderer.material.HasProperty("_BaseColor"))
                _renderer.material.SetColor("_BaseColor", c);
        }
    }
}
