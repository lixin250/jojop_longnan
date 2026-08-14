using JojoP.AOT.Settings;
using UnityEngine;

namespace JojoP.AOT
{
    /// <summary>
    /// 渠道 ID：优先 AppLauncher 覆盖 → JojoPGlobalSettings.defaultChannel → 宏/默认 gp。
    /// </summary>
    public static class ChannelId
    {
        const string PrefsKey = "jojop.channel";

        public static string Value { get; private set; } = "gp";

        public static void Init(string overrideChannel = null)
        {
            if (!string.IsNullOrEmpty(overrideChannel))
            {
                Apply(overrideChannel.Trim());
                return;
            }

            if (PlayerPrefs.HasKey(PrefsKey))
            {
                var saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(saved))
                {
                    Value = saved;
                    return;
                }
            }

            var settings = JojoPGlobalSettings.Load();
            if (settings != null && !string.IsNullOrEmpty(settings.Boot.defaultChannel))
            {
                Apply(settings.Boot.defaultChannel.Trim(), persist: false);
                return;
            }

#if JOJOP_CHANNEL_GP
            Value = "gp";
#elif JOJOP_CHANNEL_TEST
            Value = "test";
#else
            Value = "gp";
#endif
        }

        static void Apply(string channel, bool persist = true)
        {
            Value = channel;
            if (!persist) return;
            PlayerPrefs.SetString(PrefsKey, Value);
            PlayerPrefs.Save();
        }
    }
}
