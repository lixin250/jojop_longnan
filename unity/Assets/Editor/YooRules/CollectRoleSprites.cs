using System.IO;
using YooAsset.Editor;

namespace JojoP.EditorTools
{
    /// <summary>角色图 + 人声。忽略 README.md。</summary>
    [DisplayName("收集: 角色图与语音")]
    public sealed class CollectRoleSprites : IAssetFilterRule
    {
        public string FindAssetType => string.Empty;

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            string ext = Path.GetExtension(data.AssetPath);
            return ext.Equals(".png", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpg", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".ogg", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".mp3", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".wav", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
