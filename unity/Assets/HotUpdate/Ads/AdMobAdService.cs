using System;
using System.Collections;
using UnityEngine;

#if JOJOP_HAS_ADMOB
using GoogleMobileAds.Api;
#endif

namespace JojoP.Ads
{
    /// <summary>
    /// AdMob 实现。没装 SDK / 没加宏时，走“像真广告一样的模拟”，保证能编译。
    /// Without JOJOP_HAS_ADMOB: compiles with simulation. See docs/广告接入.md
    /// </summary>
    public sealed class AdMobAdService : IAdService
    {
        readonly AdsConfig _config;
        readonly MonoBehaviour _runner;

#if JOJOP_HAS_ADMOB
        RewardedAd _rewarded;
        InterstitialAd _interstitial;
#endif

        public bool IsInitialized { get; private set; }

        public bool IsRewardedReady
        {
            get
            {
#if JOJOP_HAS_ADMOB
                return IsInitialized && _rewarded != null && _rewarded.CanShowAd();
#else
                return IsInitialized;
#endif
            }
        }

        public bool IsInterstitialReady
        {
            get
            {
#if JOJOP_HAS_ADMOB
                return IsInitialized && _interstitial != null && _interstitial.CanShowAd();
#else
                return IsInitialized;
#endif
            }
        }

        public AdMobAdService(AdsConfig config, MonoBehaviour runner)
        {
            _config = config;
            _runner = runner;
        }

        public void Initialize(Action<bool> onComplete = null)
        {
#if JOJOP_HAS_ADMOB
            MobileAds.Initialize(_ =>
            {
                IsInitialized = true;
                Debug.Log("[JojoP.Ads] AdMob 初始化完成");
                LoadRewarded();
                LoadInterstitial();
                onComplete?.Invoke(true);
            });
#else
            Debug.LogWarning(
                "[JojoP.Ads] 选了 AdMob 但未定义 JOJOP_HAS_ADMOB，暂用模拟。请看 docs/广告接入.md");
            IsInitialized = true;
            onComplete?.Invoke(true);
#endif
        }

        public void LoadRewarded()
        {
#if JOJOP_HAS_ADMOB
            _rewarded?.Destroy();
            _rewarded = null;
            RewardedAd.Load(_config.androidRewardedUnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[JojoP.Ads] 激励加载失败: {error}");
                    _runner.StartCoroutine(CoRetry(LoadRewarded, 15f));
                    return;
                }

                _rewarded = ad;
            });
#endif
        }

        public void LoadInterstitial()
        {
#if JOJOP_HAS_ADMOB
            _interstitial?.Destroy();
            _interstitial = null;
            InterstitialAd.Load(_config.androidInterstitialUnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[JojoP.Ads] 插屏加载失败: {error}");
                    _runner.StartCoroutine(CoRetry(LoadInterstitial, 15f));
                    return;
                }

                _interstitial = ad;
            });
#endif
        }

        public void ShowRewarded(Action<bool> onCompleted)
        {
#if JOJOP_HAS_ADMOB
            if (!IsRewardedReady)
            {
                onCompleted?.Invoke(false);
                LoadRewarded();
                return;
            }

            _rewarded.Show(_ =>
            {
                onCompleted?.Invoke(true);
                LoadRewarded();
            });
#else
            _runner.StartCoroutine(CoSim(onCompleted, null));
#endif
        }

        public void ShowInterstitial(Action onClosed = null)
        {
#if JOJOP_HAS_ADMOB
            if (!IsInterstitialReady)
            {
                onClosed?.Invoke();
                LoadInterstitial();
                return;
            }

            _interstitial.OnAdFullScreenContentClosed += () =>
            {
                onClosed?.Invoke();
                LoadInterstitial();
            };
            _interstitial.OnAdFullScreenContentFailed += _ =>
            {
                onClosed?.Invoke();
                LoadInterstitial();
            };
            _interstitial.Show();
#else
            _runner.StartCoroutine(CoSim(null, onClosed));
#endif
        }

#if JOJOP_HAS_ADMOB
        IEnumerator CoRetry(Action action, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            action?.Invoke();
        }
#else
        IEnumerator CoSim(Action<bool> rewardedDone, Action interstitialDone)
        {
            float delay = rewardedDone != null
                ? _config.mockRewardedDelaySeconds
                : _config.mockInterstitialDelaySeconds;
            yield return new WaitForSecondsRealtime(delay);
            rewardedDone?.Invoke(true);
            interstitialDone?.Invoke();
        }
#endif
    }
}
