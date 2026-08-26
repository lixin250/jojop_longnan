using System;
using Cysharp.Threading.Tasks;
using JojoP.AOT.Boot;
using JojoP.AOT.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YooAsset;

namespace JojoP.AOT
{
    /// <summary>
    /// Bootstrap 入口（AOT）：闪屏 → Loading → Yoo 打开 Main。
    /// 产品配置只读 JojoPGlobalSettings。Main 上的 GameApp 由场景自己跑。
    /// </summary>
    public sealed class AppLauncher : MonoBehaviour
    {
        public const string MainSceneName = "Main";

        [Header("仅本场景（不要写进 GlobalSettings）")]
        [Tooltip("勾选=跳过 CDN。测 R2 / 出包必须关掉。")]
        [SerializeField] bool devDirectPlay = true;

        [Tooltip("空=用 JojoPGlobalSettings.boot.defaultChannel。")]
        [SerializeField] string channelOverride;

#if UNITY_EDITOR
        /// <summary>编辑器挂钩：用 LoadSceneAsyncInPlayMode 打开未进 Build Profile 的 Main。</summary>
        public static Func<string, UniTask> LoadSceneInEditorPlay;
#endif

        Canvas _bootCanvas;
        SplashView _splash;
        LoadingView _loading;
        JojoPGlobalSettings _settings;
        string _packageName;
        string _mainScene;
        float _splashSeconds;
        YooAsset.SceneHandle _mainSceneHandle;

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
                await EnterMainAsync();
                TeardownBootUi();
            }
            catch (Exception e)
            {
                Debug.LogError($"[JojoP.AOT] 启动失败，停在 Loading\n{e}");
                _loading?.SetProgress(1f, "启动失败", e.Message);
            }
        }

        void ApplyGlobalDefaults()
        {
            var boot = _settings != null ? _settings.Boot : null;
            _packageName = boot != null && !string.IsNullOrEmpty(boot.defaultPackageName)
                ? boot.defaultPackageName
                : "DefaultPackage";
            _mainScene = boot != null && !string.IsNullOrEmpty(boot.mainSceneName)
                ? boot.mainSceneName
                : MainSceneName;
            _splashSeconds = boot != null ? boot.splashSeconds : 0f;
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
            var host = _settings != null ? _settings.Host : null;
            string platform = JojoPHostSettings.YooPlatformFolder();
            string hostHint = host != null
                ? host.BuildChannelCdnRoot(ChannelId.Value, platform)
                : string.Empty;
            string fallbackHint = host != null
                ? host.BuildChannelCdnRoot(ChannelId.Value, platform, fallback: true)
                : hostHint;
            bool hostUpdate = host != null && host.UseRemoteCdn;
            _loading.SetProgress(0f, "准备中…", $"channel={ChannelId.Value}  {platform}");
#if UNITY_EDITOR
            _loading.SetVersionBoard(
                devDirectPlay
                    ? "Editor Play · 直开（devDirectPlay）"
                    : "Editor Play · EditorSimulateMode（不拉 R2）",
                "", "", hostHint);
#endif

            var pipeline = new BootPatchPipeline(
                _packageName,
                devSkipHeavy: devDirectPlay,
                onProgress: (p, status, detail) => _loading.SetProgress(p, status, detail),
                hostCdnRoot: hostHint,
                fallbackCdnRoot: fallbackHint,
                enableHostUpdate: hostUpdate,
                onVersionBoard: (mode, local, remote, url) =>
                    _loading.SetVersionBoard(mode, local, remote, url));

            await pipeline.RunAsync();
        }

        void TeardownBootUi()
        {
            if (_bootCanvas == null) return;
            Destroy(_bootCanvas.gameObject);
            _bootCanvas = null;
            _splash = null;
            _loading = null;
        }

        async UniTask EnterMainAsync()
        {
            if (devDirectPlay)
            {
#if UNITY_EDITOR
                if (LoadSceneInEditorPlay == null)
                    throw new Exception("编辑器未挂钩 LoadSceneInEditorPlay（JojoPEditorPlayBoot）");
                await LoadSceneInEditorPlay(_mainScene);
                Debug.Log($"[JojoP.AOT] Editor 直开 {_mainScene}");
                return;
#else
                throw new Exception("正式包不能开 AppLauncher.devDirectPlay");
#endif
            }

            if (!YooAssets.TryGetPackage(_packageName, out var package))
                throw new Exception($"Yoo 包未初始化: {_packageName}");
            if (!package.IsLocationValid(_mainScene))
                throw new Exception($"Yoo 没有场景地址 `{_mainScene}`");

            _mainSceneHandle = package.LoadSceneAsync(_mainScene, LoadSceneMode.Single);
            while (_mainSceneHandle != null && !_mainSceneHandle.IsDone)
                await UniTask.Yield();
            if (_mainSceneHandle == null || _mainSceneHandle.Status != EOperationStatus.Succeeded)
                throw new Exception($"Yoo 加载 `{_mainScene}` 失败: {_mainSceneHandle?.Error}");

            Debug.Log($"[JojoP.AOT] 已从 Yoo 进入 {_mainScene}（channel={ChannelId.Value}）");
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
    }
}
