# 热更后：配置加载 & UI 框架选型（JojoP 定案）

面向本项目：**YooAsset + HybridCLR + Luban + IAA 小体量出海**。

Manager / 单例 / 工厂 / 订阅约定见 **[架构模式约定.md](./架构模式约定.md)**。

---

## 1. 配置（Luban）怎么加载？

### 你现在的 `CfgTables`（直读磁盘）

```text
Application.dataPath/Bundle/LubanConfig/*.json
```

| | |
|--|--|
| 优点 | 编辑器零配置、改表即玩、竖切最快 |
| 缺点 | **正式包里 Assets 路径不存在**；不走差量；和「Yoo 热更」两条线 |
| 商业上？ | **只当开发兜底**，不当上线方案 |

### 商业常见拆法（本项目应对齐）

```text
Loading(AOT)
  Yoo 拉齐资源
  → Load JojoP.Config.dll.bytes     （表结构 / Bean / Tables 类）
  → Load JojoP.HotUpdate.dll.bytes  （玩法 + ConfigManager）
HotUpdate 启动
  → Yoo LoadAsset TextAsset/json     （表数据）
  → new JojoP.Cfg.Tables(loader)
  → GameApp / UI
```

| 层 | 载体 | 热更什么时候更 |
|----|------|----------------|
| 表代码 | `Config.dll.bytes`（HybridCLR） | 加字段/加表 |
| 表数据 | `LubanConfig/*.json`（Yoo AB） | 日常调数值 |
| 玩法 | `HotUpdate.dll.bytes` | 改逻辑 |

**不是**「一边 LoadDll 一边还在 Assets 里读文件」两套长期并行；开发期用 File 兜底，运行时优先 Yoo。

### 方案对比

| 方案 | 说明 | 适合 |
|------|------|------|
| A. Resources | 进包大、难差量 | demo |
| B. 磁盘/StreamingAssets 裸读 | 简单，更新靠整包或自写下载 | 单机无热更 |
| **C. Yoo + ConfigManager（定案）** | DLL 热更代码，AB 热更 json | **本项目** |
| D. Addressables | 与 Yoo 叠床架屋 | 已选 Yoo 则不必 |
| E. Luban 二进制 + 内存映射 | 包体/解析更快 | 表很大再上 |

### 本项目落地规则

1. `CfgTables` = **ConfigManager 门面**（持有 `Tables`）。  
2. `ICfgRawLoader`：`YooCfgRawLoader` 优先 → `EditorFileCfgRawLoader` 兜底（`devDirectPlay`）。  
3. location 约定：`tbrolelist`（及 `LubanConfig/tbrolelist` 兼容）。  
4. Loading 正式链补齐后：在 HotUpdate 入口 `await CfgTables.TryLoadAsync()`，不再依赖 `dataPath`。

---

## 2. 基础 UI 框架用啥？

### 对比（手游 IAA / 小团队）

| 方案 | 优点 | 缺点 | 结论 |
|------|------|------|------|
| **UGUI + Prefab + UIBinder/UIBind（定案）** | 已有、学习成本低、Prefab 进 Yoo 即可热更 | 手写绑定要纪律 | **采用** |
| FairyGUI / 其它中间件 | 策划友好、复杂 UI 快 | 包体/授权/再学一套；IAA 竖切过重 | 不做 |
| UI Toolkit | 编辑器工具强 | 运行时手游生态仍偏弱 | 不做游戏内 HUD |
| 巨型 UIFramework（栈/MVC 全家桶） | 大项目规范 | 当前面板少，过度设计 | 只留薄 `UiPanelService` |

### 定案用法

```text
Yoo 加载 UI Prefab
  → Instantiate
  → UIBinder / UIFormBase（key 取按钮文本）
  → 逻辑在 HotUpdate
```

- 运行时搭的壳（主菜单）：继续 `UIBinder.Set`  
- 正式 Prefab 面板：UIBind 生成 + `UIFormBase`  
- 打开/关闭：薄 `UiPanelService`（按 location 加载、缓存、关闭释放）  
- **不**上 FairyGUI；**不**把 UI 逻辑写进 AOT  

---

## 3. 和「商业标准」对齐的一句话

> HybridCLR 热更**代码**（Config/HotUpdate DLL），YooAsset 热更**资源**（表 json、UI Prefab、图音）；HotUpdate 里的 ConfigManager / UiPanelService 只做解析与打开，不直读工程目录。
