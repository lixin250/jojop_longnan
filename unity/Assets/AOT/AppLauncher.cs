using System;
using Cysharp.Threading.Tasks;
using JojoP.AOT.Boot;
using JojoP.AOT.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JojoP.AOT
{
    /// <summary>
    /// Bootstrap 入口（AOT）：
    /// 闪屏 → Loading（版本差量 / 渠道 YooAsset / HybridCLR）→ 切 Main。
    /// 框架地址 / 程序集镜像：Edit → Project Settings → JojoP。
    /// </summary>
    public sealed class AppLauncher : MonoBehaviour
    {
        public const string MainSceneName = "Main";
        const string HotUpdateEntryType = "JojoP.HotUpdate.GameApp";

        [Header("开发")]
        [Tooltip("勾选=Loading 走短桩（仍显示闪屏/进度），跳过真实 CDN/HybridCLR")]
        [SerializeField] bool devDirectPlay = true;

        [Header("渠道")]
        [Tooltip("空=用 JojoPGlobalSettings / ChannelId；打包脚本可写入如 gp / test")]
        [SerializeField] string channelOverride;

        [Header("闪屏（≤0 则用全局设置）")]
        [SerializeField] float splashSeconds = -1f;

        [Header("YooAsset（空则用全局设置）")]
        [SerializeField] string defaultPackageName;

        [Header("热更入口（可空；Main 场景里也可预挂）")]
        [SerializeField] MonoBehaviour gameApp;

        Canvas _bootCanvas;
        SplashView _splash;
        LoadingView _loading;
        JojoPGlobalSettings _settings;
        string _packageName;
        string _mainScene;
        float _splashSeconds;

        async void Start()
        {
            Application.targetFrameRate = 60;
            DontDestroyOnLoad(gameObject);

            _settings = JojoPGlobalSettings.Load();
            ApplyGlobalDefaults();
            ChannelId.Init(channelOverride);
            EnsureEventSystem();
            ConfigureLauncherCamera();
            BuildBootUi();

            try
            {
                await ShowSplashAsync();
                await RunLoadingAsync();
                TeardownBootUi();
                await EnterMainAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[JojoP.AOT] 启动异常，尝试进 Main\n{e}");
                _loading?.SetProgress(1f, "启动异常，进入游戏…", e.Message);
                await UniTask.Delay(500);
                TeardownBootUi();
                await EnterMainAsync();
            }
        }

        void ApplyGlobalDefaults()
        {
            var boot = _settings != null ? _settings.Boot : null;
            _packageName = !string.IsNullOrEmpty(defaultPackageName)
                ? defaultPackageName
                : (boot?.defaultPackageName ?? "DefaultPackage");
            _mainScene = !string.IsNullOrEmpty(boot?.mainSceneName) ? boot.mainSceneName : MainSceneName;
            _splashSeconds = splashSeconds >= 0f
                ? splashSeconds
                : (boot != null ? boot.splashSeconds : 0f);
        }

        void BuildBootUi()
        {
            var go = new GameObject("BootCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            _bootCanvas = go.GetComponent<Canvas>();
            _bootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _bootCanvas.sortingOrder = 5000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _splash = SplashView.Create(_bootCanvas.transform);
            _loading = LoadingView.Create(_bootCanvas.transform);
        }

        async UniTask ShowSplashAsync()
        {
            _splash.gameObject.SetActive(true);
            _loading.Hide();
            if (_splashSeconds <= 0.001f)
            {
                _splash.Hide();
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(_splashSeconds));
            _splash.Hide();
        }

        async UniTask RunLoadingAsync()
        {
            _loading.Show();
            string hostHint = _settings != null
                ? _settings.Host.BuildChannelCdnRoot(ChannelId.Value, Application.platform.ToString())
                : string.Empty;
            _loading.SetProgress(0f, "准备中…", $"channel={ChannelId.Value}");

            var pipeline = new BootPatchPipeline(
                _packageName,
                devSkipHeavy: devDirectPlay,
                onProgress: (p, status, detail) => _loading.SetProgress(p, status, detail),
                hostCdnRoot: hostHint);

            await pipeline.RunAsync();
        }

        void TeardownBootUi()
        {
            if (_bootCanvas != null)
            {
                Destroy(_bootCanvas.gameObject);
                _bootCanvas = null;
                _splash = null;
                _loading = null;
            }
        }

        async UniTask EnterMainAsync()
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.name == _mainScene)
            {
                EnsureGameApp();
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(_mainScene))
            {
                Debug.LogWarning(
                    $"[JojoP.AOT] 场景 `{_mainScene}` 未进 Build Settings，在当前场景创建 GameApp。" +
                    "请跑菜单 JojoP/1. 生成场景和配置");
                EnsureGameApp();
                return;
            }

            var op = SceneManager.LoadSceneAsync(_mainScene);
            while (op != null && !op.isDone)
                await UniTask.Yield();

            EnsureGameApp();
            Debug.Log($"[JojoP.AOT] 已进入 {_mainScene}（channel={ChannelId.Value}）");
        }

        void EnsureGameApp()
        {
            if (gameApp != null) return;

            var type = FindType(HotUpdateEntryType);
            if (type == null)
            {
                Debug.LogError($"[JojoP.AOT] 找不到类型 {HotUpdateEntryType}");
                return;
            }

            var existing = FindAnyObjectByType(type) as MonoBehaviour;
            if (existing != null)
            {
                gameApp = existing;
                return;
            }

            var go = new GameObject("GameApp");
            gameApp = go.AddComponent(type) as MonoBehaviour;
            Debug.Log("[JojoP.AOT] 已在 Main 创建 GameApp（反射）");
        }

        static void ConfigureLauncherCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                cam = FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.gameObject.name = "Main Camera";
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
