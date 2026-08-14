#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JojoP.EditorTools
{
    /// <summary>出包菜单。Android 需先在 Hub 装 Android Build Support。</summary>
    public static class JojoPAndroidBuildMenu
    {
        const string OutputDir = "Builds/Android";

        [MenuItem("JojoP/打 Android APK（开发包）")]
        public static void BuildDevelopmentApk() => Build(false);

        [MenuItem("JojoP/打 Android AAB")]
        public static void BuildAab()
        {
            EditorUserBuildSettings.buildAppBundle = true;
            Build(true);
        }

        [MenuItem("JojoP/打 Windows 冒烟包")]
        public static void BuildWindowsSmoke()
        {
            Directory.CreateDirectory("Builds/Windows");
            string path = Path.Combine("Builds/Windows", "JojoPStack.exe");
            ApplyCommonPlayerSettings();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            LogReport("Windows", path, report);
        }

        static void Build(bool aab)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError(
                    "[JojoP] 当前编辑器没装 Android 模块。\n" +
                    "Unity Hub → 安装 → 6000.4.0f1 → 添加模块 → Android Build Support\n" +
                    "装好前可先用：JojoP/打 Windows 冒烟包");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, aab ? "JojoPStack.aab" : "JojoPStack.apk");
            ApplyCommonPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = aab;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            LogReport("Android", path, report);
        }

        static void ApplyCommonPlayerSettings()
        {
            PlayerSettings.companyName = "JojoP";
            PlayerSettings.productName = "JojoP Stack";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.jojop.stack");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
        }

        static void LogReport(string label, string path, BuildReport report)
        {
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[JojoP] {label} 出包成功 → {path}");
            else
                Debug.LogError($"[JojoP] {label} 出包失败: {report.summary.result}");
        }

        static string[] GetEnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled) list.Add(s.path);
            }

            if (list.Count == 0)
            {
                list.Add("Assets/Scenes/Bootstrap.unity");
                list.Add("Assets/Scenes/Main.unity");
            }
            return list.ToArray();
        }
    }
}
#endif
