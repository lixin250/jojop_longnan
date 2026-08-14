using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace JojoP.Editor.UIBind
{
    /// <summary>
    /// 无 Form.asset 时，以已生成的 <c>XxxRegister.cs</c> 中 OnRegister 监听为准，反推 Callback。
    /// </summary>
    static class UIBindRegisterInference
    {
        static readonly Regex Btn = new(@"AddBtnClickListener\s*\(\s*(\w+)\s*,", RegexOptions.Compiled);
        static readonly Regex Tog = new(@"AddToggleClickListener\s*\(\s*(\w+)\s*,", RegexOptions.Compiled);
        static readonly Regex Input = new(@"AddInputFieldListener\s*\(\s*(\w+)\s*,", RegexOptions.Compiled);

        public static HashSet<string> ReadWiredFieldNames(string registerFilePath)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(registerFilePath) || !File.Exists(registerFilePath))
                return set;

            var text = File.ReadAllText(registerFilePath);
            CollectMatches(Btn, text, set);
            CollectMatches(Tog, text, set);
            CollectMatches(Input, text, set);
            return set;
        }

        static void CollectMatches(Regex regex, string text, HashSet<string> set)
        {
            foreach (Match m in regex.Matches(text))
            {
                if (m.Groups.Count > 1 && !string.IsNullOrEmpty(m.Groups[1].Value))
                    set.Add(m.Groups[1].Value);
            }
        }

        public static string GetRegisterPath(UIBindSettings settings, string className)
        {
            return Path.Combine(settings.CodeRoot, className, className + "Register.cs").Replace('\\', '/');
        }

        /// <summary>
        /// - 不在「可回调类型」→ Callback=false（Image/Text 等）
        /// - 有 Register → 以 AddXxxListener 为准
        /// - 无 Register → 仅「默认勾选回调」为 true（通常只有 Button）
        /// </summary>
        public static void ApplyCallbackState(
            List<UIBindGenerator.BindComp> comps,
            UIBindSettings settings,
            string className)
        {
            if (comps == null || settings == null) return;

            var registerPath = GetRegisterPath(settings, className);
            var hasRegister = File.Exists(registerPath);
            var wired = hasRegister ? ReadWiredFieldNames(registerPath) : null;

            foreach (var c in comps)
            {
                if (!settings.CanHaveCallback(c.Type))
                {
                    c.Callback = false;
                    continue;
                }

                c.Callback = hasRegister
                    ? wired.Contains(c.Name)
                    : settings.DefaultCallbackEnabled(c.Type);
            }
        }
    }
}
