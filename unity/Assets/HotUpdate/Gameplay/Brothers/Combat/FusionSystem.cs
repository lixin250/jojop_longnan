using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>扫场上派系 → 授予 FusionSkill.grant_skill_id。</summary>
    public static class FusionSystem
    {
        public static void Refresh(List<BattleUnit> brothers, SkillCastSystem skills)
        {
            if (!CfgTables.Ready || brothers == null || skills == null) return;

            var present = new HashSet<EFactionTag>();
            bool anyJob = false;
            foreach (var u in brothers)
            {
                if (u == null || !u.IsAlive || u.BoundBrother == null) continue;
                var role = RoleCatalog.FindRole(u.BoundBrother.DefId);
                if (role?.FactionTags == null) continue;
                foreach (var t in role.FactionTags) present.Add(t);
                if (u.BoundBrother.JobSkillUnlocked) anyJob = true;
            }

            int grantBudget = System.Math.Max(1, (brothers.Count + 2) / 3);
            int granted = 0;
            foreach (var bond in CfgTables.Tables.TbFusionSkill.DataList)
            {
                if (bond.RequiredTags == null || bond.RequiredTags.Count == 0) continue;
                bool ok = true;
                foreach (var need in bond.RequiredTags)
                {
                    if (!present.Contains(need))
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok) continue;
                if (bond.RequireJobUnlocked && !anyJob) continue;

                // 授给第一个带 required 首标签的兄弟
                var firstTag = bond.RequiredTags[0];
                foreach (var u in brothers)
                {
                    if (u == null || !u.IsAlive || u.BoundBrother == null) continue;
                    var role = RoleCatalog.FindRole(u.BoundBrother.DefId);
                    if (role?.FactionTags == null || !role.FactionTags.Contains(firstTag)) continue;
                    skills.GrantSkill(u, bond.GrantSkillId);
                    granted++;
                    break;
                }
                if (granted >= grantBudget) break;
            }
        }
    }
}
