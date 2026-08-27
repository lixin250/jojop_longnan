using System.Collections.Generic;
using JojoP.Cfg;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>团队攻击核心：只改变普攻命中分布，角色技能仍由 SkillCastSystem 负责。</summary>
    public sealed class AttackFormSystem
    {
        readonly List<BattleUnit> _enemies;
        readonly List<BattleUnit> _scratch = new List<BattleUnit>();
        readonly List<BattleUnit> _hit = new List<BattleUnit>();

        public AttackFormSystem(List<BattleUnit> enemies)
        {
            _enemies = enemies;
        }

        public void Apply(BattleUnit attacker, BattleUnit target, float rawDamage, Equipment equipment)
        {
            if (target == null || !target.IsAlive) return;
            if (equipment == null)
            {
                target.ApplyDamage(rawDamage);
                AttackTrace.Line(attacker.transform.position, target.transform.position, new Color(1f, 0.85f, 0.35f));
                return;
            }

            float mainDamage = rawDamage * Mathf.Max(0.05f, equipment.DamageMul);
            switch (equipment.AttackForm)
            {
                case EAttackForm.Pierce:
                    ApplyPierce(attacker, target, mainDamage, equipment);
                    break;
                case EAttackForm.Chain:
                    ApplyChain(attacker, target, mainDamage, equipment);
                    break;
                case EAttackForm.Splash:
                    ApplySplash(attacker, target, mainDamage, equipment);
                    break;
                default:
                    target.ApplyDamage(mainDamage);
                    AttackTrace.Line(attacker.transform.position, target.transform.position, new Color(1f, 0.85f, 0.35f));
                    break;
            }
        }

        void ApplyPierce(BattleUnit attacker, BattleUnit target, float mainDamage, Equipment equipment)
        {
            target.ApplyDamage(mainDamage);
            _scratch.Clear();
            Vector3 origin = attacker.transform.position;
            Vector3 direction = (target.transform.position - origin).normalized;
            foreach (var enemy in _enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                Vector3 delta = enemy.transform.position - origin;
                float along = Vector3.Dot(delta, direction);
                if (along <= 0f || along > equipment.Radius) continue;
                float side = (delta - direction * along).magnitude;
                if (side <= 0.7f) _scratch.Add(enemy);
            }
            _scratch.Sort((a, b) =>
                Vector3.SqrMagnitude(a.transform.position - origin)
                    .CompareTo(Vector3.SqrMagnitude(b.transform.position - origin)));

            int secondaryCount = Mathf.Max(0, equipment.MaxTargets - 1);
            Vector3 end = target.transform.position;
            for (int i = 0; i < _scratch.Count && i < secondaryCount; i++)
            {
                _scratch[i].ApplyDamage(mainDamage * equipment.SecondaryMul);
                end = _scratch[i].transform.position;
            }
            AttackTrace.Line(origin, end, new Color(0.3f, 0.9f, 1f), 0.09f);
        }

        void ApplyChain(BattleUnit attacker, BattleUnit target, float mainDamage, Equipment equipment)
        {
            target.ApplyDamage(mainDamage);
            _hit.Clear();
            _hit.Add(target);
            var points = new List<Vector3> { attacker.transform.position, target.transform.position };
            BattleUnit current = target;
            int secondaryCount = Mathf.Max(0, equipment.MaxTargets - 1);
            for (int i = 0; i < secondaryCount; i++)
            {
                BattleUnit next = null;
                float best = equipment.Radius * equipment.Radius;
                foreach (var enemy in _enemies)
                {
                    if (enemy == null || !enemy.IsAlive || _hit.Contains(enemy)) continue;
                    float sq = (enemy.transform.position - current.transform.position).sqrMagnitude;
                    if (sq >= best) continue;
                    best = sq;
                    next = enemy;
                }
                if (next == null) break;
                next.ApplyDamage(mainDamage * equipment.SecondaryMul);
                _hit.Add(next);
                points.Add(next.transform.position);
                current = next;
            }
            AttackTrace.Polyline(points, new Color(0.45f, 0.75f, 1f), 0.08f);
        }

        void ApplySplash(BattleUnit attacker, BattleUnit target, float mainDamage, Equipment equipment)
        {
            target.ApplyDamage(mainDamage);
            int hitCount = 1;
            float radiusSq = equipment.Radius * equipment.Radius;
            foreach (var enemy in _enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy == target) continue;
                if ((enemy.transform.position - target.transform.position).sqrMagnitude > radiusSq) continue;
                enemy.ApplyDamage(mainDamage * equipment.SecondaryMul);
                if (++hitCount >= equipment.MaxTargets) break;
            }
            AttackTrace.Circle(target.transform.position, equipment.Radius, new Color(1f, 0.55f, 0.2f));
        }
    }

    static class AttackTrace
    {
        static Material _material;

        public static void Line(Vector3 from, Vector3 to, Color color, float width = 0.06f)
        {
            Polyline(new List<Vector3> { from, to }, color, width);
        }

        public static void Polyline(IReadOnlyList<Vector3> points, Color color, float width)
        {
            if (points == null || points.Count < 2) return;
            var mat = Material;
            if (mat == null) return;
            var go = new GameObject("AttackTrace");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width * 0.65f;
            line.positionCount = points.Count;
            line.useWorldSpace = true;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i] + Vector3.back * 0.1f);
            Object.Destroy(go, 0.12f);
        }

        public static void Circle(Vector3 center, float radius, Color color)
        {
            const int segments = 24;
            var points = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                points.Add(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -0.1f) * radius);
            }
            Polyline(points, color, 0.07f);
        }

        static Material Material
        {
            get
            {
                if (_material != null) return _material;
                _material = BattleField.MakeTintMaterial(Color.white);
                return _material;
            }
        }
    }
}
