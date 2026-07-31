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

## 进展 2（2026-07-25）：存档修改（为实机测试做准备）

**目标**：游戏内还没有坐过长椅，无法测试"坐长椅换纹章"链路。直接修改存档。

**存档格式研究结论**（参考社区项目 just-addwater/silksong-saveeditor 与反编译验证）：
- 路径：`%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\default\user1.dat`
- 结构：.NET BinaryFormatter 风格 22 字节头 + 7-bit 变长长度前缀 + Base64 字符串 + 0x0B 尾
- 加密：Base64 解码后为 **AES-256-ECB（PKCS7）**，密钥为 ASCII `UKu52ePUBwetZ9wNX88o54dnfKRu0T1l`，解密后是 JSON（playerData / sceneData / ToolEquips / Tools）
- 长椅重生机制（反编译 `HeroController`/`PlayerData`/`SceneTeleportMap` 确认）：
  - `respawnScene` + `respawnMarkerName` + `respawnType=1` → 在长椅上醒来（respawnType=1 且 marker 物体带 "Bench Control" FSM）
  - `resources.assets` 内嵌 SceneTeleportMap 可解析出每个场景合法的重生点；骨底镇 `Bonetown` 的重生点含 `RestBench`

**已修改内容**（原文件已备份为 `user1.dat.bak_before_edit`）：
1. 出生点 → `Bonetown` / `RestBench`（骨底镇长椅，开局最近的城镇长椅）
2. 二段跳 `hasDoubleJump`、冲刺 `hasDash`、飞针冲刺 `hasHarpoonDash`（即"丝针"类能力）
3. 灵丝技能"丝之矛"：Tools 列表加入 `Silk Spear` 并设 `hasSilkSpecial`
4. 所有纹章：ToolEquips 列表补齐 10 个纹章（Hunter / Hunter_v2 / Hunter_v3 / Reaper / Wanderer / Warrior / Toolmaster / Witch / Spell / Cursed）并全部 `IsUnlocked`

**难点**：
- "丝针"在中文 wiki 无此确切名称，按最接近的能力处理为丝之矛 + 飞针冲刺（两者都解锁，覆盖两种理解）
- 存档字段必须与游戏 `PlayerData` 完全一致，多写不存在的字段有反序列化风险（如 HasSeenDoubleJump 并不存在，已避免）
- 写回时 7-bit 长度前缀需重算，已做完整解密→修改→加密→回读验证（round-trip 通过）

## 进展 3（2026-07-25）：可视化存档修改工具 + 重要 bug 修复

**部分纹章组合已实机验证成功** ✅（用户反馈）

### 新工具：`tools/SaveEditor/index.html`

单文件、零依赖、纯本地的网页存档修改器，双击即可在浏览器使用（拖放或选择 `user*.dat`）：
- **出生点**：场景 + 重生点两级下拉，选项来自从游戏 `resources.assets` 解析出的 SceneTeleportMap（142 个场景、89 个长椅场景的真实数据）；选 RestBench 自动联动"在长椅上醒来"（respawnType=1）
- **血量**：当前血量 health、血量上限 maxHealth/maxHealthBase/prevHealth
- **伤害**：织针等级 nailUpgrades 0–4（对应伤害 5/9/13/17/21，反编译 PlayerData 确认）
- **灵丝上限** silkMax（附带）
- **一键习得所有技能**：8 个先祖技艺（冲刺/攀墙/二段跳/滑翔/飞针/灵丝升腾/蓄力斩/织忆弦针）+ 6 个灵丝技能（同时写 PlayerData 布尔位和 Tools 解锁条目）
- **一键解锁所有纹章**：10 个纹章写入 ToolEquips
- 实现：纯 JS AES-256-ECB + Base64 + BinaryFormatter 包装，通过了 AES 标准测试向量校验，并用 Python 对 JS 输出做交叉验证（round-trip 一致）

### 重要 bug 修复：纹章/工具解锁写错位置 ⚠️

