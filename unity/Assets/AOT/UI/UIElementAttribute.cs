using System;

namespace JojoP.AOT.UI
{
    /// <summary>标记 UI 窗口：缓存、类型、层级。预制体名默认 = 类型名。</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class UIElementAttribute : Attribute
    {
        public bool NeedCache { get; }
        public Type Type { get; }
        public int Layer { get; }
        public string Prefab => Type.Name;

        public UIElementAttribute(bool needCache, Type type, int layer = 0)
        {
            NeedCache = needCache;
            Type = type;
            Layer = layer;
        }
    }
}
