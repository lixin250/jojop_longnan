#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using JojoP.AOT;
using JojoP.AOT.Settings;
using JojoP.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace JojoP.EditorTools.Build
{
    /// <summary>
    /// Yoo 构建 → 直传 Cloudflare R2 → 出 APK。Worker 不参与热更文件。
    /// </summary>
    public sealed class JojoPBuildWindow : EditorWindow
    {
        const string PrefCopyStreaming = "JojoP.Build.CopyStreaming";
        const string PrefChannel = "JojoP.Build.Channel";
        const string PrefTarget = "JojoP.Build.Target";
        const string PrefAccount = "JojoP.Build.R2AccountId";
        const string PrefAccessKey = "JojoP.Build.R2AccessKey";
        const string PrefSecret = "JojoP.Build.R2Secret";
        const string PrefBucket = "JojoP.Build.R2Bucket";
        const string PrefSelectedVersion = "JojoP.Build.YooVersion";
        const string PackageName = "DefaultPackage";
        const string DefaultAccount = "e681185d0116492537698c1467d957fd";
        static readonly string[] Excluded = { "OutputCache", "Simulate" };

        string _channel = "gp";
        BuildTarget _yooTarget = BuildTarget.Android;
        bool _copyToStreaming = true;
        JojoPCdnSource _cdnSource = JojoPCdnSource.Local;
        string _accountId = DefaultAccount;
        string _accessKey = "";
        string _secret = "";
        string _bucket = "jojop-cdn";
        string[] _versions = Array.Empty<string>();
        int _versionIndex;
        bool _clearDevDirectPlay;
        Vector2 _scroll;

        [MenuItem("JojoP/构建与热更")]
        public static void Open()
        {
            var win = GetWindow<JojoPBuildWindow>("构建与热更");
            win.minSize = new Vector2(500, 720);
            win.Show();
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Close();
                return;
            }
            _channel = EditorPrefs.GetString(PrefChannel, "gp");
            _copyToStreaming = EditorPrefs.GetBool(PrefCopyStreaming, true);
            _accountId = EditorPrefs.GetString(PrefAccount, DefaultAccount);
            _accessKey = EditorPrefs.GetString(PrefAccessKey, "");
            _secret = EditorPrefs.GetString(PrefSecret, "");
            _bucket = EditorPrefs.GetString(PrefBucket, "jojop-cdn");
            _clearDevDirectPlay = true;
            if (Enum.TryParse(EditorPrefs.GetString(PrefTarget, "Android"), out BuildTarget t))
                _yooTarget = t;
            var settings = JojoPGlobalSettings.Load();
            if (settings != null && settings.Host != null)
                _cdnSource = settings.Host.cdnSource;
            RefreshVersions();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            var windows = Resources.FindObjectsOfTypeAll<JojoPBuildWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null)
                    windows[i].Close();
            }
        }

        void OnGUI()
        {
            if (EditorApplication.isPlaying)
                return;
            var settings = JojoPGlobalSettings.Load();
            var host = settings != null ? settings.Host : null;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSharedSettings(host);
            EditorGUILayout.Space(10);
            DrawHotUpdateSection();
            EditorGUILayout.Space(10);
            DrawPlayerBuildSection();

            EditorGUILayout.EndScrollView();
        }

        void DrawSharedSettings(JojoPHostSettings host)
        {
            EditorGUILayout.LabelField("共用", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _channel = EditorGUILayout.TextField("渠道 (gp / test)", _channel);
                _yooTarget = (BuildTarget)EditorGUILayout.EnumPopup("Yoo 构建平台", _yooTarget);

                var newSource = (JojoPCdnSource)EditorGUILayout.EnumPopup("客户端 CDN", _cdnSource);
                if (newSource != _cdnSource)
                {
                    _cdnSource = newSource;
                    WriteCdnSource(_cdnSource);
                }

                if (host != null)
                {
                    EditorGUI.BeginChangeCheck();
                    string publicUrl = EditorGUILayout.TextField("R2 公开 URL", host.hostServerUrl);
                    if (EditorGUI.EndChangeCheck())
                        WriteHostUrl(publicUrl);
                }

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("上传前缀", CdnPrefix());
                EditorGUILayout.TextField("完整 Host URL", FullHostUrl(host));
                EditorGUI.EndDisabledGroup();

                _accountId = EditorGUILayout.TextField("R2 Account ID", _accountId);
                _accessKey = EditorGUILayout.TextField("R2 Access Key", _accessKey);
                _secret = EditorGUILayout.PasswordField("R2 Secret", _secret);
                _bucket = EditorGUILayout.TextField("Bucket", _bucket);

                EditorGUILayout.HelpBox(
                    "Local：Loading 只用 StreamingAssets。CloudflareR2：去公开 URL 拉差量。\n" +
                    "上传走 S3 API。凭证不要进 git。",
                    MessageType.Info);

                if (_yooTarget == BuildTarget.Android &&
                    EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    EditorGUILayout.HelpBox(
                        "当前编辑器不是 Android。测编辑器热更请把平台改成 StandaloneWindows64；出 APK 再改回 Android。",
                        MessageType.Warning);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("探测远端版本", GUILayout.Height(22)))
                        ProbeRemote();
                    if (GUILayout.Button("下载 version 对照", GUILayout.Height(22)))
                        DownloadRemoteVersion();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_versions.Length == 0)
                        EditorGUILayout.Popup("本地版本", 0, new[] { "（暂无，请先构建）" });
                    else
                        _versionIndex = EditorGUILayout.Popup("本地版本", _versionIndex, _versions);
                    if (GUILayout.Button("刷新", GUILayout.Width(48)))
                        RefreshVersions();
                }

                _copyToStreaming = EditorGUILayout.Toggle("Yoo 同时写入 StreamingAssets（首包内置）", _copyToStreaming);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("输出目录", SelectedOutputDir() ?? "");
                EditorGUI.EndDisabledGroup();

                var dir = SelectedOutputDir();
                if (!string.IsNullOrEmpty(dir) && GUILayout.Button("打开选中版本目录", GUILayout.Height(22)))
                {
                    if (Directory.Exists(dir))
                        EditorUtility.RevealInFinder(dir);
                }
            }
        }

        void DrawHotUpdateSection()
        {
            EditorGUILayout.LabelField("热更（不出新 APK）", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "只换 Config / HotUpdate。AOT .bytes 不要动，跟用户手里的 APK 绑定。",
                    MessageType.Info);

                if (GUILayout.Button("1. 编译热更 DLL → Bundle/Dll", GUILayout.Height(28)))
                    CompileHotUpdateDlls();
                if (GUILayout.Button("2. 构建 YooAsset", GUILayout.Height(28)))
                    BuildYooAsset();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("3. 增量上传 R2", GUILayout.Height(28)))
                        Upload(false);
                    if (GUILayout.Button("全量覆盖 R2", GUILayout.Height(28)))
                        Upload(true);
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("一键 1→3（编 DLL + Yoo + 增量上传）", GUILayout.Height(26)))
                    RunHotUpdate123();
            }
        }

        void DrawPlayerBuildSection()
        {
            EditorGUILayout.LabelField("出包（新 APK）", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "il2cpp 变了才走这里。顺序：打 APK → 拷这次裁剪 AOT → Yoo → 上传。",
                    MessageType.Warning);

                _clearDevDirectPlay = EditorGUILayout.Toggle("出包前关闭 Bootstrap 开发直开", _clearDevDirectPlay);
                if (GUILayout.Button("准备：关直开 + 切 CloudflareR2", GUILayout.Height(22)))
                    SwitchToR2Play();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("1. 打 Android APK", GUILayout.Height(28)))
                    {
                        SavePrefs();
                        ApplyBootForPlayerBuild();
                        JojoPAndroidBuildMenu.BuildDevelopmentApk();
                    }

                    if (GUILayout.Button("1b. Windows 冒烟", GUILayout.Height(28)))
                    {
                        SavePrefs();
                        ApplyBootForPlayerBuild();
                        JojoPAndroidBuildMenu.BuildWindowsSmoke();
                    }
                }

                if (GUILayout.Button("2. 拷这次 APK 的 AOT 元数据", GUILayout.Height(28)))
                    CopyAotMetaAfterApk();
                if (GUILayout.Button("3. 构建 YooAsset", GUILayout.Height(28)))
                    BuildYooAsset();
                if (GUILayout.Button("4. 增量上传 R2", GUILayout.Height(28)))
                    Upload(false);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("一键 1→4（GenerateAll + APK + AOT + Yoo + R2）", GUILayout.Height(26)))
                {
                    SavePrefs();
                    ApplyBootForPlayerBuild();
                    JojoPOneShotApkBuild.QueueFromMenu();
                }
            }
        }

        void RunHotUpdate123()
        {
            try
            {
                JojoPDllBytesCopy.CopyHotUpdateDlls(_yooTarget);
                if (BuildYooAsset())
                    Upload(false);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("热更 1→3 失败", e.Message, "确定");
                Debug.LogError(e);
            }
        }

        void SwitchToR2Play()
        {
            _cdnSource = JojoPCdnSource.CloudflareR2;
            WriteCdnSource(_cdnSource);
            ApplyBootForPlayerBuild();
            EditorUtility.DisplayDialog(
                "已切到 R2 拉取",
                "已关掉 AppLauncher.devDirectPlay，客户端 CDN=CloudflareR2。\n" +
                "Editor Play 走 HostPlayMode，不用 EditorSimulate。\n" +
                "请先把 Yoo 平台设为 StandaloneWindows64 并上传 R2。",
                "确定");
        }

        string CdnPrefix() => $"{_channel}/{_yooTarget}";

        string FullHostUrl(JojoPHostSettings host)
        {
            if (host == null || string.IsNullOrEmpty(host.hostServerUrl)) return "";
            return $"{host.hostServerUrl.TrimEnd('/')}/{CdnPrefix()}";
        }

        string PublicFileUrl(JojoPHostSettings host, string fileName)
        {
            return $"{FullHostUrl(host)}/{fileName}";
        }

        void SavePrefs()
        {
            EditorPrefs.SetBool(PrefCopyStreaming, _copyToStreaming);
            EditorPrefs.SetString(PrefChannel, _channel ?? "gp");
            EditorPrefs.SetString(PrefTarget, _yooTarget.ToString());
            EditorPrefs.SetString(PrefAccount, _accountId ?? "");
            EditorPrefs.SetString(PrefAccessKey, _accessKey ?? "");
            EditorPrefs.SetString(PrefSecret, _secret ?? "");
            EditorPrefs.SetString(PrefBucket, _bucket ?? "jojop-cdn");
            EditorPrefs.SetString(PrefSelectedVersion, SelectedVersion() ?? "");
            WriteDefaultChannel(_channel);
            WriteCdnSource(_cdnSource);
        }

        static void WriteCdnSource(JojoPCdnSource source)
        {
            var settings = JojoPGlobalSettings.Load();
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var host = so.FindProperty("host");
            var prop = host != null ? host.FindPropertyRelative("cdnSource") : null;
            if (prop == null || prop.enumValueIndex == (int)source) return;
            prop.enumValueIndex = (int)source;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            JojoPGlobalSettings.ClearCache();
        }

        static void WriteHostUrl(string url)
        {
            var settings = JojoPGlobalSettings.Load();
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var host = so.FindProperty("host");
            if (host == null) return;
            host.FindPropertyRelative("hostServerUrl").stringValue = url ?? "";
            host.FindPropertyRelative("fallbackHostServerUrl").stringValue = url ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            JojoPGlobalSettings.ClearCache();
        }

        static void WriteDefaultChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel)) return;
            var settings = JojoPGlobalSettings.Load();
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var boot = so.FindProperty("boot");
            var ch = boot != null ? boot.FindPropertyRelative("defaultChannel") : null;
            if (ch == null || ch.stringValue == channel) return;
            ch.stringValue = channel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        string PackageRoot()
        {
            return Path.Combine(
                BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                _yooTarget.ToString(),
                PackageName);
        }

        void RefreshVersions(bool selectLatest = false, string prefer = null)
        {
            var root = PackageRoot();
            if (!Directory.Exists(root))
            {
                _versions = Array.Empty<string>();
                _versionIndex = 0;
                return;
            }

            _versions = Directory.GetDirectories(root)
                .Where(IsVersionDir)
                .OrderByDescending(Directory.GetLastWriteTime)
                .Select(Path.GetFileName)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();

            if (_versions.Length == 0)
            {
                _versionIndex = 0;
                return;
            }

            if (!string.IsNullOrEmpty(prefer))
            {
                int i = Array.IndexOf(_versions, prefer);
                _versionIndex = i >= 0 ? i : 0;
                return;
            }

            if (selectLatest)
            {
                _versionIndex = 0;
                return;
            }

            var saved = EditorPrefs.GetString(PrefSelectedVersion, "");
            int si = Array.IndexOf(_versions, saved);
            _versionIndex = si >= 0 ? si : 0;
        }

        static bool IsVersionDir(string dir)
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) return false;
            if (Excluded.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                return false;
            return File.Exists(Path.Combine(dir, PackageName + ".version"));
        }

        string SelectedVersion()
        {
            if (_versions == null || _versions.Length == 0) return null;
            if (_versionIndex < 0 || _versionIndex >= _versions.Length) return _versions[0];
            return _versions[_versionIndex];
        }

        string SelectedOutputDir()
        {
            var v = SelectedVersion();
            return string.IsNullOrEmpty(v) ? null : Path.Combine(PackageRoot(), v);
        }

        void CompileHotUpdateDlls()
        {
            try
            {
                int n = JojoPDllBytesCopy.CopyHotUpdateDlls(_yooTarget);
                EditorUtility.DisplayDialog(
                    "热更 DLL",
                    $"已拷 {n} 个到 Assets/Bundle/Dll\nAOT 元数据未改，仍跟当前 APK 绑定",
                    "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("编译 DLL 失败", e.Message, "确定");
                Debug.LogError(e);
            }
        }

        void CopyAotMetaAfterApk()
        {
            try
            {
                int n = JojoPDllBytesCopy.CopyAotMetaDlls(_yooTarget);
                EditorUtility.DisplayDialog(
                    "AOT 元数据",
                    $"已拷 {n} 个。再点「构建 Yoo 并增量上传」，让 CDN 跟这只 APK 对齐。",
                    "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("拷 AOT 失败", e.Message, "确定");
                Debug.LogError(e);
            }
        }

        bool BuildYooAsset()
        {
            SavePrefs();
            var packageVersion = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
            string pipelineName = BundleBuilderSetting.GetPackageBuildPipeline(PackageName);

            var uniqueBundleName = BundleCollectorSettingData.Setting.UniqueBundleName;
            var shaderBundle = DefaultBundlePackRule.CreateShadersPackRuleResult()
                .GetBundleName(PackageName, uniqueBundleName);

            var buildParameters = new ScriptableBuildParameters();
            buildParameters.BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
            buildParameters.BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = pipelineName;
            buildParameters.BuildBundleType = (int)EBundleType.AssetBundle;
            buildParameters.BuildTarget = _yooTarget;
            buildParameters.PackageName = PackageName;
            buildParameters.PackageVersion = packageVersion;
            buildParameters.EnableSharePackRule = true;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.FileNameStyle = EFileNameStyle.HashName;
            buildParameters.BundledCopyOption = _copyToStreaming
                ? EBundledCopyOption.ClearAndCopyAll
                : EBundledCopyOption.None;
            buildParameters.BundledCopyParams = string.Empty;
            buildParameters.CompressOption = ECompressOption.LZ4;
            buildParameters.ClearBuildCacheFiles = false;
            buildParameters.UseAssetDependencyDB = true;
            buildParameters.BuiltinShadersBundleName = shaderBundle;

            var pipeline = new ScriptableBuildPipeline();
            var buildResult = pipeline.Run(buildParameters, true);
            if (!buildResult.Success)
            {
                EditorUtility.DisplayDialog("YooAsset 失败", buildResult.ErrorInfo, "确定");
                return false;
            }

            RefreshVersions(selectLatest: true, prefer: packageVersion);
            SavePrefs();
            EditorUtility.DisplayDialog(
                "YooAsset 构建成功",
                $"版本: {packageVersion}\n{buildResult.OutputPackageDirectory}\n\n" +
                (_copyToStreaming ? "已写入 StreamingAssets" : "未写入 StreamingAssets，可直接上传 R2"),
                "确定");
            EditorUtility.RevealInFinder(buildResult.OutputPackageDirectory);
            Repaint();
            return true;
        }

        void Upload(bool force)
        {
            SavePrefs();
            var settings = JojoPGlobalSettings.Load();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("上传失败", "找不到 JojoPGlobalSettings。", "确定");
                return;
            }

            var dir = SelectedOutputDir();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                EditorUtility.DisplayDialog("上传失败", "请先构建 YooAsset。", "确定");
                return;
            }

            if (force && !EditorUtility.DisplayDialog("全量覆盖 R2",
                    $"会覆盖远端 {CdnPrefix()}\n确认？", "覆盖", "取消"))
                return;

            try
            {
                var result = JojoPR2Uploader.UploadFolder(
                    dir,
                    new JojoPR2Uploader.Credentials
                    {
                        AccountId = _accountId,
                        AccessKeyId = _accessKey,
                        SecretAccessKey = _secret,
                        Bucket = _bucket
                    },
                    CdnPrefix(),
                    force,
                    (done, total, name) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            force ? "全量上传 R2" : "增量上传 R2",
                            $"{done}/{total} {name}",
                            total > 0 ? (float)done / total : 1f);
                    });
                EditorUtility.ClearProgressBar();
                string cdn = FullHostUrl(settings.Host);
                EditorUtility.DisplayDialog(
                    result.Failed == 0 ? "上传完成" : "部分失败",
                    $"上传 {result.Uploaded}  跳过 {result.Skipped}  失败 {result.Failed}\nCDN:\n{cdn}",
                    "确定");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("上传失败", e.Message, "确定");
            }
        }

        void ProbeRemote()
        {
            SavePrefs();
            var settings = JojoPGlobalSettings.Load();
            if (settings == null)
            {
                EditorUtility.DisplayDialog("探测失败", "找不到 JojoPGlobalSettings。", "确定");
                return;
            }

            string versionUrl = PublicFileUrl(settings.Host, PackageName + ".version");
            try
            {
                string remote = JojoPR2Uploader.GetText(versionUrl);
                string local = SelectedVersion() ?? "（本地还没构建）";
                bool same = !string.IsNullOrEmpty(remote) && remote == local;
                EditorUtility.DisplayDialog(
                    same ? "版本一致" : "版本不同",
                    $"本地: {local}\nR2: {Blank(remote)}\n\n{versionUrl}",
                    "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "探测失败",
                    $"请确认已上传，且 R2 公开 URL 正确。\n{versionUrl}\n\n{e.Message}",
                    "确定");
            }
        }

        void DownloadRemoteVersion()
        {
            SavePrefs();
            var settings = JojoPGlobalSettings.Load();
            if (settings == null) return;
            string versionUrl = PublicFileUrl(settings.Host, PackageName + ".version");
            string dest = Path.Combine(Path.GetTempPath(), "JojoP", CdnPrefix().Replace('/', '_'), PackageName + ".version");
            try
            {
                JojoPR2Uploader.DownloadToFile(versionUrl, dest);
                string remote = File.ReadAllText(dest).Trim();
                EditorUtility.RevealInFinder(dest);
                EditorUtility.DisplayDialog(
                    "已下载远端 version",
                    $"远端: {Blank(remote)}\n本地: {Blank(SelectedVersion())}\n\n{dest}",
                    "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("下载失败", $"{versionUrl}\n\n{e.Message}", "确定");
            }
        }

        static string Blank(string v) => string.IsNullOrEmpty(v) ? "无" : v;

        void ApplyBootForPlayerBuild()
        {
            const string scenePath = "Assets/Scenes/Bootstrap.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            var launcher = UnityEngine.Object.FindAnyObjectByType<AppLauncher>();
            if (launcher == null)
            {
                Debug.LogWarning("[JojoP] Bootstrap 里没有 AppLauncher");
                return;
            }

            var so = new SerializedObject(launcher);
            var play = so.FindProperty("devDirectPlay");
            var channel = so.FindProperty("channelOverride");
            bool dirty = false;
            if (_clearDevDirectPlay && play != null && play.boolValue)
            {
                play.boolValue = false;
                dirty = true;
            }

            if (channel != null && channel.stringValue != _channel)
            {
                channel.stringValue = _channel ?? "gp";
                dirty = true;
            }

            if (!dirty) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[JojoP] Bootstrap channelOverride={_channel} devDirectPlay={play?.boolValue}");
        }
    }
}
#endif
