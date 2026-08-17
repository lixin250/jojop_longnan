using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 从 Bundle 路径加载角色战斗图 / 头像。Editor 与 PlayMode(Editor) 走 AssetDatabase；
    /// 真机包体需走 Yoo 时再扩。
    /// </summary>
    public static class RoleArtLoader
    {
        const string PortraitFolder = "Assets/Bundle/Role/大头贴";
        const string BattleFolder = "Assets/Bundle/Role/battle";

        public static Sprite LoadPortrait(string avatarLoc)
        {
            if (string.IsNullOrWhiteSpace(avatarLoc)) return null;
            string stem = StripExt(avatarLoc);
            return LoadSprite($"{PortraitFolder}/{stem}.png")
                   ?? LoadSprite($"{PortraitFolder}/{stem}.jpg");
        }

        public static Sprite LoadBattle(string battleLoc, string avatarLocFallback = null)
        {
            if (!string.IsNullOrWhiteSpace(battleLoc))
            {
                string stem = StripExt(battleLoc);
                var s = LoadSprite($"{BattleFolder}/{stem}.png");
                if (s != null) return s;
            }

            if (!string.IsNullOrWhiteSpace(avatarLocFallback))
                return LoadPortrait(avatarLocFallback);
            return null;
        }

        static string StripExt(string loc)
        {
            loc = loc.Replace('\\', '/');
            int slash = loc.LastIndexOf('/');
            if (slash >= 0) loc = loc.Substring(slash + 1);
            int dot = loc.LastIndexOf('.');
            return dot > 0 ? loc.Substring(0, dot) : loc;
        }

        static Sprite LoadSprite(string assetPath)
        {
#if UNITY_EDITOR
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.15f), 100f);
#else
            return null;
#endif
        }
    }
}
