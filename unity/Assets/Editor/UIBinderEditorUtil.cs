#if UNITY_EDITOR
using JojoP.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.EditorTools
{
    /// <summary>UIBinder 右键：按子节点名字自动收集绑定。</summary>
    public static class UIBinderEditorUtil
    {
        [MenuItem("CONTEXT/UIBinder/自动收集子节点（按名字）")]
        static void AutoCollect(MenuCommand cmd)
        {
            var binder = cmd.context as UIBinder;
            if (binder == null) return;

            var items = binder.Items;
            items.Clear();
            foreach (var t in binder.GetComponentsInChildren<Transform>(true))
            {
                if (t == binder.transform) continue;

                Component c = t.GetComponent<Button>();
                if (c == null) c = t.GetComponent<Text>();
                if (c == null) c = t.GetComponent<Image>();
                if (c == null) c = t;

                items.Add(new UIBindItem { key = t.name, target = c });
            }

            EditorUtility.SetDirty(binder);
            Debug.Log($"[UIBinder] 已收集 {items.Count} 条绑定");
        }
    }
}
#endif
