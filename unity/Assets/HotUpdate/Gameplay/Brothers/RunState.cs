using System;
using System.Collections.Generic;
using JojoP.Config;

namespace JojoP.Gameplay.Brothers
{
    [Serializable]
    public sealed class BrotherRuntime
    {
        public string DefId;
        public string DisplayName;
        public float MaxHp;
        public float Hp;
        public float Atk;
        public float Move;
        public float Defense;
        public float CritRate;
        public float CritDamage = 1.5f;
        public float AttackInterval = 0.4f;
        public bool Recruited;
        public bool Injured;
        public int InjuredBreaksLeft;
        public int CampusSkillLv;
        public int JobSkillLv;
        public bool JobSkillUnlocked;
        public bool CareerGrowthApplied;
        public float GraduationSkillMul = 1f;
        public float CareerHpMul = 1f;
        public float CareerAtkMul = 1f;
        public float CareerMoveMul = 1f;
        public float CareerSkillCdMul = 1f;
        public string LootSkillId;
        public float JoinPowerMul = 1f;
        public int JoinPenaltyWaves;
        public BrotherTag[] Tags = Array.Empty<BrotherTag>();
        public string JobSkillId;
        public string[] SkillIds = Array.Empty<string>();
        public string AvatarLoc;
        public string BattleLoc;

        public bool CanFight => Recruited && !Injured && Hp > 0f;
    }

    /// <summary>单局 run：日历 + 小队 + 波次进度。</summary>
    public sealed class RunState
    {
        public ChapterId Chapter;
        public int GradeYear = 1;
        public CalendarPhase Phase = CalendarPhase.UpperTerm;
        public int SocietyYearIndex = 1;
        public UniversityRoute UniRoute = UniversityRoute.Reunion;
        public string CurrentSceneId;
        public int WaveInNode;
        public int KillsThisWave;
        public int KillsThisRun;
        public float TeamBuffNextWave;
        public float TeamShieldNextWave;
        public string EquipmentId = "equip_normal";
        public int BaseActiveSlots = 3;
        public int AdExtraSlotLimit = 2;
        public int AdExtraSlotsUsed;
        public int AffinityNeeded = 3;
        public int RewardedRerollLimit = 1;
        public int RewardRerollsUsedOnPage;
        public float ExtraMemberEnemyMul = 0.45f;
        public float ExtraMemberSpawnMul = 0.12f;
        public float ExtraMemberEliteBonus = 0.02f;
        public readonly List<BrotherRuntime> Squad = new List<BrotherRuntime>();
        public readonly List<string> OutList = new List<string>();
        public readonly Dictionary<string, int> Affinity = new Dictionary<string, int>();
        public readonly List<string> ReadyToJoin = new List<string>();

        public ChapterDef ChapterDef => GameTables.FindChapter(Chapter);
        public int ActiveSlotLimit => BaseActiveSlots < 0 ? int.MaxValue : BaseActiveSlots + AdExtraSlotsUsed;
        public bool HasUnlimitedSlots => BaseActiveSlots < 0;

        public int FightableCount
        {
            get
            {
                int n = 0;
                foreach (var b in Squad)
                    if (b.CanFight) n++;
                return n;
            }
        }

        public void Bootstrap(ChapterId chapter, MetaProgress meta)
        {
            Chapter = chapter;
            GradeYear = 1;
            Phase = chapter == ChapterId.Society ? CalendarPhase.SocietyNewYear : CalendarPhase.UpperTerm;
            SocietyYearIndex = 1;
            WaveInNode = 0;
            KillsThisWave = 0;
            KillsThisRun = 0;
            TeamBuffNextWave = 0f;
            TeamShieldNextWave = 0f;
            EquipmentId = "equip_normal";
            AdExtraSlotsUsed = 0;
            RewardRerollsUsedOnPage = 0;
            Squad.Clear();
            OutList.Clear();
            Affinity.Clear();
            ReadyToJoin.Clear();
            ApplyChapterRule();

            var starter = GameTables.FindBrother(RoleCatalog.StarterId)
                          ?? GameTables.FindBrother("player");
            var me = CreateFromDef(starter, meta, recruited: true);
            Squad.Add(me);

            CurrentSceneId = PickScene();
        }

        public static BrotherRuntime CreateFromDef(BrotherDef def, MetaProgress meta, bool recruited)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            float hp = def.BaseHp * (meta?.HpMul ?? 1f);
            float atk = def.BaseAtk * (meta?.AtkMul ?? 1f);
            return new BrotherRuntime
            {
                DefId = def.Id,
                DisplayName = def.DisplayName,
                MaxHp = hp,
                Hp = hp,
                Atk = atk,
                Move = def.BaseMove,
                Defense = Math.Max(0f, def.BaseDefense),
                CritRate = Math.Max(0f, Math.Min(0.75f, def.CritRate)),
                CritDamage = Math.Max(1f, def.CritDamage),
                AttackInterval = Math.Max(0.15f, def.AttackInterval),
                Recruited = recruited,
                Injured = false,
                InjuredBreaksLeft = 0,
                CampusSkillLv = 0,
                JobSkillLv = 0,
                JobSkillUnlocked = false,
                CareerGrowthApplied = false,
                GraduationSkillMul = Math.Max(1f, def.GraduationSkillMul),
                CareerHpMul = Math.Max(0.1f, def.CareerHpMul),
                CareerAtkMul = Math.Max(0.1f, def.CareerAtkMul),
                CareerMoveMul = Math.Max(0.1f, def.CareerMoveMul),
                CareerSkillCdMul = Math.Max(0.1f, def.CareerSkillCdMul),
                LootSkillId = null,
                JoinPowerMul = 1f,
                JoinPenaltyWaves = 0,
                Tags = def.Tags ?? Array.Empty<BrotherTag>(),
                JobSkillId = def.JobSkillId,
                SkillIds = def.SkillIds ?? Array.Empty<string>(),
                AvatarLoc = def.AvatarLoc,
                BattleLoc = def.BattleLoc
            };
        }

