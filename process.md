# 进展记录（process.md）

## 当前完成度对照需求

| 需求 | 状态 | 说明 |
| --- | --- | --- |
| 1. 自定义纹章编辑器 | ✅ | BepInEx 插件，按 F2 开关（可在配置中改键） |
| 2. UI 可移动、可调整大小 | ✅ | 标题栏拖动；四边/四角 8 向拖拽缩放（仿 Windows 窗口），带最小尺寸限制 |
| 3. 默认半屏大小、字体放大 | ✅ | 首次打开默认占屏幕 50%×50%，居中偏上；字号已放大（标题 18 / 正文 13） |
| 4. 中文 UI | ✅ | 全部界面文案为中文 |
| 5. 图片/动画纯色占位 | ✅ | `ProceduralTextures` 按纹章 ID 的哈希色相程序化生成 8 帧纯色动画，每个模块一种图案 |
| 6. 组合原版纹章模块 | ✅ | 每个模块可独立从任一原版纹章中选择（如漫游者普攻 + 野兽下劈跳） |
| 8. 纹章拆分 a–h | ✅ | 插槽 / 普通攻击 / 回血（缚丝）方式 / 蓄力攻击 / 冲刺攻击 / 下劈跳 / 回血后特效 / **上劈**（本次补齐） |
| 9. 从原版纹章中选择模块 | ✅ | 运行时从 `Gameplay` 单例反射读取全部 `ToolCrest`；读取不到时用占位数据 |
| 10. 模块横向平铺 | ✅ | 每个模块一行，选项横向排列、可横向滚动 |
| 11. 保存后不立即生效，坐长椅切换纹章时装备 | ✅ | 保存仅写入 `charms.json`；通过 Harmony 补丁把合成纹章注入 `ToolItemManager.GetAllCrests/GetCrestByName`，长椅换纹章界面中可选；图标沿用"插槽"所选纹章，名字显示为自定义名 |

## 本次进展（2026-07-25）

1. **补齐"上劈模块"（需求 8h）**：此前 `CharmPart` 枚举只有 7 个部分，缺少需求中的 h.上劈模块。
   - 用 `strings` 直接扫描游戏 `Assembly-CSharp.dll`，确认游戏内上劈相关字段：`UpSlash` / `UpSlashDamager` / `UpSlashObject`、`AltUpSlash` 系列及标量字段 `upSlashOffset`。
   - `CharmPart` 新增 `UpSlash`，中文名"上劈"；标量字段映射 `upSlashOffset`，组字段映射 6 个 UpSlash/AltUpSlash 引用字段。
   - 占位动画新增"上劈"图案（向上突刺，与下劈跳镜像）。
   - 编辑器滚动条数组长度由硬编码 6 改为跟随 `NonSlotParts.Count`，避免再加模块时越界。
2. **构建验证通过**：环境中没有 .NET SDK（Windows 侧只有运行时），在 `/tmp/opencode/dotnet` 临时安装 .NET 8 SDK，以 `GameDir=../Silksong` 构建，`0 警告 0 错误`，DLL 已自动部署到 `../Silksong/BepInEx/plugins/SilksongHelper/`。

## 已实现的架构

- `Plugin.cs`：BepInEx 入口，初始化目录/存档/Harmony 补丁。
- `Charm/CrestCatalog.cs`：反射读取原版纹章（`ToolCrest` + `HeroConfig`），失败时回退到 6 个占位纹章（漫游者/野兽/收割者/猎手/巫女/工匠）。
- `Charm/CharmPart.cs`：8 个模块的定义，及每个模块对应的 `HeroControllerConfig` 标量字段（`PartFields`）和配置组引用字段（`PartGroupFields`）。
- `Charm/CustomCharm.cs` + `CharmSaveData.cs`：自定义纹章数据与 JSON 持久化（`BepInEx/config/SilksongHelper/charms.json`）。
- `Editor/CharmEditor.cs`：IMGUI 窗口（拖动/8 向缩放/中文/半屏默认尺寸/横向平铺选项/预览/已保存列表）。
- `Game/CustomCrestRegistry.cs`：为每个已保存纹章克隆其"插槽"对应的原版 `ToolCrest`，命名为 `__silksong_custom__<id>` 哨兵名注入游戏，因此切换纹章界面图标与插槽纹章一致、名字不同（需求 11）。
- `Game/CrestInventoryPatches.cs`：Harmony 补丁——`GetAllCrests`/`GetCrestByName` 注入合成纹章、`DisplayName` 换成自定义名、`IsUnlocked` 强制解锁、`HeroController.ResetAllCrestState` 后应用/还原覆盖。
- `Game/CharmApplier.cs`：把所选各模块来源纹章的字段值复制到当前激活配置，记录原值可随时还原。
- `Animation/`：纯色占位图与逐帧动画。

## 难点与风险

1. **无源码、无公开 API**：游戏类型与字段只能靠反射 + 字符串扫描二进制确认（如 `UpSlash` 字段）。字段名若随游戏更新改动，Mod 会静默跳过（有日志），需要重新核对。
2. **构建环境**：本机（含 WSL）没有 .NET SDK，本次临时装到 `/tmp/opencode/dotnet` 并需设 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`（缺 libicu）。后续构建沿用此方式或在 Windows 装 SDK。
3. **模块拆分的语义边界**：哪些字段属于"普通攻击"哪些是"下劈跳"是按字段名人工归类的（见 `PartFields`），实际效果需在游戏内逐一验证微调。
4. **动画刷新**：`CharmApplier` 通过反射调 `SetHeroControllerConfig` 刷新动画，方法签名若变化需适配。
5. **未实机验证**：目前只做到编译通过 + 部署，尚未在游戏中实际运行验证坐长椅换纹章的完整链路，这是下一步重点。

## 下一步

1. 启动游戏实机验证：F2 打开编辑器 → 组合并保存纹章 → 坐长椅 → 在换纹章界面装备自定义纹章 → 验证攻击/回血/下劈等行为是否符合所选模块。
2. 根据实机结果修正 `PartFields`/`PartGroupFields` 的字段归类。
3. 用真实贴图替换纯色占位图（需求 5 允许后期再换）。
