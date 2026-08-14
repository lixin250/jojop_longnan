using UnityEngine;

namespace JojoP.AOT.UI
{
    /// <summary>
    /// UI 窗口基类。场景挂载时 Awake/OnEnable 自动走生命周期。
    /// 以后接 UIManager 时，可关掉场景自托管。
    /// </summary>
    public abstract class UIFormBase : UIBehaviour
    {
        [SerializeField]
        bool _autoLifecycleWhenSceneHosted = true;

        /// <summary>由 UIManager 打开时关闭场景自托管生命周期。</summary>
        public void SetManagedByManager() => _autoLifecycleWhenSceneHosted = false;

        protected virtual void Awake()
        {
            if (_autoLifecycleWhenSceneHosted)
                EnsureReady();
        }

        protected virtual void OnEnable()
        {
            if (_autoLifecycleWhenSceneHosted)
                InternalOpen();
        }

        protected virtual void OnDisable()
        {
            if (_autoLifecycleWhenSceneHosted)
                InternalClose();
        }

        protected void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
