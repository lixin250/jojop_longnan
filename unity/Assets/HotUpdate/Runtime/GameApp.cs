using System;
using JojoP.Ads;
using JojoP.Backend;
using JojoP.Gameplay.Brothers;
using JojoP.Privacy;
using JojoP.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JojoP.HotUpdate
{
    /// <summary>
    /// 热更流程壳：隐私 → 远程配置 → 广告 → 主界面 ↔ 我和我的龙兄南弟。
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        enum AppFlow
        {
            Booting,
            MainMenu,
            Brothers
        }

        [Header("配置（可空，空则运行时造默认）")]
        [SerializeField] AdsConfig adsConfig;
        [SerializeField] BackendConfig backendConfig;
        [SerializeField] string privacyPolicyUrl = "https://example.com/privacy";

        IAdService _ads;
        RewardedCapTracker _rewardedCap;
        CloudflareApiClient _api;
        RemoteGameConfig _remote = new RemoteGameConfig();
        MainMenuView _mainMenu;
        SettingsPanel _settings;
        BrothersSessionController _brothers;
        BrothersModeView _brothersUi;
        Canvas _canvas;
        AppFlow _flow = AppFlow.Booting;
        bool _busyAd;
        string _deviceId;

        async void Start()
        {
            Application.targetFrameRate = 60;
            _deviceId = SystemInfo.deviceUniqueIdentifier;
            EnsureEventSystem();
            _canvas = CreateCanvas();

            if (adsConfig == null)
            {
                adsConfig = ScriptableObject.CreateInstance<AdsConfig>();
                adsConfig.provider = AdProvider.Mock;
            }

            if (backendConfig == null)
            {
                backendConfig = ScriptableObject.CreateInstance<BackendConfig>();
                backendConfig.fetchOnBoot = true;
            }

            _api = new CloudflareApiClient(backendConfig);
            _rewardedCap = new RewardedCapTracker(adsConfig.defaultDailyRewardedCap);

            if (!PrivacyConsent.HasAccepted)
            {
                PrivacyConsentView.Show(_canvas.transform, privacyPolicyUrl, () => _ = AfterConsentAsync());
                return;
            }

            await AfterConsentAsync();
        }

        async System.Threading.Tasks.Task AfterConsentAsync()
        {
            if (backendConfig.fetchOnBoot)
            {
                var remote = await _api.FetchConfigAsync();
                if (remote != null)
                {
                    _remote = remote;
                    _rewardedCap.SetCap(remote.dailyRewardedCap);
                    Debug.Log("[JojoP] 已应用远程配置 / Remote config OK");
                }
            }

            _ads = AdServiceFactory.Create(adsConfig, this);
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            _ads.Initialize(ok => tcs.TrySetResult(ok));
            await tcs.Task;

            EnterMainMenu();
            _ = SyncCloudSaveAsync();
        }

        void EnterMainMenu()
        {
            _flow = AppFlow.MainMenu;
            CleanupBrothersSession();

            if (_mainMenu == null)
                _mainMenu = MainMenuView.Create(_canvas.transform);

            if (_settings == null)
                _settings = SettingsPanel.Create(_canvas.transform);

            _settings.Hide();
            _mainMenu.Show(onSettings: OpenSettings, onBrothers: StartBrothers);
        }

        void OpenSettings()
        {
            if (_settings == null)
                _settings = SettingsPanel.Create(_canvas.transform);
            _settings.Show(privacyPolicyUrl);
        }

        void StartBrothers()
        {
            if (_flow == AppFlow.Brothers || _busyAd) return;

            _flow = AppFlow.Brothers;
            _mainMenu?.Hide();
            _settings?.Hide();
            BattleField.ApplyCamera(Camera.main);

            if (_brothers == null)
            {
                var go = new GameObject("BrothersSession");
                _brothers = go.AddComponent<BrothersSessionController>();
                _brothers.Bootstrap();
            }

            if (_brothersUi == null)
                _brothersUi = BrothersModeView.Create(_canvas.transform);

            _brothersUi.gameObject.SetActive(true);
            _brothersUi.Bind(_brothers, onHome: ReturnToMainMenu, onRewardedAd: TryRewardedForBrothers);
            _brothers.ReturnToHub();
        }

        void TryRewardedForBrothers(Action onSuccess)
        {
            if (_busyAd) return;

            if (!_remote.adsEnabled || !_remote.rewardedEnabled)
            {
                onSuccess?.Invoke();
                return;
            }

            if (!_rewardedCap.CanShow)
            {
                Debug.Log("[JojoP] 今日广告次数已用完");
                return;
            }

            if (!_ads.IsRewardedReady)
            {
                _ads.LoadRewarded();
                Debug.Log("[JojoP] 广告还没好");
                return;
            }

            _busyAd = true;
            _ads.ShowRewarded(success =>
            {
                _busyAd = false;
                if (!success) return;
                _rewardedCap.RecordShow();
                onSuccess?.Invoke();
            });
        }

        void ReturnToMainMenu()
        {
            if (_busyAd) return;
            EnterMainMenu();
            _ = SyncCloudSaveAsync();
        }

        void CleanupBrothersSession()
        {
            if (_brothersUi != null)
            {
                Destroy(_brothersUi.gameObject);
                _brothersUi = null;
            }

            if (_brothers != null)
            {
                Destroy(_brothers.gameObject);
                _brothers = null;
            }
        }

        async System.Threading.Tasks.Task SyncCloudSaveAsync()
        {
            if (_api == null || !_api.HasBaseUrl) return;
            string payload = $"{{\"device\":\"{_deviceId}\"}}";
            await _api.PutSaveAsync(_deviceId, payload);
        }

        Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }
    }
}
