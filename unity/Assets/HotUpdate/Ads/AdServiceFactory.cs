using UnityEngine;

namespace JojoP.Ads
{
    /// <summary>按配置创建广告实现。</summary>
    public static class AdServiceFactory
    {
        public static IAdService Create(AdsConfig config, MonoBehaviour runner)
        {
            if (config == null)
            {
                Debug.LogError("[JojoP.Ads] 缺少 AdsConfig，回退 Mock");
                config = ScriptableObject.CreateInstance<AdsConfig>();
                config.provider = AdProvider.Mock;
            }

            return config.provider == AdProvider.AdMob
                ? (IAdService)new AdMobAdService(config, runner)
                : new MockAdService(config, runner);
        }
    }
}
