# 热更 DLL + AOT 补充元数据（.bytes）

两条线分开，不要混在一次「日常热更」里。

## 日常热更（Yoo / R2，不出新 APK）

只更新：

- `JojoP.Config.dll.bytes`
- `JojoP.HotUpdate.dll.bytes`

菜单：**JojoP / 编译热更 DLL → Bundle/Dll**  
或构建窗口按钮 1。**不要**覆盖 AOT 那几个 `.bytes`。Yoo 仍会把已有的 AOT 文件打进去，跟用户手里的 APK 对齐。

## 出新 APK 之后

il2cpp 变了，才从这次包的裁剪目录覆盖 AOT：

`HybridCLRData/AssembliesPostIl2CppStrip/{平台}/` → `mscorlib.dll.bytes` 等（列表看 `AOTGenericReferences.PatchedAOTAssemblyList`）

菜单：**JojoP / 出包后拷 AOT 补充元数据 → Bundle/Dll**  
然后构建 Yoo 并上传。一键出包会按「先 APK、再拷这次 strip、再 Yoo/R2」自动做。

官方：https://www.hybridclr.cn/docs/basic/aotgeneric  
打包后这份裁剪 dll 不要每次热更都换成最新 AOT。