- 上次直接改存档时，把 `ToolEquips`/`Tools` 写到了 JSON **顶层**，而游戏只读 `playerData.ToolEquips` / `playerData.Tools` → "所有纹章"和"丝之矛"实际**没生效**（游戏下次存档时还把这些未知顶层字段丢弃了）。能验证成功是因为 Mod 的自定义纹章走的是运行时注入，不依赖存档解锁。
- 已在存档工具和手动修改中一并修正；用户存档已重新正确写入 10 纹章 + 6 灵丝技能工具（备份 `user1.dat.bak_fix2`）。

### 难点记录

1. 初版 JS AES 轮函数中 AddRoundKey 与 MixColumns 顺序写反，输出全错 → 用 AES-256 标准测试向量（FIPS-197）定位并修复，重写为干净的轮结构实现。
2. 存档 JSON 顶层只有 `playerData` 和 `sceneData`，纹章/工具列表藏在 playerData 内部——靠回读验证才发现写错层级，教训：**修改后必须用游戏真实读取路径验证**。
3. SceneTeleportMap 是 resources.assets 里的二进制 ScriptableObject，手写了一个长度前缀字符串解析器提取了全部场景/重生点数据（含 `RestBench`、`TrapBench`、`Death Respawn Marker *` 等）。

## 进展 4（2026-07-25）：存档工具实测通过 + 首个自设计纹章「疾风纹章」

### 存档工具：jsdom 端到端自动化测试（16 项全部 PASS）

在 `/tmp/opencode/savetest` 用 jsdom 模拟完整用户流程并配合 Python 独立校验：
- 页面脚本零错误、点击虚线框正确触发文件选择、真实存档加载后各字段正确回显
- 切换场景联动重生点列表；RestBench↔respawnType=1 自动联动（修复了切换场景后残留旧 marker 导致联动失效的 bug）
- 一键全技能/全纹章后状态显示 10/10；修改血量/伤害/灵丝后保存触发下载
- 下载产物用 Python 独立解密校验：出生点/血量上限/织针等级/14 技能/10 纹章全部正确，且 JSON 顶层无脏字段

### 新纹章：「疾风纹章」（v0.4.0）

**设计方案**（`src/SilksongHelper/Charm/DesignedCrests.cs`）：以漫游者纹章为蓝本克隆，对其 HeroControllerConfig 施加相对倍率修改（鲁棒：无需知道游戏内绝对数值）：

| 模块 | 修改 |
| --- | --- |
| 普通攻击 | 攻击冷却 ×0.6、快速攻击冷却 ×0.6、攻击时长 ×0.8、后摇 ×0.7、可转身挥砍 |
| 蓄力攻击 | 可蓄力、突进速度 ×1.4 |
| 冲刺攻击 | 速度 ×1.3、时间 ×0.85、**2 段** |
| 下劈跳 | 下刺速度 ×1.25、前摇 ×0.7、恢复 ×0.7、带推力与爆发特效 |
| 缚丝 | 可缚丝 |

**接入方式**：与自定义纹章同一套 Harmony 注入管线——`GetAllCrests`/`GetCrestByName` 注入克隆纹章（id=`Gale`）、`DisplayName` 显示"疾风纹章"、`IsUnlocked` 强制解锁；同时加入 `CrestCatalog`，因此在编辑器的每个模块行里也能单独选用疾风纹章的模块（如只借它的下劈跳）。基底解析失败（如主菜单）时按 def 粒度延迟重试。

**难点**：游戏配置的绝对数值在资产包里难以读取 → 改用"克隆+倍率"的相对设计；`ToolCrest.heroConfig` 为共享资产，必须克隆后再改，避免污染原版漫游者纹章。

## 进展 5（2026-07-25）：修复疾风纹章无攻击动作 + 斩击特效改造

### 问题：疾风纹章完全没有攻击动作 ⚠️（用户实机反馈）

**根因**（反编译 `HeroController.UpdateConfig` 确认）：配置组（ConfigGroup，含攻击预制体 NailSlash/动画库/ActiveRoot）按 **HeroConfig 引用相等**匹配。v0.4.0 克隆了新的 HeroControllerConfig，引用匹配失败 → `configGroup was null!` → 不设置任何 slash 对象 → 完全没有攻击动作。

