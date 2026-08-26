using JojoP.AOT.Settings;

namespace JojoP.AOT
{
    /// <summary>
    /// 渠道：AppLauncher.channelOverride → JojoPGlobalSettings.boot.defaultChannel → gp。
    /// </summary>
    public static class ChannelId
    {
        public static string Value { get; private set; } = "gp";

        public static void Init(string overrideChannel = null)
        {
            if (!string.IsNullOrEmpty(overrideChannel))
            {
                Value = overrideChannel.Trim();
                return;
            }

            var settings = JojoPGlobalSettings.Load();
            string fromSettings = settings != null ? settings.Boot.defaultChannel : null;
            Value = string.IsNullOrEmpty(fromSettings) ? "gp" : fromSettings.Trim();
        }
    }
}
