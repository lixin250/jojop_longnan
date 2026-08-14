using System;
using System.Collections.Generic;
using JojoP.Cfg;

namespace JojoP.Gameplay.Brothers
{
    [Serializable]
    public sealed class BrotherDef
    {
        public string Id;
        public string DisplayName;
        public BrotherTag[] Tags;
        public float BaseHp = 40f;
        public float BaseAtk = 6f;
        public float BaseMove = 2.2f;
        public float BaseDefense;
        public float CritRate;
        public float CritDamage = 1.5f;
        public float AttackInterval = 0.4f;
        public EEducationLevel EducationLevel;
        public ELifeRoute LifeRoute;
        public ECareerSector CareerSector;
        public int JobSkillDelayYears;
        public float GraduationSkillMul = 1f;
        public float CareerHpMul = 1f;
        public float CareerAtkMul = 1f;
        public float CareerMoveMul = 1f;
        public float CareerSkillCdMul = 1f;
        public string JobSkillId;
        public string[] SkillIds;
    }

    [Serializable]
    public sealed class JobSkillDef
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public BrotherTag RequiredTag;
    }

    [Serializable]
    public sealed class EnemyThemeDef
    {
        public string Id;
        public string DisplayName;
        public float HpMul = 1f;
        public float AtkMul = 1f;
        public float MoveMul = 1f;
        public bool IsElite;
        public bool HighArmor;
        public string Tier; // primary / high / society
    }

    [Serializable]
    public sealed class SceneDef
    {
        public string Id;
        public string DisplayName;
        public ChapterId[] Chapters;
        public string[] EnemyThemeIds;
    }

    [Serializable]
    public sealed class ChapterDef
    {
        public ChapterId Id;
        public string DisplayName;
        public string HubPlace;
        public int MaxGradeYears;
        public bool UsesSemesterBreaks;
        public string[] ScenePool;
        public bool LockedByDefault;
    }

    /// <summary>静态表：竖切可玩数据 + 高章/社会预埋。</summary>
    public static class GameTables
    {
        public const int MaxSquad = 5;
        public const int StartSquad = 1;
        public const int StaminaPerRun = 1;
        public const int DailyStaminaCap = 10;

        // 数值目标：小学小1 单人能清 4 只杂兵并剩约半血；后期靠人数/难度爬坡。
        public static readonly BrotherDef[] Brothers =
        {
            new BrotherDef
            {
                Id = "player", DisplayName = "我",
                Tags = Array.Empty<BrotherTag>(),
                BaseHp = 110f, BaseAtk = 14f, BaseMove = 2.7f,
                JobSkillDelayYears = 0, JobSkillId = "startup_risk"
            },
            new BrotherDef
            {
                Id = "xiebo", DisplayName = "谢博",
                Tags = new[] { BrotherTag.Phd, BrotherTag.VehicleEng, BrotherTag.Mechanical },
                BaseHp = 95f, BaseAtk = 16f, BaseMove = 2.4f,
                JobSkillDelayYears = 5, JobSkillId = "mech_dismantle"
            },
            new BrotherDef
            {
                Id = "lao_wang", DisplayName = "老王",
                Tags = new[] { BrotherTag.Kaogong, BrotherTag.CivilServant },
                BaseHp = 125f, BaseAtk = 11f, BaseMove = 2.2f,
                JobSkillDelayYears = 2, JobSkillId = "civil_aura"
            },
            new BrotherDef
            {
                Id = "xiao_lin", DisplayName = "小林",
                Tags = new[] { BrotherTag.Master, BrotherTag.Doctor },
                BaseHp = 100f, BaseAtk = 12f, BaseMove = 2.5f,
                JobSkillDelayYears = 2, JobSkillId = "clinic_heal"
            },
            new BrotherDef
            {
                Id = "da_cheng", DisplayName = "大成",
                Tags = new[] { BrotherTag.CivilEng },
                BaseHp = 150f, BaseAtk = 13f, BaseMove = 2.1f,
                JobSkillDelayYears = 0, JobSkillId = "civil_smash"
            },
            new BrotherDef
            {
                Id = "xiao_gu", DisplayName = "小骨",
                Tags = new[] { BrotherTag.Orthopedics },
                BaseHp = 105f, BaseAtk = 11f, BaseMove = 2.45f,
                JobSkillDelayYears = 0, JobSkillId = "ortho_revive"
            }
        };

        public static readonly JobSkillDef[] JobSkills =
        {
            new JobSkillDef { Id = "mech_dismantle", DisplayName = "拆了重装", Description = "对精英/高防巨伤，小怪概率解体", RequiredTag = BrotherTag.VehicleEng },
            new JobSkillDef { Id = "mech_torque", DisplayName = "扭矩全开", Description = "短时攻速冲锋撞退", RequiredTag = BrotherTag.Mechanical },
            new JobSkillDef { Id = "mech_patch", DisplayName = "应急维修", Description = "友军铁皮盾", RequiredTag = BrotherTag.Mechanical },
            new JobSkillDef { Id = "mech_summon", DisplayName = "工装召唤", Description = "召唤工程机械影助战", RequiredTag = BrotherTag.VehicleEng },
            new JobSkillDef { Id = "civil_aura", DisplayName = "体制光环", Description = "周围友军攻↑", RequiredTag = BrotherTag.CivilServant },
            new JobSkillDef { Id = "clinic_heal", DisplayName = "民心急救", Description = "治疗并偶发召唤热心市民", RequiredTag = BrotherTag.Doctor },
            new JobSkillDef { Id = "civil_smash", DisplayName = "工地砸", Description = "前排承伤范围砸", RequiredTag = BrotherTag.CivilEng },
            new JobSkillDef { Id = "ortho_revive", DisplayName = "骨科复位", Description = "缩短养伤，战中拉起", RequiredTag = BrotherTag.Orthopedics },
            new JobSkillDef { Id = "startup_risk", DisplayName = "搏一把", Description = "高风险暴击", RequiredTag = BrotherTag.Startup }
        };

        public static readonly EnemyThemeDef[] Enemies =
        {
            new EnemyThemeDef { Id = "dog", DisplayName = "流浪狗", Tier = "primary", HpMul = 0.55f, AtkMul = 0.55f, MoveMul = 0.9f },
            new EnemyThemeDef { Id = "kids_gang", DisplayName = "小团伙", Tier = "primary", HpMul = 0.75f, AtkMul = 0.7f },
            new EnemyThemeDef { Id = "bully", DisplayName = "校霸", Tier = "primary", HpMul = 1.35f, AtkMul = 1.1f, IsElite = true },
            new EnemyThemeDef { Id = "court_bully", DisplayName = "霸场哥", Tier = "middle", HpMul = 1.2f },
            new EnemyThemeDef { Id = "drunk", DisplayName = "醉汉", Tier = "high", HpMul = 1.1f, AtkMul = 1.1f },
            new EnemyThemeDef { Id = "ex", DisplayName = "前任", Tier = "high", HpMul = 1.15f },
            new EnemyThemeDef { Id = "current", DisplayName = "现任", Tier = "high", HpMul = 1.15f },
            new EnemyThemeDef { Id = "ex_of_current", DisplayName = "前任的现任", Tier = "high", HpMul = 1.2f },
            new EnemyThemeDef { Id = "current_of_ex", DisplayName = "现任的前任", Tier = "high", HpMul = 1.2f },
            new EnemyThemeDef { Id = "reunion_elite", DisplayName = "复合冲锋", Tier = "society", HpMul = 2f, IsElite = true },
            new EnemyThemeDef { Id = "auntie", DisplayName = "劝分七大姑", Tier = "society", AtkMul = 0.9f },
            new EnemyThemeDef { Id = "ktv_king", DisplayName = "麦霸", Tier = "society", IsElite = true, HpMul = 1.6f },
            new EnemyThemeDef { Id = "spa_scam", DisplayName = "碰瓷推销", Tier = "society" },
            new EnemyThemeDef { Id = "fake_mgr", DisplayName = "假经理", Tier = "society", IsElite = true },
            new EnemyThemeDef { Id = "queue_cutter", DisplayName = "加塞", Tier = "high" },
            new EnemyThemeDef { Id = "boss_lady", DisplayName = "老板娘", Tier = "society", IsElite = true, HpMul = 1.5f },
            new EnemyThemeDef { Id = "pc_snatcher", DisplayName = "抢机少年", Tier = "high", MoveMul = 1.3f },
            new EnemyThemeDef { Id = "flamer", DisplayName = "开黑喷子", Tier = "society", AtkMul = 1.2f, HpMul = 0.85f },
            new EnemyThemeDef { Id = "thug", DisplayName = "小混混", Tier = "high" },
            new EnemyThemeDef { Id = "black_bro", DisplayName = "社会黑哥", Tier = "society", IsElite = true, HighArmor = true, HpMul = 1.8f },
            new EnemyThemeDef { Id = "bass_car", DisplayName = "低音炮怪", Tier = "society", HighArmor = true, HpMul = 1.7f },
            new EnemyThemeDef { Id = "marriage_push", DisplayName = "催婚军团", Tier = "society" },
            new EnemyThemeDef { Id = "debt_kid", DisplayName = "人情债追债仔", Tier = "society" },
            new EnemyThemeDef { Id = "rival_school", DisplayName = "外校挑事团", Tier = "high" },
            new EnemyThemeDef { Id = "dean_ghost", DisplayName = "年级主任幻影", Tier = "high", AtkMul = 0.6f, HpMul = 1.3f }
        };

        public static readonly SceneDef[] Scenes =
        {
            new SceneDef
            {
                Id = "longxiang", DisplayName = "龙翔广场",
                Chapters = new[] { ChapterId.Primary },
                // 校霸只在小3+ 由战斗层加权加入；前期只用狗/小团伙
                EnemyThemeIds = new[] { "dog", "dog", "dog", "kids_gang" }
            },
            new SceneDef
            {
                Id = "stadium", DisplayName = "龙南体育场",
                Chapters = new[] { ChapterId.Middle },
                EnemyThemeIds = new[] { "court_bully", "thug", "kids_gang" }
            },
            new SceneDef
            {
                Id = "night_market", DisplayName = "步行街夜市",
                Chapters = new[] { ChapterId.High },
                EnemyThemeIds = new[] { "rival_school", "drunk", "queue_cutter", "ex", "current" }
            },
            new SceneDef
            {
                Id = "internet_cafe", DisplayName = "网吧",
                Chapters = new[] { ChapterId.High, ChapterId.Society },
                EnemyThemeIds = new[] { "pc_snatcher", "flamer", "thug", "black_bro" }
            },
            new SceneDef
            {
                Id = "late_night_stall", DisplayName = "夜宵摊",
                Chapters = new[] { ChapterId.High, ChapterId.Society },
                EnemyThemeIds = new[] { "drunk", "queue_cutter", "boss_lady", "ex", "current" }
            },
            new SceneDef
            {
                Id = "ktv", DisplayName = "KTV",
                Chapters = new[] { ChapterId.Society },
                EnemyThemeIds = new[] { "ktv_king", "drunk", "ex_of_current", "current_of_ex" }
            },
            new SceneDef
            {
                Id = "foot_spa", DisplayName = "洗脚城",
                Chapters = new[] { ChapterId.Society },
                EnemyThemeIds = new[] { "spa_scam", "fake_mgr", "auntie" }
            },
            new SceneDef
            {
                Id = "station_home", DisplayName = "车站/围屋",
                Chapters = new[] { ChapterId.University },
                EnemyThemeIds = new[] { "thug", "drunk", "debt_kid" }
            }
        };

        public static readonly ChapterDef[] Chapters =
        {
            new ChapterDef
            {
                Id = ChapterId.Primary, DisplayName = "第一章·小学", HubPlace = "龙翔广场",
                MaxGradeYears = 6, UsesSemesterBreaks = true,
                ScenePool = new[] { "longxiang" }, LockedByDefault = false
            },
            new ChapterDef
            {
                Id = ChapterId.Middle, DisplayName = "第二章·初中", HubPlace = "龙南体育场",
                MaxGradeYears = 3, UsesSemesterBreaks = true,
                ScenePool = new[] { "stadium" }, LockedByDefault = true
            },
            new ChapterDef
            {
                Id = ChapterId.High, DisplayName = "第三章·高中", HubPlace = "夜市/网吧/夜宵摊",
                MaxGradeYears = 3, UsesSemesterBreaks = true,
                ScenePool = new[] { "night_market", "internet_cafe", "late_night_stall" }, LockedByDefault = true
            },
            new ChapterDef
            {
                Id = ChapterId.University, DisplayName = "第四章·大学", HubPlace = "车站/围屋",
                MaxGradeYears = 4, UsesSemesterBreaks = true,
                ScenePool = new[] { "station_home" }, LockedByDefault = true
            },
            new ChapterDef
            {
                Id = ChapterId.Society, DisplayName = "出社会·过年", HubPlace = "夜宵摊/KTV/洗脚城/网吧",
                MaxGradeYears = 99, UsesSemesterBreaks = false,
                ScenePool = new[] { "late_night_stall", "ktv", "foot_spa", "internet_cafe" }, LockedByDefault = true
            }
        };

        public static int GetJobDelayYears(BrotherDef def)
        {
            if (def == null || def.Tags == null || def.Tags.Length == 0)
                return 0;

            int delay = def.JobSkillDelayYears;
            foreach (var tag in def.Tags)
            {
                delay = Math.Max(delay, TagDefaultDelay(tag));
            }

            return delay;
        }

        public static int TagDefaultDelay(BrotherTag tag)
        {
            switch (tag)
            {
                case BrotherTag.Kaogong: return 2;
                case BrotherTag.Master: return 2;
                case BrotherTag.Phd: return 5;
                default: return 0;
            }
        }

        public static float TagDamageTakenMul(BrotherTag tag)
        {
            switch (tag)
            {
                case BrotherTag.Kaogong: return 0.92f;
                case BrotherTag.Master: return 0.94f;
                case BrotherTag.Phd: return 0.9f;
                default: return 1f;
            }
        }

        public static BrotherDef FindBrother(string id)
        {
            var fromCfg = RoleCatalog.FindBrother(id);
            if (fromCfg != null) return fromCfg;

            // 旧 id 别名 → 表角色
            if (id == "player") return RoleCatalog.FindBrother(RoleCatalog.StarterId);
            if (id == "xiebo") return RoleCatalog.FindBrother("todo_shitbro");
            if (id == "lao_wang") return RoleCatalog.FindBrother("GWY_wang");
            if (id == "xiao_lin") return RoleCatalog.FindBrother("Dr_chen");
            if (id == "da_cheng") return RoleCatalog.FindBrother("TM_yugu");
            if (id == "xiao_gu") return RoleCatalog.FindBrother("Dr_xiao");

            foreach (var b in Brothers)
                if (b.Id == id) return b;
            return null;
        }

        public static EnemyThemeDef FindEnemy(string id)
        {
            foreach (var e in Enemies)
                if (e.Id == id) return e;
            return null;
        }

        public static SceneDef FindScene(string id)
        {
            foreach (var s in Scenes)
                if (s.Id == id) return s;
            return null;
        }

        public static ChapterDef FindChapter(ChapterId id)
        {
            foreach (var c in Chapters)
                if (c.Id == id) return c;
            return Chapters[0];
        }

        public static JobSkillDef FindJobSkill(string id)
        {
            foreach (var s in JobSkills)
                if (s.Id == id) return s;
            return null;
        }

        public static IReadOnlyList<string> RecruitPoolIds()
        {
            var fromCfg = RoleCatalog.RecruitPoolIds();
            if (fromCfg != null && fromCfg.Count > 0) return fromCfg;

            var list = new List<string>();
            foreach (var b in Brothers)
            {
                if (b.Id == "player") continue;
                list.Add(b.Id);
            }

            return list;
        }
    }
}
