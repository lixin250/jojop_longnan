using System;
using System.Reflection;
using UnityEngine;
using YooAsset;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>
    /// 角色图加载。Yoo 寻址：{id}/avatar、{id}/battle/idle。
    /// Editor 未打清单时回落 AssetDatabase 全路径。
    /// </summary>
    public static class RoleArtLoader
    {
        public const string RoleRoot = "Assets/Bundle/Role";
        const string PortraitFolder = RoleRoot + "/大头贴";
        const string BattleFolder = RoleRoot + "/battle";
        const string HalfFolder = RoleRoot + "/halfbody";
        const string PosterFolder = RoleRoot + "/poster";
        const string DefaultPackage = "DefaultPackage";

        static MethodInfo _loadAsset;

        public static string Addr(string roleId, string slot)
        {
            if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(slot)) return "";
            return roleId + "/" + slot;
        }

        public static Sprite LoadPortrait(string avatarLoc)
        {
            if (string.IsNullOrWhiteSpace(avatarLoc)) return null;
            string id = RoleFolder(avatarLoc);
            string stem = StripExt(avatarLoc);
            return LoadFirst(
                Addr(id, "avatar"),
                $"{RoleRoot}/{id}/avatar.png",
                $"{RoleRoot}/{id}/avatar.jpg",
                $"{RoleRoot}/{id}/{stem}.png",
                $"{RoleRoot}/oldAvatar/{stem}.png",
                $"{RoleRoot}/oldAvatar/{stem}.jpg",
                $"{PortraitFolder}/{stem}.png",
                $"{PortraitFolder}/{stem}.jpg");
        }

        public static Sprite LoadHalf(string loc)
        {
            if (string.IsNullOrWhiteSpace(loc)) return null;
            string id = RoleFolder(loc);
            string stem = StripExt(loc);
            return LoadFirst(
                       Addr(id, "half"),
                       Addr(id, "poster"),
                       $"{RoleRoot}/{id}/half.png",
                       $"{HalfFolder}/{stem}.png",
                       $"{HalfFolder}/role_{id}_half.png")
                   ?? LoadPortrait(loc);
        }

        public static Sprite LoadPoster(string loc)
        {
            if (string.IsNullOrWhiteSpace(loc)) return null;
            string id = RoleFolder(loc);
            return LoadFirst(
                       Addr(id, "poster"),
                       Addr(id, "banner"),
                       $"{RoleRoot}/{id}/poster.png",
                       $"{PosterFolder}/role_{id}_poster.png")
                   ?? LoadHalf(loc);
        }

        public static Sprite LoadRogueIcon(string kindKey, string portraitLoc = null)
        {
            if (!string.IsNullOrWhiteSpace(portraitLoc))
            {
                var portrait = LoadPortrait(portraitLoc);
                if (portrait != null) return portrait;
            }

            string key = (kindKey ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key)) key = "stat";
            return LoadFirst(
                $"Assets/Bundle/Item/icon/rogue_{key}.png",
                $"{RoleRoot}/ui/rogue_{key}.png",
                Addr("ui", "rogue_" + key));
        }

        public static Sprite LoadBattle(string battleLoc, string avatarLocFallback = null)
        {
            return LoadBattleSet(battleLoc, avatarLocFallback).Fallback;
        }

        public static BattlePoseSet LoadBattleSet(string battleLoc, string avatarLocFallback = null)
        {
            var set = new BattlePoseSet();
            string id = RoleFolder(string.IsNullOrWhiteSpace(battleLoc) ? avatarLocFallback : battleLoc);
            string stem = StripExt(battleLoc);

            if (!string.IsNullOrEmpty(id))
            {
                set.Idle = LoadFirst(Addr(id, "battle/idle"), $"{RoleRoot}/{id}/battle/idle.png");
                set.Walk = LoadFirst(Addr(id, "battle/walk"), $"{RoleRoot}/{id}/battle/walk.png");
                set.Atk = LoadFirst(Addr(id, "battle/atk"), $"{RoleRoot}/{id}/battle/atk.png");
                set.Hurt = LoadFirst(Addr(id, "battle/hurt"), $"{RoleRoot}/{id}/battle/hurt.png");
                set.Dead = LoadFirst(Addr(id, "battle/dead"), $"{RoleRoot}/{id}/battle/dead.png");
                set.Fallback = LoadFirst(Addr(id, "battle/fallback"), $"{RoleRoot}/{id}/battle/fallback.png")
                               ?? set.Idle
                               ?? set.Walk;
            }

            if (!string.IsNullOrWhiteSpace(stem))
            {
                if (set.Fallback == null) set.Fallback = LoadSprite($"{BattleFolder}/{stem}.png");
                if (set.Idle == null) set.Idle = LoadSprite($"{BattleFolder}/{stem}_idle.png");
                if (set.Walk == null) set.Walk = LoadSprite($"{BattleFolder}/{stem}_walk.png");
                if (set.Atk == null) set.Atk = LoadSprite($"{BattleFolder}/{stem}_atk.png");
                if (set.Hurt == null) set.Hurt = LoadSprite($"{BattleFolder}/{stem}_hurt.png");
                if (set.Dead == null) set.Dead = LoadSprite($"{BattleFolder}/{stem}_dead.png");
            }

            if (set.Fallback == null && !string.IsNullOrWhiteSpace(avatarLocFallback))
                set.Fallback = LoadPortrait(avatarLocFallback);

            if (set.Walk == null) set.Walk = set.Fallback;
            if (set.Idle == null) set.Idle = set.Fallback;
            if (set.Atk == null) set.Atk = set.Walk ?? set.Fallback;
            if (set.Hurt == null) set.Hurt = set.Idle ?? set.Fallback;
            if (set.Dead == null) set.Dead = set.Hurt ?? set.Fallback;
            return set;
        }

        /// <summary>role_lixin_battle_idle / role_lixin_avatar → lixin</summary>
        public static string RoleFolder(string loc)
        {
            if (string.IsNullOrWhiteSpace(loc)) return "";
            string s = StripExt(loc);
            if (s.StartsWith("role_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(5);
            int u = s.IndexOf('_');
            return u > 0 ? s.Substring(0, u) : s;
        }

        static string StripExt(string loc)
        {
            if (string.IsNullOrEmpty(loc)) return "";
            loc = loc.Replace('\\', '/');
            int slash = loc.LastIndexOf('/');
            if (slash >= 0) loc = loc.Substring(slash + 1);
            int dot = loc.LastIndexOf('.');
            return dot > 0 ? loc.Substring(0, dot) : loc;
        }

        static Sprite LoadFirst(params string[] keys)
        {
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                var sp = LoadSprite(k);
                if (sp != null) return sp;
            }

            return null;
        }

        static Sprite LoadSprite(string key)
        {
            var fromYoo = LoadYooSprite(key);
            if (fromYoo != null) return fromYoo;
            return LoadEditorSprite(key);
        }

        static Sprite LoadYooSprite(string location)
        {
            if (!YooAssets.IsInitialized) return null;
            if (!YooAssets.TryGetPackage(DefaultPackage, out var pkg) || pkg == null) return null;
            if (pkg.InitializeStatus != EOperationStatus.Succeeded) return null;

            AssetHandle handle = null;
            try
            {
                handle = pkg.LoadAssetSync<Sprite>(location);
                if (handle == null || handle.Status != EOperationStatus.Succeeded)
                {
                    handle?.Dispose();
                    return null;
                }

                var sp = handle.GetAssetObject<Sprite>();
                handle.Dispose();
                return sp;
            }
            catch
            {
                handle?.Dispose();
                return null;
            }
        }

        static Sprite LoadEditorSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;

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
