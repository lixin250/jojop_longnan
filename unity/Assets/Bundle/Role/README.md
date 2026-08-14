# Role 资源

规划见仓库根目录 `docs/角色资源与Luban.md`。

当前可直接把大头贴放到 `Assets/Bundle/Role/大头贴/`，文件名对齐表字段 `avatar_loc`。

Unity 平衡窗口会按 `avatar_loc` → 角色 Id → 中文名 → Id 尾段前缀 匹配图片。

完整文件名清单见仓库 `docs/初始数值平衡工具.md`。

后续再迁到每角色一目录 `{roleId}/avatar`；未改 Yoo `AddressByFileName` 前，
文件名保持全局唯一，例如 `role_xiebo_avatar.png`。
