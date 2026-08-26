using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HybridCLR;
using JojoP.AOT.Settings;
using UnityEngine;
using YooAsset;

namespace JojoP.AOT.Boot
{
    /// <summary>
    /// Loading：Host 拉版本 → 对比内置 → 差量下载 → HybridCLR 桩。
    /// </summary>
    public sealed class BootPatchPipeline
    {
        readonly string _packageName;
        readonly bool _devSkipHeavy;
        readonly bool _enableHostUpdate;
        readonly Action<float, string, string> _onProgress;
        readonly Action<string, string, string, string> _onVersionBoard;
        readonly string _hostCdnRoot;
        readonly string _fallbackCdnRoot;
        string _modeLabel;

        public BootPatchPipeline(
            string packageName,
            bool devSkipHeavy,
            Action<float, string, string> onProgress,
            string hostCdnRoot = null,
            string fallbackCdnRoot = null,
            bool enableHostUpdate = true,
            Action<string, string, string, string> onVersionBoard = null)
        {
            _packageName = packageName;
            _devSkipHeavy = devSkipHeavy;
            _onProgress = onProgress;
            _onVersionBoard = onVersionBoard;
            _hostCdnRoot = hostCdnRoot;
            _fallbackCdnRoot = fallbackCdnRoot;
            _enableHostUpdate = enableHostUpdate;
        }

        public async UniTask RunAsync()
        {
            _modeLabel = DescribeMode();
            Board("", "");
            Report(0.04f, "读取渠道",
                string.IsNullOrEmpty(_hostCdnRoot)
                    ? $"channel={ChannelId.Value}"
                    : $"channel={ChannelId.Value}\ncdn={_hostCdnRoot}");
            await UniTask.Yield();

            await InitYooAssetForChannelAsync();
            await LoadHybridClrAndAotMetaAsync();

            Report(1f, "准备进入游戏", "即将打开主界面");
            await UniTask.Delay(200);
        }

        string DescribeMode()
        {
            bool host = !_devSkipHeavy && _enableHostUpdate && !string.IsNullOrEmpty(_hostCdnRoot);
#if UNITY_EDITOR
            if (_devSkipHeavy)
                return "Editor Play · 直开（不走 Yoo）";
            return "Editor Play · EditorSimulateMode（本机资源，不拉 R2）";
#else
            if (host)
                return "Player · HostPlayMode → Cloudflare R2";
            return "Player · OfflinePlayMode 内置包";
#endif
        }

        void Board(string localVer, string remoteVer) =>
            _onVersionBoard?.Invoke(_modeLabel, localVer, remoteVer, _hostCdnRoot);

