# Broadcast 与 PositionTone 开发手册

> 用途：根据当前 Unity 脚本、关卡场景序列化数据和已确定的声音设计，说明 Broadcast 与 PositionTone 目前做到哪里、还缺什么，以及推荐的实施顺序。本文件是开发说明，不表示下面的未完成步骤已经实施。

## 1. 设计约定

本手册严格区分两个触发场景：

### Broadcast

玩家主动请求地图播报。系统按列依次播放地图格子信息；格子的高度由音高表示，类型由音色/音色组表示，水平位置由声像和播报时间表示。**格子离玩家越远，Broadcast 音量越低**，由 Wwise distance RTPC 控制。播报是主动查询地图，不是玩家踩格时的提示。

### PositionTone

玩家踩上/进入一个新格时，播放当前位置提示音。提示音依据当前格子的类型选择，并依据其高度选择音高。它只表示当前位置，**不使用距离音量衰减**。跳跃中的 Airborne 音调是另一条反馈链路，不应与 Broadcast 距离 RTPC 混用。

## 2. 代码核对结果

状态定义：

- **已实现**：当前脚本中存在可运行的逻辑，并且关卡场景有对应序列化配置。静态核对不等于已在 Unity Play Mode 验证。
- **部分实现**：基础脚本或数据结构存在，但行为与本方案不完全相同、配置覆盖不完整，或尚未核实运行效果。
- **未实现**：在当前 `Assets/Scripts` 代码和已检查的关卡配置中没有找到所需实现。

### 2.1 Grid 数据

| 能力 | 状态 | 当前依据与差距 |
|---|---|---|
| Tilemap 导入 GridDatabase | 已实现 | `TilemapImporter` 读取非空 Tile 并按映射写入数据库；空格在查询时由 `GetCell` 临时视为 `Empty`。 |
| 查询格子、列、层，更新/移动格子 | 已实现 | `GridDatabase` 提供 `GetCell`、`GetColumn`、`GetLayer`、`SetCellType`、`MoveCell` 等接口。 |
| CellType 分类 | 部分实现 | 已有 Ground、Wall、Door、CrackedWall、Lever、Cannon、Battery、Goal、Lore、Death、Background、AirWall 及方向性大小跳提示类型。格子类型定义本身不代表对应声音或玩法都已接通。 |
| 格子类型到音频 Y 的偏移 | 未实现/存在文档冲突 | 当前 `GridAudioCoordinateUtility.GetAudioY` 直接返回 `cell.position.y`。旧项目说明曾规定 Ground 播报 y+1；当前代码并未如此处理。实施前必须确定坐标规则并统一。 |

### 2.2 Broadcast

