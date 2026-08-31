using JojoP.Cfg;
using UnityEngine;

namespace JojoP.Gameplay.Brothers
{
    /// <summary>选角雷达：底板 vs 局外培养。血/攻百分比，防按点加。</summary>
    public static class HeroStatPreview
    {
        public const int Axes = 5;
        public static readonly string[] AxisNames = { "血", "攻", "防", "速", "频" };

        const float CapHp = 160f;
        const float CapAtk = 24f;
        const float CapDef = 48f;
        const float CapMove = 3.6f;
        const float CapAsps = 3.2f;
        const float Floor = 0.08f;

        public const string BlueHex = "#5BA8E8";
        public const string GreenHex = "#3DC97A";

        public static float HpNow(RoleList role, MetaProgress meta)
        {
            if (role == null) return 0f;
            return role.BaseHp * (meta?.HpMul ?? 1f);
        }

        public static float AtkNow(RoleList role, MetaProgress meta)
        {
            if (role == null) return 0f;
            return role.BaseAtk * (meta?.AtkMul ?? 1f);
        }

        public static float DefNow(RoleList role, MetaProgress meta)
        {
            if (role == null) return 0f;
            return Mathf.Max(0f, role.BaseDefense) + (meta?.DefBonus ?? 0f);
        }

        public static void FillRadar(RoleList role, MetaProgress meta, float[] inner, float[] outer)
        {
            if (inner == null || outer == null || inner.Length < Axes || outer.Length < Axes) return;
            if (role == null)
            {
                for (int i = 0; i < Axes; i++)
                    inner[i] = outer[i] = Floor;
                return;
            }

            float asps = 1f / Mathf.Max(0.15f, role.AttackInterval);
            inner[0] = Norm(role.BaseHp, CapHp);
            outer[0] = Norm(HpNow(role, meta), CapHp);
            inner[1] = Norm(role.BaseAtk, CapAtk);
            outer[1] = Norm(AtkNow(role, meta), CapAtk);
            inner[2] = Norm(Mathf.Max(0f, role.BaseDefense), CapDef);
            outer[2] = Norm(DefNow(role, meta), CapDef);
            inner[3] = outer[3] = Norm(role.BaseMove, CapMove);
            inner[4] = outer[4] = Norm(asps, CapAsps);
        }

        public static string StatLines(RoleList role, MetaProgress meta)
        {
            if (role == null) return "";
            float hpB = role.BaseHp;
            float hpN = HpNow(role, meta);
            float atkB = role.BaseAtk;
            float atkN = AtkNow(role, meta);
            float defB = Mathf.Max(0f, role.BaseDefense);
            float defN = DefNow(role, meta);
            return
                Line("生命", hpB, hpN, "0") + "\n" +
                Line("攻击", atkB, atkN, "0.#") + "\n" +
                Line("防御", defB, defN, "0") + "\n" +
                $"移速  <color={BlueHex}>{role.BaseMove:0.#}</color>   " +
                $"攻速  <color={BlueHex}>{role.AttackInterval:0.##}s</color>";
        }

        static string Line(string name, float baseV, float nowV, string fmt)
        {
            string blue = $"<color={BlueHex}>{baseV.ToString(fmt)}</color>";
            float d = nowV - baseV;
            if (d > 0.04f)
                return $"{name}  {blue}  <color={GreenHex}>+{d.ToString(fmt)}</color>";
            return $"{name}  {blue}";
        }

        static float Norm(float v, float cap) => Mathf.Clamp(v / cap, Floor, 1f);
    }
}
