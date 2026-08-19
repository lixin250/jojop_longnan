using System;
using System.IO;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>局外进度：persistentDataPath JSON 为主，PlayerPrefs 仅作迁移。</summary>
    [Serializable]
    public sealed class MetaSaveData
    {
        public int stamina;
        public int staminaDay;
        public int train;
        public int favor;
        public int renown;
        public int unlockedChapter = 1;
        public int potentialHp;
        public int potentialAtk;
        public int healTier;
        public int highestGrade;
        public string selectedHero = RoleCatalog.StarterId;
        public int trainSpent;
        public int kills;
        public string archive = "";
        public string bonds = "";
        public int version = 1;
    }

    public static class LocalSaveStore
    {
        const string FileName = "meta.json";

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, "jojop", FileName);

        public static MetaSaveData LoadOrNull()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<MetaSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JojoP] 本地存档读取失败: " + e.Message);
                return null;
            }
        }

        public static void Write(MetaSaveData data)
        {
            if (data == null) return;
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[JojoP] 本地存档写入失败: " + e.Message);
            }
        }
    }
}
