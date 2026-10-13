# 《玉桂狗跑酷》Project Handoff

这份文档用于把当前对话中的关键上下文移植到正式 Unity project 里继续工作。

## 继续工作入口

为了在 Projects 页面更容易检索和延续，本项目现在增加了几个更结构化的入口文档：

- `PROJECT_INDEX.md`：项目索引和搜索关键词
- `PROJECT_STATUS.md`：当前阶段、脚本状态、验收标准
- `PROJECT_DECISIONS.md`：已定稿设计和技术规则
- `PROJECT_NEXT_STEPS.md`：下一步 Unity Editor 设置清单

这份 `PROJECT_HANDOFF.md` 保留为历史完整交接记录。

## 回复偏好

- 之后默认用中文回复。
- 用户主要承担设计师/策划工作，不负责补全缺失代码。
- 当用户说“开发计划第一步 / 第 1 步 / 1步”等，应理解为：实现 Grid World System（地图逻辑层）。
- 给代码时应默认提供完整、可复制、可编译脚本，不只给核心片段。

## 项目定位

项目名暂定：《玉桂狗跑酷》/ Cinnamoroll Audio Platformer。

类型：

- Unity 2D 平台跳跃
- 解谜
- Audio Game
- 面向盲人和视障玩家
- 使用 Wwise 作为音频引擎

核心设计：

```text
玩家不是“看见地图”，而是“聆听地图”。
```

空间映射：

```text
X 轴 = 时间推进
Y 轴 = 音高
Tile 类型 = 音色
地图结构 = 和弦进行
```

核心链路：

```text
Grid
↓
Query
↓
Audio
```

Unity 负责世界逻辑、Grid、Query、交互；Wwise 只负责播放，不理解游戏逻辑。

## 当前开发阶段

当前正在做开发计划第一步：

```text
Grid World System（地图逻辑层）
```

这一阶段目标：

- 建立真实地图数据层 `GridDatabase`
- 从 Tilemap 导入 Grid 数据
- 支持单格、列、层查询
- 支持动态更新接口
- 为之后 Audio Query System 做准备
- 用 Debug 可视化确认 Grid 数据正确

## 已定稿的重要规则

### Tile 与 Grid 尺寸

测试地图：

```text
16 x 10 tiles
```

每个 tile：

```text
16 x 16 pixels
```

Unity 设置建议：

```text
Sprite Pixels Per Unit = 16
Grid Cell Size = 1, 1, 0
```

因此：

```text
1 tile = 16 pixels
1 tile = 1 Unity Unit
1 tile = 1 Grid Cell
```

测试关卡坐标范围：

```text
X: 0 到 15
Y: 0 到 9
```

### Ground 规则

已经讨论并定稿：

```text
Ground 是真实地面实体
玩家站在 Ground 上方
Ground 本身不可进入
音频播报时，Ground 的 Y 轴播报为 y + 1
```

也就是：

```text
真实 Grid:
Ground at y = 0

音频播报:
Walkable surface at y = 1
```

这样技术上符合传统 Unity 2D 平台游戏物理逻辑，同时玩家听到的是“自己能站在哪里”。

### Walkable 规则

当前 `GridCellFactory` 规则：

```text
Empty        walkable true
Ground       walkable false
Wall         walkable false
Door         walkable false
CrackedWall  walkable false
Lever        walkable true, interactable true
Cannon       walkable true, interactable true
Battery      walkable true, interactable true
Goal         walkable true, interactable true
Enemy        walkable false
```

注意：

- `Ground.walkable = false`，因为 Ground 是实体地面。
- 之后 QuerySystem 播报 Ground 时使用 `GridAudioCoordinateUtility.GetAudioY(cell)`，将其播报为 `cell.position.y + 1`。

## 第一阶段脚本列表

当前第一阶段需要这些脚本：

```text
Assets/Scripts/Grid/CellType.cs
Assets/Scripts/Grid/GridCell.cs
Assets/Scripts/Grid/GridCellFactory.cs
Assets/Scripts/Grid/GridDatabase.cs
Assets/Scripts/Grid/GridAudioCoordinateUtility.cs
Assets/Scripts/Grid/TileTypeMapping.cs
Assets/Scripts/Grid/TilemapImporter.cs
Assets/Scripts/Grid/GridWorldManager.cs
Assets/Scripts/Grid/GridCoordinateUtility.cs
Assets/Scripts/Player/PlayerPositionTracker.cs
Assets/Scripts/Debug/DebugGridVisualizer.cs
```

