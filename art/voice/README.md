# 人声：一段音色 → 表里所有普通话文案

对着 `文案.txt` 念。换电脑把整个 `art/voice` 带走（`secrets.env` 要单独拷，git 不会带）。

龙南话 **不生成**。朋友想录就自己录，填 `langPath_ln`；不录就播普通话 `langPath`。

## 表结构（Luban `TbRoleVoice`）

主 key 三段式：`{who}_{module}_{meaning}`

| 列 | 例 | 说明 |
|----|----|------|
| id | `lixin_battle_skill` | 唯一；必须等于 who+module+meaning |
| who | `lixin` | 音色和目录。源文件 `samples/lixin.mp3` |
| module | `battle` / `profile` / `hub` / `settle` | 模块 |
| meaning | `skill` / `show` / `spawn` / `hurt` / `win` | 具体意义 |
| text_zh | 搞快点，这单今晚要交。 | 只这一列进 TTS |
| langPath | `lixin/voice/lixin_battle_skill` | 默认加载 |
| langPath_ln | 空 或 `lixin/voice/lixin_battle_skill_ln` | 有文件则优先 |
| emotion | `calm` / `happy` / `angry` | MiniMax 原值，仅合成用。不要中文描述列、不要跳表 |

运行时：`RoleVoiceLoader.Play("lixin_battle_skill")`  
优先 `langPath_ln`（文件存在），否则 `langPath`。

资源：

```
Assets/Bundle/Role/{who}/voice/{id}.ogg        ← TTS 普通话
Assets/Bundle/Role/{who}/voice/{id}_ln.ogg     ← 可选龙南话自录音
```

作者改 `art/voice/lines.csv`，再：

```powershell
python art/tools/minimax_voice.py sync    # → config/Config/Data/语音.xlsx
```

然后 Luban 导表。

## 命令

```powershell
# 样本：art/voice/samples/lixin.mp3
python art/tools/minimax_voice.py clone
python art/tools/minimax_voice.py synth
python art/tools/minimax_voice.py import
```

`secrets.env` 已 gitignore。克隆后 7 天内至少 synth 一次才会留住 `voice_id`。

## 便携文生音（可整夹拷走）

`tts-kit/`：txt + job 配置 → mp3，保留已克隆声纹，做有声小说或别的项目。见 [tts-kit/README.md](tts-kit/README.md)。
## emotion（写 API 原值）

`speech-2.8-hd` 直接填下面字符串，空则模型按文案自选。官方没有 `neutral`，中性用 `calm`。

| 值 | 含义 | 2.8-hd |
|----|------|--------|
| `happy` | 高兴 | 可用 |
| `sad` | 悲伤 | 可用 |
| `angry` | 愤怒 | 可用 |
| `fearful` | 害怕 | 可用 |
| `disgusted` | 厌恶 | 可用 |
| `surprised` | 惊讶 | 可用 |
| `calm` | 中性 | 可用 |
| `fluent` | 生动 | 仅 2.6 |
| `whisper` | 低语 | 仅 2.6；2.8 不支持 |

## 可选项（以后要再加列，现在不要）

文案里就能写、不必加列：停顿 `<#0.3#>`、注音 `(he2)平`、语气词 `(laughs)` `(sighs)` `(breath)`。

按行（以后可进 `lines.csv`）：`speed` 0.5～2.0。  
按角色（以后可进 `voices.json`）：`vol` (0,10]、`pitch` -12～12、`voice_modify`（pitch/intensity/timbre -100～100）。

脚本里目前写死 `speed=1` `vol=1` `pitch=0`。采样率/音效/混合音色不开放。
