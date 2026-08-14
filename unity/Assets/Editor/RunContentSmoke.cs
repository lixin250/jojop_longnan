using System;
using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;
using JojoP.Gameplay.Brothers;
using UnityEngine;

namespace JojoP.EditorTools
{
    /// <summary>命令行：Unity -batchmode -executeMethod JojoP.EditorTools.RunContentSmoke.Run。</summary>
    public static class RunContentSmoke
    {
        public static void Run()
        {
            Require(CfgTables.TryLoad(), "CfgTables load failed");
            RoleCatalog.Rebuild();
            var meta = new MetaProgress();

            var oban = RoleCatalog.FindBrother("Energy_oban");
            var uzi = RoleCatalog.FindBrother("Energy_uzi");
            Require(oban != null && oban.JobSkillDelayYears == 1,
                "Production-energy role should unlock early after high school");
            Require(uzi != null && uzi.JobSkillDelayYears >= 2 && uzi.GraduationSkillMul > oban.GraduationSkillMul,
                "Master storage-energy role should unlock later with a stronger graduation multiplier");
            Require(Math.Abs(oban.AttackInterval - 0.4f) < 0.001f &&
                    oban.CritDamage >= 1f && oban.BaseDefense >= 0f,
                "Advanced role stats should flow through RoleCatalog");
            var defenseProbe = new GameObject("DefenseProbe").AddComponent<BattleUnit>();
            defenseProbe.MaxHp = 100f;
            defenseProbe.Hp = 100f;
            defenseProbe.Defense = 100f;
            defenseProbe.ApplyDamage(20f);
            Require(Math.Abs(defenseProbe.Hp - 90f) < 0.01f,
                "100 defense should halve incoming damage");
            UnityEngine.Object.DestroyImmediate(defenseProbe.gameObject);
            Require(CfgTables.Tables.TbSkillIndex.GetOrDefault("tm_junjun_inspect") != null,
                "Supervisor inspection skill should be generated");
            Require(CfgTables.Tables.TbSkillIndex.GetOrDefault("dr_xiao_drunk") != null &&
                    CfgTables.Tables.TbSkillIndex.GetOrDefault("cs_ayun_gaoliao") != null,
                "Promoted free-form skill drafts should be generated");
            Require(CfgTables.Tables.TbFusionSkill.GetOrDefault("bond_joint_inspection") != null &&
                    CfgTables.Tables.TbFusionSkill.GetOrDefault("bond_supply_chain") != null,
                "New cross-industry bonds should be generated");

            var primary = new RunState();
            primary.Bootstrap(ChapterId.Primary, meta);
            Require(primary.BaseActiveSlots == 3, "Primary base slots should be 3");
            Require(primary.AdExtraSlotLimit == 2, "Primary ad slot limit should be 2");

            MeetToFull(primary, "CS_ayun", meta);
            MeetToFull(primary, "BZ_jihong", meta);
            Require(primary.RecruitedCount == 3, "Affinity recruits should fill three base slots");

            MeetToFull(primary, "GWY_wang", meta);
            Require(primary.RecruitedCount == 3 && primary.ReadyToJoin.Contains("GWY_wang"),
                "Full team should preserve ready-to-join friend");
            Require(primary.TryAddAdSlot(meta, out _), "First rewarded expansion should succeed");
            Require(primary.RecruitedCount == 4, "First rewarded expansion should auto-join ready friend");

            MeetToFull(primary, "YH_hangu", meta);
            Require(primary.ReadyToJoin.Contains("YH_hangu"), "Fifth friend should wait at full affinity");
            Require(primary.TryAddAdSlot(meta, out _), "Second rewarded expansion should succeed");
            Require(primary.RecruitedCount == 5 && primary.ActiveSlotLimit == 5,
                "Primary rewarded expansion should reach five active friends");
            Require(!primary.TryAddAdSlot(meta, out _), "Third rewarded expansion must be rejected");

            var society = new RunState();
            society.Bootstrap(ChapterId.Society, meta);
            Require(society.HasUnlimitedSlots && society.ActiveSlotLimit == int.MaxValue,
                "Society chapter should honor -1 unlimited slots");
            Require(!society.TryAddAdSlot(meta, out _), "Unlimited chapter must not consume rewarded expansion");
            Require(TimelineCatalog.Current(society)?.Id == "graduation_2019",
                "Society year one should use the 2019 graduation split");
            society.SocietyYearIndex = 2;
            Require(TimelineCatalog.Current(society)?.Id == "city_2020",
                "Society year two should use county-to-city milestone");
            society.SocietyYearIndex = 3;
            Require(TimelineCatalog.Current(society)?.Id == "hsr_open_2021",
                "Society year three should unlock the high-speed-rail milestone");

            var high = new RunState();
            high.Bootstrap(ChapterId.High, meta);
            Require(TimelineCatalog.Current(high)?.Id == "economy_zone_2013",
                "High school should begin at the 2013 development-zone milestone");
            Require(TimelineCatalog.EncounterWeightMultiplier("todo_shitbro", high) > 1f,
                "2013 industry milestone should boost mechanical encounters");

            foreach (var timeline in CfgTables.Tables.TbTimelineEvent.DataList)
            {
                Require(!timeline.Verified || !string.IsNullOrEmpty(timeline.SourceUrl),
                    $"Verified timeline event {timeline.Id} must retain its source");
                Require(timeline.SourceKind != EHistorySourceKind.Oral || !timeline.Verified,
                    $"Oral timeline event {timeline.Id} must not be marked verified");
            }

            var rewards = new RunRewardSystem();
            primary.ResetRewardPage();
            var first = rewards.RollThree(primary, reroll: false);
            Require(first.Count == 3, "Reward page should contain three choices");
            Require(primary.TryUseRewardReroll(), "First rewarded reroll should be available");
            var second = rewards.RollThree(primary, reroll: true);
            Require(second.Count == 3, "Rerolled reward page should contain three choices");
            var oldKeys = new HashSet<string>();
            foreach (var option in first) oldKeys.Add(option.Key);
            foreach (var option in second)
                Require(!oldKeys.Contains(option.Key), $"Reroll repeated option {option.Key}");
            Require(!primary.TryUseRewardReroll(), "Second reroll on same page must be rejected");

            rewards.Apply(new RogueOption
            {
                Key = "smoke_equipment",
                Kind = ERogueRewardKind.Equipment,
                Title = "竹竿一捅",
                RefId = "equip_pierce",
            }, primary, meta);
            Require(primary.EquipmentId == "equip_pierce", "Equipment reward should replace attack core");

            var target = primary.Squad[0];
            rewards.Apply(new RogueOption
            {
                Key = "smoke_loot",
                Kind = ERogueRewardKind.LootSkill,
                RefId = "loot_stone_throw",
                TargetRoleId = target.DefId,
            }, primary, meta);
            Require(target.LootSkillId == "loot_stone_throw", "Loot skill should occupy temporary skill slot");

            Debug.Log("[JojoP] RUN_CONTENT_SMOKE_OK primary=5 society=timeline reroll=unique");
        }

        static void MeetToFull(RunState run, string roleId, MetaProgress meta)
        {
            for (int i = 0; i < run.AffinityNeeded; i++)
                run.AddAffinity(roleId, 1, meta);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[RunContentSmoke] " + message);
        }
    }
}
