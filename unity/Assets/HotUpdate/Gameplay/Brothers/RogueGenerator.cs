using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    public sealed class RogueOption
    {
        public string Key;
        public ERogueRewardKind Kind;
        public string Title;
        public string Desc;
        public string RefId;
        public string TargetRoleId;
        public string EventId;
        public string Stat;
        public float Value;
    }

    /// <summary>一局一个实例：按配置预算生成奖励并记住广告刷新前的选项。</summary>
    public sealed class RunRewardSystem
    {
        readonly HashSet<string> _previousKeys = new HashSet<string>();

        public List<RogueOption> RollThree(RunState run, bool reroll)
        {
            var result = CfgTables.Ready ? RollConfigured(run, reroll) : RollFallback(run);
            _previousKeys.Clear();
            foreach (var option in result) _previousKeys.Add(option.Key);
            return result;
        }

        List<RogueOption> RollConfigured(RunState run, bool reroll)
        {
            int budget = run.Chapter == ChapterId.Society
                ? 3
                : Mathf.Clamp(2 + (run.GradeYear - 1) / 2, 2, 3);
            var candidates = new List<RogueReward>();
            foreach (var reward in CfgTables.Tables.TbRogueReward.DataList)
            {
                if (reward.PowerCost > budget || reward.Weight <= 0) continue;
                if (ChapterOrder(reward.MinChapter) > (int)run.Chapter) continue;
                if (reroll && WasPreviouslyShown(reward.Id)) continue;
                if (!IsContextValid(reward, run)) continue;
                candidates.Add(reward);
            }

            var result = new List<RogueOption>();
            var usedKinds = new HashSet<ERogueRewardKind>();
            int guard = 0;
            while (result.Count < 3 && candidates.Count > 0 && guard++ < 30)
            {
                var diverse = candidates.FindAll(item => !usedKinds.Contains(item.Kind));
                var source = diverse.Count > 0 ? diverse : candidates;
                var picked = WeightedPick(source);
                candidates.Remove(picked);
                var option = BuildOption(picked, run);
                if (option == null) continue;
                result.Add(option);
                usedKinds.Add(option.Kind);
            }

            FillFallback(result, run);
            return result;
        }

        bool WasPreviouslyShown(string rewardId)
        {
            foreach (var key in _previousKeys)
                if (key == rewardId || key.StartsWith(rewardId + "|")) return true;
            return false;
        }

        static RogueReward WeightedPick(List<RogueReward> source)
        {
            int total = 0;
            foreach (var item in source) total += Mathf.Max(1, item.Weight);
            int roll = Random.Range(0, total);
            foreach (var item in source)
            {
                roll -= Mathf.Max(1, item.Weight);
                if (roll < 0) return item;
            }
            return source[source.Count - 1];
        }

        bool IsContextValid(RogueReward reward, RunState run)
        {
            switch (reward.Kind)
            {
                case ERogueRewardKind.Encounter:
                    return EncounterCandidates(run).Count > 0;
                case ERogueRewardKind.Equipment:
                    return !string.IsNullOrEmpty(reward.RefId) && reward.RefId != run.EquipmentId;
                case ERogueRewardKind.LootSkill:
                case ERogueRewardKind.CampusSkill:
                    return RandomRecruited(run) != null;
                case ERogueRewardKind.JobSkill:
                    return RandomJobUnlocked(run) != null;
                default:
                    return true;
            }
        }

        RogueOption BuildOption(RogueReward reward, RunState run)
        {
            var option = new RogueOption
            {
                Key = reward.Id,
                Kind = reward.Kind,
                Title = reward.Title,
                Desc = reward.Desc,
                RefId = reward.RefId,
                Stat = reward.Stat,
                Value = reward.Value,
            };

            if (reward.Kind == ERogueRewardKind.Encounter)
            {
                var encounter = WeightedEncounter(run);
                if (encounter == null) return null;
                option.TargetRoleId = encounter.RoleId;
                var def = GameTables.FindBrother(encounter.RoleId);
                int before = run.GetAffinity(encounter.RoleId);
                option.Title = $"碰见 {def?.DisplayName ?? encounter.RoleId}";
                option.Desc = $"熟络度 {before}/{run.AffinityNeeded} → {Mathf.Min(run.AffinityNeeded, before + encounter.AffinityPerMeet)}/{run.AffinityNeeded}";
                option.Value = encounter.AffinityPerMeet;
            }
            else if (reward.Kind == ERogueRewardKind.LootSkill)
            {
                var target = RandomRecruited(run);
                if (target == null) return null;
                option.TargetRoleId = target.DefId;
                var skill = CfgTables.Tables.TbSkillIndex.GetOrDefault(reward.RefId);
                string replace = string.IsNullOrEmpty(target.LootSkillId) ? "装上" : "替换";
                option.Desc = $"{target.DisplayName}{replace}临时技能：{skill?.Name ?? reward.RefId}";
            }
            else if (reward.Kind == ERogueRewardKind.Event)
            {
                var evt = PickEvent(run);
                if (evt == null) return null;
                option.EventId = evt.Id;
                option.Title = evt.Title;
                option.Desc = evt.Desc;
                option.Key += "|" + evt.Id;
            }

            return option;
        }

        public string Apply(RogueOption opt, RunState run, MetaProgress meta)
        {
            if (opt == null) return "";

            switch (opt.Kind)
            {
                case ERogueRewardKind.Encounter:
                    return run.AddAffinity(opt.TargetRoleId, Mathf.RoundToInt(opt.Value), meta);
                case ERogueRewardKind.Stat:
                {
                    var b = RandomRecruited(run);
                    if (b == null) return "没有可升级的人";
                    if (opt.Stat == "hp")
                    {
                        b.MaxHp += opt.Value;
                        b.Hp += opt.Value;
                        return $"{b.DisplayName} 生命+{opt.Value:0.#}";
                    }

                    b.Atk += opt.Value;
                    return $"{b.DisplayName} 攻击+{opt.Value:0.#}";
                }
                case ERogueRewardKind.CampusSkill:
                {
                    var b = RandomRecruited(run);
                    if (b == null) return "没有可学的人";
                    b.CampusSkillLv++;
                    return $"{b.DisplayName} 校园技 Lv{b.CampusSkillLv}";
                }
                case ERogueRewardKind.Recovery:
                    foreach (var b in run.Squad)
                    {
                        if (!b.Recruited || b.Injured) continue;
                        b.Hp = Mathf.Min(b.MaxHp, b.Hp + b.MaxHp * opt.Value);
                    }

                    return $"全体现役回复 {opt.Value * 100f:0}%";
                case ERogueRewardKind.TeamBuff:
                    run.TeamBuffNextWave += opt.Value;
                    return $"下一波全队攻击+{opt.Value * 100f:0}%";
                case ERogueRewardKind.JobSkill:
                {
                    var b = RandomJobUnlocked(run);
                    if (b == null) return "还没人毕业就业";
                    b.JobSkillLv++;
                    return $"{b.DisplayName} 就业技 Lv{b.JobSkillLv}";
                }
                case ERogueRewardKind.Equipment:
                    run.EquipmentId = opt.RefId;
                    return $"攻击核心替换为：{opt.Title}";
                case ERogueRewardKind.LootSkill:
                {
                    var b = FindBrother(run, opt.TargetRoleId) ?? RandomRecruited(run);
                    if (b == null) return "没有可装技能的人";
                    b.LootSkillId = opt.RefId;
                    return $"{b.DisplayName} 装上 {CfgTables.Tables.TbSkillIndex.GetOrDefault(opt.RefId)?.Name ?? opt.RefId}";
                }
                case ERogueRewardKind.Event:
                    return ApplyEvent(opt.EventId, run, meta);
                default:
                    return "";
            }
        }

        string ApplyEvent(string eventId, RunState run, MetaProgress meta)
        {
            var evt = CfgTables.Tables.TbRunEvent.GetOrDefault(eventId);
            if (evt == null) return "事情过去了";
            bool success = HasFaction(run, evt.RequiredTag);
            var effect = success ? evt.SuccessEffect : evt.FailEffect;
            float value = success ? evt.SuccessValue : evt.FailValue;
            ApplyEventEffect(effect, value, run, meta);
            return success ? evt.SuccessDesc : evt.FailDesc;
        }

        void ApplyEventEffect(ERunEventEffect effect, float value, RunState run, MetaProgress meta)
        {
            switch (effect)
            {
                case ERunEventEffect.HealTeam:
                    foreach (var b in run.Squad)
                        if (b.Recruited && !b.Injured) b.Hp = Mathf.Min(b.MaxHp, b.Hp + b.MaxHp * value);
                    break;
                case ERunEventEffect.DamageTeam:
                    foreach (var b in run.Squad)
                        if (b.Recruited && !b.Injured) b.Hp = Mathf.Max(1f, b.Hp - b.MaxHp * value);
                    break;
                case ERunEventEffect.NextWaveAtk:
                    run.TeamBuffNextWave += value;
                    break;
                case ERunEventEffect.NextWaveShield:
                    run.TeamShieldNextWave += value;
                    break;
                case ERunEventEffect.AddFavor:
                    meta?.AddFavor(Mathf.RoundToInt(value));
                    break;
                case ERunEventEffect.AddAffinity:
                {
                    var encounter = WeightedEncounter(run);
                    if (encounter != null) run.AddAffinity(encounter.RoleId, Mathf.RoundToInt(value), meta);
                    break;
                }
            }
        }

        CharacterEncounter WeightedEncounter(RunState run)
        {
            var candidates = EncounterCandidates(run);
            if (candidates.Count == 0) return null;
            int total = 0;
            foreach (var item in candidates) total += EncounterWeight(item, run);
            int roll = Random.Range(0, total);
            foreach (var item in candidates)
            {
                roll -= EncounterWeight(item, run);
                if (roll < 0) return item;
            }
            return candidates[candidates.Count - 1];
        }

        static int EncounterWeight(CharacterEncounter encounter, RunState run)
        {
            float multiplier = TimelineCatalog.EncounterWeightMultiplier(encounter.RoleId, run);
            return Mathf.Max(1, Mathf.RoundToInt(encounter.Weight * multiplier));
        }

        static RunEvent PickEvent(RunState run)
        {
            var timeline = TimelineCatalog.Current(run);
            if (timeline?.EventIds != null && timeline.EventIds.Count > 0)
            {
                string id = timeline.EventIds[Random.Range(0, timeline.EventIds.Count)];
                var linked = CfgTables.Tables.TbRunEvent.GetOrDefault(id);
                if (linked != null) return linked;
            }

            var events = CfgTables.Tables.TbRunEvent.DataList;
            return events.Count > 0 ? events[Random.Range(0, events.Count)] : null;
        }

        List<CharacterEncounter> EncounterCandidates(RunState run)
        {
            var result = new List<CharacterEncounter>();
            foreach (var item in CfgTables.Tables.TbCharacterEncounter.DataList)
            {
                if (ChapterOrder(item.MinChapter) > (int)run.Chapter) continue;
                if (run.GetAffinity(item.RoleId) >= run.AffinityNeeded) continue;
                bool joined = false;
                foreach (var brother in run.Squad)
                    if (brother.Recruited && brother.DefId == item.RoleId) joined = true;
                if (!joined) result.Add(item);
            }
            return result;
        }

        static bool HasFaction(RunState run, string requiredTag)
        {
            if (string.IsNullOrEmpty(requiredTag)) return true;
            foreach (var brother in run.Squad)
            {
                if (!brother.Recruited) continue;
                var role = RoleCatalog.FindRole(brother.DefId);
                if (role?.FactionTags == null) continue;
                foreach (var tag in role.FactionTags)
                    if (tag.ToString() == requiredTag) return true;
            }
            return false;
        }

        static BrotherRuntime FindBrother(RunState run, string roleId)
        {
            foreach (var b in run.Squad)
                if (b.Recruited && b.DefId == roleId) return b;
            return null;
        }

        static BrotherRuntime RandomRecruited(RunState run)
        {
            var list = new List<BrotherRuntime>();
            foreach (var b in run.Squad)
                if (b.Recruited && !b.Injured) list.Add(b);
            if (list.Count == 0)
            {
                foreach (var b in run.Squad)
                    if (b.Recruited) list.Add(b);
            }

            if (list.Count == 0) return null;
            return list[Random.Range(0, list.Count)];
        }

        static BrotherRuntime RandomJobUnlocked(RunState run)
        {
            var list = new List<BrotherRuntime>();
            foreach (var b in run.Squad)
                if (b.Recruited && !b.Injured && b.JobSkillUnlocked) list.Add(b);
            return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
        }

        static int ChapterOrder(string chapter)
        {
            return chapter switch
            {
                "Primary" => (int)ChapterId.Primary,
                "Middle" => (int)ChapterId.Middle,
                "High" => (int)ChapterId.High,
                "University" => (int)ChapterId.University,
                "Society" => (int)ChapterId.Society,
                _ => int.MaxValue,
            };
        }

        static List<RogueOption> RollFallback(RunState run)
        {
            var result = new List<RogueOption>();
            FillFallback(result, run);
            return result;
        }

        static void FillFallback(List<RogueOption> result, RunState run)
        {
            var fallbacks = new[]
            {
                new RogueOption { Key = "fallback_atk", Kind = ERogueRewardKind.Stat, Title = "加餐练功", Desc = "随机一人攻击+1.5", Stat = "atk", Value = 1.5f },
                new RogueOption { Key = "fallback_hp", Kind = ERogueRewardKind.Stat, Title = "多睡一觉", Desc = "随机一人生命+8", Stat = "hp", Value = 8f },
                new RogueOption { Key = "fallback_heal", Kind = ERogueRewardKind.Recovery, Title = "创可贴", Desc = "全体回复30%", Value = 0.3f },
            };
            foreach (var fallback in fallbacks)
            {
                if (result.Count >= 3) break;
                bool exists = result.Exists(item => item.Key == fallback.Key);
                if (!exists) result.Add(fallback);
            }
        }
    }
}
