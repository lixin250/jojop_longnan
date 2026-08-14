using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JojoP.UI;
using UnityEngine;
using YooAsset;

namespace JojoP.UI
{
    /// <summary>
    /// 薄 UI 面板服务：Yoo 加载 Prefab → 取 UIBinder。
    /// 不做巨型 MVC；正式面板逻辑仍用 UIBind/UIFormBase。见 docs/热更配置与UI选型.md
    /// </summary>
    public sealed class UiPanelService
    {
        readonly string _packageName;
        readonly Transform _root;
        readonly Dictionary<string, GameObject> _opened = new Dictionary<string, GameObject>();

        public UiPanelService(Transform uiRoot, string packageName = "DefaultPackage")
        {
            _root = uiRoot;
            _packageName = packageName;
        }

        public bool CanLoadFromYoo
        {
            get
            {
                if (!YooAssets.IsInitialized) return false;
                if (!YooAssets.TryGetPackage(_packageName, out var pkg) || pkg == null) return false;
                return pkg.InitializeStatus == EOperationStatus.Succeeded;
            }
        }

        /// <summary>同步打开（Yoo 已就绪时）。失败返回 null。</summary>
        public UIBinder Open(string location, bool reuse = true)
        {
            if (reuse && _opened.TryGetValue(location, out var exist) && exist != null)
            {
                exist.SetActive(true);
                return exist.GetComponent<UIBinder>() ?? exist.GetComponentInChildren<UIBinder>(true);
            }

            if (!CanLoadFromYoo)
            {
                Debug.LogWarning("[UiPanelService] Yoo 未就绪，无法加载 " + location + "（开发期可继续用代码搭 UI）");
                return null;
            }

            var package = YooAssets.GetPackage(_packageName);
            var handle = package.LoadAssetSync<GameObject>(location);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogError("[UiPanelService] Load failed: " + location + " " + handle.Error);
                handle.Dispose();
                return null;
            }

            var prefab = handle.GetAssetObject<GameObject>();
            var go = Object.Instantiate(prefab, _root, false);
            go.name = location;
            handle.Dispose();

            _opened[location] = go;
            return go.GetComponent<UIBinder>() ?? go.GetComponentInChildren<UIBinder>(true);
        }

        public async UniTask<UIBinder> OpenAsync(string location, bool reuse = true)
        {
            await UniTask.Yield();
            return Open(location, reuse);
        }

        public void Close(string location, bool destroy = true)
        {
            if (!_opened.TryGetValue(location, out var go) || go == null) return;
            if (destroy)
            {
                Object.Destroy(go);
                _opened.Remove(location);
            }
            else
            {
                go.SetActive(false);
            }
        }

        public void CloseAll(bool destroy = true)
        {
            var keys = new List<string>(_opened.Keys);
            foreach (var k in keys)
                Close(k, destroy);
        }
    }
}
