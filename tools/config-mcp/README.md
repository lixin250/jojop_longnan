# JojoP Config MCP

本地 MCP：让 Cursor AI 安全读取、修改并校验 Luban Excel，不再为每次改表生成 Python。

## 工作流

```text
用户描述真人性格/经历
  → get_character_context（锁定 name/desc）
  → AI 提炼 education_level / life_route / career_sector / faction_tags / 技能机制
  → upsert_skill_bundle（技能 + effects/buff）
  → update_character_design（挂 skill_ids / tags）
  → upsert_fusion（可选，按 Tag 触发）
  → validate_config
  → run_luban
```

MCP 只负责结构化操作；性格到机制的推理仍由 Cursor AI 完成。

## 安全约束

- `RoleList.name` / `desc` / `id` 禁止通过 MCP 修改。
- 写入前备份到项目根目录 `.config-mcp-backup/`。
- 技能、效果使用 string key。
- 融合条件使用通用 `EFactionTag`，不硬编码人名。
- 枚举定义以 `__enums__.xlsx` 为准；教育、路线、职业成长分别在
  `EducationProgram` / `LifeRouteGrowth` / `CareerGrowth`。
- `run_luban` 在引用校验通过后才执行。

## 工具

- `list_config`
- `read_sheet`
- `get_character_context`
- `update_character_design`
- `batch_update_role_stats`（批量回写 Unity 平衡窗口中的基础数值）
- `promote_skill_draft`（把技能表中的自由备注行转成正式技能，并保留原备注）
- `retarget_character_encounter`（人物 Id 被用户改名后同步相遇表引用）
- `upsert_skill_bundle`
- `upsert_fusion`
- `upsert_run_content`（章节槽位、奖励、装备、相遇、龙南事件与大事件时间线）
- `validate_config`
- `run_luban`

局内内容位于 `局内成长.xlsx`：`RunChapterRule / RogueReward / Equipment /
CharacterEncounter / RunEvent / TimelineEvent`。`TimelineEvent.anchor_year` 只是玩笑锚点，
实际按 `chapter_id + sequence` 推进；`boost_tags` 提高对应行业兄弟的相遇权重。
`Official/Gazetteer/Media` 的核验记录必须有来源链接，`Oral/Creative` 不得标成已核验。
临时技能仍复用 `技能.xlsx`，`owner_id=loot`。

## 开发

```powershell
cd tools/config-mcp
npm install
npm run build
npm run smoke
```

项目级注册位于 `.cursor/mcp.json`。首次新增或修改后，在 Cursor 的 MCP 设置中重启 `jojop-config`，或 Reload Window。

## VibeCoding 填表

可以直接在 `SkillIndex` 新增一行，只先写 `owner_id / name / ##灵感`，其余留空。
AI 读取后用 `promote_skill_draft` 补齐 Id、机制、数值和 Effect；`##` 中的原始口述不会删除。
人物表仍需先写稳定 `id / name / desc`，其中 `name / desc` 由 MCP 锁定，AI 只补玩法字段。

Unity 菜单 `JojoP/数值平衡/英雄初始数值` 用中文勾选派系、毕业技和校园/其他技能，
并显示 `Assets/Bundle/Role/大头贴` 中的头像。派系已拆成互联网 / 创业 / 银行 / 烟草。
窗口不给战斗定位建议。保存时回写 Excel 的
`base_hp/atk/move/defense`、`crit_rate/crit_damage/attack_interval`、
`faction_tags`、`skill_ids`、`avatar_loc`。详细规则见 `docs/初始数值平衡工具.md`。
