#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JojoP.EditorTools
{
    /// <summary>
    /// 把仓库根目录 art/_final/{role}/ 下的 png 按 manifest.json 覆盖到 Assets/Bundle。
    /// 同名覆盖保留 .meta GUID；新文件自动设为 Sprite (2D and UI)。
    /// </summary>
    public static class ArtFinalImporter
    {
        const string MenuPath = "JojoP/Art/从 art/_final 覆盖导入 Bundle";
        const string BundleRoot = "Assets/Bundle";

        [Serializable]
        class Manifest
        {
            public string role_id;
            // JsonUtility 更稳吃数组；manifest 里 items 是 JSON array
            public ManifestItem[] items;
        }

        [Serializable]
        class ManifestItem
        {
            public string key;
            public string file;
            public string unity_subdir;
        }

        [MenuItem(MenuPath)]
        public static void ImportAllFinal()
        {
            string artFinal = FindArtFinalRoot();
            if (string.IsNullOrEmpty(artFinal) || !Directory.Exists(artFinal))
            {
                EditorUtility.DisplayDialog(
                    "Art 导入",
                    "找不到 art/_final。先在仓库根目录运行:\npython art/tools/crop_concept_sheet.py",
                    "OK");
                return;
            }

            var roleDirs = Directory.GetDirectories(artFinal);
            if (roleDirs.Length == 0)
            {
                EditorUtility.DisplayDialog("Art 导入", $"art/_final 为空:\n{artFinal}", "OK");
                return;
            }

            int copied = 0;
            var touched = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string roleDir in roleDirs)
                {
                    string manPath = Path.Combine(roleDir, "manifest.json");
                    if (!File.Exists(manPath))
                    {
                        Debug.LogWarning($"[ArtImport] 跳过（无 manifest）: {roleDir}");
                        continue;
                    }

                    var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manPath));
                    if (manifest?.items == null)
                    {
                        Debug.LogWarning($"[ArtImport] manifest 解析失败: {manPath}");
                        continue;
                    }

                    foreach (var item in manifest.items)
                    {
                        if (string.IsNullOrEmpty(item.file) || string.IsNullOrEmpty(item.unity_subdir))
                            continue;

                        string src = Path.Combine(roleDir, item.file);
                        if (!File.Exists(src))
                        {
                            Debug.LogWarning($"[ArtImport] 缺文件: {src}");
                            continue;
                        }

                        string sub = item.unity_subdir.Replace('\\', '/').Trim('/');
                        EnsureBundleFolder(sub);

                        string dstAsset = $"{BundleRoot}/{sub}/{item.file}".Replace('\\', '/');
                        string dstAbs = ToAbsoluteAssetPath(dstAsset);
                        Directory.CreateDirectory(Path.GetDirectoryName(dstAbs) ?? BundleRoot);
                        File.Copy(src, dstAbs, overwrite: true);
                        touched.Add(dstAsset);
                        copied++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            foreach (string assetPath in touched)
            {
                ConfigureSpriteImport(assetPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtImport] 覆盖 {copied} 张 → {BundleRoot}");
            EditorUtility.DisplayDialog("Art 导入", $"已覆盖 {copied} 张到 {BundleRoot}\n见 Console 日志。", "OK");
        }

        static void EnsureBundleFolder(string relativeUnderBundle)
        {
            string[] parts = relativeUnderBundle.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string cursor = BundleRoot;
            foreach (string part in parts)
            {
                string next = $"{cursor}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cursor, part);
                cursor = next;
            }
        }

        static void ConfigureSpriteImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
        }

        static string FindArtFinalRoot()
        {
            // Unity 工程在 unity/，仓库根在上一级
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            string candidate = Path.Combine(repoRoot, "art", "_final");
            if (Directory.Exists(candidate)) return candidate;

            // 兜底：若 Unity 工程就在仓库根
            candidate = Path.Combine(projectRoot, "art", "_final");
            return Directory.Exists(candidate) ? candidate : candidate;
        }

        static string ToAbsoluteAssetPath(string assetPath)
        {
            // Assets/... → 绝对路径
            string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }
    }
}
#endif
