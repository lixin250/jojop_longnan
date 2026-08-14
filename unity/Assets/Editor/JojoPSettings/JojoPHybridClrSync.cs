#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using JojoP.AOT.Settings;
using UnityEditor;
using UnityEngine;

namespace JojoP.EditorTools.Settings
{
    /// <summary>从官方 HybridCLR Settings 同步程序集列表到 JojoPGlobalSettings。</summary>
    public static class JojoPHybridClrSync
    {
        public static void SyncFromHybridClr(JojoPGlobalSettings settings)
        {
            if (settings == null) return;

            var mirror = settings.HybridClr;
            var hot = SettingsUtil.HotUpdateAssemblyFilesIncludePreserved;
            var aot = SettingsUtil.AOTAssemblyNames;

            if (hot != null && hot.Count > 0)
            {
                mirror.hotUpdateAssemblies = new List<string>(hot);
                if (string.IsNullOrEmpty(mirror.logicMainDllName) ||
                    !mirror.hotUpdateAssemblies.Contains(mirror.logicMainDllName))
                {
                    mirror.logicMainDllName = mirror.hotUpdateAssemblies
                        .FirstOrDefault(x => x.Contains("HotUpdate"))
                        ?? mirror.hotUpdateAssemblies[0];
                }
            }

            if (aot != null && aot.Count > 0)
            {
                mirror.aotMetaAssemblies = aot
                    .Select(n => n.EndsWith(".dll") ? n : n + ".dll")
                    .ToList();
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[JojoP] 已从 HybridCLR Settings 同步：HotUpdate={mirror.hotUpdateAssemblies.Count}, AOT={mirror.aotMetaAssemblies.Count}");
        }

        public static JojoPGlobalSettings EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<JojoPGlobalSettings>(JojoPGlobalSettings.AssetPath);
            if (existing != null) return existing;

            Directory.CreateDirectory("Assets/Resources/JojoP");
            var asset = ScriptableObject.CreateInstance<JojoPGlobalSettings>();
            AssetDatabase.CreateAsset(asset, JojoPGlobalSettings.AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SyncFromHybridClr(asset);
            return asset;
        }
    }
}
#endif
