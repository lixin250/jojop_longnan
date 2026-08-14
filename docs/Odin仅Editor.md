# Odin 使用约定（仅 Editor）

工程里暂时继续用 Odin，但**只允许 Editor**，正式 App 不打包 Odin。

## 已做限制

1. 主 DLL（Attributes / Serialization / Utilities / Editor）→ **仅 Editor**
2. `Assemblies/NoEditor`、`NoEmitAndNoEditor` 下的 DLL → **全部平台关闭**
3. `link.xml` 已清空，避免 IL2CPP 强行保留 Sirenix
4. 已关闭并移除 Odin `Unity.Mathematics` 模块（工程未引 `com.unity.mathematics`，开着会刷红字）
5. 运行时代码（`JojoP.AOT` / `JojoP.HotUpdate`）**不引用** Sirenix
6. 只有 `UIBind.Editor` 等编辑器工具引用 Odin（`overrideReferences` + 显式 precompiledReferences）

## 你怎么自查打进包没有

打完 APK/AAB 或 Windows 包后，在输出目录搜：

- `Sirenix`
- `OdinInspector`

搜不到才算干净。

## 注意

- 运行时脚本不要写 `using Sirenix...`，也不要继承 `SerializedMonoBehaviour`
- UIBind 窗口可以继续用 Odin；生成出来的运行时 UI 代码不要依赖 Odin
- 不要在 Odin Module Manager 里启用 Mathematics / Entities 等模块（除非先装对应 UPM 包）
