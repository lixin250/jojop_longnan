# 角色资源 + Luban + YooAsset 规划

面向我和我的龙兄南弟：**档案大头贴**、**战场单位**、**配置表**如何放进 `Assets/Bundle`，并被现有 YooAsset 收集。

---

## 1. 战场用 Spine 还是 Mesh？（定案）

| 用途 | 方案 | 原因 |
|------|------|------|
| **割草战场** | **2D Sprite / 简单 Mesh（Quad+贴图）** | 同屏几十单位、自动寻敌；Spine 骨骼+动画贵，IAA 割草不划算 |
| **档案 / 结算 / 招募卡** | **大头贴 Sprite**（主） | 已有 `Bundle/Role/大头贴`；UI 直接挂 Image |
| **以后单人展示** | Spine **可选**，不进战场 | 仅「看角色」界面；与战场 Prefab 分离 |

竖切不做战场 Spine。战场 Prefab：`SpriteRenderer`（或 URP Unlit Quad）+ 逻辑挂 `BattleUnit`。

---

## 2. Bundle 目录规划（替换中文散落）

当前：`Assets/Bundle/Role/大头贴/`。平衡窗口按 `avatar_loc` 文件名加载，
例如 `role_xiebo_avatar.png`。完整文件名清单见 `docs/初始数值平衡工具.md`。

```text
Assets/Bundle/
├── Role/
│   ├── _shared/                 # 可选：默认框、阴影
│   ├── player/
│   │   ├── avatar.png           # 大头贴（档案、HUD 头像）
│   │   └── battle.png           # 战场小图（或 battle.prefab）
│   ├── xiebo/
│   │   ├── avatar.png
│   │   └── battle.png
│   └── lixin/                   # 例：现有立新资源迁到此
│       ├── avatar.png           # 由 lixin1 定稿一张主头像
│       └── battle.png
├── Config/                      # Luban 导出的二进制/JSON（见下）
│   └── luban/
│       ├── tbrole.bytes
│       ├── tbenemy.bytes
│       └── ...
└── UI/                          # 以后面板 Prefab
```

**地址约定（YooAsset Location）**  
现收集规则：`Assets/Bundle` → `AddressByFileName` + `PackDirectory`（见 `BundleCollectorSetting.asset`）。

- 若继续 **按文件名寻址**：全局文件名必须唯一 → 用 `role_xiebo_avatar`、`role_xiebo_battle`（推荐改名），或改 Address 规则为 **按相对路径**。
- **推荐尽快改收集规则**（Role/Config 组）：
  - Address：`AddressByFilePath`（或自定义 `role/{id}/avatar`）
  - Pack：`PackDirectory`（一角色一目录一包，或 Config 单独一包）

未改规则前，文件命名强制唯一前缀：`role_{id}_avatar.png`。

### 大头贴用在哪

| 位置 | 资源键 | 说明 |
|------|--------|------|
| 档案面板 | `avatar` | 大图 |
| 角色身上（战场血条旁） | `battle` 小头 / 或 avatar 缩略 | 别直接扔原图像素到战场 |
| 招募三选一卡 | `avatar` | UI |
| 散旁/得胜名单 | `avatar` 小圆头 | UI |

运行时：`YooAssets.LoadAssetAsync<Sprite>(loc)`，loc 来自表字段，不写死路径散落在代码里。

---

## 3. Luban 怎么接、怎么进 Bundle

### 3.1 仓库布局（定案）

```text
JojoP/
├── config/Config/               # Luban 工程（Excel + luban.conf + gen.bat）
└── unity/Assets/
    ├── Script/LubanCode/
    │   ├── JojoP.Config.asmdef  # 程序集（勿放进 Gen）
    │   └── Gen/                 # outputCodeDir（每次 gen 清空）
    └── Bundle/LubanConfig/      # outputDataDir（Yoo 收集，每次 gen 清空）
```

`luban.conf` 的 `xargs` 已指向上述两目录。`JojoP.HotUpdate` 引用 `JojoP.Config`。

### 3.2 导出两路

| 产物 | 输出目录 | 用途 |
|------|----------|------|
| C#（`code_cs_bin` 等） | `HotUpdate/Cfg/Gen` | 热更程序集读表 |
| 数据（`data_bin` / `data_json`） | `Bundle/Config/luban` | 打进 AB，运行时加载 |

Editor 菜单建议：`JojoP → Luban → 导出配置`（调 luban 命令行，拷到上述两目录）。

### 3.3 运行时加载顺序

```text
Boot / GameApp 就绪
  → YooAsset 包可用
  → 加载 Bundle/Config/luban 全部（或按需）
  → Tables.Load(loader)   // Luban 官方 ByteBuf loader
  → BrotherCatalog 用 TbRole 填运行时，替换手写 GameTables.Brothers
```

