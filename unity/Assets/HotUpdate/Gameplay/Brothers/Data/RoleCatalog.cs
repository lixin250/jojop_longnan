using System;
using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>TbRoleList → BrotherDef；章节/敌人主题仍用 GameTables。</summary>
    public static class RoleCatalog
    {
        public const string StarterId = "CS_lixin";

        static readonly Dictionary<string, BrotherDef> Cache = new Dictionary<string, BrotherDef>();

        public static void Rebuild()
        {
            Cache.Clear();
            if (!CfgTables.Ready) return;

            foreach (var role in CfgTables.Tables.TbRoleList.DataList)
            {
                if (role.Camp != EUnitCamp.Hero) continue;
                Cache[role.Id] = ToDef(role);
            }
        }

        public static BrotherDef FindBrother(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.Count == 0 && CfgTables.Ready) Rebuild();
            return Cache.TryGetValue(id, out var d) ? d : null;
        }

        public static RoleList FindRole(string id)
        {
            if (!CfgTables.Ready || string.IsNullOrEmpty(id)) return null;
            return CfgTables.Tables.TbRoleList.GetOrDefault(id);
        }

        public static IReadOnlyList<string> RecruitPoolIds()
        {
            if (Cache.Count == 0 && CfgTables.Ready) Rebuild();
            var list = new List<string>();
            foreach (var kv in Cache)
            {
                var role = FindRole(kv.Key);
                if (role == null) continue;
                if (kv.Key == StarterId) continue;
                if (!role.Recruitable) continue;
                list.Add(kv.Key);
            }

            list.Sort((a, b) =>
            {
                var ra = FindRole(a);
                var rb = FindRole(b);
                return (ra?.Sort ?? 0).CompareTo(rb?.Sort ?? 0);
            });
            return list;
        }

        static BrotherDef ToDef(RoleList role)
        {
            var tags = MapTags(role);
            var skillIds = role.SkillIds != null ? role.SkillIds.ToArray() : Array.Empty<string>();
            string primaryJob = PickPrimaryJobSkill(skillIds);
            var education = CfgTables.Tables.TbEducationProgram.GetOrDefault(role.EducationLevel);
            var route = CfgTables.Tables.TbLifeRouteGrowth.GetOrDefault(role.LifeRoute);
            var career = CfgTables.Tables.TbCareerGrowth.GetOrDefault(role.CareerSector);
            return new BrotherDef
            {
                Id = role.Id,
                DisplayName = role.Name,
                Tags = tags,
                BaseHp = role.BaseHp,
                BaseAtk = role.BaseAtk,
                BaseMove = role.BaseMove,
                BaseDefense = role.BaseDefense,
                CritRate = role.CritRate,
                CritDamage = role.CritDamage,
                AttackInterval = role.AttackInterval,
                EducationLevel = role.EducationLevel,
                LifeRoute = role.LifeRoute,
                CareerSector = role.CareerSector,
                JobSkillDelayYears = DelayYears(education, route, tags),
                GraduationSkillMul = education?.GraduationSkillMul ?? 1f,
                CareerHpMul = career?.HpMul ?? 1f,
                CareerAtkMul = career?.AtkMul ?? 1f,
                CareerMoveMul = career?.MoveMul ?? 1f,
                CareerSkillCdMul = career?.SkillCdMul ?? 1f,
                JobSkillId = primaryJob,
                SkillIds = skillIds
            };
        }

        static string PickPrimaryJobSkill(string[] skillIds)
        {
            if (!CfgTables.Ready || skillIds == null) return null;
            foreach (var id in skillIds)
            {
                var sk = CfgTables.Tables.TbSkillIndex.GetOrDefault(id);
                if (sk == null) continue;
                if (sk.ShowTags != null && sk.ShowTags.Contains(ESkillShowTag.Job))
                    return id;
            }

            return skillIds.Length > 0 ? skillIds[0] : null;
        }

        static BrotherTag[] MapTags(RoleList role)
        {
            var list = new List<BrotherTag>();
            if (role.FactionTags == null) return Array.Empty<BrotherTag>();

            foreach (var f in role.FactionTags)
            {
                switch (f)
                {
                    case EFactionTag.Mechanical: list.Add(BrotherTag.Mechanical); list.Add(BrotherTag.VehicleEng); break;
                    case EFactionTag.Civil: list.Add(BrotherTag.CivilEng); break;
                    case EFactionTag.Medical: list.Add(BrotherTag.Doctor); break;
                    case EFactionTag.Official: list.Add(BrotherTag.CivilServant); break;
                    case EFactionTag.Startup: list.Add(BrotherTag.Startup); break;
                    case EFactionTag.Finance: list.Add(BrotherTag.Bank); break;
                    case EFactionTag.Energy: list.Add(BrotherTag.Energy); break;
                    case EFactionTag.Internet: list.Add(BrotherTag.Internet); break;
                    case EFactionTag.Tobacco: list.Add(BrotherTag.Tobacco); break;
                }
            }

            if (role.EducationLevel == EEducationLevel.Master)
                list.Add(BrotherTag.Master);
            else if (role.EducationLevel == EEducationLevel.Doctor)
                list.Add(BrotherTag.Phd);

            if (role.LifeRoute == ELifeRoute.CivilExam)
                list.Add(BrotherTag.Kaogong);

            if (role.Id == "Dr_xiao" && !list.Contains(BrotherTag.Orthopedics))
                list.Add(BrotherTag.Orthopedics);

            return list.ToArray();
        }

        static int DelayYears(EducationProgram education, LifeRouteGrowth route, BrotherTag[] tags)
        {
            int delay = Math.Max(education?.JobDelayYears ?? 0, route?.WaitDefaultYears ?? 0);
            foreach (var t in tags)
                delay = Math.Max(delay, GameTables.TagDefaultDelay(t));
            return delay;
        }
    }
}
