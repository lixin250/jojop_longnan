using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace JojoP.AOT.Boot
{
    /// <summary>
    /// Loading 阶段管线（AOT）：
    /// 1) 请求版本参数与资源差
    /// 2) 按渠道初始化 YooAsset + HybridCLR / AOT 元数据补充（桩可逐步实装）
    /// </summary>
    public sealed class BootPatchPipeline
    {
        readonly string _packageName;
        readonly bool _devSkipHeavy;
        readonly Action<float, string, string> _onProgress;
        readonly string _hostCdnRoot;

        public BootPatchPipeline(
            string packageName,
            bool devSkipHeavy,
            Action<float, string, string> onProgress,
            string hostCdnRoot = null)
        {
            _packageName = packageName;
            _devSkipHeavy = devSkipHeavy;
            _onProgress = onProgress;
            _hostCdnRoot = hostCdnRoot;
        }

        public async UniTask RunAsync()
        {
            Report(0.05f, "读取渠道",
                string.IsNullOrEmpty(_hostCdnRoot)
                    ? $"channel={ChannelId.Value}"
                    : $"channel={ChannelId.Value}\ncdn={_hostCdnRoot}");
            await UniTask.Yield();

            // 1) 版本参数 + 资源差
            await RequestVersionAndDiffAsync();

            // 2) 渠道资源 + HybridCLR / AOT 补充
            await InitYooAssetForChannelAsync();
            await LoadHybridClrAndAotMetaAsync();

            Report(1f, "准备进入游戏", "即将打开主界面");
            await UniTask.Delay(200);
        }

        async UniTask RequestVersionAndDiffAsync()
        {
            Report(0.15f, "请求版本参数", "查询远端版本与资源清单差量…");

            if (_devSkipHeavy)
            {
                await UniTask.Delay(250);
                Report(0.35f, "版本检查完成", "开发直开：跳过真实差量下载");
                return;
            }

            // 正式：可先打 Cloudflare / CDN version API，再交给 YooAsset RequestPackageVersion
            await UniTask.Delay(100);
            Report(0.35f, "版本检查完成", "已取得版本参数");
        }

        async UniTask InitYooAssetForChannelAsync()
        {
            Report(0.45f, "初始化资源", $"YooAsset channel={ChannelId.Value}");

            if (_devSkipHeavy)
            {
                await UniTask.Delay(250);
                Report(0.7f, "资源就绪", "开发直开：未走真实 CDN");
                return;
            }

            if (!YooAssets.IsInitialized)
                YooAssets.Initialize();

            if (!YooAssets.TryGetPackage(_packageName, out var package))
                package = YooAssets.CreatePackage(_packageName);

#if UNITY_EDITOR
            // Editor 联调请配 EditorSimulateModeOptions；未配时由上层决定是否仅开发直开
            throw new Exception(
                "Editor 下完整热更请配置 Simulate 包，或勾选 AppLauncher.devDirectPlay");
#else
            // 渠道 CDN 根路径示例：后续用 HostPlayMode + RemoteService(channel)
            var initParams = new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
            };

            var initOp = package.InitializePackageAsync(initParams);
            await WaitOp(initOp);
            if (initOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"Initialize 失败: {initOp.Error}");

            Report(0.55f, "请求资源版本", null);
            var verOp = package.RequestPackageVersionAsync();
            await WaitOp(verOp);
            if (verOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"RequestVersion 失败: {verOp.Error}");

            Report(0.62f, "更新资源清单", $"version={verOp.PackageVersion}");
            var manOp = package.LoadPackageManifestAsync(
                new LoadPackageManifestOptions(verOp.PackageVersion, 60));
            await WaitOp(manOp);
            if (manOp.Status != EOperationStatus.Succeeded)
                throw new Exception($"LoadManifest 失败: {manOp.Error}");

            // 差量下载：CreateResourceDownloader → 正式接进度条
            Report(0.7f, "资源就绪", $"package={_packageName} ver={verOp.PackageVersion}");
#endif
        }

        async UniTask LoadHybridClrAndAotMetaAsync()
        {
            Report(0.8f, "加载热更运行时", "HybridCLR + AOT 元数据补充…");

            if (_devSkipHeavy)
            {
                await UniTask.Delay(200);
                Report(0.92f, "热更运行时就绪", "开发直开：热更 DLL 仍在主包内");
                return;
            }

            // 正式步骤（后续补齐），与 docs/热更配置与UI选型.md 一致：
            // 1) AOT 元数据补充 HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly
            // 2) Yoo 按序加载 Bundle/Dll：
            //    JojoP.Config.dll.bytes → Assembly.Load
            //    JojoP.HotUpdate.dll.bytes → Assembly.Load（必须后加载）
            // 3) 反射创建 GameApp；HotUpdate 内 CfgTables（Yoo 读 LubanConfig json）
            // 注意：表数据不要在 AOT 里 File.ReadAllText，走 Yoo + ConfigManager
            await UniTask.Delay(100);
            Report(0.92f, "热更运行时就绪", "HybridCLR DLL → HotUpdate CfgTables(Yoo)");
        }

        void Report(float p, string status, string detail) => _onProgress?.Invoke(p, status, detail);

        static async UniTask WaitOp(AsyncOperationBase op)
        {
            while (op != null && !op.IsDone)
                await UniTask.Yield();
        }
    }
}
