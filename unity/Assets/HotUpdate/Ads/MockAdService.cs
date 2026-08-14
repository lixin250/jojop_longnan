using System;
using System.Collections;
using UnityEngine;

namespace JojoP.Ads
{
    /// <summary>假广告：无网也能测复活/双倍/插屏流程。</summary>
    public sealed class MockAdService : IAdService
    {
        readonly AdsConfig _config;
        readonly MonoBehaviour _runner;
        bool _rewardedReady = true;
        bool _interstitialReady = true;

        public bool IsInitialized { get; private set; }
        public bool IsRewardedReady => IsInitialized && _rewardedReady;
        public bool IsInterstitialReady => IsInitialized && _interstitialReady;

        public MockAdService(AdsConfig config, MonoBehaviour runner)
        {
            _config = config;
            _runner = runner;
        }

        public void Initialize(Action<bool> onComplete = null)
        {
            IsInitialized = true;
            Debug.Log("[JojoP.Ads] Mock 广告已初始化");
            onComplete?.Invoke(true);
        }

        public void LoadRewarded() => _rewardedReady = true;
        public void LoadInterstitial() => _interstitialReady = true;

        public void ShowRewarded(Action<bool> onCompleted)
        {
            if (!IsRewardedReady)
            {
                onCompleted?.Invoke(false);
                return;
            }

            _rewardedReady = false;
            _runner.StartCoroutine(CoFakeRewarded(onCompleted));
        }

        public void ShowInterstitial(Action onClosed = null)
        {
            if (!IsInterstitialReady)
            {
                onClosed?.Invoke();
                return;
            }

            _interstitialReady = false;
            _runner.StartCoroutine(CoFakeInterstitial(onClosed));
        }

        IEnumerator CoFakeRewarded(Action<bool> onCompleted)
        {
            Debug.Log("[JojoP.Ads] Mock 激励播放中...");
            yield return new WaitForSecondsRealtime(_config.mockRewardedDelaySeconds);
            onCompleted?.Invoke(true);
            LoadRewarded();
        }

        IEnumerator CoFakeInterstitial(Action onClosed)
        {
            Debug.Log("[JojoP.Ads] Mock 插屏播放中...");
            yield return new WaitForSecondsRealtime(_config.mockInterstitialDelaySeconds);
            onClosed?.Invoke();
            LoadInterstitial();
        }
    }
}
