using UnityEngine;

namespace JojoP.Ads
{
    /// <summary>广告配置。默认用 Google 测试 ID，上线再换成自己的。</summary>
    [CreateAssetMenu(fileName = "AdsConfig", menuName = "JojoP/广告配置 AdsConfig")]
    public class AdsConfig : ScriptableObject
    {
        [Header("模式")]
        [Tooltip("Mock=本地假广告；AdMob=真 SDK（需装插件并加宏 JOJOP_HAS_ADMOB）")]
        public AdProvider provider = AdProvider.Mock;

        [Header("Android 广告位（测试 ID）")]
        public string androidAppId = "ca-app-pub-3940256099942544~3347511713";
        public string androidRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";
        public string androidInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";

        [Header("频控")]
        [Tooltip("每天最多激励次数（远程配置还能再压）")]
        public int defaultDailyRewardedCap = 8;

        public float mockRewardedDelaySeconds = 1.25f;
        public float mockInterstitialDelaySeconds = 0.8f;
    }

    public enum AdProvider
    {
        Mock = 0,
        AdMob = 1
    }
}
