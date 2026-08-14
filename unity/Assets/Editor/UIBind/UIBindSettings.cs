using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace JojoP.Editor.UIBind
{
    /// <summary>全局配置（仅一份）。不存每个 Form 的组件表。</summary>
    public sealed class UIBindSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Editor/UIBind/UIBindSettings.asset";

        [FolderPath, LabelText("代码根目录")]
        [Tooltip("其下按 Prefab 名建子文件夹")]
        public string CodeRoot = "Assets/HotUpdate/UI";

        [LabelText("命名空间")]
        public string NamespaceName = "JojoP.HotUpdate.UI";

        [LabelText("基类（含命名空间）")]
        public string FormBaseType = "JojoP.AOT.UI.UIFormBase";

        [LabelText("扫描 Tag")]
        [Tooltip("可用 Image&Button 复合 Tag")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        public List<string> Tags = new()
        {
            "Button",
            "Image",
            "Text",
            "TextMeshProUGUI",
            "TMP_InputField",
            "Toggle",
            "Slider",
            "ScrollRect",
            "RawImage",
            "InputField",
            "Dropdown",
            "CanvasGroup",
            "RectTransform",
            "GameObject",
            "Image&Button",
        };

        [LabelText("可回调类型")]
        [Tooltip("只有这些类型才会出现可勾选的 Callback（Image/Text 等没有）。")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<string> CallBacks = new()
        {
            "Button",
            "Toggle",
            "InputField",
        };

        [LabelText("默认勾选回调")]
        [Tooltip("首次生成（尚无 Register）时自动勾上的类型。一般只放 Button；输入框等需在窗口里手动勾。")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<string> DefaultCallBackOn = new()
        {
            "Button",
        };

        [Button("打开 UI Bind 窗口", ButtonSizes.Medium)]
        void OpenWindow() => UIBindWindow.Open();

        [Button("同步 Tag 到 TagManager", ButtonSizes.Medium)]
        void SyncTags()
        {
            UIBindGenerator.EnsureTagsExist(this);
            Debug.Log("[UIBind] Tag 已同步到 TagManager。");
        }

        public bool CanHaveCallback(string type) =>
            !string.IsNullOrEmpty(type) && CallBacks != null && CallBacks.Contains(type);

        public bool DefaultCallbackEnabled(string type) =>
            CanHaveCallback(type)
            && DefaultCallBackOn != null
            && DefaultCallBackOn.Contains(type);

        public static UIBindSettings LoadOrCreate()
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UIBindSettings>(AssetPath);
            if (asset != null)
            {
                // 旧资产可能没有 DefaultCallBackOn
                if (asset.DefaultCallBackOn == null || asset.DefaultCallBackOn.Count == 0)
                {
                    asset.DefaultCallBackOn = new List<string> { "Button" };
                    UnityEditor.EditorUtility.SetDirty(asset);
                }
                return asset;
            }

            var dir = System.IO.Path.GetDirectoryName(AssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            asset = CreateInstance<UIBindSettings>();
            UnityEditor.AssetDatabase.CreateAsset(asset, AssetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            return asset;
#else
            return null;
#endif
        }
    }
}
