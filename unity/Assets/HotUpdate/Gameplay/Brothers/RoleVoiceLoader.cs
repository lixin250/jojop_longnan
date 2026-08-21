using System;
using System.Collections.Generic;
using System.Reflection;
using JojoP.Cfg;
using JojoP.Config;
using UnityEngine;
using YooAsset;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 人声加载。表 key：{who}_{module}_{meaning}。
    /// 有 langPath_ln 且资源存在则播龙南话自录音，否则播普通话 langPath。
    /// </summary>
    public static class RoleVoiceLoader
    {
        public const string RoleRoot = RoleArtLoader.RoleRoot;
        const string DefaultPackage = "DefaultPackage";
        static MethodInfo _loadAsset;
        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();
        static readonly List<AssetHandle> Handles = new List<AssetHandle>();

        public static RoleVoice Get(string id)
        {
            if (string.IsNullOrEmpty(id) || !CfgTables.Ready) return null;
            return CfgTables.Tables.TbRoleVoice.GetOrDefault(id);
        }

        public static IEnumerable<RoleVoice> ByWhoModule(string who, string module)
        {
            if (!CfgTables.Ready) yield break;
            foreach (var row in CfgTables.Tables.TbRoleVoice.DataList)
            {
                if (row.Who == who && row.Module == module)
                    yield return row;
            }
        }

        public static string ResolveLoc(RoleVoice row)
        {
            if (row == null) return "";
            if (!string.IsNullOrEmpty(row.LangPathLn) && Exists(row.LangPathLn))
                return row.LangPathLn;
            return row.LangPath ?? "";
        }

        public static AudioClip Load(string id) => Load(Get(id));

        public static AudioClip Load(RoleVoice row)
        {
            string loc = ResolveLoc(row);
            if (string.IsNullOrEmpty(loc)) return null;
            return LoadClip(loc);
        }

        public static bool Play(string id, float volume = 1f)
        {
            var clip = Load(id);
            if (clip == null) return false;
            var src = Bus();
            src.PlayOneShot(clip, volume);
            return true;
        }

        public static bool Exists(string loc)
        {
            return LoadClip(loc) != null;
        }

        static AudioClip LoadClip(string loc)
        {
            loc = (loc ?? "").Replace('\\', '/');
            if (string.IsNullOrEmpty(loc)) return null;
            if (ClipCache.TryGetValue(loc, out var cached) && cached != null) return cached;
            var clip = LoadYoo(loc);
            if (clip == null)
            {
                foreach (var ext in new[] { ".ogg", ".mp3", ".wav" })
                {
                    clip = LoadEditor($"{RoleRoot}/{loc}{ext}");
                    if (clip != null) break;
                }
            }

            if (clip != null) ClipCache[loc] = clip;
            return clip;
        }

        static AudioSource Bus()
        {
            if (_src != null) return _src;
            var go = new GameObject("RoleVoiceBus");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            return _src;
        }

        static AudioClip LoadYoo(string location)
        {
            if (!YooAssets.IsInitialized) return null;
            if (!YooAssets.TryGetPackage(DefaultPackage, out var pkg) || pkg == null) return null;
            if (pkg.InitializeStatus != EOperationStatus.Succeeded) return null;
            AssetHandle handle = null;
            try
            {
                handle = pkg.LoadAssetSync<AudioClip>(location);
                if (handle == null || handle.Status != EOperationStatus.Succeeded)
                {
                    handle?.Dispose();
                    return null;
                }

                var clip = handle.GetAssetObject<AudioClip>();
                Handles.Add(handle);
                return clip;
            }
            catch
            {
                handle?.Dispose();
                return null;
            }
        }

        static AudioClip LoadEditor(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;
            var method = EditorLoadAsset();
            if (method == null) return null;
            return method.MakeGenericMethod(typeof(AudioClip)).Invoke(null, new object[] { assetPath }) as AudioClip;
        }

        static MethodInfo EditorLoadAsset()
        {
            if (_loadAsset != null) return _loadAsset;
            var t = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (t == null) return null;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "LoadAssetAtPath" || !m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                {
                    _loadAsset = m;
                    break;
                }
            }

            return _loadAsset;
        }
    }
}
