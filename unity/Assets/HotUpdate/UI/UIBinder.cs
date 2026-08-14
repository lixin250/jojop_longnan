using System;
using System.Collections.Generic;
using UnityEngine;

namespace JojoP.UI
{
    /// <summary>
    /// 单条 UI 绑定：key → 组件。Prefab 上拖好，热更后代码只认 key。
    /// One bind entry: key → component.
    /// </summary>
    [Serializable]
    public class UIBindItem
    {
        public string key;
        public Component target;
    }

    /// <summary>
    /// UI 绑定器。挂在面板根节点上。
    /// 用法：binder.Get&lt;Button&gt;("btn_revive")
    /// </summary>
    public sealed class UIBinder : MonoBehaviour
    {
        [SerializeField] List<UIBindItem> items = new List<UIBindItem>();

        Dictionary<string, Component> _map;

        void Awake() => Rebuild();

        public void Rebuild()
        {
            _map = new Dictionary<string, Component>(StringComparer.Ordinal);
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || string.IsNullOrEmpty(it.key) || it.target == null)
                    continue;
                _map[it.key] = it.target;
            }
        }

        /// <summary>运行时注册（代码动态搭 UI 时用）。</summary>
        public void Set(string key, Component target)
        {
            if (_map == null) Rebuild();
            if (string.IsNullOrEmpty(key) || target == null) return;
            _map[key] = target;

            // 同步到序列化列表，方便调试看
            if (items == null) items = new List<UIBindItem>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].key == key)
                {
                    items[i].target = target;
                    return;
                }
            }

            items.Add(new UIBindItem { key = key, target = target });
        }

        public T Get<T>(string key) where T : Component
        {
            if (_map == null) Rebuild();
            if (_map != null && _map.TryGetValue(key, out var c) && c is T t)
                return t;

            Debug.LogWarning($"[UIBinder] 找不到绑定: {key} ({typeof(T).Name}) on {name}");
            return null;
        }

        public bool TryGet<T>(string key, out T value) where T : Component
        {
            value = Get<T>(key);
            return value != null;
        }

        /// <summary>编辑器菜单用：暴露列表。</summary>
        public System.Collections.Generic.List<UIBindItem> Items => items;
    }
}