用户截图中当前脚本已存在：

```text
CellType
DebugGridVisualizer
GridAudioCoordinateUtility
GridCell
GridCellFactory
GridCoordinateUtility
GridDatabase
GridWorldManager
PlayerPositionTracker
TilemapImporter
TileTypeMapping
```

说明：脚本数量和名字已经完整。

`PlayerPositionTracker` 当前如果放在 `Assets/Scripts/Grid/` 里也能编译，只是不够整洁；之后可以移动到 `Assets/Scripts/Player/`。

## 已遇到并解决的问题

曾经出现报错：

```text
The namespace '<global namespace>' already contains a definition for 'NewBehaviourScript'
```

原因：

- Unity 自动生成的 `NewBehaviourScript.cs` 没有重命名，或
- 某个脚本底部残留了默认模板：

```csharp
public class NewBehaviourScript : MonoBehaviour
{
    void Start() {}
    void Update() {}
}
```

处理方式：

- 删除未使用的 `NewBehaviourScript.cs`
- 确保 `GridCell.cs` 里只有 `GridCell` 类

该报错已经修好。

## Unity 设置位置

### Sprite Pixels Per Unit = 16

位置：

```text
Project 面板点击 tile 图片素材
↓
Inspector
↓
Pixels Per Unit = 16
↓
Apply
```

如果是 tileset：

```text
Sprite Mode = Multiple
Pixels Per Unit = 16
Filter Mode = Point (no filter)
Compression = None
Sprite Editor → Slice → Grid By Cell Size → 16 x 16
```

### Grid Cell Size = 1, 1, 0

位置：

```text
Hierarchy 里的 Grid 物体
↓
Inspector
↓
Grid Component
↓
Cell Size = X 1, Y 1, Z 0
```

### 地图尺寸 16 x 10

不是在某个尺寸框里设置，而是在 Tilemap 上画出：

```text
16 列 x 10 行
```

建议范围：

```text
X: 0 到 15
Y: 0 到 9
```

## 推荐 Unity Hierarchy

MVP 阶段可以这样：

```text
Scene
├── GridSystem
│   ├── LevelTilemap
│   ├── GridDatabase
│   ├── TilemapImporter
│   ├── GridWorldManager
│   └── DebugGridVisualizer
└── Player
    └── PlayerPositionTracker
```

也可以先把 Grid 相关脚本挂在同一个 `GridSystem` 空物体上，简单优先。

## 下一步要做的事

用户即将进入 Unity project 继续设置。

接下来应该引导用户完成：

1. 检查 tile 图片 `Pixels Per Unit = 16`
2. 如果是 tileset，按 16 x 16 切片
3. 创建 Tile Palette
4. 创建 Rectangular Tilemap
5. 确认 `Grid Cell Size = 1, 1, 0`
6. 画 16 x 10 测试地图
7. 创建 `GridSystem` 空物体
8. 挂载 `GridDatabase / TilemapImporter / GridWorldManager / DebugGridVisualizer`
9. 给 `TilemapImporter` 配置 Tile → CellType 映射
10. 开启 Gizmos，验证 Debug Grid 是否显示

## 第一阶段验收标准

完成后应能确认：

```text
Tilemap 能导入 GridDatabase
GridDatabase 保存所有非 Empty 格子
Ground 的 walkable 是 false
Empty 的 walkable 是 true
GetColumn 可以取一列
GetLayer 可以取一层
MoveCell 可以移动动态格子
PlayerPositionTracker 能得到玩家 Grid 坐标
DebugGridVisualizer 能看到真实 GridCell
Ground 上方一格显示 cyan 小球，代表未来音频播报 y+1
```

## 关键架构原则

- Manager 不处理玩法逻辑。
- `AudioManager` 不扫描地图，只播放。
- `QuerySystem` 生成 Query 结果。
- `Interactable` 不读取输入，由 `PlayerInteraction` 读取输入后调用。
- Wwise 不理解地图、不读取 Grid，只负责声音播放。
- Tilemap 只是编辑器工具，真实世界数据是 `GridDatabase`。
- 当前最重要目标是先让 Grid World System 跑通。
