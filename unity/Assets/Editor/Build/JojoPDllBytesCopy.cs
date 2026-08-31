#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using JojoP.AOT.Settings;
using UnityEditor;
using UnityEngine;

namespace JojoP.EditorTools.Build
{
    /// <summary>
    /// 热更 DLL 与 AOT 补充元数据分开拷。
    /// 日常热更只拷 Config/HotUpdate；AOT .bytes 跟当前 APK 的裁剪目录绑定，出新包后再拷。
    /// </summary>
    public static class JojoPDllBytesCopy
    {
        const string DestRel = "Bundle/Dll";

        [MenuItem("JojoP/编译热更 DLL → Bundle/Dll")]
        public static void CopyHotUpdateFromMenu()
        {
            try
            {
                int n = CopyHotUpdateDlls(EditorUserBuildSettings.activeBuildTarget);
                EditorUtility.DisplayDialog("热更 DLL", $"已拷 {n} 个到 Assets/{DestRel}\n未改 AOT 元数据", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("拷贝失败", e.Message, "确定");
                Debug.LogError(e);
            }
        }

        [MenuItem("JojoP/出包后拷 AOT 补充元数据 → Bundle/Dll")]
        public static void CopyAotMetaFromMenu()
        {
            try
            {
                int n = CopyAotMetaDlls(EditorUserBuildSettings.activeBuildTarget);
                EditorUtility.DisplayDialog(
                    "AOT 元数据",
                    $"已从裁剪目录拷 {n} 个到 Assets/{DestRel}\n请用打出当前 APK 时的 strip，不要用后来 Generate/AotDlls 的新文件。",
                    "确定");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("拷贝失败", e.Message, "确定");
                Debug.LogError(e);
            }
        }

        public static int CopyHotUpdateDlls(BuildTarget target)
        {
            CompileDllCommand.CompileDll(target);
            string src = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            string dest = DestDir();
            Directory.CreateDirectory(dest);
            string[] names = { "JojoP.Config.dll", "JojoP.HotUpdate.dll" };
            int n = 0;
            foreach (var name in names)
            {
                string from = Path.Combine(src, name);
                if (!File.Exists(from))
                    throw new Exception("没有热更 DLL: " + from);
                File.Copy(from, Path.Combine(dest, name + ".bytes"), true);
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[JojoP] 热更 DLL 已拷 {n} 个 → Assets/{DestRel}（AOT .bytes 未动）");
            return n;
        }

        public static int CopyAotMetaDlls(BuildTarget target)
        {
            var list = ReadPatchedAotAssemblyList();
            if (list.Count == 0)
                throw new Exception(
                    "AOTGenericReferences.PatchedAOTAssemblyList 为空。先跑 HybridCLR/Generate/AOTGenericReference");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string stripDir = Path.Combine(
                projectRoot, "HybridCLRData", "AssembliesPostIl2CppStrip", target.ToString());
            if (!Directory.Exists(stripDir))
                throw new Exception(
                    $"没有裁剪 AOT 目录: {stripDir}\n先打过一次 {target} 包，或菜单 HybridCLR/Generate/AotDlls");

            string dest = DestDir();
            Directory.CreateDirectory(dest);
            int n = 0;
            foreach (var raw in list)
            {
                string name = NormalizeDllName(raw);
                string from = Path.Combine(stripDir, name);
                if (!File.Exists(from))
                    throw new Exception(
                        $"PatchedAOTAssemblyList 需要 {name}，但裁剪目录没有:\n{from}");
                File.Copy(from, Path.Combine(dest, name + ".bytes"), true);
                n++;
            }

            SyncSettings(list);
            AssetDatabase.Refresh();
            Debug.Log($"[JojoP] AOT 补充元数据已拷 {n} 个 ← {stripDir}");
            return n;
        }

        public static List<string> ReadPatchedAotAssemblyList()
        {
            var names = new List<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("AOTGenericReferences");
                if (t == null) continue;
                var f = t.GetField("PatchedAOTAssemblyList", BindingFlags.Public | BindingFlags.Static);
                if (f?.GetValue(null) is System.Collections.IEnumerable raw)
                {
                    foreach (var item in raw)
                    {
                        if (item is string s && !string.IsNullOrWhiteSpace(s))
                            names.Add(NormalizeDllName(s));
                    }
                }

                break;
            }

            return names.Distinct().ToList();
        }

        static void SyncSettings(List<string> list)
        {
            var settings = JojoPGlobalSettings.Load();
            if (settings == null)
                settings = AssetDatabase.LoadAssetAtPath<JojoPGlobalSettings>(JojoPGlobalSettings.AssetPath);
            if (settings != null && settings.HybridClr != null)
            {
                settings.HybridClr.aotMetaAssemblies = new List<string>(list);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        static string DestDir() => Path.Combine(Application.dataPath, DestRel);

        static string NormalizeDllName(string name)
        {
            string n = (name ?? "").Trim();
            if (n.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 6);
            if (!n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                n += ".dll";
            return n;
        }
    }
}
#endif