        async UniTask InitYooAssetForChannelAsync()
        {
            if (_devSkipHeavy)
            {
                Board("未调用", "未调用");
                Report(0.2f, "版本检查", "开发直开：跳过 GetPackageVersion / RequestPackageVersionAsync");
                await UniTask.Delay(200);
                Report(0.7f, "资源就绪", "开发直开：未走 CDN。测 R2 请关掉 Bootstrap 上 AppLauncher.devDirectPlay");
                return;
            }

            if (!YooAssets.IsInitialized)
                YooAssets.Initialize();

            if (!YooAssets.TryGetPackage(_packageName, out var package))
                package = YooAssets.CreatePackage(_packageName);

            bool host = _enableHostUpdate && !string.IsNullOrEmpty(_hostCdnRoot);
#if UNITY_EDITOR
            Report(0.12f, "初始化资源系统", "EditorSimulateMode");
            await InitEditorSimulateAsync(package);
            host = false;
#else
            Report(0.12f, "初始化资源系统", host ? "Host 联机模式" : "离线内置模式");
            if (host)
                await InitHostAsync(package);
            else
                await InitOfflineAsync(package);
#endif

            string localVer = ReadLocalVersion(package);
            Board(localVer, "");
            Report(0.28f, "对比版本",
                string.IsNullOrEmpty(localVer)
                    ? "GetPackageVersion：无内置清单，将按远端全量拉取"
                    : $"GetPackageVersion：{localVer}");

            var verOp = package.RequestPackageVersionAsync();
            await WaitOp(verOp);
            if (verOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"RequestVersion 失败: {verOp.Error}");

            string remoteVer = verOp.PackageVersion;
            bool same = !string.IsNullOrEmpty(localVer) && localVer == remoteVer;
            Board(localVer, remoteVer);
            Report(0.42f, same ? "版本一致" : "发现新版本",
                $"GetPackageVersion {Blank(localVer)}\nRequestPackageVersionAsync {Blank(remoteVer)}");

            var manOp = package.LoadPackageManifestAsync(
                new LoadPackageManifestOptions(remoteVer, 60));
            await WaitOp(manOp);
            if (manOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"LoadManifest 失败: {manOp.Error}");

            if (!host)
            {
                Report(0.72f, "资源就绪", $"内置版本 {remoteVer}");
                return;
            }

            await DownloadDiffAsync(package, localVer, remoteVer);
        }

#if UNITY_EDITOR
        async UniTask InitEditorSimulateAsync(ResourcePackage package)
        {
            Report(0.16f, "模拟构建资源清单", "EditorSimulateBuildInvoker");
            var buildResult = EditorSimulateBuildInvoker.Build(_packageName, (int)EBundleType.VirtualAssetBundle);
            if (buildResult == null || string.IsNullOrEmpty(buildResult.PackageRootDirectory))
                throw new Exception("EditorSimulate 构建失败，没有 PackageRootDirectory");

            var initParams = new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                    buildResult.PackageRootDirectory)
            };
            var initOp = package.InitializePackageAsync(initParams);
            await WaitOp(initOp);
            if (initOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"EditorSimulate 初始化失败: {initOp.Error}");
        }
