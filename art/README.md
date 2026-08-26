# Art 流水线（概念图 → Bundle）

## 总图干什么、不干什么

概念总图是 **风格锁定板 + UI 裁切清单**，不是动画源。

| 槽 | 从总图怎么来 | 战场怎么用 |
|----|--------------|------------|
| 大头贴 / 半身 / 海报 / 技能 icon | 按 `layouts/*.json` 裁切即可 | 不进战场 |
| 战场动作 | 总图里「游戏内动作」**按姿势单独裁**，去掉中文标签 | `SpriteRenderer` 切帧 |

**不要**把带「待机/移动/攻击」字样的整块动作区丢进 `battle_loc`。  
**不要**用 AI 视频当战斗循环。有 `idle_1..n` 就播序列；只有一帧时待机才轻微上下浮动。Spine 只给图鉴。

## 三视图 → 战斗帧（默认 MiniMax）

OpenAI `gpt-image-2` 免费档不能出图，战斗帧先走 **MiniMax image-01** 把目录、切帧、导入跑通。画风会和 GPT 大底有差，额度够了再把 `POSE_PROVIDER=openai`。

落地锁 **512×512**，脚在 `ground_frac=0.12`。MiniMax **不补间**：永远拿 `lock_ref` 当 `subject_reference`，prompt 只写动作，一次出 3 张候选人审，每动作只留 `_1.png`。不要拿生成图再生成。

```powershell
python art/tools/minimax_pose.py gen --who lixin --poses idle
# 看 art/pose/.cache/lixin/_cand_idle_1.png … 选一张
python art/tools/minimax_pose.py pack --who lixin --poses idle --cand 2
python art/tools/minimax_pose.py import --who lixin
```

### 你要填什么

`art/voice/secrets.env`（已 gitignore）：

```
MINIMAX_API_KEY=...
MINIMAX_GROUP_ID=...
POSE_PROVIDER=minimax
```

```powershell
python art/tools/minimax_pose.py lock --who lixin
python art/tools/minimax_pose.py gen --who lixin --poses idle
python art/tools/minimax_pose.py import --who lixin
```

缓存：`art/pose/.cache/lixin/_key_idle.png`、`_raw_idle_1.png` … 再铺成 `idle_1.png`。缩放以 **key 帧** 为准，脚踩同一条地平线。

已有 raw 可只重铺：

```powershell
python art/tools/minimax_pose.py pack --who lixin --poses idle
```

## 战场 2D 文件约定

表字段 `battle_loc` 仍是 `role_{id}_battle`（不用改表）。优先按人目录：

```text
Assets/Bundle/Role/{id}/
  avatar.png  half.png  poster.png  banner.png
  battle/idle_1.png idle_2.png idle_3.png
         walk_1.png walk_2.png walk_3.png
         atk_1.png atk_2.png atk_3.png
         skill_1.png hurt_1.png dead_1.png fallback.png
```

仍识别旧的单帧 `idle.png` / `walk.png`（总图裁切那套）。`RoleArtLoader` 先找 `clip_1..8`，没有再回落单帧。

旧路径 `Role/大头贴/`、`Role/battle/role_*_battle_*.png` 仍作回落。

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
