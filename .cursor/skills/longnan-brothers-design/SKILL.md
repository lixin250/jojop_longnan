---
name: longnan-brothers-design
description: >-
  我和我的龙兄南弟玩法/技能/人物表设计约束。设计或改就业技、羁绊、派系、硕博延迟、局内数值平衡、
  人物&怪物.xlsx / 技能.xlsx 时必须遵循。Use when editing brothers skills, bonds,
  RoleList, job delay, graduation skills, or balance.
---

# 我和我的龙兄南弟 · 技能与人物设计约束

改玩法技能、人物表、羁绊前先读本 skill，并对照 `docs/技能设计约束.md`、`docs/人物刻画与羁绊.md`。

## 硬性限定（用户定案）

1. **硕/博晚毕业 → 就业技更值钱**  
   - 硕士/博士：战斗过程中晚一点毕业；**毕业（就业技解锁）后**才有该技能。  
   - 晚解锁的就业技 **效果必须更好**（倍率/覆盖/召唤质量高于同学历正常上班同学的同职技能）。  
   - 延迟参考：硕默认 2 年（可 2～3）、博 **5 年**、考公/编默认 2（可 1～3）、正常上班第 1 年。  
   - 延迟期补偿：盾/声望/人情，**不能**提前给完整就业技。

2. **基础数值可调，服务局内升级打斗平衡**  
   - 初始数值使用 Unity 菜单 `JojoP/数值平衡/英雄初始数值`；Excel 是唯一数据源，
     保存经 `unityBalanceBridge` 回写，禁止另建一份 ScriptableObject 基础数值。
   - 窗口派系显示中文：互联网、创业、银行、烟草已拆开，互不混用。
   - 毕业技与校园/其他技能从 SkillIndex 动态多选。
   - 开局主预算仍是 `base_hp/atk/move`；`base_defense/crit_rate/crit_damage/attack_interval`
     已进战斗，作职业特性。闪避/吸血/幸运不进人物底板。
   - 头像读 `avatar_loc`，文件放 `Assets/Bundle/Role/大头贴/{avatar_loc}.png`。
   - 每人强度和成长曲线由策划在英雄人物卡里定；窗口**不给**战斗定位建议。
   - 同职允许 ±10～15% 差异；靠三选一升级拉开，不靠开挂学历。  
   - 开局全员小学生数值；现实行业只影响 **毕业后就业技倾向** 和相遇权重，不提前碾压。

3. **同职相似 + 特性；跨职要有好搭配**  
   - 同职业技能骨架可相似，但每人必须有可读差异（例：双土木「砸」vs「审批」）。  
   - 跨职业明确搭配位（例：谢博 `Mechanical` × 物资哥 `Energy`）。  
   - 融合技/羁绊用 `faction_tags` 组合，不要硬编码姓名。

## 改表纪律

- **禁止**擅自改朋友的 `name`、`desc`。只补派系、数值、技能、资源位、延迟标签。  
- 改人物/技能/Effect/Buff/羁绊时，优先调用项目 MCP `jojop-config`：
  `get_character_context` → AI 梳理 → `upsert_*` → `validate_config` → `run_luban`。
- 用户可在 `SkillIndex` 只写 `owner_id / name / ##口述灵感`；用
  `promote_skill_draft` 转成正式技能，必须保留原始 `##` 备注。
- 不要为每次改表新建 Python；Python 只用于一次性 schema migration，完成后删除。
- 枚举以 `__enums__.xlsx` 为准。人物路径拆成
  `education_level` / `life_route` / `career_sector`，不要再恢复单一 `degree`。
- 年限与毕业技查 `EducationProgram`，路线等待查 `LifeRouteGrowth`，
  就业成长查 `CareerGrowth`。
- 局内奖励/装备/人物相遇/事件查 `局内成长.xlsx`：
  `RunChapterRule / RogueReward / Equipment / CharacterEncounter / RunEvent / TimelineEvent`。
- 大事件是捏造的城市/游戏时间点，年份只是玩笑锚点；局内按
  `chapter_id + sequence` 推进。`boost_tags` 只提高对应行业兄弟相遇权重，
  不凭空把未毕业技能提前解锁。
- Timeline 一律 `Oral/Creative` 且 `verified=false`，不得冒充县志史实。
  若以后再写 `Official/Gazetteer/Media`，必须留来源链接。
- 兄弟经历、真人逸事同样写 `Oral/Creative` 且 `verified=false`。
- 人物按个人好感多次相遇后集合；不得恢复“一次三选一直接掉整个人”。
- 上阵位按章节配置，`-1` 不限；广告只做本局扩编与奖励刷新，基础 3 人必须能组成核心双 Tag 羁绊。
- 团队攻击核心互相替换；每人最多 1 个 `loot_*` 临时技能槽，不能覆盖固有校园技/就业技。
- 技能 Id / 效果 Id 用 **string key**：`{归属}_{动作}`（如 `xiebo_dismantle`），`RoleList.skill_ids` 跳转同 key。  
- 人物表：`RoleList@人物&怪物.xlsx`；技能：`技能.xlsx`（SkillIndex / SkillEffect / FusionSkill）。  
- 物资哥勿再复用谢博技能 Id；能源用 `Energy` 派系。  
- 战斗实现约定见 `docs/战斗行为与技能系统.md`（GameObject + 轻量 System，不上 DOTS）。
- 章节地图解锁见 `docs/章节地图解锁.md`。

## 真人描述 → 通用机制流程

1. 先读原始 `name/desc`，两者锁定。
2. 从性格/经历提炼可复用 Tag（职业/派系/行为），不要把姓名作为触发条件。
3. 串成：`Trigger → Target → Effect → Buff/Tag → CD`。
4. 同 Tag 可触发羁绊；融合条件写 `required_tags`。
5. 回复用户时说明“哪条性格 → 哪个机制”，并指出强度/延迟代价。

## 毕业技强度档（设计时对照）

| 学历路径 | 解锁时机 | 强度预期 |
|----------|----------|----------|
| 正常上班 | 出社会第 1 个过年 | 基准 1.0 |
| 硕 | +2～3 年 | 约 1.25～1.35，或更强单目标/控制 |
| 博 | +5 年 | 约 1.45～1.6，工科技术类 当前可带 机械召唤/破甲核心 |
| 考公/编 | +1～3 年 | 光环/团队向，个人 DPS 可略低 |

## 架构模式（Manager）

加 Manager / 服务时遵循 `docs/架构模式约定.md`：

- 表查询：`CfgTables` 等 **static 门面** OK  
- 一局状态：**实例**挂 Session，禁止 MonoBehaviour 单例丛林  
- 跨模块：`event Action`，禁止全局字符串 EventBus  
- 不上 Zenject/VContainer（当前体量）

## 输出时

改完人物/技能后，在回复里简短列出：未改动的姓名描述、新增/调整的技能与羁绊、是否触及延迟规则。
