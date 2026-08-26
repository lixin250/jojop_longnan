#if UNITY_EDITOR
using JojoP.AOT.Settings;
using JojoP.EditorTools.Build;
using UnityEditor;
using UnityEngine;

namespace JojoP.EditorTools.Settings
{
    [CustomEditor(typeof(JojoPGlobalSettings))]
    sealed class JojoPGlobalSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("构建与热更", GUILayout.Height(24)))
                    JojoPBuildWindow.Open();
                if (GUILayout.Button("Project Settings", GUILayout.Height(24)))
                    SettingsService.OpenProjectSettings("Project/JojoP");
            }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }
    }
}
#endif
