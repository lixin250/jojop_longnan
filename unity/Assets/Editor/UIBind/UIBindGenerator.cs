using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace JojoP.Editor.UIBind
{
    /// <summary>
    /// Tag 扫描 → Xxx.cs + XxxRegister.cs → SerializedObject 自动绑。
    /// 可视化入口见 <see cref="UIBindWindow"/>。
    /// </summary>
    public static class UIBindGenerator
    {
        public sealed class BindComp
        {
            public GameObject Go;
            public string Name;
            public string Type;
            public string Path;
            public bool Callback;
        }

        [MenuItem("Tools/UI Bind/Open Settings")]
        public static void OpenSettings()
        {
            var s = UIBindSettings.LoadOrCreate();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        [MenuItem("Tools/UI Bind/Ensure Tags")]
        public static void MenuEnsureTags()
        {
            EnsureTagsExist(UIBindSettings.LoadOrCreate());
            Debug.Log("[UIBind] Tag 已同步到 TagManager。");
        }

        [MenuItem("Assets/UI Bind/Generate And Bind", true)]
        static bool ValidateGenerate() => Selection.activeObject is GameObject;

        [MenuItem("Assets/UI Bind/Generate And Bind")]
        public static void MenuGenerateAndBind()
        {
            if (!TryGetSelectedPrefab(out var prefab)) return;
            GenerateAndBind(prefab, UIBindSettings.LoadOrCreate());
        }

        [MenuItem("Assets/UI Bind/Bind Only", true)]
        static bool ValidateBindOnly() => Selection.activeObject is GameObject;

        [MenuItem("Assets/UI Bind/Bind Only")]
        public static void MenuBindOnly()
        {
            if (!TryGetSelectedPrefab(out var prefab)) return;
            BindOnly(prefab, UIBindSettings.LoadOrCreate());
        }

        public static void EnsureTagsExist(UIBindSettings settings)
        {
            if (settings?.Tags == null) return;
            foreach (var tag in settings.Tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!InternalEditorUtility.tags.Contains(tag))
                    InternalEditorUtility.AddTag(tag);
            }
        }

        public static List<BindComp> Collect(GameObject root, UIBindSettings settings)
        {
            var list = new List<BindComp>();
            if (root == null || settings == null) return list;
            CollectRecursive(root.transform, root.name, root, settings, list);
            return list;
        }

        /// <summary>扫描 Prefab，并从已有 Register 反推 Callback（无 Form.asset 时的状态来源）。</summary>
        public static List<BindComp> CollectWithCallbackState(GameObject root, UIBindSettings settings)
        {
            var list = Collect(root, settings);
            if (root != null)
                UIBindRegisterInference.ApplyCallbackState(list, settings, root.name);
            return list;
        }

        public static void MergeCallbacks(List<BindComp> comps, IReadOnlyDictionary<string, bool> callbackByField)
        {
            if (comps == null || callbackByField == null) return;
            foreach (var c in comps)
            {
                if (callbackByField.TryGetValue(c.Name, out var cb))
                    c.Callback = cb;
            }
        }

        /// <summary>写逻辑 + Register；不自动绑。返回是否写出了有效组件。</summary>
        public static bool GenerateCode(GameObject prefab, UIBindSettings settings, List<BindComp> comps = null)
        {
            EnsureTagsExist(settings);
            comps ??= CollectWithCallbackState(prefab, settings);
            if (comps.Count == 0)
            {
                EditorUtility.DisplayDialog("UI Bind",
                    "未扫到带 Tag 的节点。请给控件节点设置 Tag（如 Button、TextMeshProUGUI、Image&Button）。",
                    "OK");
                return false;
            }

            var className = prefab.name;
            var outDir = Path.Combine(settings.CodeRoot, className).Replace('\\', '/');
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            WriteLogicIfMissing(outDir, className, comps, settings);
            WriteRegister(outDir, className, comps, settings);
            AssetDatabase.Refresh();
            Debug.Log($"[UIBind] 已生成 {className}（{comps.Count} 个字段，回调 {comps.Count(p => p.Callback)} 个）。编译后可点「仅绑定」。");
            return true;
        }

        public static void BindOnly(GameObject prefab, UIBindSettings settings, List<BindComp> comps = null)
        {
            EnsureTagsExist(settings);
            comps ??= Collect(prefab, settings);
            if (comps.Count == 0)
            {
                EditorUtility.DisplayDialog("UI Bind", "未扫到带 Tag 的节点。", "OK");
                return;
            }
            BindPrefab(prefab, prefab.name, comps, settings);
        }

        public static void GenerateAndBind(GameObject prefab, UIBindSettings settings, List<BindComp> comps = null)
        {
            comps ??= CollectWithCallbackState(prefab, settings);
            if (!GenerateCode(prefab, settings, comps)) return;

            var className = prefab.name;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () => BindPrefab(prefab, className, comps, settings);
            };
        }

        static bool TryGetSelectedPrefab(out GameObject prefab)
        {
            prefab = null;
            var go = Selection.activeObject as GameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("UI Bind", "请先选中一个 Prefab。", "OK");
                return false;
            }

            var path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("UI Bind", "请选中 Project 窗口中的 Prefab 资源。", "OK");
                return false;
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null;
        }

        static void CollectRecursive(Transform trans, string rootName, GameObject collectRoot,
            UIBindSettings settings, List<BindComp> list)
        {
            TryAdd(trans.gameObject, rootName, settings, list);

            for (int i = 0; i < trans.childCount; i++)
            {
                var child = trans.GetChild(i);
                var go = child.gameObject;

                // 跳过嵌套 Prefab 实例（不扫其内部）
                if (PrefabUtility.IsAnyPrefabInstanceRoot(go) && go != collectRoot)
                    continue;

                if (IsInsideNestedPrefab(go, collectRoot))
                    continue;

                CollectRecursive(child, rootName, collectRoot, settings, list);
            }
        }

        static bool IsInsideNestedPrefab(GameObject go, GameObject collectRoot)
        {
            if (go == null || collectRoot == null) return false;
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) return false;
            var nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            return nearestRoot != null && nearestRoot != collectRoot;
        }

        static void TryAdd(GameObject go, string rootName, UIBindSettings settings, List<BindComp> list)
        {
            var tag = go.tag;
            if (tag == "Untagged" || !settings.Tags.Contains(tag))
                return;

            var path = BuildPath(go.transform, rootName);
            var types = tag.Split('&');
            foreach (var type in types)
            {
                if (list.Any(p => p.Go == go && p.Type == type))
                    continue;

                list.Add(new BindComp
                {
                    Go = go,
                    Name = go.name + Suffix(type),
                    Type = type,
                    Path = path,
                    Callback = settings.DefaultCallbackEnabled(type),
                });
            }
        }

        static string BuildPath(Transform t, string rootName)
        {
            if (t.name == rootName) return rootName;
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur.name != rootName)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static void WriteLogicIfMissing(string outDir, string className, List<BindComp> comps, UIBindSettings settings)
        {
            var file = Path.Combine(outDir, className + ".cs");
            if (File.Exists(file))
            {
                // 增量：补缺失的 UI 事件方法
                var text = File.ReadAllText(file);
                var sbInsert = new StringBuilder();
                foreach (var c in comps.Where(p => p.Callback))
                {
                    foreach (var method in EventMethodNames(c))
                    {
                        if (text.Contains(method)) continue;
                        sbInsert.AppendLine(EventMethodBody(c, method));
                    }
                }

                if (sbInsert.Length > 0)
                {
                    const string marker = "#region UI事件";
                    var idx = text.IndexOf(marker, StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        var insertAt = text.IndexOf('\n', idx) + 1;
                        text = text.Insert(insertAt, sbInsert.ToString());
                        File.WriteAllText(file, text, Encoding.UTF8);
                        Debug.Log($"[UIBind] 已向 {file} 增量插入事件方法。");
                    }
                }
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("//=====================================================");
            sb.AppendLine("//备 注：此代码为工具生成 修改不会被下次生成覆盖");
            sb.AppendLine("//       再次生成会在原有的基础上新增事件方法");
            sb.AppendLine("//=====================================================");
            sb.AppendLine("using JojoP.AOT.UI;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            if (comps.Any(p => p.Type is "TextMeshProUGUI" or "TMP_InputField"))
                sb.AppendLine("using TMPro;");
            sb.AppendLine();
            sb.AppendLine($"namespace {settings.NamespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    [UIElement(true, typeof({className}), 0)]");
            sb.AppendLine($"    public partial class {className} : UIFormBase");
            sb.AppendLine("    {");
            sb.AppendLine("        #region 周期函数");
            sb.AppendLine("        protected override void OnInit()");
            sb.AppendLine("        {");
            sb.AppendLine("            base.OnInit();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnOpen()");
            sb.AppendLine("        {");
            sb.AppendLine("            base.OnOpen();");
            sb.AppendLine("        }");
            sb.AppendLine("        #endregion");
            sb.AppendLine();
            sb.AppendLine("        #region UI事件");
            sb.AppendLine();
            foreach (var c in comps.Where(p => p.Callback))
            {
                foreach (var method in EventMethodNames(c))
                    sb.AppendLine(EventMethodBody(c, method));
            }
            sb.AppendLine("        #endregion");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[UIBind] 生成逻辑: {file}");
        }

        static IEnumerable<string> EventMethodNames(BindComp c)
        {
            switch (c.Type)
            {
                case "Button":
                    yield return $"On{c.Name}Click";
                    break;
                case "Toggle":
                    yield return $"On{c.Name}Change";
                    break;
                case "InputField":
                    yield return $"On{c.Name}Change";
                    yield return $"On{c.Name}End";
                    break;
            }
        }

        static string EventMethodBody(BindComp c, string method)
        {
            var param = c.Type switch
            {
                "Toggle" => "bool state, Toggle toggle",
                "InputField" when method.EndsWith("Change") || method.EndsWith("End") => "string text",
                _ => "",
            };
            return
                $"        private void {method}({param})\n" +
                "        {\n" +
                "            // TODO\n" +
                "        }\n";
        }

        static void WriteRegister(string outDir, string className, List<BindComp> comps, UIBindSettings settings)
        {
            var file = Path.Combine(outDir, className + "Register.cs");
            var sb = new StringBuilder();
            sb.AppendLine("//=====================================================");
            sb.AppendLine("//备 注：此代码为工具生成 任何修改都会被下次生成覆盖");
            sb.AppendLine("//=====================================================");
            sb.AppendLine("using JojoP.AOT.UI;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            if (comps.Any(p => p.Type is "TextMeshProUGUI" or "TMP_InputField"))
                sb.AppendLine("using TMPro;");
            sb.AppendLine();
            sb.AppendLine($"namespace {settings.NamespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>[{className}] 组件自动化代码</summary>");
            sb.AppendLine($"    public partial class {className}");
            sb.AppendLine("    {");
            sb.AppendLine("        #region Fields");
            sb.AppendLine();
            foreach (var c in comps)
                sb.AppendLine($"        [SerializeField] private {CsType(c.Type)} {c.Name};");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine();
            sb.AppendLine("        #region Func");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnRegister()");
            sb.AppendLine("        {");
            if (comps.Any(p => p.Callback))
            {
                sb.AppendLine("            // 组件事件绑定");
                foreach (var c in comps.Where(p => p.Callback))
                {
                    switch (c.Type)
                    {
                        case "Button":
                            sb.AppendLine($"            AddBtnClickListener({c.Name}, On{c.Name}Click);");
                            break;
                        case "Toggle":
                            sb.AppendLine($"            AddToggleClickListener({c.Name}, On{c.Name}Change);");
                            break;
                        case "InputField":
                            sb.AppendLine($"            AddInputFieldListener({c.Name}, On{c.Name}Change, On{c.Name}End);");
                            break;
                    }
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        #endregion");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[UIBind] 生成绑定: {file}");
        }

        static string CsType(string type) => type switch
        {
            "TextMeshProUGUI" => "TextMeshProUGUI",
            "TMP_InputField" => "TMP_InputField",
            "GameObject" => "GameObject",
            _ => type,
        };

        static void BindPrefab(GameObject prefabAsset, string className, List<BindComp> comps, UIBindSettings settings)
        {
            var scriptType = FindType($"{settings.NamespaceName}.{className}");
            if (scriptType == null)
            {
                Debug.LogWarning($"[UIBind] 找不到类型 {settings.NamespaceName}.{className}，请等编译完成后再执行 Bind Only。");
                return;
            }

            var existing = prefabAsset.GetComponent(scriptType);
            if (existing == null)
            {
                existing = prefabAsset.AddComponent(scriptType);
                EditorUtility.SetDirty(prefabAsset);
            }

            // 去掉旧的非 partial 同名组件冲突：若是旧 Login MonoBehaviour 已替换为 partial，无额外处理

            var so = new SerializedObject(existing);
            foreach (var c in comps)
            {
                GameObject target;
                if (prefabAsset.name == c.Path || string.IsNullOrEmpty(c.Path))
                    target = prefabAsset;
                else
                    target = FindByPath(prefabAsset, c.Path);

                if (target == null)
                {
                    Debug.LogWarning($"[UIBind] 找不到路径: {c.Path}");
                    continue;
                }

                var reference = GetReference(target, c.Type);
                if (reference == null)
                {
                    Debug.LogWarning($"[UIBind] 拿不到组件 {c.Type} @ {c.Path}");
                    continue;
                }

                var prop = so.FindProperty(c.Name);
                if (prop == null)
                {
                    Debug.LogWarning($"[UIBind] 字段不存在: {c.Name}（脚本是否已编译？）");
                    continue;
                }

                prop.objectReferenceValue = reference;
                Debug.Log($"[UIBind] 绑定 {c.Name} = {reference.name}");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIBind] 绑定完成: {className}");
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static GameObject FindByPath(GameObject root, string path)
        {
            if (string.IsNullOrEmpty(path) || path == root.name) return root;
            var parts = path.Split('/');
            Transform cur = root.transform;
            // path 不含根名
            foreach (var part in parts)
            {
                var child = cur.Find(part);
                if (child == null)
                {
                    for (int i = 0; i < cur.childCount; i++)
                    {
                        if (cur.GetChild(i).name.Equals(part, StringComparison.OrdinalIgnoreCase))
                        {
                            child = cur.GetChild(i);
                            break;
                        }
                    }
                }
                if (child == null) return null;
                cur = child;
            }
            return cur.gameObject;
        }

        static UnityEngine.Object GetReference(GameObject target, string componentType)
        {
            switch (componentType)
            {
                case "GameObject": return target;
                case "Transform": return target.transform;
                case "RectTransform": return target.GetComponent<RectTransform>();
                case "Button": return target.GetComponent<Button>();
                case "Image": return target.GetComponent<Image>();
                case "Text": return target.GetComponent<Text>();
                case "TextMeshProUGUI": return target.GetComponent<TextMeshProUGUI>();
                case "TMP_InputField": return target.GetComponent<TMP_InputField>();
                case "Toggle": return target.GetComponent<Toggle>();
                case "InputField": return target.GetComponent<InputField>();
                case "ScrollRect": return target.GetComponent<ScrollRect>();
                case "Slider": return target.GetComponent<Slider>();
                case "RawImage": return target.GetComponent<RawImage>();
                case "Dropdown": return target.GetComponent<Dropdown>();
                case "CanvasGroup": return target.GetComponent<CanvasGroup>();
                default:
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var type = asm.GetType(componentType)
                                   ?? asm.GetType("UnityEngine.UI." + componentType)
                                   ?? asm.GetType("TMPro." + componentType);
                        if (type != null && typeof(Component).IsAssignableFrom(type))
                            return target.GetComponent(type);
                    }
                    return null;
            }
        }

        static string Suffix(string type) => type switch
        {
            "Button" => "Btn",
            "Image" => "Img",
            "Text" => "Txt",
            "TextMeshProUGUI" => "Tmp",
            "TMP_InputField" => "TmpInput",
            "InputField" => "Input",
            "RawImage" => "RImg",
            "ScrollRect" => "Scroll",
            "Slider" => "Sld",
            "Toggle" => "Tog",
            "Dropdown" => "Drop",
            "Transform" => "Trans",
            "RectTransform" => "RectTrans",
            "GameObject" => "Go",
            "CanvasGroup" => "CanvasGroup",
            _ => type,
        };
    }
}
