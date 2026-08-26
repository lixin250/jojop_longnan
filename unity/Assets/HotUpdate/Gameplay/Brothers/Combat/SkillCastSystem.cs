using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;
using UnityEngine;
using Random = UnityEngine.Random;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>数据驱动施法：读 TbSkillIndex / TbSkillEffect，不写死每人脚本。</summary>
    public sealed class SkillCastSystem
    {
        readonly List<BattleUnit> _brothers;
        readonly List<BattleUnit> _enemies;
        readonly System.Action<string, Vector3> _onSummon;

        public SkillCastSystem(List<BattleUnit> brothers, List<BattleUnit> enemies, System.Action<string, Vector3> onSummon)
        {
            _brothers = brothers;
            _enemies = enemies;
            _onSummon = onSummon;
        }

        public void EquipFromRole(BattleUnit unit, BrotherRuntime br)
        {
            unit.Skills.Clear();
            if (!CfgTables.Ready || br?.SkillIds == null) return;

            foreach (var id in br.SkillIds)
            {
                var sk = CfgTables.Tables.TbSkillIndex.GetOrDefault(id);
                if (sk == null) continue;

                bool passive = sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Passive);
                // 校园技始终可用；就业技必须等学历/路线配置判定毕业后解锁
                bool isJob = sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Job);
                bool isFusion = sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Fusion);
                if (isJob && !br.JobSkillUnlocked) continue;
                if (isFusion) continue; // 由 FusionSystem 临时塞入

                unit.Skills.Add(new SkillSlot
                {
                    SkillId = id,
                    CdLeft = passive ? 0f : Random.Range(0f, Mathf.Min(2f, sk.Cd * 0.3f)),
                    IsPassive = passive
                });
            }

            if (!string.IsNullOrEmpty(br.LootSkillId))
            {
                var loot = CfgTables.Tables.TbSkillIndex.GetOrDefault(br.LootSkillId);
                if (loot != null)
                {
                    unit.Skills.Add(new SkillSlot
                    {
                        SkillId = loot.Id,
                        CdLeft = Random.Range(0f, Mathf.Min(1.5f, loot.Cd * 0.25f)),
                        IsPassive = loot.ShowTags != null && loot.ShowTags.Contains(ESkillShowTag.Passive)
                    });
                }
            }
        }

        public void GrantSkill(BattleUnit unit, string skillId)
        {
            if (unit == null || string.IsNullOrEmpty(skillId)) return;
            foreach (var s in unit.Skills)
                if (s.SkillId == skillId) return;

            var sk = CfgTables.Ready ? CfgTables.Tables.TbSkillIndex.GetOrDefault(skillId) : null;
            unit.Skills.Add(new SkillSlot
            {
                SkillId = skillId,
                CdLeft = 1f,
                IsPassive = sk != null && sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Passive)
            });
        }

        public void Tick(BattleUnit caster, float dt)
        {
            if (caster == null || !caster.IsAlive || !CfgTables.Ready) return;

            TickBuffs(caster, dt);

            for (int i = 0; i < caster.Skills.Count; i++)
            {
                var slot = caster.Skills[i];
                var sk = CfgTables.Tables.TbSkillIndex.GetOrDefault(slot.SkillId);
                if (sk == null) continue;

                if (slot.IsPassive)
                {
                    if (!slot.PassiveApplied)
                    {
                        Cast(caster, sk, null);
                        slot.PassiveApplied = true;
                    }

                    continue;
                }

                slot.CdLeft -= dt;
                if (slot.CdLeft > 0f) continue;

                var focus = FindFocus(caster);
                if (NeedsEnemy(sk) && (focus == null || !focus.IsAlive)) continue;

                Cast(caster, sk, focus);
                float cd = sk.Cd;
                if (caster.BoundBrother != null && caster.BoundBrother.JobSkillLv > 0 &&
                    sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Job))
                {
                    cd *= Mathf.Max(0.55f, 1f - 0.06f * caster.BoundBrother.JobSkillLv);
                    cd *= caster.BoundBrother.CareerSkillCdMul;
                }
                slot.CdLeft = Mathf.Max(0.35f, cd);
            }
        }

        bool NeedsEnemy(SkillIndex sk)
        {
            if (sk.EffectIds == null) return false;
            foreach (var eid in sk.EffectIds)
            {
                var e = CfgTables.Tables.TbSkillEffect.GetOrDefault(eid);
                if (e == null) continue;
                if (e.Target == EEffectTarget.Enemy || e.Target == EEffectTarget.EnemyAoe)
                    return true;
            }

            return false;
        }

        BattleUnit FindFocus(BattleUnit caster)
        {
            var pool = caster.Side == UnitSide.Brother ? _enemies : _brothers;
            BattleUnit best = null;
            float bestSq = float.MaxValue;
            var p = caster.transform.position;
            for (int i = 0; i < pool.Count; i++)
            {
                var u = pool[i];
                if (u == null || !u.IsAlive) continue;
                // 精英优先一点点
                float sq = (u.transform.position - p).sqrMagnitude;
                if (u.IsElite || u.HighArmor) sq *= 0.75f;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = u;
                }
            }

            return best;
        }

        public void Cast(BattleUnit caster, SkillIndex sk, BattleUnit focus)
        {
            if (sk?.EffectIds == null) return;
            float lvMul = 1f;
            if (caster.BoundBrother != null && sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Job))
                lvMul = caster.BoundBrother.GraduationSkillMul *
                        (1f + 0.12f * caster.BoundBrother.JobSkillLv);

            caster.NotifySkill();
            foreach (var eid in sk.EffectIds)
            {
                var fx = CfgTables.Tables.TbSkillEffect.GetOrDefault(eid);
                if (fx == null) continue;
                PlayVfx(caster, focus, fx);
                ApplyEffect(caster, focus, fx, lvMul);
            }
        }

        static void PlayVfx(BattleUnit caster, BattleUnit focus, SkillEffect fx)
        {
            if (caster == null || string.IsNullOrEmpty(fx.VfxKey)) return;
            Vector3 from = caster.transform.position + Vector3.up * 0.55f;
            BattleUnit look = fx.Target == EEffectTarget.Self || focus == null || !focus.IsAlive
                ? caster
                : focus;
            Vector3 to = look.transform.position + Vector3.up * 0.4f;
            bool follow = fx.Kind == ESkillEffectKind.AddBuff;
            float life = follow ? Mathf.Max(0.9f, fx.Duration + 0.15f) : 0.65f;
            SkillVfx.Play(fx.VfxKey, from, to, follow ? caster.transform : null, life);
        }

        void ApplyEffect(BattleUnit caster, BattleUnit focus, SkillEffect fx, float lvMul)
        {
            switch (fx.Kind)
            {
                case ESkillEffectKind.InstantDamage:
                    ApplyDamage(caster, focus, fx, lvMul);
                    break;
                case ESkillEffectKind.Heal:
                    foreach (var t in ResolveTargets(caster, focus, fx.Target, fx.Radius))
                        t.Heal((fx.Value > 0f ? fx.Value : 10f) * lvMul);
                    break;
                case ESkillEffectKind.Shield:
                    foreach (var t in ResolveTargets(caster, focus, fx.Target, fx.Radius))
                        t.Shield += (fx.Value > 0f ? fx.Value : 8f) * lvMul;
                    break;
                case ESkillEffectKind.AddBuff:
                    foreach (var t in ResolveTargets(caster, focus, fx.Target, fx.Radius))
                        AddBuff(t, fx);
                    break;
                case ESkillEffectKind.Summon:
                    if (!string.IsNullOrEmpty(fx.SummonRoleId))
                        _onSummon?.Invoke(fx.SummonRoleId, caster.transform.position + Vector3.right * 0.6f);
                    break;
            }
        }

        void ApplyDamage(BattleUnit caster, BattleUnit focus, SkillEffect fx, float lvMul)
        {
            float amount;
            if (fx.Ratio > 0f && fx.Target != EEffectTarget.Self)
                amount = caster.Atk * caster.AtkMul * fx.Ratio * lvMul;
            else
                amount = fx.Value * lvMul;

            if (fx.Target == EEffectTarget.Self)
            {
                caster.ApplyDamage(Mathf.Max(0f, amount));
                return;
            }

            foreach (var t in ResolveTargets(caster, focus, fx.Target, fx.Radius))
            {
                float dmg = amount;
                if ((t.HighArmor || t.IsElite) && caster.MechBonusVsArmor > 0f)
                    dmg *= 1f + caster.MechBonusVsArmor;
                // 解体位：ratio==1 且非精英时小概率秒
                if (fx.Ratio <= 1.01f && fx.Value <= 0f && !t.IsElite && !t.HighArmor && Random.value < 0.12f)
                    dmg = t.Hp + 1f;
                t.ApplyDamage(dmg);
            }
        }

        void AddBuff(BattleUnit t, SkillEffect fx)
        {
            // value>0 → 攻；value<0 → 移速/当作减速（MoveMul）
            t.Buffs.Add(new RuntimeBuff
            {
                BuffId = fx.BuffId,
                Value = fx.Value,
                Remain = fx.Duration,
                Permanent = fx.Duration <= 0f
            });
            RecomputeBuffs(t);
        }

        static void TickBuffs(BattleUnit u, float dt)
        {
            bool dirty = false;
            for (int i = u.Buffs.Count - 1; i >= 0; i--)
            {
                var b = u.Buffs[i];
                if (b.Permanent) continue;
                b.Remain -= dt;
                if (b.Remain > 0f) continue;
                u.Buffs.RemoveAt(i);
                dirty = true;
            }

            if (dirty) RecomputeBuffs(u);
        }

        static void RecomputeBuffs(BattleUnit u)
        {
            float atk = 1f;
            float move = 1f;
            float taken = u.BaseDamageTakenMul;
            foreach (var b in u.Buffs)
            {
                if (b.Value >= 0f) atk += b.Value;
                else move *= 1f + b.Value; // -0.35 → 0.65 move
            }

            u.AtkMul = u.BaseAtkMul * atk;
            u.MoveMul = Mathf.Clamp(move, 0.35f, 2.2f);
            u.DamageTakenMul = taken;
        }

        List<BattleUnit> ResolveTargets(BattleUnit caster, BattleUnit focus, EEffectTarget target, float radius)
        {
            var result = new List<BattleUnit>(4);
            switch (target)
            {
                case EEffectTarget.Self:
                    result.Add(caster);
                    break;
                case EEffectTarget.Enemy:
                    if (focus != null && focus.IsAlive) result.Add(focus);
                    break;
                case EEffectTarget.Ally:
                {
                    var ally = LowestAlly(caster);
                    if (ally != null) result.Add(ally);
                    break;
                }
                case EEffectTarget.EnemyAoe:
                    CollectInRadius(caster.Side == UnitSide.Brother ? _enemies : _brothers,
                        focus != null ? focus.transform.position : caster.transform.position,
                        radius > 0f ? radius : 1.5f, result);
                    break;
                case EEffectTarget.AllyAoe:
                    CollectInRadius(caster.Side == UnitSide.Brother ? _brothers : _enemies,
                        caster.transform.position, radius > 0f ? radius : 2.5f, result);
                    break;
            }

            return result;
        }

        BattleUnit LowestAlly(BattleUnit caster)
        {
            var pool = caster.Side == UnitSide.Brother ? _brothers : _enemies;
            BattleUnit best = null;
            foreach (var u in pool)
            {
                if (u == null || !u.IsAlive) continue;
                if (best == null || u.Hp / u.MaxHp < best.Hp / best.MaxHp) best = u;
            }

            return best ?? caster;
        }

        static void CollectInRadius(List<BattleUnit> pool, Vector3 center, float radius, List<BattleUnit> into)
        {
            float r2 = radius * radius;
            for (int i = 0; i < pool.Count; i++)
            {
                var u = pool[i];
                if (u == null || !u.IsAlive) continue;
                if ((u.transform.position - center).sqrMagnitude <= r2)
                    into.Add(u);
            }
        }
    }
}
