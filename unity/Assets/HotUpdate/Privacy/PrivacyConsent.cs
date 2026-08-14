using UnityEngine;

namespace JojoP.Privacy
{
    /// <summary>隐私同意。未同意前禁止初始化广告。</summary>
    public static class PrivacyConsent
    {
        const string PrefKey = "jojop.privacy.accepted.v1";

        public static bool HasAccepted => PlayerPrefs.GetInt(PrefKey, 0) == 1;

        public static void Accept()
        {
            PlayerPrefs.SetInt(PrefKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>调试用：清掉同意状态，方便再测弹窗。</summary>
        public static void ResetForDebug()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }
    }
}
