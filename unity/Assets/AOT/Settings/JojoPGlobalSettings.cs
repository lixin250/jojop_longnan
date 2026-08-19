using System;
using System.Collections.Generic;
using UnityEngine;

namespace JojoP.AOT.Settings
{
    /// <summary>启动 / 渠道相关（Loading 前可读）。</summary>
    [Serializable]
    public class JojoPBootSettings
    {
        [Tooltip("默认渠道。打包脚本也可覆盖 AppLauncher.channelOverride")]
        public string defaultChannel = "gp";

        [Tooltip("YooAsset 默认包名")]
        public string defaultPackageName = "DefaultPackage";

        [Tooltip("闪屏秒数。0=跳过，预留公司 logo")]
        public float splashSeconds = 0f;

        [Tooltip("主场景名")]
        public string mainSceneName = "Main";
    }

    /// <summary>热更 / CDN / 轻后端地址。按渠道拼路径时用 host + channel。</summary>
    [Serializable]
    public class JojoPHostSettings
    {
        [Tooltip("主 CDN / Host（YooAsset Host 模式）")]
        public string hostServerUrl = "http://127.0.0.1:8081";

        [Tooltip("备用 CDN")]
        public string fallbackHostServerUrl = "http://127.0.0.1:8081";

        [Tooltip("Cloudflare Worker（业务 /config、/save），不要末尾斜杠")]
        public string workerBaseUrl = "http://127.0.0.1:8787";

        [Tooltip("是否启用版本/更新检查（关闭则 Loading 只走本地）")]
        public bool enableUpdateCheck = true;

        public string windowsUpdateDataUrl = "http://127.0.0.1:8081";
        public string androidUpdateDataUrl = "http://127.0.0.1:8081";
        public string iosUpdateDataUrl = "http://127.0.0.1:8081";
        public string webglUpdateDataUrl = "http://127.0.0.1:8081";

        /// <summary>示例：{host}/{channel}/{platform}/</summary>
        public string BuildChannelCdnRoot(string channel, string platform)
        {
            string host = (hostServerUrl ?? string.Empty).TrimEnd('/');
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

        [Tooltip("AOT 补充元数据程序集（含 .dll）")]
        public List<string> aotMetaAssemblies = new List<string>
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll"
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
