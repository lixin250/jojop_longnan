# UIBind 使用说明

从参考项目移植的 **Tag 扫描 → 生成代码 → 自动绑定** 工具（依赖 Odin Inspector，仅 Editor）。

## 用法

1. 给 Prefab 上的控件节点打 Tag（如 `Button`、`Text`、`Image&Button`）
2. 菜单 **Tools → UI Bind → Window**（或选中 Prefab 右键 **Assets/UI Bind**）
3. 扫描 → 勾选 Callback → **生成并绑定** / **仅绑定**

生成到：`Assets/HotUpdate/UI/<Prefab名>/`
- `Xxx.cs`：逻辑（不会被覆盖，只增量补事件）
- `XxxRegister.cs`：字段 + OnRegister（每次生成覆盖）

基类：`JojoP.AOT.UI.UIFormBase`（含 Button/Toggle/InputField 监听）

## 配置

`Assets/Editor/UIBind/UIBindSettings.asset`

- 代码根目录：`Assets/HotUpdate/UI`
- 命名空间：`JojoP.HotUpdate.UI`
- 基类：`JojoP.AOT.UI.UIFormBase`

## 与简易 UIBinder 的关系

- **UIBind 工具链**（Tag 生成 partial）：适合正式 Prefab 面板
- **UIBinder**（key→组件）：主界面 / GameHud 等运行时搭的 UI 在用，两者可并存

已定运行时 key：

| 面板 | key |
|------|-----|
| 主界面 | `txt_best` / `btn_start` / `btn_settings` |
| 设置 | `btn_privacy` / `btn_close` |
| 局内 HUD | `txt_score` / `txt_hint` / `btn_revive` / `btn_double` / `btn_retry` / `btn_home` |
