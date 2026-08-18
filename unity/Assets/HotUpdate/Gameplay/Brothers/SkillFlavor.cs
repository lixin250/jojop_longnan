using JojoP.Cfg;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>局内展示用的技能分类口吻。机制仍看 Job/Campus/Fusion 标签。</summary>
    public static class SkillFlavor
    {
        public const string Campus = "课间绝活";
        public const string Job = "工牌绝活";
        public const string Fusion = "兄弟连招";
        public const string Loot = "地摊技";
        public const string Trait = "性格技";
        public const string Summon = "临时上场";

        public static string Category(SkillIndex skill)
        {
            if (skill?.ShowTags != null)
            {
                if (skill.ShowTags.Contains(ESkillShowTag.Fusion)) return Fusion;
                if (skill.ShowTags.Contains(ESkillShowTag.Job)) return Job;
                if (skill.ShowTags.Contains(ESkillShowTag.Campus)) return Campus;
            }

            string owner = skill?.OwnerId ?? "";
            if (owner == "loot") return Loot;
            if (owner == "mow" || owner == "eng_xie" || owner == "temp_hire" || owner == "temp_old")
                return Summon;
            return Trait;
        }
    }
}
