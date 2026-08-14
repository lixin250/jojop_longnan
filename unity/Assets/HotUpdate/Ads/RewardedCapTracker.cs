using System;
using UnityEngine;

namespace JojoP.Ads
{
    /// <summary>
    /// 激励视频每日次数上限（防刷 eCPM）。
    /// Daily rewarded cap to protect eCPM.
    /// </summary>
    public sealed class RewardedCapTracker
    {
        const string PrefKeyDay = "jojop.rewarded.day";
        const string PrefKeyCount = "jojop.rewarded.count";

        int _cap;

        public RewardedCapTracker(int dailyCap) => SetCap(dailyCap);

        public void SetCap(int dailyCap) => _cap = Mathf.Max(0, dailyCap);

        public int RemainingToday
        {
            get
            {
                EnsureDay();
                return Mathf.Max(0, _cap - PlayerPrefs.GetInt(PrefKeyCount, 0));
            }
        }

        public bool CanShow => RemainingToday > 0;

        public void RecordShow()
        {
            EnsureDay();
            PlayerPrefs.SetInt(PrefKeyCount, PlayerPrefs.GetInt(PrefKeyCount, 0) + 1);
            PlayerPrefs.Save();
        }

        void EnsureDay()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (PlayerPrefs.GetString(PrefKeyDay, "") == today) return;

            PlayerPrefs.SetString(PrefKeyDay, today);
            PlayerPrefs.SetInt(PrefKeyCount, 0);
            PlayerPrefs.Save();
        }
    }
}
