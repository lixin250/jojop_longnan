using UnityEngine;

namespace JojoP.UI
{
    /// <summary>
    /// 面板基类：统一拿 UIBinder，方便以后 YooAsset 加载 Prefab。
    /// Panel base with UIBinder.
    /// </summary>
    public abstract class UIPanelBase : MonoBehaviour
    {
        [SerializeField] protected UIBinder binder;

        protected virtual void Awake()
        {
            if (binder == null)
                binder = GetComponent<UIBinder>() ?? gameObject.AddComponent<UIBinder>();
            binder.Rebuild();
            OnBind();
        }

        /// <summary>在这里 Get 按钮/文本并绑事件。</summary>
        protected abstract void OnBind();

        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
    }
}
