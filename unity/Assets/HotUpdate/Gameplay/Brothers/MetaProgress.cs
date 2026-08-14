using System;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 局外养成：体力 / 培养点 / 人情 / 声望 / 编年史解锁 / 潜力。
    /// 本地 PlayerPrefs；不进对战实时存档。
    /// </summary>
    public sealed class MetaProgress
    {
        const string PrefPrefix = "jojop.brothers.";
        const string KeyStamina = PrefPrefix + "stamina";
        const string KeyStaminaDay = PrefPrefix + "stamina.day";
        const string KeyTrain = PrefPrefix + "train";
        const string KeyFavor = PrefPrefix + "favor";
        const string KeyRenown = PrefPrefix + "renown";
        const string KeyUnlocked = PrefPrefix + "unlockedChapter";
        const string KeyPotentialHp = PrefPrefix + "pot.hp";
        const string KeyPotentialAtk = PrefPrefix + "pot.atk";
        const string KeyHealTier = PrefPrefix + "healTier";
        const string KeyHighestGrade = PrefPrefix + "highest.grade";

        public int Stamina { get; private set; }
        public int TrainPoints { get; private set; }
        public int Favor { get; private set; }
        public int Renown { get; private set; }
        public int UnlockedChapter { get; private set; } = 1;
        public int PotentialHp { get; private set; }
        public int PotentialAtk { get; private set; }
        public int HealShortenTier { get; private set; }
        public int HighestGradeReached { get; private set; }

        public void Load()
        {
            RefreshDailyStamina();
            Stamina = PlayerPrefs.GetInt(KeyStamina, GameTables.DailyStaminaCap);
            TrainPoints = PlayerPrefs.GetInt(KeyTrain, 0);
            Favor = PlayerPrefs.GetInt(KeyFavor, 0);
            Renown = PlayerPrefs.GetInt(KeyRenown, 0);
            UnlockedChapter = Mathf.Clamp(PlayerPrefs.GetInt(KeyUnlocked, 1), 1, 5);
            PotentialHp = PlayerPrefs.GetInt(KeyPotentialHp, 0);
            PotentialAtk = PlayerPrefs.GetInt(KeyPotentialAtk, 0);
            HealShortenTier = PlayerPrefs.GetInt(KeyHealTier, 0);
            HighestGradeReached = PlayerPrefs.GetInt(KeyHighestGrade, 0);
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeyStamina, Stamina);
            PlayerPrefs.SetInt(KeyStaminaDay, TodayKey());
            PlayerPrefs.SetInt(KeyTrain, TrainPoints);
            PlayerPrefs.SetInt(KeyFavor, Favor);
            PlayerPrefs.SetInt(KeyRenown, Renown);
            PlayerPrefs.SetInt(KeyUnlocked, UnlockedChapter);
            PlayerPrefs.SetInt(KeyPotentialHp, PotentialHp);
            PlayerPrefs.SetInt(KeyPotentialAtk, PotentialAtk);
            PlayerPrefs.SetInt(KeyHealTier, HealShortenTier);
            PlayerPrefs.SetInt(KeyHighestGrade, HighestGradeReached);
            PlayerPrefs.Save();
        }

        void RefreshDailyStamina()
        {
            int today = TodayKey();
            int savedDay = PlayerPrefs.GetInt(KeyStaminaDay, -1);
            if (savedDay == today) return;

            PlayerPrefs.SetInt(KeyStamina, GameTables.DailyStaminaCap);
            PlayerPrefs.SetInt(KeyStaminaDay, today);
            PlayerPrefs.Save();
        }

        static int TodayKey()
        {
            var n = DateTime.Now;
            return n.Year * 10000 + n.Month * 100 + n.Day;
        }

        public bool TrySpendStamina(int cost = GameTables.StaminaPerRun)
        {
            if (Stamina < cost) return false;
            Stamina -= cost;
            Save();
            return true;
        }

        public void AddStamina(int amount)
        {
            Stamina = Mathf.Min(GameTables.DailyStaminaCap + 5, Stamina + amount);
            Save();
        }

        public void AddTrain(int amount)
        {
            TrainPoints += Mathf.Max(0, amount);
            Save();
        }

        public void AddFavor(int amount)
        {
            Favor += Mathf.Max(0, amount);
            Save();
        }

        public void AddFavorRaw(int delta)
        {
            Favor = Mathf.Max(0, Favor + delta);
            Save();
        }

        public void AddRenown(int amount)
        {
            Renown += Mathf.Max(0, amount);
            Save();
        }

        public bool TryBuyPotentialHp(int cost = 3)
        {
            if (TrainPoints < cost) return false;
            TrainPoints -= cost;
            PotentialHp++;
            Save();
            return true;
        }

        public bool TryBuyPotentialAtk(int cost = 3)
        {
            if (TrainPoints < cost) return false;
            TrainPoints -= cost;
            PotentialAtk++;
            Save();
            return true;
        }

        public bool TryBuyHealTier(int cost = 5)
        {
            if (TrainPoints < cost || HealShortenTier >= 3) return false;
            TrainPoints -= cost;
            HealShortenTier++;
            Save();
            return true;
        }

        public void UnlockChapter(ChapterId chapter)
        {
            int id = (int)chapter;
            if (id <= UnlockedChapter) return;
            UnlockedChapter = id;
            Save();
        }

        public void NoteGrade(int gradeYear)
        {
            if (gradeYear <= HighestGradeReached) return;
            HighestGradeReached = gradeYear;
            Save();
        }

        public bool IsChapterUnlocked(ChapterId chapter) => (int)chapter <= UnlockedChapter;

        public float HpMul => 1f + PotentialHp * 0.08f;
        public float AtkMul => 1f + PotentialAtk * 0.08f;

        /// <summary>养伤回合：假期结束后的 Injured 清除加速档。</summary>
        public int InjuredBreaksNeeded => Mathf.Max(1, 2 - HealShortenTier);
    }
}
