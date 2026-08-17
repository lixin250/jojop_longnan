# Art 流水线（概念图 → Bundle）

## 你要不要装别的 AI / ffmpeg？

| 需求 | 做法 |
|------|------|
| **框裁**（头像/技能框/海报） | 本仓库 `art/tools/crop_concept_sheet.py` + `layouts/*.json` 即可 |
| **去羊皮纸底**（武器 icon、全身站姿） | 脚本内置 `parchment` 近似抠图（Pillow） |
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
  Role/大头贴/
  Role/battle/
  Role/halfbody/
  Role/poster/
  Skill/icon/
  Item/icon/
```

## 批量裁切（利欣 / 欧版 / 老陈）

```powershell
cd e:\Project\JojoP
python art/tools/crop_concept_sheet.py --all
```

Unity 菜单：`JojoP/Art/从 art/_final 覆盖导入 Bundle`

| 概念图 | role_id | 主头像 | 战场图 |
|--------|---------|--------|--------|
| GPT-利欣 | lixin | `role_lixin_avatar`（兼 `role_player_avatar`） | `role_lixin_battle` |
| GPT-欧版 | oban | `role_oban_avatar` | `role_oban_battle` |
| GPT-老陈 | xiaolin | `role_xiaolin_avatar` | `role_xiaolin_battle` |