竖切过渡：**手写 `GameTables` 保留作 Fallback**；有 Luban 数据则覆盖。

### 3.4 角色表字段草案（`role.xlsx` / `TbRole`）

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | `xiebo` / `player` |
| name | string | 显示名 |
| tags | list/enum | 考公/硕/博/机械… |
| base_hp / base_atk / base_move | float | 养成前基底 |
| job_skill_id | string | 就业技 |
| job_delay_years | int | 出社会延迟 |
| avatar_loc | string | Yoo 地址，如 `role_xiebo_avatar` |
| battle_loc | string | 战场图/Prefab 地址 |
| archive_sort | int | 档案排序 |
| unlock_chapter | int | 最早可招集章 |

敌人/场景表同理：`icon_loc`、`theme_id`、场景 `enemy_pool`。

---

## 4. 「同名类」怎么规划（避免和玩法代码撞车）

常见冲突：`Brother` / `Skill` / `Unit` 既像玩法类又像表 Bean。

**定案：**

| 层 | 命名空间 / 命名 | 例子 |
|----|-----------------|------|
| Luban 生成 | `JojoP.Cfg`（或 `cfg`） | `JojoP.Cfg.Role`、`TbRole`、`TbEnemy` |
| 玩法运行时 | `JojoP.Gameplay.Brothers` | `BrotherRuntime`、`BrotherDef`（过渡）、`BattleUnit` |
| 适配器 | `JojoP.Gameplay.Brothers.Data` | `RoleCatalog.FromCfg(Tables)`：Cfg → Runtime |

规则：

1. **禁止**把 Luban 生成类改名后手改进玩法目录；只改 Excel + 重新导出。  
2. 玩法 **不直接**拿 `cfg.Role` 当 MonoBehaviour 数据；进局前拷到 `BrotherRuntime`。  
3. 表名用 `TbRole` / `TbJobSkill`，避免 `TbBrother` 与口语「兄弟」混在代码补全里刷屏（可选，但 Role 更通用）。  
4. Unity 组件名避开 `Role` 单字 → 用 `BrotherView` / `RoleAvatarUI`。

---

## 5. YooAsset 收集（现状 → 建议）

**现状**（已可用）：

- Package：`DefaultPackage`
- Group `Bundle`：`CollectPath = Assets/Bundle`，`AddressByFileName`，`PackDirectory`，`CollectAll`

因此：**只要资源在 `Assets/Bundle/` 下就会进包**；`Role`、`Config/luban` 无需另建根目录。

**建议增量（实现阶段）：**

1. 拆 Group（可选但清晰）：
   - `Role` → `Assets/Bundle/Role`
   - `Config` → `Assets/Bundle/Config`（小、常更，可单独版本）
2. `EnableAddressable` 视团队习惯；Location 与表字段 `*_loc` 对齐。  
3. 大图 jpg 转 **Sprite (2D and UI)**，生成 Sprite 再进 UI；战场用较小分辨率。  
4. 中文路径 `大头贴` **迁走**，避免部分工具/地址规则踩坑。

打 AB 流程仍按 [热更接入.md](热更接入.md)：先 `devDirectPlay` 用 `Resources`/直接引用验收，再走 Simulate/Host。

---

## 6. 和现有兄弟团代码的衔接

```text
GameTables.Brothers  (手写，竖切)
        ↓ 替换为
RoleCatalog ← JojoP.Cfg.TbRole + Yoo 加载 avatar/battle
        ↓
RunState / BattleUnit（逻辑不变，只换取数来源）
```

档案 UI：列表读 `TbRole` → 显示 `name` + `LoadSprite(avatar_loc)`。  
战场：`battle_loc` → Sprite；暂无 Prefab 时可用代码 `CreatePrimitive` + 贴图（与现竖切兼容）。

---

## 7. 落地顺序（实现时按此做）

1. 定角色 id 列表；把 `大头贴/lixin*` 迁成 `Role/lixin/role_lixin_avatar.*`（先保证文件名唯一）。  
2. 建 `conf/` Luban 最小表：`role` + 导出脚本 → `Bundle/Config/luban` + `HotUpdate/Cfg/Gen`。  
3. `RoleCatalog` 读表；档案面板先做只读列表（大头贴）。  
4. 战场 `BattleUnit` 支持挂 Sprite（替换纯色块）。  
5. 再拆 Yoo Group / 改 Address 规则（若需要路径寻址）。  
6. Spine 若要做，只加 `Bundle/Role/{id}/spine/`，**表另字段 `spine_loc`，战场忽略**。

---

## 8. 非目标（本规划不做）

- 战场上 Spine 同屏几十人  
- 配置进 `Resources` 双份维护  
- Luban 类与 `BrotherRuntime` 混在同一命名空间  
