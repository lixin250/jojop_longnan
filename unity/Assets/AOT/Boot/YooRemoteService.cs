using System.Collections.Generic;
using YooAsset;

namespace JojoP.AOT.Boot
{
    /// <summary>YooAsset 3.x 远端地址：主 CDN + 备用 CDN。</summary>
    public sealed class YooRemoteService : IRemoteService
    {
        readonly string _main;
        readonly string _fallback;

        public YooRemoteService(string mainHost, string fallbackHost)
        {
            _main = (mainHost ?? string.Empty).TrimEnd('/');
            _fallback = string.IsNullOrEmpty(fallbackHost)
                ? _main
                : fallbackHost.TrimEnd('/');
        }

        public IReadOnlyList<string> GetRemoteUrls(string fileName)
        {
            return new[]
            {
                $"{_main}/{fileName}",
                $"{_fallback}/{fileName}"
            };
        }
    }
}
