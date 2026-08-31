using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YooAsset;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 按 SkillEffect.vfx_key 播一次性粒子。缺资源则静默跳过。
    /// 寻址：Yoo AddressByFileName（prefab 名）或 Editor 下 Assets/Bundle/Vfx/{key}.prefab
    /// </summary>
    public static class SkillVfx
    {
        public const string Root = "Assets/Bundle/Vfx";
        const string Package = "DefaultPackage";
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        static MethodInfo _loadAsset;

        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "fx_crit", "fx_lixin_gamble" },
            { "fx_buff_spd", "fx_lixin_crunch" },
            { "fx_overwork", "fx_lixin_overwork" },
        };

        public static void Play(string key, Vector3 from, Vector3 to, Transform follow, float life)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (Alias.TryGetValue(key, out var mapped))
                key = mapped;

            var prefab = BattleAssetHub.Current != null
                ? BattleAssetHub.Current.Prefab(key)
                : Load(key);
            if (prefab == null) return;

            var pos = from;
            var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            Vector3 dir = to - from;
            dir.z = 0f;
            // 自身特效 from/to 几乎重叠，不要把锥体拧到脚下
            if (Mathf.Abs(dir.x) + Mathf.Abs(dir.y) > 0.35f)
                go.transform.rotation = Quaternion.LookRotation(dir);

            if (follow != null)
                go.transform.SetParent(follow, true);
            else if (BattleAssetHub.Current != null)
                go.transform.SetParent(BattleAssetHub.Current.VfxRoot, true);

            var kill = go.GetComponent<SkillVfxAutoKill>();
            if (kill == null) kill = go.AddComponent<SkillVfxAutoKill>();
            kill.Life = Mathf.Max(0.25f, life);

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        static GameObject Load(string key)
        {
            if (Cache.TryGetValue(key, out var hit) && hit != null)
                return hit;

            GameObject prefab = LoadYoo(key) ?? LoadEditor($"{Root}/{key}.prefab");
            if (prefab != null)
                Cache[key] = prefab;
            return prefab;
        }

        static GameObject LoadYoo(string location)
        {
            if (!YooAssets.IsInitialized) return null;
            if (!YooAssets.TryGetPackage(Package, out var pkg) || pkg == null) return null;
            if (pkg.InitializeStatus != EOperationStatus.Succeeded) return null;

            AssetHandle handle = null;
            try
            {
                handle = pkg.LoadAssetSync<GameObject>(location);
                if (handle == null || handle.Status != EOperationStatus.Succeeded)
                {
                    handle?.Dispose();
                    return null;
                }

                var go = handle.GetAssetObject<GameObject>();
                handle.Dispose();
                return go;
            }
            catch
            {
                handle?.Dispose();
                return null;
            }
        }

        static GameObject LoadEditor(string assetPath)
        {
            var method = EditorLoad();
            if (method == null) return null;
            return method.MakeGenericMethod(typeof(GameObject)).Invoke(null, new object[] { assetPath }) as GameObject;
        }

        static MethodInfo EditorLoad()
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

    public sealed class SkillVfxAutoKill : MonoBehaviour
    {
        public float Life = 1.2f;

        void Start()
        {
            Destroy(gameObject, Life);
        }
    }
}
