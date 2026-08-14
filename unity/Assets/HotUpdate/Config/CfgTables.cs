using System;
using System.IO;
using Cysharp.Threading.Tasks;
using JojoP.Cfg;
using Luban.SimpleJSON;
using UnityEngine;

namespace JojoP.Config
{
    /// <summary>
    /// ConfigManager 门面：持有 JojoP.Cfg.Tables。
    /// 商业路径：Yoo 出 json；开发直开：Editor 文件兜底。见 docs/热更配置与UI选型.md
    /// </summary>
    public static class CfgTables
    {
        public static Tables Tables { get; private set; }
        public static bool Ready => Tables != null;
        public static string LastLoaderHint { get; private set; } = "";

        static ICfgRawLoader _loader;

        /// <summary>可注入自定义 loader（单测 / 自定义包名）。</summary>
        public static void SetLoader(ICfgRawLoader loader)
        {
            _loader = loader;
        }

        public static bool TryLoad(bool force = false)
        {
            if (Ready && !force) return true;

            try
            {
                EnsureLoader();
                Tables = new Tables(LoadJson);
                Debug.Log(
                    $"[JojoP] CfgTables OK roles={Tables.TbRoleList.DataList.Count} skills={Tables.TbSkillIndex.DataList.Count} via={LastLoaderHint}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[JojoP] CfgTables load failed: " + e.Message);
                Tables = null;
                return false;
            }
        }

        /// <summary>正式包：Yoo 初始化并差量完成后调用（可先同步预热）。</summary>
        public static async UniTask<bool> TryLoadAsync(bool force = false)
        {
            await UniTask.Yield();
            return TryLoad(force);
        }

        public static void Unload()
        {
            Tables = null;
        }

        static void EnsureLoader()
        {
            if (_loader != null) return;
            string pkg = "DefaultPackage";
#if UNITY_EDITOR
            // 与 JojoPGlobalSettings.boot.defaultPackageName 对齐时可再注入
#endif
            _loader = CascadingCfgRawLoader.CreateDefault(pkg);
            LastLoaderHint = "Yoo→EditorFile→Resources";
        }

        static JSONNode LoadJson(string fileName)
        {
            EnsureLoader();
            string text = _loader.LoadText(fileName);
            if (string.IsNullOrEmpty(text))
                throw new FileNotFoundException(
                    "LubanConfig missing: " + fileName + " (tried Yoo locations + Assets/Bundle/LubanConfig)");
            return JSON.Parse(text);
        }
    }
}
