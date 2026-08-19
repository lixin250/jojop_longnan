using System.IO;
using YooAsset.Editor;

namespace JojoP.EditorTools
{
    /// <summary>
    /// 相对 Assets/Bundle/Role/ 的路径作寻址，无扩展名。
    /// lixin/avatar.png → lixin/avatar
    /// lixin/battle/idle.png → lixin/battle/idle
    /// 内置 AddressByFolderAndFileName 只取直接父文件夹，战场 idle 会全员撞成 battle_idle。
    /// </summary>
    [DisplayName("定位地址: Role相对路径")]
    public sealed class AddressByRoleRelative : IAddressRule
    {
        public const string RoleRoot = "Assets/Bundle/Role/";

        string IAddressRule.GetAssetAddress(AddressRuleData data)
        {
            string path = (data.AssetPath ?? "").Replace('\\', '/');
            if (path.StartsWith(RoleRoot))
                path = path.Substring(RoleRoot.Length);
            int dot = path.LastIndexOf('.');
            if (dot > 0) path = path.Substring(0, dot);
            return path;
        }
    }
}