**修复**：疾风纹章的 `heroConfig` 改为**共享漫游者的配置资产**（引用相等 → 攻击动作/动画/判定全部正常工作），自定义数值改为装备时 `ApplyRuntime` 写入、卸下时 `RestoreRuntime` 还原（与 CharmApplier 相同的 originals 记录模式）。调用链验证：`ResetAllCrestState` → 我们的 `GetCrestByName` 补丁返回 Gale 克隆 → `crestConfig`=共享配置 → `UpdateConfig` 匹配漫游者组 → postfix 应用数值。

### 斩击特效改造（在蓝本动画基础上的运行时视觉设计）

真正全新的攻击动画需要制作 tk2d 精灵帧资产（后续工作）。当前在运行时对斩击特效做可逆改造，使疾风纹章视觉与漫游者明显不同：
- 斩击对象（普通/交替/上劈/交替上劈）**缩放 ×1.2**——攻击弧更大，判定范围同步放大（多边形碰撞体随 transform 缩放）
- 斩击网格**染色为疾风青蓝**（0.55, 0.95, 1），卸下时恢复原色（材质实例化，不污染共享资产）

### 难点记录

1. 「克隆配置」与「引用匹配」的矛盾：克隆保证不污染原版，但游戏按引用选配置组——最终方案是共享配置 + 运行时改值 + 精确还原。
2. 共享资产的污染风险：tk2d 动画剪辑 fps、材质等都是共享资产，不能直接改；材质通过 `renderer.material`（自动实例化）+ 记录原色还原解决。

## 进展 6（2026-07-27）：修复"换纹章后无法攻击"（v0.4.1）

### 现象与定位

用户实测：首次装备疾风纹章攻击正常（蓝色斩击 ✓）；但新建一个自定义纹章（ACM，借用了漫游者模块）后再换回疾风纹章 → 完全无法攻击。

在 Mod 中埋了 `[DIAG]` 诊断补丁（Attack/UpdateConfig/NailSlash.StartSlash 日志），从用户实机日志拿到铁证：
```
applied custom charm overrides 'ACM' (59 fields)
designed crest '疾风纹章' applied (16 fields)
[DIAG] NailSlash.StartSlash on Slash active=False   ← 斩击对象处于禁用状态
```
攻击逻辑在跑（StartSlash 被调用），但斩击 GameObject 未激活 → 无动画无伤害。

### 根因

`CharmApplier.ActivateRoot` 会激活源纹章的配置组根对象（ACM 借了漫游者模块 → 漫游者根对象被记录进 `_activatedRoots`）。换回疾风纹章时，`HeroController.SetConfigGroup` 刚为疾风激活漫游者根对象，随后我们的 postfix 调用 `RestoreOverrides()` 把 `_activatedRoots` 里所有根对象 `SetActive(false)` —— **把当前正在使用的根对象误关了**。

### 修复（v0.4.1）

1. `CharmApplier.RestoreOverrides(hero)`：禁用根对象前，先解析 `CurrentConfigGroup.ActiveRoot` 并跳过它。
2. `ResetCrestStatePatch`：把 `RestoreOverrides`/`RestoreRuntime` 统一提到分支之前执行（任何切换路径都先干净还原），再按纹章类型应用新状态。

### 难点记录

- 游戏侧日志无任何异常（攻击流程"正常"跑完），只有埋点诊断日志才能发现 `active=False`；反射操作共享配置组状态时，**恢复顺序与游戏自身的 SetConfigGroup 激活顺序**是关键。
- 诊断补丁 `Game/DebugDiagPatches.cs` 暂保留用于下一轮实机验证，确认无误后应删除（刷日志较多）。

## 进展 7（2026-07-27）：真正移植攻击动画 + 修复特效中途失效（v0.5.0）

### 用户反馈的三个问题与对策