| 能力 | 状态 | 当前依据与差距 |
|---|---|---|
| S/W 全图正向/反向扫描 | 已实现 | `GridBroadcastController` 按 X 列生成步骤；方向键默认为 S/W。 |
| D/A 从玩家位置向右/向左扫描 | 已实现 | 使用 `PlayerPositionTracker.CurrentGridPosition.x` 作为起点。 |
| Shift 只播报当前 Y 层 | 已实现 | 左右 Shift 都会进入当前层模式。要实测玩家网格 Y 与“玩家脚下/可站立层”是否一致。 |
| Q 停止当前播报 | 已实现 | 会停协程、循环 tick 和当前活动事件，并带可配置淡出时间。 |
| 按列播放，列内上下格形成和弦 | 已实现 | 同一列里包含的格子会同时调用 Wwise Event；下一列按 BPM 与 beats per step 的节奏触发。 |
| 类型音色选择 | 部分实现 | 有 `Broadcast_Cell_Type` Switch Group 和按 CellType 设置 Switch 的逻辑。硬编码名称只覆盖部分类型；比如方向性跳跃提示、Death、AirWall 等需要决定如何播报/是否跳过。 |
| 高度/行对应音高 | 部分实现 | 支持每个 Y 配独立 Row Event，或以 `Broadcast_Cell_PitchStep` Switch 选择音阶。pitch step 当前按格子原始 `position.y` 与配置基准计算；与 Ground y+1 决策尚未统一。 |
| 水平声像 | 已实现 | 每个播放 GameObject 按 X 列位置移到 listener 左右；这与玩家到格子的距离音量是不同参数。 |
| 距玩家越远越轻 | **未实现** | `GridBroadcastController` 没有 Broadcast distance RTPC 字段，也没有计算玩家到格子距离或向 Wwise 写该值的代码。当前 emitter 的水平声像不等于 distance RTPC。 |
| Broadcast Wwise 内容 | 部分实现 | 当前 Windows/Mac `SoundbanksInfo.json` 中已有 Broadcast Bus、`Broadcast_Cell_Type` 和 `Broadcast_Cell_PitchStep` Switch，以及逐音高的 `Play_Broadcast_Cell` Events。已列出的 Game Parameters 有 `Jump_HintTone_Distance`，但未见 Broadcast 专用距离参数。需要在目标 Wwise 2025.1.3 项目中核实/建立。 |
| 播报中事件对象的 RTPC 稳定性 | 需核实 | `GridBroadcastController` 会复用每个垂直 voice index 对应的 GameObject；下一列可能在旧音符仍响时改其 Switch/位置/RTPC。需要确认 Wwise 是否会让在播声音跟着对象的新值变化；若会，需要为并发声音隔离对象或锁存/局部化参数。 |

当前 15 个关卡场景都包含 Broadcast 控制器，并配置了手动 X/Y 边界、行 Event、类型 Switch、音高 Switch 和声像引用。因此这是已有功能，不是从零开发；应在保留当前工作的基础上扩展 distance RTPC 和修正数据映射。

### 2.3 PositionTone、XPulse、Airborne 与脚步

| 能力 | 状态 | 当前依据与差距 |
|---|---|---|
| 持续位置音调 | 已实现 | `PlayerPositionToneController` 启动 Wwise Tone Event，并持续更新音高 RTPC、水平声像；部分关卡配置为读取玩家 Collider 脚底，另一些场景值可能不同。 |
| Airborne 显示/隐藏 | 已实现 | `PlayerFootstepAudio` 监听 `PlayerJump.JumpStarted`/`Landed`，分别发 PositionTone Show/Hide Event；高度音调突兀属于声音设计问题，代码里没有平滑或渐进方案。 |
| XPulse 每跨水平格触发 | 已实现但范围较窄 | `PlayerFootstepAudio.UpdateStep` 只看 `CurrentGridPosition.x` 是否变化，且默认只在 grounded 时播放。它不是任意 X/Y 格子的完整 enter 事件。 |
| 进入格子时按格子 CellType 选提示音 | **未实现** | XPulse 当前是单一 `positionTonePulseEvent`，脚本没有查当前格子类型并映射到专属当前位置 Event 的逻辑。 |
| PositionTone 音高按当前格高度 | 部分实现 | 持续位置 Tone 的 Y pitch RTPC 已存在；但脚步脉冲并未以 CellType+高度为键生成一次性的完整格子提示音。要规定这两种音调是否并存、谁负责音高。 |
| 每移动 1/4 格脚步定位 | **未实现** | `PlayerFootstepAudio` 控制的是移动时启动/停止 Wwise 脚步 loop，并在 X 格变化时发 pulse；没有按累计水平位移每 0.25 格触发的逻辑。Wwise Loop Event 内部是否随机播放，需要在 Wwise Authoring 中听取/检查，C# 代码无法证明。 |
| 方向性小跳/大跳提示 | 部分实现 | `CellType`、`GridCellFactory` 和 Debug 可视化认识这些类型；未在当前脚本中找到按玩家朝向筛选并播放跳跃提示音的逻辑。 |

旧聊天里说的 `PlayerPositionToneController_XPulse`、`PlayerPositionToneController_Airborne` 是关卡中两个对象的名称；它们实际使用同一个 `PlayerPositionToneController` 类。当前 15 个关卡都包含两套位置音调控制器和 `PlayerFootstepAudio`。XPulse 的 Inspector 参数在不同关卡可能不同，不能仅用某一关的设置推断全部关卡状态。

