using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using JojoP.Cfg;
using JojoP.Config;
using JojoP.Gameplay.Brothers;
using UnityEditor;
using UnityEngine;

namespace JojoP.EditorTools
{
    /// <summary>人物卡式平衡窗口；Excel 是唯一权威数据源。</summary>
    public sealed class RoleBalanceWindow : EditorWindow
    {
        const string NodePref = "JojoP.RoleBalance.Node";

        static readonly FactionOption[] Factions =
        {
            new FactionOption(EFactionTag.Mechanical, "机械车辆"),
            new FactionOption(EFactionTag.Civil, "土木建造"),
            new FactionOption(EFactionTag.Medical, "医疗骨科"),
            new FactionOption(EFactionTag.Academic, "学术科研"),
            new FactionOption(EFactionTag.Official, "公职体制"),
            new FactionOption(EFactionTag.Internet, "互联网"),
            new FactionOption(EFactionTag.Startup, "创业"),
            new FactionOption(EFactionTag.Street, "街头社会"),
            new FactionOption(EFactionTag.Finance, "银行"),
            new FactionOption(EFactionTag.Tobacco, "烟草"),
            new FactionOption(EFactionTag.Energy, "能源供电"),
        };

        readonly List<RoleBalanceRow> _rows = new List<RoleBalanceRow>();
        readonly List<SkillOption> _jobSkills = new List<SkillOption>();
        readonly List<SkillOption> _otherSkills = new List<SkillOption>();
        Vector2 _scroll;
        string _nodePath = "node";
        string _status = "";
        MessageType _statusType = MessageType.Info;

        [MenuItem("JojoP/数值平衡/英雄初始数值")]
        public static void Open()
        {
            var window = GetWindow<RoleBalanceWindow>("英雄人物卡");
            window.minSize = new Vector2(1480, 640);
            window.Show();
        }

        void OnEnable()
        {
            _nodePath = EditorPrefs.GetString(NodePref, "node");
            Reload();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "派系按中文多选：互联网、创业、银行、烟草已拆开，保存仍是英文枚举。" +
                "工牌绝活和课间/性格技从 SkillIndex 勾选后回写 Excel。头像读取 Assets/Bundle/Role/大头贴。" +
                "每人强度和成长曲线在本窗口自己调，不给战斗定位建议。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("从已导出配置重载", EditorStyles.toolbarButton, GUILayout.Width(130))) Reload();
                if (GUILayout.Button("保存到 Excel", EditorStyles.toolbarButton, GUILayout.Width(100))) Save(false);
                if (GUILayout.Button("保存并导出 Luban", EditorStyles.toolbarButton, GUILayout.Width(135))) Save(true);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Node", GUILayout.Width(35));
                string next = GUILayout.TextField(_nodePath, EditorStyles.toolbarTextField, GUILayout.Width(180));
                if (next != _nodePath)
                {
                    _nodePath = next;
                    EditorPrefs.SetString(NodePref, _nodePath);
                }
            }

