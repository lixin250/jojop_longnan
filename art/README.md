# Art 流水线（概念图 → Bundle）

## 总图干什么、不干什么

概念总图是 **风格锁定板 + UI 裁切清单**，不是动画源。

| 槽 | 从总图怎么来 | 战场怎么用 |
|----|--------------|------------|
| 大头贴 / 半身 / 海报 / 技能 icon | 按 `layouts/*.json` 裁切即可 | 不进战场 |
| 战场动作 | 总图里「游戏内动作」**按姿势单独裁**，去掉中文标签 | `SpriteRenderer` 切帧 |

**不要**把带「待机/移动/攻击」字样的整块动作区丢进 `battle_loc`。  
**不要**用 AI 视频当战斗循环。Idle 最多做轻微上下浮动；Mesh deform 以后只考虑待机；Spine 只给图鉴。

## 战场 2D 文件约定

表字段 `battle_loc` 仍是 `role_{id}_battle`（不用改表）。优先按人目录：

```text
Assets/Bundle/Role/{id}/
  avatar.png  half.png  poster.png  banner.png
  battle/idle.png  walk.png  atk.png  hurt.png  dead.png  fallback.png
```

旧路径 `Role/大头贴/`、`Role/battle/role_*_battle_*.png` 仍作回落。

运行时 `RoleArtLoader.LoadBattleSet` 读新目录再读旧后缀，`BattlePoseDriver` 按走/打/受击换图。

## 你要不要装别的 AI / ffmpeg？

| 需求 | 做法 |
|------|------|
| **框裁**（头像/技能框/海报） | 本仓库 `art/tools/crop_concept_sheet.py` + `layouts/*.json` 即可 |
| **去羊皮纸底**（武器 icon、全身站姿） | 脚本内置 `parchment` 近似抠图（Pillow）；战场帧加 `"trim": true` 收紧透明边 |
| **精细抠图**（发丝、光晕） | 另用 remove.bg / Photopea / 修好的 `rembg`；**不需要 ffmpeg** |
| **理解图里写了啥** | 可选 OCR；当前用人工标定 layout，更稳 |

ffmpeg 用于视频抽帧，不适合这张概念总图。

## 目录

```text
art/
  概念图/                 # 风格锁定总图（不进 Unity 包）
  layouts/                # 每张总图的裁切标定 JSON
  tools/crop_concept_sheet.py
  _export/{role}/         # 草稿裁切（可删）
  _final/{role}/          # 定稿文件名 = Unity 覆盖名 + manifest.json
```

Unity 目标：

```text
Assets/Bundle/
  Role/{id}/                  # 优先：一人一目录
  Role/大头贴/  Role/battle/  # 旧回落
  Skill/icon/
  Item/icon/
```

## 批量裁切（利欣 / 欧版 / 老陈）

```powershell
cd e:\Project\JojoP
python art/tools/crop_concept_sheet.py --all
```

只重出猩哥战场动作：

```powershell
python art/tools/crop_concept_sheet.py --layout art/layouts/lixin_gpt.json
```

Unity 菜单：`JojoP/Art/从 art/_final 覆盖导入 Bundle`

| 概念图 | role_id | 主头像 | 战场图 |
|--------|---------|--------|--------|
| GPT-利欣 | lixin | `role_lixin_avatar`（兼 `role_player_avatar`） | `role_lixin_battle` + `_idle/_walk/_atk` |
| GPT-欧版 | oban | `role_oban_avatar` | `role_oban_battle` |
| GPT-老陈 | xiaolin | `role_xiaolin_avatar` | `role_xiaolin_battle` |