**1. 打两次蓝色斩击后特效消失**
- 根因：v0.4.1 把还原逻辑改为无条件执行，而 `ResetAllCrestState` 会被很多事件触发（TOOL EQUIPS CHANGED 等）；某瞬间反射解析当前纹章 ID 失败时，疾风的修改被还原且不再应用。
- 修复：补丁改为**幂等+按需**——当前纹章与已生效纹章相同则直接跳过；ID 解析失败则什么都不做；只有确认切换到不同纹章时才还原并应用新状态。

**2. 疾跑攻击方向异常**
- 根因：`dashStabSteps=2` 覆盖破坏了漫游者冲刺突刺的方向逻辑。
- 修复：移除该覆盖（保留冲刺速度 ×1.3 / 时间 ×0.85）。

**3. "本质上只是复用漫游者动画改颜色"——接受批评，这次真正移植动画**
- 新增 `GroupSwaps` 机制：装备疾风纹章时，把**收割者纹章的普攻/上劈**和**野兽纹章的下劈**整套斩击预制体引用（NailSlash/Damager/GameObject）写入当前配置组，并激活来源纹章的根对象——普攻/上劈/下劈使用的是**别的纹章的挥砍动画**，不再是漫游者的动作。
- 疾风纹章 v2 设计：收割者横扫普攻+上劈（大开大合）× 野兽强力下劈 × 漫游者迅捷身法（冷却 ×0.75）+ 斩击放大 1.2 + 青色染色。
- 卸下时精确还原全部引用/缩放/染色，禁用借来的根对象（跳过当前激活根对象，沿用 v0.4.1 的修复）。

### 难点记录

- 斩击预制体挂在各纹章自己的 ActiveRoot 下，移植引用后必须激活来源根对象；恢复时又要避免误关正在使用的根对象——与 CharmApplier 的模块混合机制同一套模式，已验证可行。
- `ResetAllCrestState` 触发时机比预想多得多（每次攻击、工具栏变化都会调 UpdateConfig），所有运行时修改都必须幂等可重入。

## 进展 8（2026-07-27）：完全自创攻击动作「旋风丝刃」（v0.6.0）

### 需求

用户明确要求：新纹章的攻击动作必须是**自己设计的全新动作**，不能来自漫游者/收割者/任何已有纹章（v0.5.0 的"移植其他纹章动画"方案被否决，已移除 GroupSwaps 机制）。

### 自创招式「旋风丝刃」（Silk Cyclone）

装备疾风纹章后进行**水平攻击**时，原版斩击被完全拦截（Harmony prefix `NailSlash.StartSlash` return false），替换为自创招式：织针化作两片丝刃月牙，环绕大黄蜂全身高速旋转两周（0.5 秒），对四周 1.9 米内所有敌人造成多段伤害并径向击退。

- **动画全新**：16 帧月牙丝刃旋转动画全部由 `ProceduralTextures.BuildCyclone` 程序化绘制（角向羽化+径向渐变+外缘刀锋亮线），不取自任何游戏资产；已用 Python 复刻算法生成预览图做视觉校验。
- **伤害逻辑自实现**：`Physics2D.OverlapCircleAll` + `HealthManager.Hit(HitInstance)`（反射构造：AttackType=Nail、伤害=织针×0.6、径向击退 CircleDirection），同一敌人 0.24s 受击间隔，最多 3 个旋风并存。
- **安全拦截**：只拦截 normal/alternate 水平斩；上劈/下劈/冲刺/蓄力保留漫游者基础动作+提速+青色染色。cState.attacking 由计时器驱动，跳过 StartSlash 无副作用（反编译确认）。
- 技术要点：普通 `SpriteRenderer`（sortingOrder=100）直接渲染，无需 tk2d 资产；每帧跟随英雄位置（不父子绑定，避免朝向翻转影响）；`HitInstance` 为全局命名空间结构体，反射装箱传参。

### 参考

- 用户提供的视频为丝之歌源码解析（架构/移动逻辑），本项目攻击链路结论均来自本地反编译（ilspycmd）验证。

## 进展 9（2026-07-28）：精细特效体系——五套独立招式特效（v0.7.0）

### 用户反馈

