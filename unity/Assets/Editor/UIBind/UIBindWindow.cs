using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace JojoP.Editor.UIBind
{
    /// <summary>
    /// 选 Prefab → 扫描 → 仅「可回调类型」可勾 Callback → 生成/绑定。
    /// Image/Text 等无 Callback；Button 默认勾；InputField 等需手动勾。
    /// </summary>
    public sealed class UIBindWindow : OdinEditorWindow
    {
        [MenuItem("Tools/UI Bind/Window")]
        public static void Open()
        {
            var w = GetWindow<UIBindWindow>();
            w.titleContent = new GUIContent("UI Bind");
            w.minSize = new Vector2(520, 360);
            w.Show();
        }

        [MenuItem("Assets/UI Bind/Open Window", false, 2000)]
        public static void OpenWithSelection()
        {
            if (!TryGetSelectedPrefab(out var prefab))
            {
                Open();
                return;
            }

            var w = GetWindow<UIBindWindow>();
            w.titleContent = new GUIContent("UI Bind");
            w.minSize = new Vector2(520, 360);
            w.Prefab = prefab;
            w.Show();
            w.RefreshScan();
        }

        [MenuItem("Assets/UI Bind/Open Window", true)]
        static bool OpenWithSelectionValidate() => TryGetSelectedPrefab(out _);

        static bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = null;
            if (Selection.activeObject is not GameObject go) return false;
            var path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return false;
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null;
        }

        [Title("目标 Prefab", bold: false)]
        [AssetsOnly, Required]
        [OnValueChanged(nameof(RefreshScan))]
        [LabelText("Prefab")]
        public GameObject Prefab;

        [ShowInInspector, ReadOnly, LabelText("类名")]
        string ClassName => Prefab != null ? Prefab.name : "—";

        [Title("扫描结果",
            subtitle: "仅 Button/Toggle/InputField 等可勾 Callback。Button 默认开；输入框等需勾选才会注册事件。",
            bold: false)]
        [ShowInInspector]
        [TableList(AlwaysExpanded = true, IsReadOnly = false, ShowIndexLabels = false,
            DrawScrollView = true, MaxScrollViewHeight = 280,
            HideToolbar = true)]
        [HideLabel]
        List<UIBindScanRow> _rows = new();

        [PropertySpace(10)]
        [HorizontalGroup("Gen")]
        [Button("刷新", ButtonSizes.Large)]
        void RefreshScan()
        {
            _rows = new List<UIBindScanRow>();
            if (Prefab == null) return;

            var path = AssetDatabase.GetAssetPath(Prefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[UIBind] 请指定 Project 中的 Prefab 资源。");
                return;
            }

            var settings = UIBindSettings.LoadOrCreate();
            UIBindGenerator.EnsureTagsExist(settings);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var comps = UIBindGenerator.CollectWithCallbackState(asset, settings);
            foreach (var c in comps)
            {
                _rows.Add(new UIBindScanRow
                {
                    Path = c.Path,
                    Type = c.Type,
                    Field = c.Name,
                    CanCallback = settings.CanHaveCallback(c.Type),
                    Callback = c.Callback,
                });
            }
        }

        [HorizontalGroup("Gen")]
        [Button("生成代码", ButtonSizes.Large), GUIColor(0.4f, 0.85f, 0.55f)]
        void Generate() => Run(bind: false);

        [HorizontalGroup("Gen")]
        [Button("仅绑定", ButtonSizes.Large), GUIColor(0.55f, 0.7f, 1f)]
        void BindOnly() => Run(bindOnly: true);

        [HorizontalGroup("Gen")]
        [Button("生成并绑定", ButtonSizes.Large), GUIColor(0.95f, 0.75f, 0.35f)]
        void GenerateAndBind() => Run(bind: true);

        void Run(bool bind = false, bool bindOnly = false)
        {
            if (!TryGetPrefabAsset(out var asset)) return;
            var settings = UIBindSettings.LoadOrCreate();
            UIBindGenerator.EnsureTagsExist(settings);

            var comps = UIBindGenerator.Collect(asset, settings);
            var map = _rows?.ToDictionary(r => r.Field, r => r.CanCallback && r.Callback)
                      ?? new Dictionary<string, bool>();
            UIBindGenerator.MergeCallbacks(comps, map);
            // 不可回调类型强制 false
            foreach (var c in comps)
            {
                if (!settings.CanHaveCallback(c.Type))
                    c.Callback = false;
            }

            if (bindOnly)
                UIBindGenerator.BindOnly(asset, settings, comps);
            else if (bind)
                UIBindGenerator.GenerateAndBind(asset, settings, comps);
            else
                UIBindGenerator.GenerateCode(asset, settings, comps);

            RefreshScan();
        }

        bool TryGetPrefabAsset(out GameObject asset)
        {
            asset = null;
            if (Prefab == null)
            {
                EditorUtility.DisplayDialog("UI Bind", "请先指定 Prefab。", "OK");
                return false;
            }

            var path = AssetDatabase.GetAssetPath(Prefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("UI Bind", "请指定 Project 中的 Prefab 资源。", "OK");
                return false;
            }

            asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return asset != null;
        }

        [Serializable]
        public sealed class UIBindScanRow
        {
            [TableColumnWidth(200), ReadOnly]
            public string Path;

            [TableColumnWidth(110), ReadOnly]
            public string Type;

            [TableColumnWidth(150), ReadOnly]
            public string Field;

            [TableColumnWidth(70)]
            [EnableIf(nameof(CanCallback))]
            public bool Callback;

            [HideInInspector]
            public bool CanCallback;
        }
    }
}
