using System.Text;
using JojoP.Cfg;
using JojoP.Config;

namespace JojoP.Gameplay.Brothers
{
    public static class HeroDossier
    {
        public static string Body(RoleList role, MetaProgress meta)
        {
            if (role == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine(role.Desc);
            sb.AppendLine();
            string path = PathLine(role);
            if (!string.IsNullOrEmpty(path))
                sb.AppendLine(path);
            if (role.FactionTags != null && role.FactionTags.Count > 0)
            {
                sb.Append("派系 ");
                for (int i = 0; i < role.FactionTags.Count; i++)
                {
                    if (i > 0) sb.Append(" · ");
                    sb.Append(TagName(role.FactionTags[i]));
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            if (role.SkillIds != null)
            {
                foreach (var id in role.SkillIds)
                {
                    var sk = CfgTables.Ready ? CfgTables.Tables.TbSkillIndex.GetOrDefault(id) : null;
                    if (sk == null)
                    {
                        sb.AppendLine("· " + id);
                        continue;
                    }

                    sb.Append("· ").Append(SkillFlavor.Category(sk)).Append("  ").Append(sk.Name);
                    if (sk.Cd > 0.01f) sb.Append("  CD").Append(sk.Cd.ToString("0.#")).Append("s");
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(sk.Desc))
                        sb.Append("  ").Append(sk.Desc).AppendLine();
                }
            }

            sb.AppendLine();
            sb.Append(HeroUnlock.Hint(role, meta));
            return sb.ToString().TrimEnd();
        }

        static string PathLine(RoleList role)
        {
            string edu = role.EducationLevel switch
            {
                EEducationLevel.Master => "硕士",
                EEducationLevel.Doctor => "博士",
                EEducationLevel.Bachelor => "本科",
                _ => ""
            };
            string route = role.LifeRoute switch
            {
                ELifeRoute.ContinueStudy => "继续读",
                ELifeRoute.CivilExam => "考公/编",
                ELifeRoute.Startup => "创业",
                ELifeRoute.DirectWork => "直接上班",
                _ => ""
            };
            if (string.IsNullOrEmpty(edu) && string.IsNullOrEmpty(route)) return "";
            return $"路径 {edu} · {route}".Trim();
        }

        static string TagName(EFactionTag tag)
        {
            return tag switch
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
                _ => tag.ToString()
            };
        }
    }
}
