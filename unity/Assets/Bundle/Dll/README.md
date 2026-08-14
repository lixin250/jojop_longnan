# 热更 DLL（.bytes）

HybridCLR 编译产物拷到这里，由 YooAsset 随 `Assets/Bundle` 收集：

- `JojoP.Config.dll.bytes`（Luban 表代码，先加载）
- `JojoP.HotUpdate.dll.bytes`（玩法，后加载）

开发期 `devDirectPlay` 可不放文件，程序集仍在编辑器主域。
正式包：CompileDll → 复制到本目录 → 打 AB → Loading 里 Assembly.Load。

路径与 `JojoPGlobalSettings.hybridClr.assemblyTextAssetPath` 一致。
