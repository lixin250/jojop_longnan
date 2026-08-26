using System.IO;
using YooAsset.Editor;

namespace JojoP.EditorTools
{
    /// <summary>热更场景。Bootstrap 只进 APK，不要打进 Yoo。</summary>
    [DisplayName("收集: 热更场景(不含Bootstrap)")]
    public sealed class CollectHotScenes : IAssetFilterRule
    {
        public string FindAssetType => string.Empty;

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            if (string.IsNullOrEmpty(data.AssetPath))
                return false;
            if (!data.AssetPath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                return false;
            string name = Path.GetFileNameWithoutExtension(data.AssetPath);
            return !name.Equals("Bootstrap", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
