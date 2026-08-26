using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YooAsset;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 一场战斗的资源会话：Yoo handle 挂在这里，退战斗一次 Release。
    /// 特效 prefab、单位战斗图都走 Load，换人/换皮肤只换 location。
    /// </summary>
    public sealed class BattleAssetHub
    {
        public const string PackageName = "DefaultPackage";
        public const string VfxFolder = "Assets/Bundle/Vfx";

        public static BattleAssetHub Current { get; private set; }

        readonly List<AssetHandle> _handles = new List<AssetHandle>(32);
        readonly Dictionary<string, UnityEngine.Object> _cache = new Dictionary<string, UnityEngine.Object>(64);
        Transform _vfxRoot;
        static MethodInfo _loadAsset;

        public static BattleAssetHub Ensure(Transform parent = null)
        {
            if (Current == null)
                Current = new BattleAssetHub();
            Current.BindParent(parent);
            return Current;
        }

        public static void Release()
        {
            Current?.Dispose();
            Current = null;
        }

        public Transform VfxRoot => BindParent(null);

        Transform BindParent(Transform parent)
        {
            if (_vfxRoot == null)
            {
                var go = new GameObject("BattleVfx");
                _vfxRoot = go.transform;
            }

            if (parent != null && _vfxRoot.parent != parent)
                _vfxRoot.SetParent(parent, false);
            return _vfxRoot;
        }

        public Sprite Sprite(string location) => Load<Sprite>(location);

        public GameObject Prefab(string location) => Load<GameObject>(location);

        public T Load<T>(string location) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(location)) return null;
            string key = typeof(T).Name + ":" + location;
            if (_cache.TryGetValue(key, out var hit) && hit != null)
                return hit as T;

            var asset = LoadYoo<T>(location);
            if (asset == null && typeof(T) == typeof(GameObject))
                asset = LoadEditor(VfxFolder + "/" + location + ".prefab") as T;
            if (asset != null)
                _cache[key] = asset;
            return asset;
        }

        T LoadYoo<T>(string location) where T : UnityEngine.Object
        {
            if (!YooAssets.IsInitialized) return null;
            if (!YooAssets.TryGetPackage(PackageName, out var pkg) || pkg == null) return null;
            if (pkg.InitializeStatus != EOperationStatus.Succeeded) return null;

            AssetHandle handle = null;
            try
            {
                handle = pkg.LoadAssetSync<T>(location);
                if (handle == null || handle.Status != EOperationStatus.Succeeded)
                {
                    handle?.Dispose();
                    return null;
                }

                var asset = handle.GetAssetObject<T>();
                _handles.Add(handle);
                return asset;
            }
            catch
            {
                handle?.Dispose();
                return null;
            }
        }

        static UnityEngine.Object LoadEditor(string assetPath)
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

        void Dispose()
        {
            if (_vfxRoot != null)
            {
                UnityEngine.Object.Destroy(_vfxRoot.gameObject);
                _vfxRoot = null;
            }

            for (int i = 0; i < _handles.Count; i++)
            {
                try { _handles[i]?.Dispose(); }
                catch { /* ignore */ }
            }

            _handles.Clear();
            _cache.Clear();
        }
    }
}
