#if UNITY_EDITOR
using System.IO;
using JojoP.Ads;
using JojoP.AOT;
using JojoP.Backend;
using JojoP.HotUpdate;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JojoP.EditorTools
{
    /// <summary>一键生成配置资源 + Bootstrap / Main 场景。</summary>
    public static class JojoPSetupMenu
    {
        const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        const string MainPath = "Assets/Scenes/Main.unity";
        const string AdsConfigPath = "Assets/HotUpdate/Ads/AdsConfig.asset";
        const string BackendConfigPath = "Assets/HotUpdate/Backend/BackendConfig.asset";

        [MenuItem("JojoP/1. 生成场景和配置")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/HotUpdate/Ads");
            Directory.CreateDirectory("Assets/HotUpdate/Backend");

            var ads = EnsureAsset<AdsConfig>(AdsConfigPath);
            var backend = EnsureAsset<BackendConfig>(BackendConfigPath);

            CreateBootstrapScene();
            CreateMainScene(ads, backend);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(MainPath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JojoP] Bootstrap + Main 已生成。Play Bootstrap → 进主界面 → 我和我的龙兄南弟。");
        }

        [MenuItem("JojoP/打开验收清单")]
        public static void OpenChecklist()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/验收清单.md"));
            if (File.Exists(path))
                EditorUtility.RevealInFinder(path);
            else
                EditorUtility.DisplayDialog("JojoP", "找不到 docs/验收清单.md", "OK");
        }

        static void CreateBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var launcherGo = new GameObject("AppLauncher");
            launcherGo.AddComponent<AppLauncher>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            EditorSceneManager.SaveScene(scene, BootstrapPath);
        }

        static void CreateMainScene(AdsConfig ads, BackendConfig backend)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }

            var appGo = new GameObject("GameApp");
            var app = appGo.AddComponent<GameApp>();
            var so = new SerializedObject(app);
            so.FindProperty("adsConfig").objectReferenceValue = ads;
            so.FindProperty("backendConfig").objectReferenceValue = backend;
            so.FindProperty("privacyPolicyUrl").stringValue = "https://example.com/privacy";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainPath);
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif
