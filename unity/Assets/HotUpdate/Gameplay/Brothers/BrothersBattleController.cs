using System;
using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>割草自动战：寻敌普攻 + 表驱动技能。</summary>
    public sealed class BrothersBattleController : MonoBehaviour
    {
        public event Action<int> EnemyKilled;
        public event Action WaveCleared;
        public event Action SanpangTriggered;
        public event Action HudDirty;

        readonly List<BattleUnit> _brothers = new List<BattleUnit>();
        readonly List<BattleUnit> _enemies = new List<BattleUnit>();
        SkillCastSystem _skills;
        AttackFormSystem _attackForms;
        Equipment _equipment;
        Material _matBrother;
        Material _matEnemy;
        Material _matElite;
        Material _matSummon;
        bool _running;
        bool _ended;
        float _spawnBudget;
        int _toSpawn;
        string[] _themePool = Array.Empty<string>();
        float _difficulty = 1f;
        float _spawnInterval = 0.55f;
        ChapterId _chapter = ChapterId.Primary;
        int _gradeYear = 1;
        float _eliteChanceBonus;

        public int AliveBrothers
        {
            get
            {
                int n = 0;
                foreach (var u in _brothers)
                    if (u != null && u.IsAlive) n++;
                return n;
            }
        }

        public int AliveEnemies
        {
            get
            {
                int n = 0;
                foreach (var u in _enemies)
                    if (u != null && u.IsAlive) n++;
                return n;
            }
        }

        public bool IsRunning => _running;

        public void Bootstrap()
        {
            _matBrother = CreateMat(new Color(0.35f, 0.75f, 0.95f));
            _matEnemy = CreateMat(new Color(0.9f, 0.4f, 0.35f));
            _matElite = CreateMat(new Color(0.95f, 0.75f, 0.2f));
            _matSummon = CreateMat(new Color(0.55f, 0.85f, 0.45f));
            _skills = new SkillCastSystem(_brothers, _enemies, SpawnSummonAlly);
            _attackForms = new AttackFormSystem(_enemies);
            EnsureArenaVisual();
            EnsureLights();
        }

        public void StartWave(RunState run, MetaProgress meta, int enemyCount, float difficulty)
        {
            ClearField();
            _ended = false;
            _running = true;
            _chapter = run.Chapter;
            _gradeYear = run.GradeYear;
            _difficulty = Mathf.Max(0.45f, difficulty);
            int extraMembers = Mathf.Max(0, run.RecruitedCount - 3);
            float enemyMul = 1f + extraMembers * run.ExtraMemberEnemyMul;
            _toSpawn = Mathf.Max(2, Mathf.CeilToInt(enemyCount * enemyMul));
            _spawnBudget = 0.35f;
            _spawnInterval = run.Chapter == ChapterId.Primary && run.GradeYear <= 2 ? 0.7f : 0.5f;
            _spawnInterval /= 1f + extraMembers * run.ExtraMemberSpawnMul;
            _eliteChanceBonus = Mathf.Clamp01(extraMembers * run.ExtraMemberEliteBonus);
            _equipment = CfgTables.Ready
                ? CfgTables.Tables.TbEquipment.GetOrDefault(run.EquipmentId)
                : null;

            var scene = GameTables.FindScene(run.CurrentSceneId);
            _themePool = BuildThemePool(run, scene);

            SpawnBrothers(run, meta);
            FusionSystem.Refresh(_brothers, _skills);
            TrySanpangCheck(forceIfEmptyBrothers: true);
            HudDirty?.Invoke();
        }

        static string[] BuildThemePool(RunState run, SceneDef scene)
        {
            var basePool = scene?.EnemyThemeIds ?? new[] { "dog", "kids_gang" };
            if (run.Chapter == ChapterId.Primary && run.GradeYear >= 3)
            {
                var list = new List<string>(basePool) { "bully" };
                return list.ToArray();
            }

            return basePool;
        }

        public void Stop()
        {
            _running = false;
            ClearField();
        }

        void Update()
        {
            if (!_running || _ended) return;

            float dt = Time.deltaTime;
            if (_toSpawn > 0)
            {
                _spawnBudget -= dt;
                if (_spawnBudget <= 0f)
                {
                    SpawnOneEnemy();
                    _toSpawn--;
                    _spawnBudget = _spawnInterval;
                }
            }

            TickSide(_brothers, _enemies, dt);
            TickSide(_enemies, _brothers, dt);
            TickSummons(dt);
            CleanupDead();

            if (AliveBrothers <= 0 && (AliveEnemies > 0 || _toSpawn > 0))
            {
                TrySanpangCheck(forceIfEmptyBrothers: true);
                return;
            }

            if (AliveEnemies == 0 && _toSpawn <= 0 && AliveBrothers > 0)
                EndWaveCleared();
        }

        void TickSide(List<BattleUnit> attackers, List<BattleUnit> targets, float dt)
        {
            for (int i = 0; i < attackers.Count; i++)
            {
                var a = attackers[i];
                if (a == null || !a.IsAlive) continue;
                var t = FindNearest(a, targets);
                a.TickCombat(dt, t, OnHit);
                if (a.Side == UnitSide.Brother)
                    _skills?.Tick(a, dt);
            }
        }

        void TickSummons(float dt)
        {
            for (int i = _brothers.Count - 1; i >= 0; i--)
            {
                var u = _brothers[i];
                if (u == null || u.SummonLifeLeft <= 0f) continue;
                u.SummonLifeLeft -= dt;
                if (u.SummonLifeLeft > 0f) continue;
                u.Hp = 0f;
            }
        }

        void OnHit(BattleUnit attacker, BattleUnit target, float dmg)
        {
            if (attacker.Side == UnitSide.Brother && attacker.BoundBrother != null)
            {
                _attackForms?.Apply(attacker, target, dmg, _equipment);
                if (attacker.BoundBrother.CampusSkillLv > 0 && UnityEngine.Random.value < 0.08f)
                    attacker.Heal(2f + attacker.BoundBrother.CampusSkillLv);
                return;
            }

            target.ApplyDamage(dmg);
        }

        void CleanupDead()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                if (e != null && e.IsAlive) continue;
                if (e != null)
                {
                    EnemyKilled?.Invoke(1);
                    Destroy(e.gameObject);
                }

                _enemies.RemoveAt(i);
                HudDirty?.Invoke();
            }

            for (int i = _brothers.Count - 1; i >= 0; i--)
            {
                var b = _brothers[i];
                if (b != null && b.IsAlive) continue;
                if (b != null)
                {
                    if (b.BoundBrother != null)
                        b.BoundBrother.Hp = 0f;
                    Destroy(b.gameObject);
                }

                _brothers.RemoveAt(i);
                HudDirty?.Invoke();
            }
        }

        void TrySanpangCheck(bool forceIfEmptyBrothers)
        {
            if (_ended) return;
            if (AliveBrothers > 0) return;
            if (!forceIfEmptyBrothers && AliveEnemies <= 0 && _toSpawn <= 0) return;

            if (AliveEnemies > 0 || _toSpawn > 0 || forceIfEmptyBrothers)
            {
                _ended = true;
                _running = false;
                SanpangTriggered?.Invoke();
            }
        }

        void EndWaveCleared()
        {
            if (_ended) return;
            _ended = true;
            _running = false;
            SyncBrotherHpBack();
            WaveCleared?.Invoke();
        }

        void SyncBrotherHpBack()
        {
            foreach (var u in _brothers)
            {
                if (u?.BoundBrother == null) continue;
                u.BoundBrother.Hp = u.Hp;
            }
        }

        public void SyncDeadBrothersToRun(RunState run, MetaProgress meta)
        {
            foreach (var b in run.Squad)
            {
                if (!b.Recruited || b.Injured) continue;
                bool alive = false;
                foreach (var u in _brothers)
                {
                    if (u != null && u.BoundBrother == b && u.IsAlive)
                    {
                        alive = true;
                        b.Hp = u.Hp;
                        break;
                    }
                }

                if (!alive && b.Hp <= 0f)
                    run.MarkInjured(b, meta);
            }
        }

        void SpawnBrothers(RunState run, MetaProgress meta)
        {
            float teamBuff = run.TeamBuffNextWave;
            float teamShield = run.TeamShieldNextWave;
            run.TeamBuffNextWave = 0f;
            run.TeamShieldNextWave = 0f;

            int slot = 0;
            foreach (var br in run.Squad)
            {
                if (!br.CanFight) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Brother_" + br.DisplayName;
                go.transform.SetParent(transform, false);
                go.transform.position = BattleField.SquadSlot(slot, CountFighting(run));
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var unit = go.AddComponent<BattleUnit>();
                unit.Side = UnitSide.Brother;
                unit.DisplayName = br.DisplayName;
                unit.MaxHp = br.MaxHp;
                unit.Hp = br.Hp > 0f ? br.Hp : br.MaxHp;
                unit.Atk = br.Atk;
                unit.Move = br.Move;
                unit.Defense = br.Defense;
                unit.CritRate = br.CritRate;
                unit.CritDamage = br.CritDamage;
                unit.AttackRange = 0.8f;
                unit.AttackCooldown = br.AttackInterval;
                unit.BoundBrother = br;
                unit.BaseAtkMul = (1f + teamBuff) * (br.JoinPenaltyWaves > 0 ? br.JoinPowerMul : 1f);
                unit.AtkMul = unit.BaseAtkMul;
                unit.Shield = teamShield;
                unit.BaseDamageTakenMul = 1f;
                unit.DamageTakenMul = 1f;
                foreach (var tag in br.Tags)
                    unit.BaseDamageTakenMul *= GameTables.TagDamageTakenMul(tag);
                unit.DamageTakenMul = unit.BaseDamageTakenMul;

                if (br.JobSkillUnlocked && HasFaction(br, BrotherTag.Mechanical))
                    unit.MechBonusVsArmor = 0.6f + br.JobSkillLv * 0.15f;

                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = _matBrother;
                unit.SetupVisual(new Color(0.35f, 0.75f, 0.95f), 0.55f);
                unit.TryApplyBattleSprite(RoleArtLoader.LoadBattle(br.BattleLoc, br.AvatarLoc));
                BattleFeedback.EnsureOn(unit);

                _skills?.EquipFromRole(unit, br);
                _brothers.Add(unit);
                slot++;
            }
        }

        static int CountFighting(RunState run)
        {
            int n = 0;
            foreach (var br in run.Squad)
                if (br.CanFight) n++;
            return Mathf.Max(1, n);
        }

        static bool HasFaction(BrotherRuntime br, BrotherTag tag)
        {
            if (br.Tags == null) return false;
            foreach (var t in br.Tags)
                if (t == tag) return true;
            return false;
        }

        void SpawnOneEnemy()
        {
            string themeId = _themePool[UnityEngine.Random.Range(0, _themePool.Length)];
            var theme = GameTables.FindEnemy(themeId) ?? GameTables.Enemies[0];

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Enemy_" + theme.DisplayName;
            go.transform.SetParent(transform, false);
            go.transform.position = BattleField.EnemySpawnOnRing();
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            bool isElite = theme.IsElite || (!theme.HighArmor && UnityEngine.Random.value < _eliteChanceBonus);
            float hpBase = isElite ? 22f : 11f;
            float atkBase = isElite ? 2.2f : 1.35f;
            if (_chapter == ChapterId.Primary && _gradeYear <= 2)
            {
                hpBase *= 0.85f;
                atkBase *= 0.75f;
            }

            var unit = go.AddComponent<BattleUnit>();
            unit.Side = UnitSide.Enemy;
            unit.ThemeId = theme.Id;
            unit.DisplayName = theme.DisplayName;
            unit.MaxHp = hpBase * theme.HpMul * _difficulty;
            unit.Hp = unit.MaxHp;
            unit.Atk = atkBase * theme.AtkMul * _difficulty;
            unit.Move = 1.15f * theme.MoveMul;
            unit.AttackRange = 0.5f;
            unit.HighArmor = theme.HighArmor;
            unit.IsElite = isElite;
            unit.AttackCooldown = isElite ? 0.85f : 0.95f;

            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = isElite ? _matElite : _matEnemy;
            unit.SetupVisual(isElite ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.9f, 0.4f, 0.35f),
                isElite ? 0.7f : 0.45f);
            BattleFeedback.EnsureOn(unit);

            _enemies.Add(unit);
            HudDirty?.Invoke();
        }

        void SpawnSummonAlly(string roleId, Vector3 pos)
        {
            var role = RoleCatalog.FindRole(roleId);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Summon_" + (role?.Name ?? roleId);
            go.transform.SetParent(transform, false);
            go.transform.position = BattleField.ClampToArena(pos);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var unit = go.AddComponent<BattleUnit>();
            unit.Side = UnitSide.Brother;
            unit.DisplayName = role?.Name ?? roleId;
            unit.MaxHp = role != null ? role.BaseHp : 40f;
            unit.Hp = unit.MaxHp;
            unit.Atk = role != null ? role.BaseAtk : 8f;
            unit.Move = role != null ? role.BaseMove : 2f;
            unit.Defense = role != null ? role.BaseDefense : 0f;
            unit.CritRate = role != null ? role.CritRate : 0f;
            unit.CritDamage = role != null ? role.CritDamage : 1.5f;
            unit.AttackRange = 0.65f;
            unit.AttackCooldown = role != null ? role.AttackInterval : 0.45f;
            unit.SummonLifeLeft = 12f;
            unit.BaseAtkMul = 1f;
            unit.AtkMul = 1f;

            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = _matSummon;
            unit.SetupVisual(new Color(0.55f, 0.85f, 0.45f), 0.4f);
            _brothers.Add(unit);
            HudDirty?.Invoke();
        }

        BattleUnit FindNearest(BattleUnit from, List<BattleUnit> pool)
        {
            BattleUnit best = null;
            float bestSq = float.MaxValue;
            var p = from.transform.position;
            for (int i = 0; i < pool.Count; i++)
            {
                var u = pool[i];
                if (u == null || !u.IsAlive) continue;
                float sq = (u.transform.position - p).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = u;
            }

            return best;
        }

        void ClearField()
        {
            foreach (var u in _brothers)
                if (u != null) Destroy(u.gameObject);
            foreach (var u in _enemies)
                if (u != null) Destroy(u.gameObject);
            _brothers.Clear();
            _enemies.Clear();
        }

        void EnsureArenaVisual()
        {
            if (transform.Find("ArenaFloor") != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ArenaFloor";
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0f, 1f);
            float size = BattleField.SpawnRadius * 2.15f;
            go.transform.localScale = new Vector3(size, size, 1f);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = CreateMat(new Color(0.12f, 0.16f, 0.14f));
        }

        static void EnsureLights()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static Material CreateMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}
