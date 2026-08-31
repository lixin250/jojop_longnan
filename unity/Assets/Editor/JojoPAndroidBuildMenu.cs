#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace JojoP.EditorTools
{
    /// <summary>出包菜单。Android 需先在 Hub 装 Android Build Support。</summary>
    public static class JojoPAndroidBuildMenu
    {
        const string OutputDir = "Builds/Android";
        const string AndroidPackage = "com.jojop.stack";

        [MenuItem("JojoP/打 Android APK（开发包）")]
        public static void BuildDevelopmentApk() => Build(false);

        [MenuItem("JojoP/打 Android 开发包并安装到手机")]
        public static void BuildDevelopmentApkAndInstall()
        {
            EditorUserBuildSettings.development = true;
            CloseDevDirectPlay();
            PrebuildCommand.GenerateAll();
            if (!Build(false))
                return;
            InstallAndLaunch(Path.Combine(OutputDir, "JojoPStack.apk"));
        }

        public static void RebuildDevelopmentApkAndInstall()
        {
            EditorUserBuildSettings.development = true;
            CloseDevDirectPlay();
            if (!Build(false))
                return;
            InstallAndLaunch(Path.Combine(OutputDir, "JojoPStack.apk"));
        }

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
                scenes = GetPlayerScenes(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            LogReport("Windows", path, report);
        }

        static bool Build(bool aab)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError(
                    "[JojoP] 当前编辑器没装 Android 模块。\n" +
                    "Unity Hub → 安装 → 6000.4.0f1 → 添加模块 → Android Build Support\n" +
                    "装好前可先用：JojoP/打 Windows 冒烟包");
                return false;
            }

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, aab ? "JojoPStack.aab" : "JojoPStack.apk");
            ApplyCommonPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = aab;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GetPlayerScenes(),
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            LogReport("Android", path, report);
            return report.summary.result == BuildResult.Succeeded;
        }

        static void CloseDevDirectPlay()
        {
            const string scenePath = "Assets/Scenes/Bootstrap.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb.GetType().Name != "AppLauncher")
                    continue;
                var so = new SerializedObject(mb);
                var play = so.FindProperty("devDirectPlay");
                if (play != null && play.boolValue)
                {
                    play.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[JojoP] APK: closed devDirectPlay");
                }
                break;
            }
        }

        public static void InstallAndLaunch(string apkPath)
        {
            if (!File.Exists(apkPath))
            {
                Debug.LogError("[JojoP] 没有 APK: " + apkPath);
                return;
            }

            string adb = Path.Combine(
                EditorApplication.applicationContentsPath,
                "PlaybackEngines", "AndroidPlayer", "SDK", "platform-tools", "adb.exe");
            if (!File.Exists(adb))
            {
                Debug.LogError("[JojoP] 找不到 adb: " + adb);
                return;
            }

            string abs = Path.GetFullPath(apkPath);
            if (!RunAdb(adb, $"install -r -d \"{abs}\"", out string output))
            {
                Debug.LogError("[JojoP] adb install 失败\n" + output);
                return;
            }

            Debug.Log("[JojoP] 已安装到手机\n" + output);
            RunAdb(adb, $"shell monkey -p {AndroidPackage} -c android.intent.category.LAUNCHER 1", out _);
        }

        static bool RunAdb(string adb, string args, out string output)
        {
            var psi = new ProcessStartInfo(adb, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            output = (stdout + "\n" + stderr).Trim();
            return p.ExitCode == 0;
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

        /// <summary>
        /// 玩家包只打 Bootstrap。Main 走 Yoo，改场景/DLL 不用出 APK。
        /// EditorBuildSettings 仍可留 Main，方便编辑器 Play（devDirectPlay）。
        /// </summary>
        static string[] GetPlayerScenes()
        {
            const string bootstrap = "Assets/Scenes/Bootstrap.unity";
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled && s.path.EndsWith("Bootstrap.unity", System.StringComparison.OrdinalIgnoreCase))
                    return new[] { s.path };
            }
            return new[] { bootstrap };
        }
    }
}
#endif
