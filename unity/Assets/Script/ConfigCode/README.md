# Luban 生成代码（Schema）→ 热更 DLL

```text
Excel → gen.bat → Script/LubanCode/Gen（源码）
                 ↓ HybridCLR CompileDll
            JojoP.Config.dll
                 ↓ 拷贝改名
        Bundle/Dll/JojoP.Config.dll.bytes
                 ↓ YooAsset AB
        Loading: Assembly.Load（先于 HotUpdate）
```

- `JojoP.Config.asmdef` 在本目录；`Gen/` 才是 gen 输出（会被清空）
- 日常改数值：只更 `Bundle/LubanConfig`（json AB）
- 改表结构：再编 `JojoP.Config` → 更新 `.dll.bytes` + json
