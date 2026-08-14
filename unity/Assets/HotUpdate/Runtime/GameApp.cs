using System;
using JojoP.Ads;
using JojoP.Backend;
using JojoP.Gameplay;
using JojoP.Gameplay.Brothers;
using JojoP.Privacy;
using JojoP.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JojoP.HotUpdate
{
    /// <summary>
    /// 热更流程壳：隐私 → 远程配置 → 广告 → 主界面 ↔ 我和我的龙兄南弟 / 叠叠乐。
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        enum AppFlow
        {
            Booting,
            MainMenu,
            InGame,
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
        StackGameController _game;
        GameHud _hud;
        MainMenuView _mainMenu;
        SettingsPanel _settings;
        BrothersSessionController _brothers;
        BrothersModeView _brothersUi;
        Canvas _canvas;
        AppFlow _flow = AppFlow.Booting;
        int _retryCount;
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
            CleanupGameSession();
            CleanupBrothersSession();

            if (_mainMenu == null)
                _mainMenu = MainMenuView.Create(_canvas.transform);

            if (_settings == null)
                _settings = SettingsPanel.Create(_canvas.transform);

            _settings.Hide();
            _mainMenu.Show(GetBestScore(), onStart: StartGame, onSettings: OpenSettings, onBrothers: StartBrothers);
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
            CleanupGameSession();
            EnsureMainCamera();
            if (Camera.main != null)
            {
                Camera.main.orthographic = true;
                Camera.main.orthographicSize = 5f;
                Camera.main.transform.position = new Vector3(0f, 0.5f, -10f);
            }

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
                // 竖切：远程关广告时直接发奖，方便本地验收
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

        void StartGame()
        {
            if (_flow == AppFlow.InGame) return;

            _flow = AppFlow.InGame;
            _mainMenu?.Hide();
            _settings?.Hide();
            CleanupBrothersSession();
            _retryCount = 0;

            BuildGameSession();
            _hud.SetHint("点击落块");
            _game.StartRound();
        }

        void ReturnToMainMenu()
        {
            if (_busyAd) return;
            EnterMainMenu();
            _ = SyncCloudSaveAsync();
        }

        void BuildGameSession()
        {
            EnsureMainCamera();

            if (_game == null)
            {
                var gameGo = new GameObject("StackGame");
                _game = gameGo.AddComponent<StackGameController>();
                _game.Configure(_remote.blockSpeed, _remote.speedRampPerScore, _remote.minOverlapRatio);
                _game.Bootstrap(Camera.main);
                _game.ScoreChanged += score =>
                {
                    _hud?.SetScore(score);
                    TryUpdateBest(score);
                };
                _game.RoundFailed += OnRoundFailed;
            }

            if (_hud == null)
                _hud = GameHud.Create(_canvas.transform);

            _hud.gameObject.SetActive(true);
            _hud.HideGameOver();
            _hud.SetScore(0);
        }

        void CleanupGameSession()
        {
            if (_game != null)
            {
                Destroy(_game.gameObject);
                _game = null;
            }

            if (_hud != null)
            {
                Destroy(_hud.gameObject);
                _hud = null;
            }
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

        void Update()
        {
            if (_flow != AppFlow.InGame || _game == null || !_game.IsPlaying || _busyAd)
                return;

            bool tap = Input.GetMouseButtonDown(0) ||
                       (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (!tap || IsPointerOverUi())
                return;

            _game.HandleTap();
        }

        void OnRoundFailed(int score)
        {
            TryUpdateBest(score);
            _hud.ShowGameOver(
                score,
                onRevive: () => TryRewarded(Revive),
                onDouble: () => TryRewarded(DoubleScore),
                onRetry: TryRetry,
                onHome: ReturnToMainMenu);
        }

        void TryRewarded(Action onSuccess)
        {
            if (_busyAd) return;

            if (!_remote.adsEnabled || !_remote.rewardedEnabled)
            {
                Debug.Log("[JojoP] 远程关掉了激励广告");
                return;
            }

            if (!_rewardedCap.CanShow)
            {
                _hud.SetHint("今日广告次数已用完");
                return;
            }

            if (!_ads.IsRewardedReady)
            {
                _ads.LoadRewarded();
                _hud.SetHint("广告还没好，稍后再试");
                return;
            }

            _busyAd = true;
            _ads.ShowRewarded(success =>
            {
                _busyAd = false;
                if (!success)
                {
                    _hud.SetHint("广告失败");
                    return;
                }

                _rewardedCap.RecordShow();
                onSuccess?.Invoke();
            });
        }

        void Revive()
        {
            if (_game != null && _game.TryRevive())
            {
                _hud.HideGameOver();
                _hud.SetHint("点击落块");
            }
            else
            {
                _hud.SetHint("本局已复活过");
            }
        }

        void DoubleScore()
        {
            if (_game == null) return;
            _game.ApplyDoubleScore();
            _hud.SetScore(_game.Score);
            _hud.SetHint("分数已翻倍！");
            TryUpdateBest(_game.Score);
            _ = SyncCloudSaveAsync();
        }

        void TryRetry()
        {
            if (_busyAd || _game == null || _hud == null) return;

            void StartFresh()
            {
                _retryCount++;
                _hud.HideGameOver();
                _hud.SetHint("点击落块");
                _game.StartRound();
                _ = SyncCloudSaveAsync();
            }

            bool needInterstitial =
                _remote.adsEnabled &&
                _remote.interstitialEnabled &&
                _remote.interstitialEveryNRetries > 0 &&
                _retryCount > 0 &&
                _retryCount % _remote.interstitialEveryNRetries == 0 &&
                _ads.IsInterstitialReady;

            if (!needInterstitial)
            {
                StartFresh();
                return;
            }

            _busyAd = true;
            _ads.ShowInterstitial(() =>
            {
                _busyAd = false;
                StartFresh();
            });
        }

        static int GetBestScore() => PlayerPrefs.GetInt("jojop.best", 0);

        static void TryUpdateBest(int score)
        {
            int best = GetBestScore();
            if (score <= best) return;
            PlayerPrefs.SetInt("jojop.best", score);
            PlayerPrefs.Save();
        }

        async System.Threading.Tasks.Task SyncCloudSaveAsync()
        {
            if (_api == null || !_api.HasBaseUrl) return;

            int best = GetBestScore();
            if (_game != null && _game.Score > best)
            {
                best = _game.Score;
                PlayerPrefs.SetInt("jojop.best", best);
                PlayerPrefs.Save();
            }

            string payload = $"{{\"best\":{best},\"device\":\"{_deviceId}\"}}";
            await _api.PutSaveAsync(_deviceId, payload);
        }

        static void EnsureMainCamera()
        {
            var cam = Camera.main;
            if (cam != null) return;

            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
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

        static bool IsPointerOverUi()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0)
                return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
