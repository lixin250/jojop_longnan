using System.Collections.Generic;

namespace JojoP.Gameplay.Brothers
{
    public sealed class SkillSlot
    {
        public string SkillId;
        public float CdLeft;
        public bool PassiveApplied;
        public bool IsPassive;
    }

    /// <summary>单位上的短时数值 buff（表 AddBuff 的最小实现）。</summary>
    public sealed class RuntimeBuff
    {
        public int BuffId;
        public float Value;
        public float Remain;
        public bool Permanent;
    }
}