        public string PickScene()
        {
            var ch = ChapterDef;
            if (ch.ScenePool == null || ch.ScenePool.Length == 0)
                return "longxiang";

            if (Chapter == ChapterId.Society)
            {
                // 过年抽 1 个场景作本小波入口（一场年波内可推进多场景）
                int idx = (SocietyYearIndex + WaveInNode) % ch.ScenePool.Length;
                return ch.ScenePool[idx];
            }

            if (Chapter == ChapterId.High)
            {
                int idx = (GradeYear + (int)Phase) % ch.ScenePool.Length;
                return ch.ScenePool[idx];
            }

            return ch.ScenePool[0];
        }

        public string PhaseLabel()
        {
            if (Chapter == ChapterId.Society)
                return $"出社会 第{SocietyYearIndex}年·过年";

            string grade = Chapter switch
            {
                ChapterId.Primary => $"小{GradeYear}",
                ChapterId.Middle => $"初{GradeYear}",
                ChapterId.High => $"高{GradeYear}",
                ChapterId.University => $"大{GradeYear}",
                _ => $"年{GradeYear}"
            };

            string phase = Phase switch
            {
                CalendarPhase.UpperTerm => "上学期",
                CalendarPhase.WinterBreak => "寒假休整",
                CalendarPhase.LowerTerm => "下学期",
                CalendarPhase.SummerBreak => "暑假休整",
                CalendarPhase.YearWave => "学年波",
                _ => Phase.ToString()
            };
            return $"{grade} · {phase}";
        }

        public bool IsBreakPhase =>
            Phase == CalendarPhase.WinterBreak || Phase == CalendarPhase.SummerBreak;

        public void MarkInjured(BrotherRuntime b, MetaProgress meta)
        {
            if (b == null) return;
            b.Hp = 0f;
            b.Injured = true;
            b.InjuredBreaksLeft = meta != null ? meta.InjuredBreaksNeeded : 2;
            if (!OutList.Contains(b.DisplayName))
                OutList.Add(b.DisplayName);
        }

        public void AdvanceBreakHealing()
        {
            foreach (var b in Squad)
            {
                if (!b.Injured) continue;
                b.InjuredBreaksLeft--;
                if (b.InjuredBreaksLeft > 0) continue;
                b.Injured = false;
                b.Hp = b.MaxHp * 0.7f;
            }
        }

        public bool TryRecruit(string defId, MetaProgress meta)
        {
            if (RecruitedCount >= ActiveSlotLimit) return false;
            foreach (var s in Squad)
                if (s.DefId == defId && s.Recruited) return false;

            var def = GameTables.FindBrother(defId);
            if (def == null) return false;

            for (int i = 0; i < Squad.Count; i++)
            {
                if (Squad[i].DefId == defId)
                {
                    Squad[i].Recruited = true;
                    Squad[i].Injured = false;
                    Squad[i].Hp = Squad[i].MaxHp;
                    return true;
                }
            }

            var joined = CreateFromDef(def, meta, recruited: true);
            var encounter = CfgTables.Ready
                ? CfgTables.Tables.TbCharacterEncounter.GetOrDefault(defId)
                : null;
            joined.JoinPowerMul = Math.Max(0.1f, encounter?.JoinPowerMul ?? 0.72f);
            joined.JoinPenaltyWaves = 1;
            Squad.Add(joined);
            ReadyToJoin.Remove(defId);
            return true;
        }

        public int RecruitedCount
        {
            get
            {
                int count = 0;
                foreach (var brother in Squad)
                    if (brother.Recruited) count++;
                return count;
            }
        }

        public int GetAffinity(string roleId)
        {
            return !string.IsNullOrEmpty(roleId) && Affinity.TryGetValue(roleId, out var value) ? value : 0;
        }

        public string AddAffinity(string roleId, int amount, MetaProgress meta)
        {
            if (string.IsNullOrEmpty(roleId)) return "没有遇到合适的人";
            int next = Math.Min(AffinityNeeded, GetAffinity(roleId) + Math.Max(1, amount));
            Affinity[roleId] = next;
            var def = GameTables.FindBrother(roleId);
            string name = def?.DisplayName ?? roleId;
            if (next < AffinityNeeded)
                return $"与{name}熟络度 {next}/{AffinityNeeded}";

            if (TryRecruit(roleId, meta))
                return $"{name}好感已满，正式集合（首波磨合）";

            if (!ReadyToJoin.Contains(roleId)) ReadyToJoin.Add(roleId);
            return $"{name}好感已满，当前人齐了；扩编后自动集合";
        }

