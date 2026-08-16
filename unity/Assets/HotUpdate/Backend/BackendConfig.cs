using System;
using UnityEngine;

namespace JojoP.Backend
{
    /// <summary>远程玩法/广告开关。字段名要和 Worker JSON 对得上。</summary>
    [Serializable]
    public class RemoteGameConfig
    {
        public bool adsEnabled = true;
        public bool rewardedEnabled = true;
        public bool interstitialEnabled = true;
        public int dailyRewardedCap = 8;
        public int interstitialEveryNRetries = 2;
    }

    [CreateAssetMenu(fileName = "BackendConfig", menuName = "JojoP/后端配置 BackendConfig")]
    public class BackendConfig : ScriptableObject
    {
        [Tooltip("Cloudflare Worker 地址，不要末尾斜杠。空=只用本地默认")]
        public string baseUrl = "http://127.0.0.1:8787";

        public float requestTimeoutSeconds = 8f;

        [Tooltip("启动时是否拉 /config")]
        public bool fetchOnBoot = true;
    }
}
