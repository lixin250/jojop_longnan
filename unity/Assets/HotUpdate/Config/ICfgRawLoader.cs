using System;

namespace JojoP.Config
{
    /// <summary>Luban 表原始文本加载（Yoo / 编辑器文件 / Resources）。</summary>
    public interface ICfgRawLoader
    {
        /// <summary>fileName 不含扩展名，如 tbrolelist。</summary>
        string LoadText(string fileName);
    }
}
