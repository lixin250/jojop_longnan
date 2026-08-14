using System.IO;
using UnityEngine;
using YooAsset;

namespace JojoP.Config
{
    /// <summary>正式：从已初始化的 Yoo 包读 TextAsset（LubanConfig）。</summary>
    public sealed class YooCfgRawLoader : ICfgRawLoader
    {
        readonly string _packageName;

        public YooCfgRawLoader(string packageName = "DefaultPackage")
        {
            _packageName = packageName;
        }

        public bool IsPackageReady
        {
            get
            {
                if (!YooAssets.IsInitialized) return false;
                if (!YooAssets.TryGetPackage(_packageName, out var pkg) || pkg == null) return false;
                return pkg.InitializeStatus == EOperationStatus.Succeeded;
            }
        }

        public string LoadText(string fileName)
        {
            if (!IsPackageReady) return null;

            var package = YooAssets.GetPackage(_packageName);
            string[] locations =
            {
                fileName,
                fileName + ".json",
                "LubanConfig/" + fileName,
                "LubanConfig/" + fileName + ".json",
            };

            foreach (var loc in locations)
            {
                // CheckLocationValid 在部分版本可用；失败则尝试加载
                AssetHandle handle = null;
                try
                {
                    handle = package.LoadAssetSync<TextAsset>(loc);
                    if (handle == null || handle.Status != EOperationStatus.Succeeded)
                    {
                        handle?.Dispose();
                        continue;
                    }

                    var ta = handle.GetAssetObject<TextAsset>();
                    string text = ta != null ? ta.text : null;
                    handle.Dispose();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
                catch
                {
                    handle?.Dispose();
                }
            }

            return null;
        }
    }

    /// <summary>开发直开：读工程 Assets/Bundle/LubanConfig（仅 Editor / 未打 Yoo 时）。</summary>
    public sealed class EditorFileCfgRawLoader : ICfgRawLoader
    {
        public string LoadText(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Bundle", "LubanConfig", fileName + ".json");
            if (File.Exists(path))
                return File.ReadAllText(path);
            return null;
        }
    }

    /// <summary>可选兜底：Resources/LubanConfig/。</summary>
    public sealed class ResourcesCfgRawLoader : ICfgRawLoader
    {
        public string LoadText(string fileName)
        {
            var ta = Resources.Load<TextAsset>("LubanConfig/" + fileName);
            return ta != null ? ta.text : null;
        }
    }

    /// <summary>按优先级串联：Yoo → 编辑器文件 → Resources。</summary>
    public sealed class CascadingCfgRawLoader : ICfgRawLoader
    {
        readonly ICfgRawLoader[] _chain;

        public CascadingCfgRawLoader(params ICfgRawLoader[] chain)
        {
            _chain = chain ?? System.Array.Empty<ICfgRawLoader>();
        }

        public static CascadingCfgRawLoader CreateDefault(string packageName = "DefaultPackage")
        {
            return new CascadingCfgRawLoader(
                new YooCfgRawLoader(packageName),
                new EditorFileCfgRawLoader(),
                new ResourcesCfgRawLoader());
        }

        public string LoadText(string fileName)
        {
            for (int i = 0; i < _chain.Length; i++)
            {
                var text = _chain[i]?.LoadText(fileName);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }
    }
}