### 2.4 文档状态

仓库中的 `PROJECT_STATUS.md`、`PROJECT_NEXT_STEPS.md` 等仍描述早期 Grid World System 阶段，且旧的 Ground/audio-Y 规则与当前脚本有差异。后续开发应以场景和脚本为当前实现证据，以本手册记录新决策；完成开发阶段后再同步旧状态文档，避免把过期计划当成事实。

## 3. 推荐最终职责图

```text
TilemapImporter -> GridDatabase -> GridBroadcastController -> Wwise Broadcast Events
                                      |                     |- CellType -> 音色/Switch
                                      |                     |- Y -> 音高/Row Event
                                      |                     |- X -> 扫描时序/声像
                                      |                     `- 玩家到格子距离 -> Broadcast distance RTPC -> 音量
                                      |
                                      `-> 玩家当前格查询 -> PositionTone 当前位置提示 Event
                                                              |- CellType -> 提示音
                                                              `- Y -> 音高

PlayerJump -> PlayerFootstepAudio -> Airborne Tone show/hide + 起跳/落地音
PlayerMovement + 实际位移 -> 脚步定位反馈（计划：每 0.25 格，独立于 PositionTone）
```

**不可混淆的规则：**距离 RTPC 只写入 Broadcast 播报事件的 playback object；PositionTone、Airborne Tone 和脚步反馈不写该距离参数。PositionTone 触发的是当前所在格，而 Broadcast 触发的是玩家主动请求的地图扫描。

## 4. 详细开发步骤

建议逐阶段完成并保存可回退版本。先用一个代表性关卡做端到端实现，确认后再推广到所有关卡。不要同时重做音色、调 Airborne、升级 Wwise、改脚步和改扫描节奏，否则试玩反馈难以归因。

### 阶段 0：锁定规则与建立基线

1. 选一个地图内容有代表性的关卡作为验证场景（建议 Level 1 或者有多层、多类型格子的关卡）。
2. 在 Unity 中记录该场景的 Grid bounds、玩家初始格、所有 CellType Tile 映射、Broadcast 播报按键和 Wwise 行 Event/类型 Switch。
3. 听取当前 Broadcast、PositionTone、Airborne、脚步四条声音，分别记录“触发条件、传达信息、听起来的结果”。
4. 查明两个坐标事实：
   - 玩家站在 Ground 上时 `CurrentGridPosition.y` 是多少？
   - Ground 的 Broadcast 音高应使用真实 tile Y 还是站立面 Y（Ground Y+1）？
5. 决定 Broadcast 的距离定义。建议先用同一网格坐标下玩家位置与被播报格之间的二维直线距离；需明确地形阻挡是否不影响声学距离、竖直层差如何计入、格子中心到中心还是玩家脚下到格子中心。将选择记录到本文后再编码。
6. 明确 PositionTone 的“踩上格”语义：推荐落地后第一次进入某个 X/Y 位置时触发一次；在格内站立不重复；跳跃经过的空中格不触发，除非设计明确要求。明确 Empty、Ground、jump hint、互动格哪些类型应该有当前位置提示。

### 阶段 1：在 Wwise 建好 Broadcast 距离控制

1. 在当前准备升级的 Wwise 2025.1.3 工程中，先确认旧工程备份和 Unity 集成版本能成功生成目标平台 SoundBank。不要因为 Wwise Bus 名称 `Broadcast` 已存在，就认为它是距离 RTPC。
2. 新建专用 Game Parameter，例如 `Broadcast_Cell_Distance`（名字可按项目规范调整），设置明确的最小、最大距离范围，单位与 Unity 网格一致；范围必须覆盖最大地图尺寸。
3. 在一个测试 Broadcast 音色上建立该 Game Parameter 到音量的曲线：距离为 0 时按玩家试玩基准音量播放，距离增大时平滑减小，最大距离处仍保留可辨识的最低音量或按设计静音。不要让类型音色和音高因该参数变化。
4. 在 Wwise Profiler 或实时参数面板中手动调整 Game Parameter，确认音量方向正确、近处不过响、远处不突然消失。
5. 将 Parameter 放入所有由 Broadcast 使用的容器/音色路径，使 Row Event、Switch Container 和 fallback Event 都受同一距离衰减。不能只接到某一行或某一种 CellType。
6. Build 并确认生成的 SoundBank/Metadata 包含新参数和引用该参数的 Broadcast 声音。Unity 当前已有 `Jump_HintTone_Distance` 不应被误接到 Broadcast；它属于跳跃提示语境，职责不同。

### 阶段 2：把距离值从 Unity 写入每个 Broadcast 声音

1. 在 `GridBroadcastController` 增加独立配置引用：Broadcast distance RTPC、是否启用距离衰减（便于 A/B 和调试）、距离计算参考对象/位置的明确选择。不要复用 PositionTone 的 `volumeRtpc`。
2. 在创建 Broadcast step 时，使用玩家当前 Grid 坐标和每个 `GridCell.position` 计算距离。每个列步骤开始播放时采样玩家距离即可；如果你希望玩家移动期间已发出的音也实时改变音量，另行设计更新策略，不要默认更新所有活动声音。
3. 将计算结果限制在 Wwise Parameter 的配置范围内，并转换成 RTPC 使用的数值。规则必须一致：近=小/远=大，Wwise 曲线则将其映射为更小/更大的响度。
4. 对当前这一次要播放的 `playbackGameObject` 设置该 RTPC，再设置 CellType Switch、Pitch Switch/Row Event 参数，最后 Post Event。对每一个同时发声的格子对象都要写值。
5. 不要调用全局 RTPC 设置：多个同列格子会同时发声，且全局值会让其它音源或其它系统意外受距离曲线影响。使用与这次 Event 对应的 GameObject-scoped RTPC。
6. 调查“复用 voice object”问题：如果 Wwise 在声音播放过程中继续读取 GameObject 当前 RTPC/Switch，而 Unity 已在下一列修改它，就可能让上一个音符的音量/音色跟着变化。用 Profiler 验证；若有串扰，则为同时存活的声音分配独立 emitter，或改为播放前锁存参数且确保事件实例不会共享状态。
7. 为零距离、单格地图、手动 bounds 超出数据库范围、玩家在 bounds 外等情况加安全规则：距离计算不能除以零；缺失格子按 Empty/跳过的现有规则保持一致；bounds 外扫描起点仍 clamp。

### 阶段 3：校准 Grid 音频坐标和 Broadcast 覆盖

1. 与设计者确认 Ground 播报究竟表示“实体 Ground tile”还是“可站立表面”。若是站立面，就在一个集中函数中把 Ground 的 audio Y 转成 y+1；若不是，更新旧文档决策。不要在 Broadcast pitch 计算处到处单独加 1。
2. 让 Broadcast 的 pitch step、Row Event 查询、距离计算和 PositionTone 的 Y 映射使用明确且一致的坐标定义。物理 tile Y 与音频表示 Y 可以不同，但转换点应集中且写清楚。
3. 审核 `GetCellTypeSwitchName` 中未覆盖的 CellType。逐一决定每种类型：使用专属 Switch、映射到已存在的通用类型、作为 Empty/静音跳过，或采用独立警告音。方向性跳跃提示是否纳入全图 Broadcast 也必须作出决定。
4. 确认 bounds 是当前场景实际 tilemap 范围，或改用数据库 bounds；场景配置手动边界可能包含负坐标。逐关核查，不要假定 Level 1 配置适用于 15 关。
5. 核对 pitch switch 列表覆盖最大可播报 Y。当前有 10 个命名 pitch step；超出后 Wwise Switch name 为空会警告/停留在旧值，需明确超范围策略（扩展音域、折返八度、封顶或映射最高音）。

### 阶段 4：重做 PositionTone 的格子进入事件

1. 保留 Broadcast 调用链，与 PositionTone 分开命名和配置。不要让 PositionTone 调用地图扫描函数，也不要对它设置 `Broadcast_Cell_Distance`。
2. 在 Unity 定义单一的“当前可听格”采样点。推荐玩家脚底/地面接触位置映射成 Grid 坐标，避免角色中心跨入上一行导致音高提前变化；同一规则要与 Ground Y 约定兼容。
3. 设计一个 Enter-cell 流程：刷新坐标 -> 判断是否已经落地/进入新位置 -> 取得该格 `GridCell`（必要时考虑空格）-> 解析 CellType 对应 PositionTone Event/Switch -> 计算音高 step -> 设置该播放对象上的音高参数 -> 播放一次 Event -> 更新“上次已提示格”。
4. 以玩家实际移动为基准，确保水平、垂直移动和落地换层都可被识别；不要只比较 X。角色在同一格内抖动、物理碰撞边缘波动不能重复触发。
5. 跳跃期间是否播放落经格子的提示由阶段 0 决策控制。推荐 PositionTone 只对落地后站上的格子发一次；Airborne 音调继续表达空中高度，不要由一次性格子事件替代它。
6. 建立 CellType -> PositionTone 声音映射。它与 Broadcast 的声音可以共用素材/音色设计，但事件实例、RTPC 控制、触发时机分离。对于没有专属类型音的可站立格，设置明确 fallback。
7. 将目前 `PlayerFootstepAudio` 中“X 改变就发 `positionTonePulseEvent`”替换为/接入上述 Enter-cell 语义。旧脉冲是否保留为另一层短提示，需要通过试玩决定；避免同一步既响 generic XPulse 又响格子类型音。
8. `PlayerPositionToneController` 现在每帧驱动持续音高和水平声像，并启动长音 Event。决定它的持续 tone 是 Airborne 专用还是始终存在：推荐让 Airborne show/hide 只控制 Airborne emitter，PositionTone 一次性格子 Event 使用独立 emitter/对象状态，避免相同 GameObject 上的 RTPC 互相覆盖。

### 阶段 5：整理脚步声的可信定位功能

1. 先在 Wwise 中检查现有 `footstepLoopPlayEvent` 对应容器是否随机选样本、随机音高或随机触发间隔；随机音色变化与随机空间节拍是两件事。玩家反馈表明脚步声被当作定位信息，所以每个实际节拍必须准时、可预测。
2. 将脚步定位节拍与 PositionTone 事件分开：脚步表示玩家移动了多少，PositionTone 表示当前格在哪里/是什么。脚步可以有音色变体，但不能遗漏或额外插入会误导的节拍。
3. 若采用每 1/4 格一次，脚本应累计实际水平位移（Unity 世界单位换算为格），每累计 0.25 格触发一次；不要使用固定时间 Timer，否则速度变化时每个节拍代表的距离会变。
4. 只累计有效移动：地面状态、输入方向与 Rigidbody 实际位移方向相符，水平速度超过阈值；撞墙、松开方向键、控制锁定、暂停、死亡重置、场景切换时清理累积状态。是否在空中继续发脚步定位，需要设计决定；不要把 Airborne 连续音当作步数。
5. 反向移动时清晰重置/反向处理未完成的四分之一格余量，防止来回抖动跨边界导致多发/漏发；快速移动跨越多个阈值时要按实际跨过的数量补发或作明确合并策略。
6. 先和盲人试玩者比较无随机 loop、四分之一格、半格、每格一次的版本。每一版保持脚步音量、音色、场景和移动速度相同，只改节奏密度，记录玩家对移动方向、移动距离、停下时机和格子边界的判断。

### 阶段 6：Airborne 音调保留并单独调优

1. 先不做大规模重构；在 Broadcast 距离和 PositionTone 逻辑独立后，确认 Airborne 只在起跳/离地后显示，在落地后隐藏。
2. 通过 Wwise 单变量 A/B 调整音色、动态范围、attack/release、音量与音高连续性，每次只改一个维度。
3. 核对 Airborne 音不遮盖格子 PositionTone、脚步/落地、方向性跳跃提示和语音。需要 ducking 时指定具体 bus/状态，不用 Broadcast distance 参数。
4. 让盲人试玩者比较原版与调优版本，优先询问高度变化是否更容易听辨、音调是否还突兀、是否疲劳或遮住其它线索。

### 阶段 7：推广所有关卡与目标平台检查

1. 代表性关卡完成后，把 Broadcast RTPC 和 PositionTone 配置推广到 15 关；先确认 Unity prefab/场景对象是否真正共享配置，避免只改单场景。
2. 逐场景核对 GridDatabase、PlayerPositionTracker、BroadcastEmitter、Listener、PositionTone emitters、Wwise Events 和 Switch 名称是否有效。
3. 检查每关手动 bounds、pitch range 和 `inferredPitchOriginRowOffset`；旧聊天中的个别关卡值只是历史记录，需以当前 Inspector 为准。
4. 对目标 Wwise 2025.1.3 工程生成 SoundBank，在 Unity 编辑器与 Windows 目标构建分别核对 Broadcast、RTPC、Switch Container、PositionTone、Airborne 和脚步事件。之前 WebGL 导出问题意味着 WebGL 支持必须作为单独的平台验收条件；不能由 Windows 播放正常推断 WebGL 正常。
5. 关卡层面验收：近处格比远处格响；同一格改变距离后只改变音量、不改变音高/类型；PositionTone 总是当前位置，不随 Broadcast distance 改音量；S/W/D/A/Shift/Q 行为保持预期；快速重复请求、Q 中止和播报结束都不残留声音。
6. 盲人试玩验收：玩家能否把 Broadcast 当成主动地图概览、PositionTone 当成当前位置、脚步当成细粒度移动距离；是否能分辨高度/类型/左右方向；远处重要目标是否仍听得见。试玩者反馈优先于开发者对声音语义的假设。

## 5. 主要风险和决策清单

实施前要确认这些设计值，文档当前不擅自替用户定稿：

1. 距离是二维欧氏距离、曼哈顿距离，还是只看水平距离？是否受墙体阻挡？
2. Ground 格与玩家脚底的音频 Y 坐标如何对齐？旧文档 y+1 与当前 helper 的 identity 返回值冲突。
3. PositionTone 对哪些 CellType 发声？落地时触发还是跨格时立即触发？空格是否也有音？
4. PositionTone 音高由 CellType 的真实格子 Y 还是“站立面 Y”决定？
5. 脚步每 1/4 格是否经试玩确认？四分之一格是目标方案，仍需和较稀疏版本对比。
6. 播报列节拍快慢、同列和弦长度、远距离最低响度分别以什么试玩结果作为合格标准？
7. Wwise 2025.1.3 的迁移是否与 WebGL 目标平台一起验证；是否需要先稳定 Windows，再独立验证 WebGL？

## 6. 当前代码索引

- Grid 与 CellType：`Assets/Scripts/Grid/CellType.cs`、`GridCellFactory.cs`、`GridDatabase.cs`、`GridAudioCoordinateUtility.cs`、`TilemapImporter.cs`
- Broadcast：`Assets/Scripts/Grid/GridBroadcastController.cs`、`GridAudioParameterUtility.cs`
- 玩家位置：`Assets/Scripts/Player/PlayerPositionTracker.cs`
- PositionTone、Airborne、脚步：`Assets/Scripts/Player/PlayerPositionToneController.cs`、`PlayerFootstepAudio.cs`、`PlayerJump.cs`
- 关卡场景：`Assets/Scenes/Level 1.unity` 至 `Assets/Scenes/Level 15.unity`
- Wwise 已生成对象清单：`Assets/WwiseData/Bank/Windows/SoundbanksInfo.json` 与 Mac 对应文件。它反映上次生成的 bank 元数据，不保证与尚未构建的 Wwise 2025.1.3 工程实时一致。