「旋风丝刃可以使用，但特效不够精细」→ 要求更精细的特效，且下劈、上劈、冲刺攻击、回血各自不同。

### 实现：`FxTextures` + `GaleFx` 特效系统

**贴图层**（`Animation/FxTextures.cs`）：全部程序化绘制 + **2x 超采样抗锯齿**（2 倍尺寸绘制后平均降采样），白色带 Alpha、运行时染色：
- 柔光点（径向渐变，粒子/光晕用）、柔圆环（高斯边缘，冲击波/脉冲用）、锥形刀光 streak（亮芯+外辉+两端收尖，旋转即变向）
- 已用 Python 复刻算法生成预览图逐一视觉校验。

**五套招式特效**（`Game/GaleFx.cs`，轻量粒子组件 FxParticle/WispRing/BindAura）：

| 招式 | 特效 | 主题色 |
| --- | --- | --- |
| 普攻 旋风丝刃 | 原月牙旋风 + 新增：冲击环、10 颗外溅火花、6 点逆向旋转光尘环 | 疾风青蓝 |
| 上劈 青霄刺 | 3 道上冲光刃（扇形展开）+ 上飘光尘 + 小环 | 青空色 |
| 下劈 坠星刺 | 3 道下坠光刃 + 8 颗高速坠落光尘 | 深蓝色 |
| 冲刺 疾影突 | 4 条身后拖影光痕（错位渐隐）+ 小冲击环 | 疾风青蓝 |
| 缚丝 丝愈之环 | 缚丝期间持续：呼吸光晕 + 每 0.45s 上升脉冲环 + 螺旋上升光尘 | 丝愈青绿 |

**挂接**（`Game/GaleFxPatches.cs`，均仅疾风纹章装备时生效）：
- 上劈/下劈：`NailSlash.StartSlash` postfix 按 hc 字段比对 slash 身份
- 下刺：`Downspike.StartSlash` postfix（漫游者 downSlashType=DownSpike 走此路径）
- 冲刺：`NailSlashTravel.OnEnable` postfix + `cState.dashing` 守卫（防止换纹章时误触发）+ 0.3s 防抖
- 缚丝：`HeroController.Update` postfix 检测 `cState.isBinding` 上升沿，BindAura 持续到缚丝结束

## 进展 10（2026-07-28）：四大攻击招式全部自创（v0.8.0）

### 需求澄清

用户：特效不是加贴图——要的是**攻击动作行为本身**全新，普攻/上劈/下劈/冲刺都不能同于任何已有纹章。

### 四个自创招式（行为层面全新，`Game/GaleMoves.cs` + `Game/GaleCombat.cs`）

| 招式 | 行为设计（与任何纹章都不同） | 拦截点 |
| --- | --- | --- |
| 普攻「旋风丝刃」 | 360° 环绕多段攻击（v0.6.0 已有） | NailSlash normal/alt |
| 上劈「青霄柱」 | 大黄蜂小幅浮空，头顶生成丝刃柱（1.5×3.6 判定盒）0.45s 内 4 段攻击，可在空中连段 | NailSlash up/altUp |
| 下劈「坠星震荡」 | 高速下坠（-24 速度），落地或命中敌人瞬间爆发：直接伤害 + 向两侧各推进 6 段的**地面冲击波**；命中敌人则弹起 | NailSlash down/altDown + Downspike 双路径 |
| 冲刺「残影连突」 | **穿透**整条冲刺路径的所有敌人（每 0.06s 判定），末端小爆发径向击退——原版任何纹章的冲刺斩都只打身前 | NailSlashTravel.OnEnable（cState.dashing 守卫+防抖） |

- 伤害基础设施抽取为 `GaleCombat`：反射构造 HitInstance、OverlapCircle/Box 敌人检测、英雄状态读取，四个招式复用。
- 每招配独立特效（v0.7.0 的精细贴图粒子）：青霄柱光尘上涌、坠星落地冲击环+波前、连突路径残影+末端爆发环。
- 原版斩击全部被 prefix 拦截（return false），伤害/动画完全由自创逻辑接管；hero 攻击状态机由计时器驱动不受影响（已验证的拦截模式）。

