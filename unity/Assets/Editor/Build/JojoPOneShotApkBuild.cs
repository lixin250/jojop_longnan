#if UNITY_EDITOR
using System;
using System.IO;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using JojoP.AOT.Settings;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace JojoP.EditorTools.Build
{
    /// <summary>
    /// CompileDll → Yoo Android → R2 → GenerateAll → APK 安装。
    /// GenerateAll 若域重载，InitializeOnLoad 会接着打 APK。不要用 MCP 连点 GenerateAll。
    /// </summary>
    [InitializeOnLoad]
    public static class JojoPOneShotApkBuild
    {
        const string PackageName = "DefaultPackage";
        const string LockPath = "Temp/JojoPOneShot.lock";
        const string NeedApkKey = "JojoP.OneShot.NeedApk";
        const string PrefAccount = "JojoP.Build.R2AccountId";
        const string PrefAccessKey = "JojoP.Build.R2AccessKey";
        const string PrefSecret = "JojoP.Build.R2Secret";
        const string PrefBucket = "JojoP.Build.R2Bucket";
        const string DefaultAccount = "e681185d0116492537698c1467d957fd";

        static JojoPOneShotApkBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!SessionState.GetBool(NeedApkKey, false))
                return;
            SessionState.SetBool(NeedApkKey, false);
            EditorApplication.delayCall += FinishApk;
        }

        [MenuItem("JojoP/一键 HybridCLR+Yoo+APK（安装）")]
        public static void QueueFromMenu()
        {
            Debug.Log("[JojoP] 一键出包已排队");
            EditorApplication.delayCall += RunPhase1;
        }

        public static string Queue()
        {
            if (File.Exists(LockPath))
                return "already-running";
            Directory.CreateDirectory("Temp");
            File.WriteAllText(LockPath, DateTime.Now.ToString("o"));
            EditorApplication.delayCall += RunPhase1;
            return "queued";
        }

        static void RunPhase1()
        {
            try
            {
                RunPhase1Inner();
            }
            catch (Exception e)
            {
                SessionState.SetBool(NeedApkKey, false);
                Debug.LogError("[JojoP] 一键出包失败（Yoo/R2/GenerateAll）\n" + e);
                ClearLock();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void RunPhase1Inner()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new Exception("先退出 Play");

            EditorUserBuildSettings.development = true;
            var settings = JojoPGlobalSettings.Load();
            if (settings == null)
                throw new Exception("找不到 JojoPGlobalSettings");

            string channel = settings.Boot != null && !string.IsNullOrEmpty(settings.Boot.defaultChannel)
                ? settings.Boot.defaultChannel
                : "test";
            var target = BuildTarget.Android;

            EditorUtility.DisplayProgressBar("一键出包", "编译热更 DLL", 0.05f);
            CompileDllCommand.CompileDll(target);
            string src = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            string dest = Path.Combine(Application.dataPath, "Bundle", "Dll");
            Directory.CreateDirectory(dest);
            foreach (var name in new[] { "JojoP.Config.dll", "JojoP.HotUpdate.dll" })
            {
                string from = Path.Combine(src, name);
                if (!File.Exists(from))
                    throw new Exception("没有热更 DLL: " + from);
                File.Copy(from, Path.Combine(dest, name + ".bytes"), true);
            }
            AssetDatabase.Refresh();
            Debug.Log("[JojoP] 热更 DLL 已拷到 Bundle/Dll");

            EditorUtility.DisplayProgressBar("一键出包", "构建 YooAsset Android", 0.2f);
            string version = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
            string pipelineName = BundleBuilderSetting.GetPackageBuildPipeline(PackageName);
            var uniqueBundleName = BundleCollectorSettingData.Setting.UniqueBundleName;
            var shaderBundle = DefaultBundlePackRule.CreateShadersPackRuleResult()
                .GetBundleName(PackageName, uniqueBundleName);
            var buildParameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = PackageName,
                PackageVersion = version,
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = EBundledCopyOption.None,
                BundledCopyParams = string.Empty,
                CompressOption = ECompressOption.LZ4,
                ClearBuildCacheFiles = false,
                UseAssetDependencyDB = true,
                BuiltinShadersBundleName = shaderBundle
            };
            var yooResult = new ScriptableBuildPipeline().Run(buildParameters, true);
            if (!yooResult.Success)
                throw new Exception("Yoo 失败: " + yooResult.ErrorInfo);
            Debug.Log("[JojoP] Yoo 成功 " + version + " " + yooResult.OutputPackageDirectory);

            EditorUtility.DisplayProgressBar("一键出包", "上传 R2", 0.45f);
            var creds = new JojoPR2Uploader.Credentials
            {
                AccountId = EditorPrefs.GetString(PrefAccount, DefaultAccount),
                AccessKeyId = EditorPrefs.GetString(PrefAccessKey, ""),
                SecretAccessKey = EditorPrefs.GetString(PrefSecret, ""),
                Bucket = EditorPrefs.GetString(PrefBucket, "jojop-cdn")
            };
            if (string.IsNullOrEmpty(creds.AccessKeyId) || string.IsNullOrEmpty(creds.SecretAccessKey))
                throw new Exception("没有 R2 凭证（构建窗口里的 Access Key / Secret）");
            string prefix = channel + "/Android";
            var up = JojoPR2Uploader.UploadFolder(
                yooResult.OutputPackageDirectory,
                creds,
                prefix,
                false,
                (done, total, name) =>
                {
                    EditorUtility.DisplayProgressBar(
                        "上传 R2",
                        $"{done}/{total} {name}",
                        total > 0 ? (float)done / total : 1f);
                });
            if (up.Failed > 0)
                throw new Exception($"R2 失败 {up.Failed} 个。上传 {up.Uploaded} 跳过 {up.Skipped}");
            Debug.Log($"[JojoP] R2 {prefix} 上传 {up.Uploaded} 跳过 {up.Skipped}");

            SessionState.SetBool(NeedApkKey, true);
            EditorUtility.DisplayProgressBar("一键出包", "HybridCLR GenerateAll", 0.6f);
            PrebuildCommand.GenerateAll();
            SessionState.SetBool(NeedApkKey, false);
            FinishApk();
        }

        static void FinishApk()
        {
            try
            {
                EditorUserBuildSettings.development = true;
                EditorUtility.DisplayProgressBar("一键出包", "打 APK 并安装", 0.85f);
                JojoPAndroidBuildMenu.RebuildDevelopmentApkAndInstall();
                Debug.Log("[JojoP] 一键出包完成 → Builds/Android/JojoPStack.apk");
            }
            catch (Exception e)
            {
                Debug.LogError("[JojoP] APK 失败\n" + e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ClearLock();
            }
        }

        static void ClearLock()
        {
            if (File.Exists(LockPath))
                File.Delete(LockPath);
        }
    }
}
#endif
