using System;
using System.Collections.Generic;
using JojoP.Cfg;
using JojoP.Config;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 大事件按章节顺序触发，年份仅作史实锚点；允许同一批兄弟跨年代发挥已解锁的行业能力。
    /// </summary>
    public static class TimelineCatalog
    {
        public static TimelineEvent Current(RunState run)
        {
            if (run == null || !CfgTables.Ready) return null;
            var candidates = new List<TimelineEvent>();
            string chapter = run.Chapter.ToString();
            foreach (var item in CfgTables.Tables.TbTimelineEvent.DataList)
                if (item.ChapterId == chapter) candidates.Add(item);
            if (candidates.Count == 0) return null;

            candidates.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            int progress = run.Chapter == ChapterId.Society
                ? run.SocietyYearIndex - 1
                : run.GradeYear - 1;
            return candidates[Math.Min(Math.Max(0, progress), candidates.Count - 1)];
        }

        public static float EncounterWeightMultiplier(string roleId, RunState run)
        {
            var timeline = Current(run);
            if (timeline?.BoostTags == null || timeline.BoostTags.Count == 0) return 1f;
            var role = RoleCatalog.FindRole(roleId);
            if (role?.FactionTags == null) return 1f;

            foreach (var roleTag in role.FactionTags)
                foreach (var boostTag in timeline.BoostTags)
                    if (roleTag == boostTag) return Math.Max(1f, timeline.EncounterWeightMul);
            return 1f;
        }
    }
}