        public bool TryAddAdSlot(MetaProgress meta, out string message)
        {
            if (HasUnlimitedSlots)
            {
                message = "当前章节已不限人数";
                return false;
            }
            if (AdExtraSlotsUsed >= AdExtraSlotLimit)
            {
                message = "本局广告扩编次数已用完";
                return false;
            }

            AdExtraSlotsUsed++;
            string joined = TryJoinReady(meta);
            message = $"上阵位 {ActiveSlotLimit}" + (string.IsNullOrEmpty(joined) ? "" : $" · {joined}集合");
            return true;
        }

        public string TryJoinReady(MetaProgress meta)
        {
            if (ReadyToJoin.Count == 0 || RecruitedCount >= ActiveSlotLimit) return "";
            string roleId = ReadyToJoin[0];
            return TryRecruit(roleId, meta) ? GameTables.FindBrother(roleId)?.DisplayName ?? roleId : "";
        }

        public void AdvanceJoinPenalty()
        {
            foreach (var brother in Squad)
            {
                if (brother.JoinPenaltyWaves <= 0) continue;
                brother.JoinPenaltyWaves--;
                if (brother.JoinPenaltyWaves <= 0) brother.JoinPowerMul = 1f;
            }
        }

        public void ResetRewardPage()
        {
            RewardRerollsUsedOnPage = 0;
        }

        public bool TryUseRewardReroll()
        {
            if (RewardRerollsUsedOnPage >= RewardedRerollLimit) return false;
            RewardRerollsUsedOnPage++;
            return true;
        }

        void ApplyChapterRule()
        {
            if (!CfgTables.Ready) return;
            var rule = CfgTables.Tables.TbRunChapterRule.GetOrDefault(Chapter.ToString());
            if (rule == null) return;
            BaseActiveSlots = rule.BaseActiveSlots;
            AdExtraSlotLimit = rule.AdExtraSlotLimit;
            AffinityNeeded = Math.Max(1, rule.AffinityNeeded);
            RewardedRerollLimit = Math.Max(0, rule.RewardedRerollLimit);
            ExtraMemberEnemyMul = Math.Max(0f, rule.ExtraMemberEnemyMul);
            ExtraMemberSpawnMul = Math.Max(0f, rule.ExtraMemberSpawnMul);
            ExtraMemberEliteBonus = Math.Max(0f, rule.ExtraMemberEliteBonus);
        }

        public void RefreshJobSkillLocks()
        {
            if (Chapter != ChapterId.Society) return;

            foreach (var b in Squad)
            {
                var def = GameTables.FindBrother(b.DefId);
                int delay = GameTables.GetJobDelayYears(def);
                bool unlock = SocietyYearIndex >= Math.Max(1, delay);
                bool shouldUnlock = unlock || delay <= 0;
                if (!b.JobSkillUnlocked && shouldUnlock)
                {
                    ApplyCareerGrowth(b);
                }
                b.JobSkillUnlocked = shouldUnlock;
            }
        }

        static void ApplyCareerGrowth(BrotherRuntime brother)
        {
            if (brother == null || brother.CareerGrowthApplied) return;
            float hpRatio = brother.MaxHp > 0f ? brother.Hp / brother.MaxHp : 1f;
            brother.MaxHp *= brother.CareerHpMul;
            brother.Hp = brother.MaxHp * Math.Max(0f, Math.Min(1f, hpRatio));
            brother.Atk *= brother.CareerAtkMul;
            brother.Move *= brother.CareerMoveMul;
            brother.CareerGrowthApplied = true;
        }

        /// <summary>学期/假期推进。返回是否刚通关本章。</summary>
        public bool AdvanceAfterDesheng(out bool chapterCleared)
        {
            chapterCleared = false;
            WaveInNode = 0;
            KillsThisWave = 0;

            if (Chapter == ChapterId.Society)
            {
                SocietyYearIndex++;
                RefreshJobSkillLocks();
                CurrentSceneId = PickScene();
                return false;
            }

            if (Phase == CalendarPhase.UpperTerm)
            {
                Phase = CalendarPhase.WinterBreak;
                return false;
            }

            if (Phase == CalendarPhase.WinterBreak)
            {
                AdvanceBreakHealing();
                Phase = CalendarPhase.LowerTerm;
                CurrentSceneId = PickScene();
                return false;
            }

            if (Phase == CalendarPhase.LowerTerm)
            {
                Phase = CalendarPhase.SummerBreak;
                return false;
            }

            // SummerBreak → 下一年或通关章
            AdvanceBreakHealing();
            GradeYear++;
            var ch = ChapterDef;
            if (GradeYear > ch.MaxGradeYears)
            {
                chapterCleared = true;
                return true;
            }

            Phase = CalendarPhase.UpperTerm;
            CurrentSceneId = PickScene();
            return false;
        }
    }
}
