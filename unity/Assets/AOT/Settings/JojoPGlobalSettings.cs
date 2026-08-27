using System;
using System.Collections.Generic;
using UnityEngine;

namespace JojoP.AOT.Settings
{
    /// <summary>启动 / 渠道相关（Loading 前可读）。</summary>
    [Serializable]
    public class JojoPBootSettings
    {
        [Tooltip("默认渠道。Bootstrap 上 channelOverride 为空时用这里；出包窗口可临时覆盖场景字段")]
        public string defaultChannel = "gp";

        [Tooltip("YooAsset 默认包名")]
        public string defaultPackageName = "DefaultPackage";

        [Tooltip("闪屏秒数。0=跳过，预留公司 logo")]
        public float splashSeconds = 0f;

        [Tooltip("主场景 Yoo 地址（AddressByFileName，一般是 Main）")]
        public string mainSceneName = "Main";
    }

    public enum JojoPCdnSource
    {
        [Tooltip("StreamingAssets / 离线包，不拉远端")]
        Local = 0,
        [Tooltip("从 Cloudflare R2 公开地址拉差量")]
        CloudflareR2 = 1
    }

    /// <summary>热更 CDN + 轻后端。Yoo 文件走 R2，Worker 只做 /config /save。</summary>
    [Serializable]
    public class JojoPHostSettings
    {
        [Tooltip("Local=只用首包；CloudflareR2=Loading 去 R2 公开 URL 拉差量")]
        public JojoPCdnSource cdnSource = JojoPCdnSource.Local;

        [Tooltip("R2 公开根（r2.dev 或自定义域），不要末尾斜杠。例 https://pub-xxx.r2.dev")]
        public string hostServerUrl = "https://pub-781168dca86c49c3826ace7d12450b5a.r2.dev";

        [Tooltip("备用 CDN，一般与主地址相同")]
        public string fallbackHostServerUrl = "https://pub-781168dca86c49c3826ace7d12450b5a.r2.dev";

        [Tooltip("Cloudflare Worker 游戏接口（/config、/save），不是热更文件站")]
        public string workerBaseUrl = "http://127.0.0.1:8787";

        [Tooltip("是否启用版本/更新检查（Local 模式下忽略）")]
        public bool enableUpdateCheck = true;

        /// <summary>Yoo 资源目录名，必须和 Editor 构建/上传的 BuildTarget 文件夹一致。</summary>
        public static string YooPlatformFolder()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "StandaloneWindows64";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "StandaloneOSX";
                default:
                    return "StandaloneWindows64";
            }
        }

        public bool UseRemoteCdn =>
            cdnSource == JojoPCdnSource.CloudflareR2 && enableUpdateCheck;

        /// <summary>示例：{r2Public}/{channel}/{platform}。Local 返回空。</summary>
        public string BuildChannelCdnRoot(string channel, string platform, bool fallback = false)
        {
            if (!UseRemoteCdn) return string.Empty;
            string raw = fallback ? fallbackHostServerUrl : hostServerUrl;
            string host = (raw ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrEmpty(host)) return string.Empty;
            return $"{host}/{channel}/{platform}";
        }
    }

    /// <summary>
    /// HybridCLR 镜像：方便在 JojoP 总面板查看/扩展。
    /// 权威源仍是 Project/HybridCLR Settings；可用「同步」按钮拉齐。
    /// </summary>
    [Serializable]
    public class JojoPHybridClrMirrorSettings
    {
        [Tooltip("热更程序集（含 .dll 后缀，与 HybridCLR 同步）。含 Config + HotUpdate，加载顺序 Config 先于 HotUpdate。")]
        public List<string> hotUpdateAssemblies = new List<string>
        {
            "JojoP.Config.dll",
            "JojoP.HotUpdate.dll"
        };

        [Tooltip("AOT 补充元数据（含 .dll）。权威源是 AOTGenericReferences.PatchedAOTAssemblyList；出包拷贝会同步到这里。运行时优先读 Generate 列表。")]
        public List<string> aotMetaAssemblies = new List<string>
        {
            "Luban.Runtime.dll",
            "System.Core.dll",
            "UniTask.dll",
            "UnityEngine.CoreModule.dll",
            "UnityEngine.JSONSerializeModule.dll",
            "YooAsset.dll",
            "mscorlib.dll"
        };

        [Tooltip("主业务热更 DLL（入口仍是 GameApp，在 HotUpdate 内）")]
        public string logicMainDllName = "JojoP.HotUpdate.dll";

        [Tooltip("热更 DLL 文本资源目录（拷成 .bytes 后打进 YooAsset，建议 Bundle/Dll）")]
        public string assemblyTextAssetPath = "Assets/Bundle/Dll";

        [Tooltip("热更 DLL 资源后缀")]
        public string assemblyTextAssetExtension = ".bytes";
    }

    /// <summary>
    /// JojoP 总配置。Edit → Project Settings → JojoP。
    /// 放 Resources，Boot / 运行时都能读；以后扩展字段都往这里加。
    /// </summary>
    [CreateAssetMenu(fileName = "JojoPGlobalSettings", menuName = "JojoP/全局设置 JojoPGlobalSettings")]
    public sealed class JojoPGlobalSettings : ScriptableObject
    {
        public const string ResourcesPath = "JojoP/JojoPGlobalSettings";
        public const string AssetPath = "Assets/Resources/JojoP/JojoPGlobalSettings.asset";

        [SerializeField] JojoPBootSettings boot = new JojoPBootSettings();
        [SerializeField] JojoPHostSettings host = new JojoPHostSettings();
        [SerializeField] JojoPHybridClrMirrorSettings hybridClr = new JojoPHybridClrMirrorSettings();

        public JojoPBootSettings Boot => boot;
        public JojoPHostSettings Host => host;
        public JojoPHybridClrMirrorSettings HybridClr => hybridClr;

        static JojoPGlobalSettings _cached;

        public static JojoPGlobalSettings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<JojoPGlobalSettings>(ResourcesPath);
            return _cached;
        }

#if UNITY_EDITOR
        public static void ClearCache() => _cached = null;
#endif
    }
}
