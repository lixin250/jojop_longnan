using System.Collections.Generic;
using System.Text;
using JojoP.Cfg;
using JojoP.Config;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 局外初始英雄解锁：RoleList.unlock_conditions 全部满足才可选。
    /// type = chapter / grade / archive / train / kill / bond / favor / renown。
    /// </summary>
    public static class HeroUnlock
    {
        public const string TypeChapter = "chapter";
        public const string TypeGrade = "grade";
        public const string TypeArchive = "archive";
        public const string TypeTrain = "train";
        public const string TypeKill = "kill";
        public const string TypeBond = "bond";
        public const string TypeFavor = "favor";
        public const string TypeRenown = "renown";

        public static IReadOnlyList<RoleList> StarterCandidates()
        {
            var list = new List<RoleList>();
            if (!CfgTables.Ready) return list;
            foreach (var role in CfgTables.Tables.TbRoleList.DataList)
            {
                if (role.Camp != EUnitCamp.Hero) continue;
                if (!role.StarterSelectable) continue;
                list.Add(role);
            }

            list.Sort((a, b) => a.Sort.CompareTo(b.Sort));
            return list;
        }

        public static bool IsUnlocked(RoleList role, MetaProgress meta)
        {
            if (role == null || meta == null) return false;
            if (role.UnlockConditions == null || role.UnlockConditions.Count == 0) return true;
            foreach (var cond in role.UnlockConditions)
            {
                if (!Match(cond, meta)) return false;
            }

            return true;
        }

        public static bool CanSelect(string roleId, MetaProgress meta)
        {
            var role = RoleCatalog.FindRole(roleId);
            if (role == null || !role.StarterSelectable) return false;
            return IsUnlocked(role, meta);
        }

        public static string Hint(RoleList role, MetaProgress meta)
        {
            if (role == null) return "未知英雄";
            if (!role.StarterSelectable) return "不可作为初始英雄";
            if (IsUnlocked(role, meta)) return "已解锁，可作出征初始";

            var sb = new StringBuilder();
            sb.Append("未解锁：");
            bool first = true;
            if (role.UnlockConditions != null)
            {
                foreach (var cond in role.UnlockConditions)
                {
                    if (Match(cond, meta)) continue;
                    if (!first) sb.Append(" 且 ");
                    sb.Append(Describe(cond, meta));
                    first = false;
                }
            }

            return sb.ToString();
        }

        static bool Match(UnlockCondition cond, MetaProgress meta)
        {
            if (string.IsNullOrEmpty(cond.Type)) return true;
            switch (cond.Type)
            {
                case TypeChapter:
                    return meta.UnlockedChapter >= cond.P1;
                case TypeGrade:
                    return meta.UnlockedChapter > cond.P1
                           || (meta.UnlockedChapter == cond.P1 && meta.HighestGradeReached >= cond.P2);
                case TypeArchive:
                    return meta.ArchiveCount >= cond.P1;
                case TypeTrain:
                    return meta.TrainSpent >= cond.P1;
                case TypeKill:
                    return meta.TotalKills >= cond.P1;
                case TypeBond:
                    return meta.BondCount(cond.P1) >= cond.P2;
                case TypeFavor:
                    return meta.Favor >= cond.P1;
                case TypeRenown:
                    return meta.Renown >= cond.P1;
                default:
                    return false;
            }
        }

        static string Describe(UnlockCondition cond, MetaProgress meta)
        {
            switch (cond.Type)
            {
                case TypeChapter:
                    return $"解锁{ChapterName(cond.P1)}（{meta.UnlockedChapter}/{cond.P1}）";
                case TypeGrade:
                    return $"在{ChapterName(cond.P1)}走到第{cond.P2}学年（{meta.HighestGradeReached}/{cond.P2}）";
                case TypeArchive:
                    return $"图鉴收录≥{cond.P1}人（{meta.ArchiveCount}/{cond.P1}）";
                case TypeTrain:
                    return $"累计花费培养点≥{cond.P1}（{meta.TrainSpent}/{cond.P1}）";
                case TypeKill:
                    return $"累计击杀≥{cond.P1}（{meta.TotalKills}/{cond.P1}）";
                case TypeBond:
                    return $"{FactionName(cond.P1)}羁绊达成≥{cond.P2}次（{meta.BondCount(cond.P1)}/{cond.P2}）";
                case TypeFavor:
                    return $"人情≥{cond.P1}（{meta.Favor}/{cond.P1}）";
                case TypeRenown:
                    return $"声望≥{cond.P1}（{meta.Renown}/{cond.P1}）";
                default:
                    return $"未知条件 {cond.Type}";
            }
        }

        static string ChapterName(int id)
        {
            return id switch
            {
                1 => "小学",
                2 => "初中",
                3 => "高中",
                4 => "大学",
                5 => "出社会",
                _ => $"第{id}章"
            };
        }

        static string FactionName(int tag)
        {
            if (!System.Enum.IsDefined(typeof(EFactionTag), tag)) return $"派系{tag}";
            var f = (EFactionTag)tag;
            return f switch
            {
                EFactionTag.Mechanical => "机械",
                EFactionTag.Civil => "土木",
                EFactionTag.Medical => "医疗",
                EFactionTag.Academic => "学术",
                EFactionTag.Official => "公职",
                EFactionTag.Startup => "创业",
                EFactionTag.Street => "街头",
                EFactionTag.Finance => "银行",
                EFactionTag.Energy => "能源",
                EFactionTag.Internet => "互联网",
                EFactionTag.Tobacco => "烟草",
                _ => f.ToString()
            };
        }
    }
}