## 进展 11（2026-07-28）：攻击范围精修 + 冲刺钻头动画（v0.8.1）

### 用户反馈与修复

**1. 攻击范围太大，要为每个动作设计独特且合适的范围**

逐项重调（并让判定与动画视觉对齐，杜绝"看不见的判定"）：

| 招式 | 旧范围 | 新范围 |
| --- | --- | --- |
| 旋风丝刃 | 半径 1.9（比动画大一圈，视觉外隐形判定） | 半径 **1.3**，动画放大 1.2× 使刃圈≈判定圈 |
| 青霄柱 | 1.5×3.6 @ +2.4（半屏高） | **1.1×2.2** @ +1.5，贴身立柱 |
| 坠星震荡 | 冲击圈 1.7、冲击波 ±3.3×r0.85（≈7 米宽） | 冲击圈 **1.15**、冲击波 4 段 ×0.45（±1.8）×**r0.6** |
| 残影连突 | 路径 r1.3、末端 r1.9 | 路径 **r0.9**（与钻头同宽）、末端 **r1.25** |

**2. 冲刺攻击没有动画**

- 新增 `ProceduralTextures.BuildDrill`：程序化"螺旋织针钻头"8 帧动画（螺旋纹相位随帧移动产生旋转钻进感、针尖白热），Python 复刻算法预览校验通过。
- `PhantomLunge` 现在携带钻头动画（24fps、1.6×、枢轴偏后针尖前伸、随英雄朝向翻转、结束淡出），判定宽度与钻头视觉一致。

## 进展 12（2026-07-31）：按 description.md 实现新纹章「亵渎者」+ 真实碰撞箱挥砍（v0.9.0）

### 需求

1. 用户指出普攻（旋风丝刃）是"自动索敌"，要求**真实碰撞箱**且大黄蜂有**出刀动作**。
2. 用户亲自撰写了 `description.md`：新纹章「亵渎者」，红色圣剑"亵渎圣剑"——普攻前挥、冲刺化虚影闪现、冲刺攻击化血光+沿途烈焰、下劈下挥、上劈上挑。

### 实现（`Game/BlasphemerMoves.cs`）

**亵渎圣剑挥砍 `SwordSwing`（普攻/上劈/下劈共用）**：
- 程序化血色刀光月牙（160px 超采样，两端收尖+外缘刀锋亮线，Python 预览校验）
- **真实 PolygonCollider2D 碰撞箱**：按刀光形状生成外弧+内弧闭合多边形（22 点），随挥砍角度扫过空间；用 `Collider2D.OverlapCollider` 物理查询（不受游戏碰撞矩阵影响）判定命中——刀光扫到谁才打谁，不是索敌光环
- 挥砍运动：easeOutCubic 快出刀缓收刀（0.18s），普攻连击上下交替；大黄蜂本体挥臂动作照常播放，刀光与之同步
- 方向专属：上劈向上挑（10°→165°）、下劈向下砍（命中空中敌人**弹起** 12 速度）

**冲刺「虚影闪现」**：冲刺开始沿检测（cState.dashing 上升沿），向前瞬移 4 米（Terrain 层射线钳制防穿墙），路径残影+两端血影特效。

**冲刺攻击「血光穿刺」**：0.35s 血光附体，穿刺经过的所有敌人（1.0× 织针），每移动 0.8 米留下一团**烈焰**（程序化 6 帧跳动火焰贴图，燃烧 3 秒、每 0.5s 灼烧、熄灭前渐弱，最多 12 团）。

**纹章路由**：拦截补丁从"仅疾风"改为按 `DesignedCrests.AppliedId` 分发（Gale → 原四招；Blasphemer → 圣剑系），两个自设纹章可共存。

### 难点

- 碰撞矩阵风险：游戏自定义了 Physics2D 碰撞层，OnTriggerEnter 未必触发 → 改用真实碰撞箱 + OverlapCollider 主动查询，兼顾"真碰撞箱"与可靠性。
- 冲刺与冲刺攻击共用 dashing 状态 → 闪现放在冲刺开始沿，血光走 NailSlashTravel 拦截（带 dashing 守卫），各司其职。

