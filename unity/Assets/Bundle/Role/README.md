# Role 资源

规划见仓库根目录 `docs/角色资源与Luban.md`。  
概念图裁切见仓库 `art/README.md`。

## 目录

```text
Assets/Bundle/Role/{id}/
  avatar.png
  half.png
  poster.png
  banner.png
  battle/
    idle.png  walk.png  atk.png  hurt.png  dead.png  taunt.png  fallback.png
```

表字段仍是 `role_lixin_avatar` / `role_lixin_battle`。运行时从 loc 解析 `{id}`。

## 加载键（Address）和 AB（Pack）是两件事

Yoo **同一 Package 里 Address 必须唯一**。开了 `EnableAddressable` 之后：

| 规则 | `lixin/avatar.png` | `lixin/battle/idle.png` | `oban/battle/idle.png` |
|------|--------------------|-------------------------|------------------------|
| `AddressByFileName` | `avatar` | `idle` | `idle` **撞名** |
| `AddressByFolderAndFileName` | `lixin_avatar` | `battle_idle` | `battle_idle` **撞名** |
| 自定义 `AddressByRoleRelative` | `lixin/avatar` | `lixin/battle/idle` | `oban/battle/idle` |

内置「文件夹名_文件名」只取**直接父目录**，所以战场不能靠它。我们用相对 `Assets/Bundle/Role/` 的路径：

```csharp
package.LoadAssetSync<Sprite>("lixin/avatar");
package.LoadAssetSync<Sprite>("lixin/battle/atk");
```

开 Addressable 后仍可用全路径 `Assets/Bundle/Role/lixin/avatar.png`。`RoleArtLoader` 两种都试，Editor 未打清单时走 AssetDatabase。

**不同 AB 里同名文件可以。** 撞的是 Address，不是磁盘文件名，也不是 bundle 内文件名。

## Group ≠ AB。Role Group 下很多小包

Group 只是收集器分类，**不是**一个 AssetBundle。

当前 Role 收集器：`PackTopDirectory`（收集器下每个一级文件夹一个包）：

```text
Role/lixin/**          → assets_bundle_role_lixin.bundle     一人一包（含 battle）
Role/大头贴/**         → 旧头像回落包
Role/battle/**         → 旧战场回落包
Role/halfbody/**       → 旧半身回落包
Role/poster/**         → 旧海报回落包
```

这就是「Role group 下很多小 AB」。**不要一人一个 Group**，编辑器会炸，效果和 PackTopDirectory 一样。

| Pack 规则 | 结果 | 什么时候用 |
|-----------|------|------------|
| PackTopDirectory | 一人一包（含战斗帧） | **现在用这个** |
| PackDirectory | `lixin` 一包 UI + `lixin/battle` 一包战场 | 大厅只要头像、战场再下时 |
| PackSeparately | 每张 png 一个 AB | 不要，请求太多 |
| PackCollector / PackGroup | 全角色一个大包 | 不要，改猩哥要重下所有人 |

加载 `lixin/avatar` 时 Yoo 会自动拉 `lixin` 那个包，不必按人打 Tag。Group 上的 `role` tag 留给以后「预下所有英雄」。

## 收集配置（已改）

- Package `EnableAddressable: 1`
- 原先 `Bundle` 整包收集 `Assets/Bundle`，会和 Role **重复收集**，已拆成 Skill / Item / ConfigData
- Role：`AddressByRoleRelative` + `PackTopDirectory` + 只收 png/jpg

改完后在 Yoo 窗口构一次清单（Simulate 即可），再 Play 验证寻址。

Unity 菜单：`JojoP/Art/从 art/_final 覆盖导入 Bundle`。
