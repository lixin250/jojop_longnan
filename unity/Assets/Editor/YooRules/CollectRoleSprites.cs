using System.IO;
using YooAsset.Editor;

namespace JojoP.EditorTools
{
    /// <summary>只收角色 png/jpg，避免 README.md 进清单。</summary>
    [DisplayName("收集: 角色图 png/jpg")]
    public sealed class CollectRoleSprites : IAssetFilterRule
    {
        public string FindAssetType => string.Empty;

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            string ext = Path.GetExtension(data.AssetPath);
            return ext.Equals(".png", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpg", System.StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
