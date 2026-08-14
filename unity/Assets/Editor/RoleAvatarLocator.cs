using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JojoP.EditorTools
{
    /// <summary>
    /// 在 Assets/Bundle/Role/大头贴 按表字段、角色 Id、中文名匹配图片。
    /// 文件名不含扩展名即可，png/jpg/webp 都认。
    /// </summary>
    public static class RoleAvatarLocator
    {
        public const string PortraitFolder = "Assets/Bundle/Role/大头贴";

        public static Texture2D Resolve(string avatarLoc, string roleName, string roleId)
        {
            if (!string.IsNullOrWhiteSpace(avatarLoc) &&
                avatarLoc.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(avatarLoc);
                if (direct != null) return direct;
            }

            var byStem = IndexPortraits();
            foreach (string key in CandidateStems(avatarLoc, roleName, roleId))
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (byStem.TryGetValue(key, out var texture)) return texture;
            }

            string suffix = ShortRoleId(roleId);
            if (!string.IsNullOrEmpty(suffix))
            {
                foreach (var pair in byStem)
                    if (pair.Key.StartsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        return pair.Value;
            }

            return null;
        }

        public static string StemFromTexture(Texture2D texture)
        {
            if (texture == null) return "";
            string path = AssetDatabase.GetAssetPath(texture);
            return string.IsNullOrEmpty(path) ? texture.name : Path.GetFileNameWithoutExtension(path);
        }

        public static int CountMatched(IEnumerable<(string loc, string name, string id)> roles)
        {
            int matched = 0;
            foreach (var role in roles)
                if (Resolve(role.loc, role.name, role.id) != null) matched++;
            return matched;
        }

        static Dictionary<string, Texture2D> IndexPortraits()
        {
            var map = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(PortraitFolder)) return map;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                string stem = Path.GetFileNameWithoutExtension(path);
                if (!map.ContainsKey(stem)) map[stem] = texture;
            }

            return map;
        }

        static IEnumerable<string> CandidateStems(string avatarLoc, string roleName, string roleId)
        {
            if (!string.IsNullOrWhiteSpace(avatarLoc))
            {
                yield return Path.GetFileNameWithoutExtension(avatarLoc.Replace('\\', '/'));
                if (avatarLoc.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(avatarLoc);
                    if (direct != null) yield return Path.GetFileNameWithoutExtension(avatarLoc);
                }
            }

            if (!string.IsNullOrWhiteSpace(roleId)) yield return roleId;
            if (!string.IsNullOrWhiteSpace(roleName)) yield return roleName;
            string suffix = ShortRoleId(roleId);
            if (!string.IsNullOrEmpty(suffix)) yield return suffix;
        }

        static string ShortRoleId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            int split = id.LastIndexOf('_');
            return split >= 0 && split + 1 < id.Length ? id.Substring(split + 1) : id;
        }
    }
}
