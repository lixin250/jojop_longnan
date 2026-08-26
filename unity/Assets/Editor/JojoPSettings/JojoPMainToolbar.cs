#if UNITY_EDITOR
using System.Collections.Generic;
using JojoP.AOT.Settings;
using JojoP.EditorTools.Build;
using UnityEditor;
using UnityEditor.Toolbars;

namespace JojoP.EditorTools.Settings
{
    /// <summary>
    /// 主工具条（Play / Pause / Step 同一行，Middle dock）：设置 → Inspector，热更 → 构建窗口。
    /// 若没出现：工具条空白处右键 → JojoP，勾上这两个。
    /// </summary>
    static class JojoPMainToolbar
    {
        const string ElementId = "JojoP/QuickAccess";

        [MainToolbarElement(ElementId, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 100)]
        static IEnumerable<MainToolbarElement> Create()
        {
            yield return new MainToolbarButton(
                new MainToolbarContent("设置", "选中 JojoPGlobalSettings，在 Inspector 里改 CDN / 渠道"),
                OpenSettingsInInspector);

            yield return new MainToolbarButton(
                new MainToolbarContent("热更", "打开 JojoP → 构建与热更"),
                JojoPBuildWindow.Open);
        }

        public static void OpenSettingsInInspector()
        {
            var settings = JojoPHybridClrSync.EnsureAsset();
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
#endif