#endif

        async UniTask InitHostAsync(ResourcePackage package)
        {
            var remote = new YooRemoteService(_hostCdnRoot, _fallbackCdnRoot);
            // 薄 APK：不读 StreamingAssets。内置 version 不在包里时 Yoo 会 404，整条 Host 起不来。
            var cache = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remote);
            cache.AddParameter(EFileSystemParameter.DownloadMaxConcurrency, 10);
            cache.AddParameter(EFileSystemParameter.DownloadMaxRequestPerFrame, 5);
            var initParams = new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = null,
                CacheFileSystemParameters = cache
            };
            var initOp = package.InitializePackageAsync(initParams);
            await WaitOp(initOp);
            if (initOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"Host 初始化失败: {initOp.Error}");
        }

        async UniTask InitOfflineAsync(ResourcePackage package)
        {
            var initParams = new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
            };
            var initOp = package.InitializePackageAsync(initParams);
            await WaitOp(initOp);
            if (initOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"Offline 初始化失败: {initOp.Error}");
        }

        async UniTask DownloadDiffAsync(ResourcePackage package, string localVer, string remoteVer)
        {
            var downloader = package.CreateResourceDownloader(new ResourceDownloaderOptions(10, 3));
            int count = downloader.TotalDownloadCount;
            long bytes = downloader.TotalDownloadBytes;

            if (count <= 0)
            {
                Report(0.78f, "资源已是最新",
                    $"版本 {Blank(remoteVer)}，无需下载");
                return;
            }

            Report(0.5f, "准备下载",
                $"本地 {Blank(localVer)} → 远端 {remoteVer}\n{count} 个文件 / {FormatBytes(bytes)}");

            downloader.StartDownload();
            while (downloader != null && !downloader.IsDone)
            {
                float p = 0.5f + 0.28f * Mathf.Clamp01(downloader.Progress);
                int pct = Mathf.RoundToInt(Mathf.Clamp01(downloader.Progress) * 100f);
                Report(p, $"下载热更资源  {pct}%",
                    $"本地 {Blank(localVer)} → 远端 {remoteVer}\n" +
                    $"{downloader.CurrentDownloadCount}/{count}  " +
                    $"{FormatBytes(downloader.CurrentDownloadBytes)} / {FormatBytes(bytes)}");
                await UniTask.Yield();
            }

            if (downloader.Status != EOperationStatus.Succeeded)
                throw new Exception($"下载失败: {downloader.Error}");

            Report(0.8f, "下载完成", $"已更新到 {remoteVer}");
        }

        async UniTask LoadHybridClrAndAotMetaAsync()
        {
            Report(0.86f, "加载热更运行时", "HybridCLR + AOT 元数据…");

            if (_devSkipHeavy)
            {
                await UniTask.Delay(150);
                Report(0.94f, "热更运行时就绪", "开发直开：DLL 仍在主域");
                return;
            }

#if UNITY_EDITOR
            if (FindType("JojoP.HotUpdate.GameApp") != null)
            {
                Report(0.94f, "热更运行时就绪", "编辑器主域已有 JojoP.HotUpdate");
                return;
            }
#endif

            if (!YooAssets.TryGetPackage(_packageName, out var package))
                throw new Exception($"Yoo 包未初始化: {_packageName}");

            var settings = JojoPGlobalSettings.Load();
            var hybrid = settings != null ? settings.HybridClr : null;

            if (hybrid != null)
            {
                foreach (var raw in hybrid.aotMetaAssemblies)
                {
                    string loc = DllLocation(raw);
                    if (!package.IsLocationValid(loc))
                        continue;
                    Report(0.88f, "补充 AOT 元数据", loc);
                    byte[] meta = await LoadTextAssetBytes(package, loc);
                    var code = RuntimeApi.LoadMetadataForAOTAssembly(meta, HomologousImageMode.SuperSet);
                    if (code != LoadImageErrorCode.OK)
                        Debug.LogWarning($"[JojoP.AOT] AOT 元数据 {loc}: {code}");
                }
            }

            string[] hotDlls = hybrid != null && hybrid.hotUpdateAssemblies != null && hybrid.hotUpdateAssemblies.Count > 0
                ? hybrid.hotUpdateAssemblies.ToArray()
                : new[] { "JojoP.Config.dll", "JojoP.HotUpdate.dll" };

            for (int i = 0; i < hotDlls.Length; i++)
            {
                string loc = DllLocation(hotDlls[i]);
                Report(0.9f + 0.04f * i / Math.Max(1, hotDlls.Length), "加载热更程序集", loc);
                string asmName = PathNoExt(hotDlls[i]);
                if (AssemblyLoaded(asmName))
                    continue;
                byte[] dll = await LoadTextAssetBytes(package, loc);
                Assembly.Load(dll);
            }

            if (FindType("JojoP.HotUpdate.GameApp") == null)
                throw new Exception("Assembly.Load 之后仍找不到 JojoP.HotUpdate.GameApp");

            Report(0.94f, "热更运行时就绪", "Config + HotUpdate 已加载");
        }

        static string DllLocation(string name)
        {
            string n = (name ?? "").Trim();
            if (n.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 6);
            return n;
        }

        static string PathNoExt(string name)
        {
            string n = DllLocation(name);
            if (n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 4);
            return n;
        }

        static bool AssemblyLoaded(string assemblyName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == assemblyName)
                    return true;
            }
            return false;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static async UniTask<byte[]> LoadTextAssetBytes(ResourcePackage package, string location)
        {
            if (!package.IsLocationValid(location))
                throw new Exception($"Yoo 没有地址 {location}（AddressByFileName 应为 xxx.dll）");
            var handle = package.LoadAssetAsync<TextAsset>(location);
            while (!handle.IsDone)
                await UniTask.Yield();
            if (handle.Status != EOperationStatus.Succeeded)
                throw new Exception($"加载 {location} 失败: {handle.Error}");
            var ta = handle.GetAssetObject<TextAsset>();
            if (ta == null || ta.bytes == null || ta.bytes.Length == 0)
                throw new Exception($"{location} 为空");
            return ta.bytes;
        }

        void Report(float p, string status, string detail) => _onProgress?.Invoke(p, status, detail);

        static string ReadLocalVersion(ResourcePackage package)
        {
            try
            {
                return package.GetPackageVersion();
            }
            catch
            {
                return string.Empty;
            }
        }

        static string Blank(string v) => string.IsNullOrEmpty(v) ? "无" : v;

        static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("0.0") + " KB";
            return (bytes / (1024f * 1024f)).ToString("0.00") + " MB";
        }

        static async UniTask WaitOp(AsyncOperationBase op)
        {
            while (op != null && !op.IsDone)
                await UniTask.Yield();
        }
    }
}
