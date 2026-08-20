using System;
using System.Collections.Generic;
using YooAsset.Editor;

namespace JojoP.EditorTools
{
    /// <summary>
    /// 只额外忽略文档/程序集定义。不要忽略 .txt：Spine atlas 是 xxx.atlas.txt。
    /// .bytes 本来就不在列表里（HybridCLR DLL、Luban 二进制、Spine skel.bytes 都要进包）。
    /// </summary>
    [DisplayName("忽略: 常规+md")]
    public sealed class JojoPIgnoreRule : IAssetIgnoreRule
    {
        static readonly HashSet<string> ExtraExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".asmdef", ".asmref"
        };

        readonly NormalIgnoreRule _inner = new NormalIgnoreRule();

        public bool IsIgnoreAsset(EditorAssetInfo assetInfo)
        {
            if (assetInfo == null) return true;
            if (ExtraExt.Contains(assetInfo.FileExtension))
                return true;
            return _inner.IsIgnoreAsset(assetInfo);
        }
    }
}