            DrawLegend();
            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _rows) DrawRow(row);
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);
        }

        void DrawLegend()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"英雄 {_rows.Count} · 已修改 {DirtyCount()}", EditorStyles.boldLabel, GUILayout.Width(180));
                GUILayout.Label("预算 90–110：正常", GreenLabel(), GUILayout.Width(125));
                GUILayout.Label("85–115：观察", YellowLabel(), GUILayout.Width(115));
                GUILayout.Label("超出：建议复核", RedLabel(), GUILayout.Width(120));
                GUILayout.Label("暴率填百分数；攻间单位为秒", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Header("头像", 55);
                Header("英雄 / Id", 145);
                Header("派系（中文多选）", 180);
                Header("生命", 52);
                Header("攻击", 52);
                Header("移速", 52);
                Header("防御", 52);
                Header("暴率%", 55);
                Header("暴伤×", 55);
                Header("攻间", 55);
                Header("预算", 100);
                Header("毕业", 95);
                Header("工牌绝活", 210);
                Header("课间/性格技", 210);
            }
        }

        void DrawRow(RoleBalanceRow row)
        {
            float score = BudgetIndex(row);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = score >= 90f && score <= 110f
                ? new Color(0.65f, 1f, 0.72f)
                : score >= 85f && score <= 115f
                    ? new Color(1f, 0.9f, 0.55f)
                    : new Color(1f, 0.58f, 0.58f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(58)))
            {
                GUI.backgroundColor = old;
                var selectedAvatar = (Texture2D)EditorGUILayout.ObjectField(
                    row.Avatar, typeof(Texture2D), false, GUILayout.Width(50), GUILayout.Height(50));
                if (selectedAvatar != row.Avatar)
                {
                    row.Avatar = selectedAvatar;
                    row.AvatarLoc = RoleAvatarLocator.StemFromTexture(selectedAvatar);
                }

                GUILayout.Label($"{row.Name}\n<size=9>{row.Id}</size>", RichLabel(), GUILayout.Width(145), GUILayout.Height(45));
                if (GUILayout.Button(FactionLabel(row.FactionTags), EditorStyles.popup, GUILayout.Width(180)))
                    OpenFactionMenu(row);

                row.Hp = EditorGUILayout.FloatField(row.Hp, GUILayout.Width(52));
                row.Atk = EditorGUILayout.FloatField(row.Atk, GUILayout.Width(52));
                row.Move = EditorGUILayout.FloatField(row.Move, GUILayout.Width(52));
                row.Defense = EditorGUILayout.FloatField(row.Defense, GUILayout.Width(52));
                row.CritRate = EditorGUILayout.FloatField(row.CritRate * 100f, GUILayout.Width(55)) / 100f;
                row.CritDamage = EditorGUILayout.FloatField(row.CritDamage, GUILayout.Width(55));
                row.AttackInterval = EditorGUILayout.FloatField(row.AttackInterval, GUILayout.Width(55));

                Rect scoreRect = GUILayoutUtility.GetRect(100, 18, GUILayout.Width(100));
                EditorGUI.ProgressBar(scoreRect, Mathf.InverseLerp(70f, 130f, score), $"{score:0.0}");
                GUILayout.Label($"+{row.JobDelayYears}年\n×{row.GraduationSkillMul:0.##}",
                    EditorStyles.miniLabel, GUILayout.Width(95), GUILayout.Height(36));
                if (GUILayout.Button(SkillLabel(row.JobSkillIds, "还没领工牌绝活"), EditorStyles.popup,
                        GUILayout.Width(210), GUILayout.Height(38)))
                    OpenSkillMenu(row.JobSkillIds, _jobSkills);
                if (GUILayout.Button(SkillLabel(row.OtherSkillIds, "还没课间绝活"), EditorStyles.popup,
                        GUILayout.Width(210), GUILayout.Height(38)))
                    OpenSkillMenu(row.OtherSkillIds, _otherSkills);
            }
        }

        void Reload()
        {
            _rows.Clear();
            _jobSkills.Clear();
            _otherSkills.Clear();
            if (!CfgTables.TryLoad(force: true))
            {
                SetStatus("配置加载失败，请先运行 gen导表.bat。", MessageType.Error);
                return;
            }
            RoleCatalog.Rebuild();

            var roleNames = new Dictionary<string, string>();
            foreach (var role in CfgTables.Tables.TbRoleList.DataList) roleNames[role.Id] = role.Name;
            foreach (var skill in CfgTables.Tables.TbSkillIndex.DataList)
            {
                if (!IsAttachable(skill)) continue;
                string owner = roleNames.TryGetValue(skill.OwnerId, out var ownerName) ? ownerName : skill.OwnerId;
                var option = new SkillOption(skill.Id, $"{skill.Name}　[{owner}]");
                if (HasTag(skill, ESkillShowTag.Job)) _jobSkills.Add(option);
                else _otherSkills.Add(option);
            }
            _jobSkills.Sort((left, right) => string.CompareOrdinal(left.Label, right.Label));
            _otherSkills.Sort((left, right) => string.CompareOrdinal(left.Label, right.Label));

            foreach (var role in CfgTables.Tables.TbRoleList.DataList)
            {
                if (role.Camp != EUnitCamp.Hero) continue;
                var def = RoleCatalog.FindBrother(role.Id);
                var row = new RoleBalanceRow
                {
                    Id = role.Id,
                    Name = role.Name,
                    AvatarLoc = role.AvatarLoc,
                    Hp = role.BaseHp,
                    Atk = role.BaseAtk,
                    Move = role.BaseMove,
                    Defense = role.BaseDefense,
                    CritRate = role.CritRate,
                    CritDamage = role.CritDamage,
                    AttackInterval = role.AttackInterval,
                    JobDelayYears = def?.JobSkillDelayYears ?? 0,
                    GraduationSkillMul = def?.GraduationSkillMul ?? 1f,
                    Sort = role.Sort,
                };
                if (role.FactionTags != null) row.FactionTags.AddRange(role.FactionTags);
                if (role.SkillIds != null)
                {
                    foreach (string skillId in role.SkillIds)
                    {
                        var skill = CfgTables.Tables.TbSkillIndex.GetOrDefault(skillId);
                        if (skill != null && HasTag(skill, ESkillShowTag.Job))
                            row.JobSkillIds.Add(skillId);
                        else
                            row.OtherSkillIds.Add(skillId);
                    }
                }
                row.Avatar = RoleAvatarLocator.Resolve(role.AvatarLoc, role.Name, role.Id);
                row.CaptureOriginal();
                _rows.Add(row);
            }
            _rows.Sort((left, right) => left.Sort.CompareTo(right.Sort));
            int portraits = 0;
            foreach (var row in _rows) if (row.Avatar != null) portraits++;
            SetStatus(
                $"头像已匹配 {portraits}/{_rows.Count}。把 png/jpg 放到 {RoleAvatarLocator.PortraitFolder}，文件名对齐 avatar_loc（如 role_xiebo_avatar.png）。也可按人物 Id 或中文名命名。",
                portraits == _rows.Count ? MessageType.Info : MessageType.Warning);
            Repaint();
        }

        void Save(bool exportLuban)
        {
            if (_rows.Count == 0) return;
            try
            {
                var payload = new RoleBalancePayload { exportLuban = exportLuban };
                foreach (var row in _rows)
                {
                    Clamp(row);
                    payload.updates.Add(new RoleStatPayload
                    {
                        id = row.Id,
                        base_hp = row.Hp,
                        base_atk = row.Atk,
                        base_move = row.Move,
                        base_defense = row.Defense,
                        crit_rate = row.CritRate,
                        crit_damage = row.CritDamage,
                        attack_interval = row.AttackInterval,
                        faction_tags = JoinFactions(row.FactionTags),
                        skill_ids = JoinSkills(row),
                        avatar_loc = row.AvatarLoc,
                    });
                }

                string unityRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string projectRoot = Path.GetFullPath(Path.Combine(unityRoot, ".."));
                string bridge = Path.Combine(projectRoot, "tools", "config-mcp", "dist", "unityBalanceBridge.js");
                if (!File.Exists(bridge))
                    throw new FileNotFoundException("缺少 bridge，请先在 tools/config-mcp 执行 npm run build", bridge);

                string payloadPath = Path.Combine(unityRoot, "Library", "RoleBalanceDraft.json");
                File.WriteAllText(payloadPath, JsonUtility.ToJson(payload, true), new UTF8Encoding(false));
                var start = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(_nodePath) ? "node" : _nodePath,
                    Arguments = $"\"{bridge}\" \"{payloadPath}\"" + (exportLuban ? " --export" : ""),
                    WorkingDirectory = Path.Combine(projectRoot, "tools", "config-mcp"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                using var process = Process.Start(start);
                if (process == null) throw new InvalidOperationException("无法启动 Node 配置桥接");
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);

                foreach (var row in _rows) row.CaptureOriginal();
                if (exportLuban)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    Reload();
                    SetStatus("人物数值、派系、毕业技、校园技和头像资源名已回写 Excel，并完成 Luban 导出。", MessageType.Info);
                }
                else
                {
                    SetStatus("已回写 Excel；Unity 运行仍使用上一次 Luban JSON，请随后点击保存并导出。", MessageType.Warning);
                }
            }
            catch (Exception error)
            {
                string hint = error.Message.Contains("EBUSY") || error.Message.Contains("EPERM")
                    ? "\n请关闭正在占用 人物&怪物.xlsx 的 Excel/WPS 后重试。"
                    : "";
                SetStatus("保存失败：" + error.Message + hint, MessageType.Error);
            }
            Repaint();
        }

        void OpenFactionMenu(RoleBalanceRow row)
        {
            var menu = new GenericMenu();
            foreach (var option in Factions)
            {
                var captured = option;
                bool selected = row.FactionTags.Contains(captured.Value);
                menu.AddItem(new GUIContent(captured.Chinese), selected, () =>
                {
                    if (row.FactionTags.Contains(captured.Value)) row.FactionTags.Remove(captured.Value);
                    else row.FactionTags.Add(captured.Value);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        void OpenSkillMenu(List<string> selected, List<SkillOption> options)
        {
            var menu = new GenericMenu();
            if (options.Count == 0) menu.AddDisabledItem(new GUIContent("没有可选技能"));
            foreach (var option in options)
            {
                var captured = option;
                bool on = selected.Contains(captured.Id);
                menu.AddItem(new GUIContent(captured.Label), on, () =>
                {
                    if (selected.Contains(captured.Id)) selected.Remove(captured.Id);
                    else selected.Add(captured.Id);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        int DirtyCount()
        {
            int count = 0;
            foreach (var row in _rows) if (row.IsDirty()) count++;
            return count;
        }

        static float BudgetIndex(RoleBalanceRow row)
        {
            float effectiveHp = row.Hp * (1f + Mathf.Max(0f, row.Defense) / 100f);
            float expectedCrit = (1f - row.CritRate) + row.CritRate * Mathf.Max(1f, row.CritDamage);
            float baselineCrit = 1f;
            float dps = row.Atk / 12f * (0.4f / Mathf.Max(0.15f, row.AttackInterval)) *
                        expectedCrit / baselineCrit;
            return 100f * (0.35f * effectiveHp / 120f + 0.50f * dps + 0.15f * row.Move / 2.4f);
        }

        static string FactionLabel(List<EFactionTag> tags)
        {
            if (tags.Count == 0) return "未选择";
            var names = new List<string>();
            foreach (var tag in tags)
                foreach (var option in Factions)
                    if (option.Value == tag) names.Add(option.Chinese);
            return names.Count > 0 ? string.Join("、", names) : "未选择";
        }

        static string SkillLabel(List<string> skillIds, string empty)
        {
            if (skillIds.Count == 0) return empty;
            var names = new List<string>();
            foreach (string id in skillIds)
            {
                var skill = CfgTables.Tables.TbSkillIndex.GetOrDefault(id);
                names.Add(skill?.Name ?? id);
            }
            return string.Join("、", names);
        }

        static string JoinFactions(List<EFactionTag> tags)
        {
            var values = new List<string>();
            foreach (var tag in tags) values.Add(tag.ToString());
            return string.Join("|", values);
        }

        static string JoinSkills(RoleBalanceRow row)
        {
            var values = new List<string>(row.OtherSkillIds);
            foreach (string id in row.JobSkillIds)
                if (!values.Contains(id)) values.Add(id);
            return string.Join("|", values);
        }

        static bool HasTag(SkillIndex skill, ESkillShowTag tag) =>
            skill?.ShowTags != null && skill.ShowTags.Contains(tag);

        static bool IsAttachable(SkillIndex skill)
        {
            if (skill == null || string.IsNullOrEmpty(skill.Id)) return false;
            if (HasTag(skill, ESkillShowTag.Fusion)) return false;
            string owner = skill.OwnerId ?? "";
            return owner != "loot" && owner != "fusion" && owner != "eng_xie" && owner != "temp_hire";
        }

        static void Clamp(RoleBalanceRow row)
        {
            row.Hp = Mathf.Clamp(row.Hp, 40f, 250f);
            row.Atk = Mathf.Clamp(row.Atk, 4f, 30f);
            row.Move = Mathf.Clamp(row.Move, 1.2f, 4f);
            row.Defense = Mathf.Clamp(row.Defense, 0f, 100f);
            row.CritRate = Mathf.Clamp(row.CritRate, 0f, 0.75f);
            row.CritDamage = Mathf.Clamp(row.CritDamage, 1f, 4f);
            row.AttackInterval = Mathf.Clamp(row.AttackInterval, 0.15f, 2f);
        }

        void SetStatus(string text, MessageType type)
        {
            _status = text;
            _statusType = type;
        }

        static void Header(string text, float width) =>
            GUILayout.Label(text, EditorStyles.boldLabel, GUILayout.Width(width));

        static GUIStyle RichLabel()
        {
            var style = new GUIStyle(EditorStyles.label) { richText = true };
            return style;
        }

        static GUIStyle GreenLabel() => ColoredMini(new Color(0.2f, 0.6f, 0.25f));
        static GUIStyle YellowLabel() => ColoredMini(new Color(0.65f, 0.48f, 0.05f));
        static GUIStyle RedLabel() => ColoredMini(new Color(0.75f, 0.18f, 0.18f));

        static GUIStyle ColoredMini(Color color)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = color;
            return style;
        }

        readonly struct FactionOption
        {
            public readonly EFactionTag Value;
            public readonly string Chinese;
            public FactionOption(EFactionTag value, string chinese) { Value = value; Chinese = chinese; }
        }

        sealed class SkillOption
        {
            public readonly string Id;
            public readonly string Label;
            public SkillOption(string id, string label) { Id = id; Label = label; }
        }

        sealed class RoleBalanceRow
        {
            public string Id;
            public string Name;
            public string AvatarLoc;
            public Texture2D Avatar;
            public readonly List<EFactionTag> FactionTags = new List<EFactionTag>();
            public readonly List<string> OtherSkillIds = new List<string>();
            public readonly List<string> JobSkillIds = new List<string>();
            public float Hp;
            public float Atk;
            public float Move;
            public float Defense;
            public float CritRate;
            public float CritDamage;
            public float AttackInterval;
            public int JobDelayYears;
            public float GraduationSkillMul;
            public int Sort;
            string _original;

            public void CaptureOriginal() => _original = Snapshot();
            public bool IsDirty() => _original != Snapshot();

            string Snapshot() =>
                $"{Hp:0.###}|{Atk:0.###}|{Move:0.###}|{Defense:0.###}|{CritRate:0.###}|" +
                $"{CritDamage:0.###}|{AttackInterval:0.###}|{JoinFactions(FactionTags)}|" +
                $"{string.Join("|", JobSkillIds)}|{string.Join("|", OtherSkillIds)}|{AvatarLoc}";
        }

        [Serializable]
        sealed class RoleBalancePayload
        {
            public List<RoleStatPayload> updates = new List<RoleStatPayload>();
            public bool exportLuban;
        }

        [Serializable]
        sealed class RoleStatPayload
        {
            public string id;
            public float base_hp;
            public float base_atk;
            public float base_move;
            public float base_defense;
            public float crit_rate;
            public float crit_damage;
            public float attack_interval;
            public string faction_tags;
            public string skill_ids;
            public string avatar_loc;
        }
    }
}