## 进展 13（2026-07-31）：专属纹章图标——解决"找不到亵渎者"（v0.9.1）

### 问题定位

用户反馈"并没有新的动作"。查 BepInEx 日志：`designed crest '亵渎者' built` ✓ 但全程 `crest=Gale`——**用户一直在用疾风纹章，亵渎者从未被装备**。原因：两个自设纹章都克隆漫游者外观，长椅界面里难以分辨。

### 修复

为自设计纹章生成**程序化专属图标**（`ProceduralTextures.BuildSwordIcon` / 旋风帧 + `Silhouette` 剪影），写入克隆纹章的 `crestSprite`/`crestSilhouette`/`crestGlow` 字段：
- 亵渎者 → **红色圣剑图标**（剑刃+血槽亮线+护手+柄首圆珠，Python 预览校验）
- 疾风纹章 → 双月牙旋风图标

长椅换纹章界面现在一眼可辨。

## 进展 14（2026-07-31）：亵渎者精细度修复——冲刺位移/下劈碰撞箱（v0.9.2）

### 用户反馈与根因修复

**1. 冲刺攻击没有位移**
- 根因：`NailSlashTravel.OnEnable` 本身就是原版冲刺突进的位移驱动者，我们拦截后无人驱动 → 血光/钻头在原地空转。
- 修复：`BloodRush`/`PhantomLunge` 自驱动位移（每帧设 `Body.linearVelocity = facing × 22/24`，结束缓出 0.35× 不骤停）。
- 顺带修复普通冲刺闪现后骤停的问题：虚影闪现后保留 16 前冲速度继续滑行。

**2. 下劈没有真正的攻击碰撞箱**
- 根因①（命中盲区）：月牙碰撞箱是半径 0.79~1.4 的空心弧带，正下方敌人落在圆心空洞里恰好打不到 → 碰撞箱内缘收到 0.2，覆盖贴身扇形；下劈弧心下移到 0.55、上劈上移到 1.1，判定真正罩住脚下/头顶。
- 根因②（下刺路径静默失效）：`Downspike` 的英雄字段叫 `heroCtrl`（不是 NailSlash 的 `hc`），反射取不到 → 下刺拦截直接放行。已兼容两个字段名。

### 难点记录

- 原版组件的职责边界：NailSlashTravel 不仅是斩击特效，还驱动位移——拦截任何原版组件前必须搞清它的全部职责（反编译 FixedUpdate 可见位移逻辑）。
- 空心弧带碰撞箱适合"正前方挥砍"，对正下方/正上方目标必须用近心填充 + 弧心偏移。

## 进展 15（2026-07-31）：冲刺回弹根源修复 + 下劈弹障碍物（v0.9.3）

### 用户反馈与根因

**1. 冲刺位移后又回到原点**
- 根因（反编译 NailSlashTravel 全程确认）：原版冲刺斩的"斩击物前行 + 英雄跟随/归位"是一个完整状态机，我们 v0.9.2 拦截它后自驱动位移，与 FSM 的归位逻辑互相打架 → 位移被拉回。
- 修复：改为**共存策略**——不再拦截 NailSlashTravel（原版突进位移/动画/判定照常，不会回弹），血光附体/钻头动画/额外穿刺判定/烈焰地带作为叠加层在其上运行（postfix 触发）。

**2. 下劈砍障碍物不会弹起**
- 根因：弹起判定只认 HealthManager（敌人），不认尖刺/机关等障碍。
- 修复：碰撞箱重叠结果里，敌人（HealthManager）或障碍（**DamageHero**：尖刺/荆棘/机关）都会触发空中弹起（+12 速度）。

### 难点记录

- 教训：对深度耦合进 FSM 的原版组件（NailSlashTravel 的 Travel 协程 + setPosition 归位委托），"拦截+自实现"不如"共存+叠加"稳定；自创内容应作为增量层，只在完全自管的招式（圣剑挥砍/旋风丝刃等）上做拦截。

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
