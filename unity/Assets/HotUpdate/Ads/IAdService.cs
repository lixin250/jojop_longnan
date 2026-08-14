using System;
using UnityEngine;

namespace JojoP.Ads
{
    /// <summary>
    /// 广告能力接口。玩法只依赖接口，方便换 Mock / AdMob / 以后聚合。
    /// Ad API used by gameplay. Swap Mock / AdMob without touching game code.
    /// </summary>
    public interface IAdService
    {
        bool IsInitialized { get; }
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }

        void Initialize(Action<bool> onComplete = null);
        void LoadRewarded();
        void LoadInterstitial();

        /// <summary>激励视频。onCompleted(true)=看完发奖。</summary>
        void ShowRewarded(Action<bool> onCompleted);

        /// <summary>插屏。关闭后回调。</summary>
        void ShowInterstitial(Action onClosed = null);
    }
}
