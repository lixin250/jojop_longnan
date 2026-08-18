using System;
using System.Reflection;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 从 Bundle 路径加载角色战斗图 / 头像。
    /// Editor PlayMode 用反射调 AssetDatabase（热更程序集不引用 UnityEditor）。
    /// </summary>
    public static class RoleArtLoader
    {
        const string PortraitFolder = "Assets/Bundle/Role/大头贴";
        const string BattleFolder = "Assets/Bundle/Role/battle";

        static MethodInfo _loadAsset;

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
            var method = EditorLoadAsset();
            if (method == null) return null;

            var sprite = method.MakeGenericMethod(typeof(Sprite)).Invoke(null, new object[] { assetPath }) as Sprite;
            if (sprite != null) return sprite;

            var tex = method.MakeGenericMethod(typeof(Texture2D)).Invoke(null, new object[] { assetPath }) as Texture2D;
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.15f), 100f);
        }

        static MethodInfo EditorLoadAsset()
        {
            if (_loadAsset != null) return _loadAsset;
            var t = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (t == null) return null;
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "LoadAssetAtPath" || !m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                {
                    _loadAsset = m;
                    break;
                }
            }

            return _loadAsset;
        }
    }
}
