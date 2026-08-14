#if UNITY_EDITOR
using System.Collections.Generic;
using JojoP.AOT.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JojoP.EditorTools.Settings
{
    /// <summary>
    /// Edit → Project Settings → JojoP。
    /// 统一看热更地址、渠道、HybridCLR 程序集镜像；以后扩展字段都加到 JojoPGlobalSettings。
    /// </summary>
    public sealed class JojoPSettingsProvider : SettingsProvider
    {
        const string PathInProjectSettings = "Project/JojoP";
        SerializedObject _so;
        JojoPGlobalSettings _settings;

        public JojoPSettingsProvider()
            : base(PathInProjectSettings, SettingsScope.Project)
        {
            keywords = new HashSet<string>(new[]
            {
                "JojoP", "YooAsset", "HybridCLR", "CDN", "Channel", "HotUpdate", "AOT"
            });
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settings = JojoPHybridClrSync.EnsureAsset();
            JojoPGlobalSettings.ClearCache();
            _so = new SerializedObject(_settings);
        }

        public override void OnGUI(string searchContext)
        {
            if (_settings == null || _so == null || !_so.targetObject)
            {
                _settings = JojoPHybridClrSync.EnsureAsset();
                _so = new SerializedObject(_settings);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "JojoP 框架总设置。HybridCLR 官方编译/桥接仍以 Project/HybridCLR Settings 为准；" +
                "这里做业务侧镜像与热更地址，避免到处点开 xxx.asset。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选中 JojoPGlobalSettings.asset", GUILayout.Height(24)))
                {
                    Selection.activeObject = _settings;
                    EditorGUIUtility.PingObject(_settings);
                }

                if (GUILayout.Button("打开官方 HybridCLR Settings", GUILayout.Height(24)))
                    SettingsService.OpenProjectSettings("Project/HybridCLR Settings");
            }

            EditorGUILayout.Space(8);
            _so.Update();
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(_so.FindProperty("boot"), true);
                EditorGUILayout.Space(6);
                EditorGUILayout.PropertyField(_so.FindProperty("host"), true);
                EditorGUILayout.Space(6);

                EditorGUILayout.LabelField("HybridCLR 程序集（镜像）", EditorStyles.boldLabel);
                if (GUILayout.Button("从 HybridCLR Settings 同步 HotUpdate / AOT 列表"))
                {
                    _so.ApplyModifiedPropertiesWithoutUndo();
                    JojoPHybridClrSync.SyncFromHybridClr(_settings);
                    JojoPGlobalSettings.ClearCache();
                    _so = new SerializedObject(_settings);
                    GUIUtility.ExitGUI();
                    return;
                }

                EditorGUILayout.PropertyField(_so.FindProperty("hybridClr"), true);

                if (check.changed)
                {
                    _so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(_settings);
                }
            }

            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("扩展约定", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "以后要加：活动开关、隐私默认 URL、广告默认位等，直接在 JojoPGlobalSettings 里加 [Serializable] 区块，" +
                "本面板会自动画出（PropertyField）。运行时用 JojoPGlobalSettings.Load()。",
                MessageType.None);
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider() => new JojoPSettingsProvider();
    }

    public static class JojoPSettingsMenu
    {
        [MenuItem("JojoP/打开框架设置 (Project Settings)")]
        public static void Open()
        {
            JojoPHybridClrSync.EnsureAsset();
            SettingsService.OpenProjectSettings("Project/JojoP");
        }
    }
}
#endif
