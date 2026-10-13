# Project Status

## 一句话定位

《玉桂狗跑酷》是一个面向盲人和视障玩家的 Unity 2D 音频平台跳跃解谜游戏。玩家不是看见地图，而是聆听地图。

## 核心映射

```text
X 轴 = 时间推进
Y 轴 = 音高
Tile 类型 = 音色
地图结构 = 和弦进行
```

## 当前开发阶段

正在进行：`Grid World System`。

目标是建立地图逻辑层：

- 用 `GridDatabase` 保存真实地图数据。
- 从 Unity `Tilemap` 导入格子。
- 支持单格、列、层查询。
- 支持动态更新接口。
- 用 Gizmos 验证地图数据。
- 为之后 `Audio Query System` 提供稳定数据源。

## 当前脚本状态

已存在脚本：

- `Assets/Scripts/Grid/CellType.cs`
- `Assets/Scripts/Grid/GridCell.cs`
- `Assets/Scripts/Grid/GridCellFactory.cs`
- `Assets/Scripts/Grid/GridDatabase.cs`
- `Assets/Scripts/Grid/GridAudioCoordinateUtility.cs`
- `Assets/Scripts/Grid/TileTypeMapping.cs`
- `Assets/Scripts/Grid/TilemapImporter.cs`
- `Assets/Scripts/Grid/GridWorldManager.cs`
- `Assets/Scripts/Grid/GridCoordinateUtility.cs`
- `Assets/Scripts/Grid/DebugGridVisualizer.cs`
- `Assets/Scripts/Player/PlayerPositionTracker.cs`

已清理：

- `CellType.cs` 中残留的默认 `NewBehaviourScript` 已移除，避免全局命名空间重复类名报错。

## 当前验收标准

第一阶段完成后应能确认：

- Tilemap 能导入 `GridDatabase`。
- `GridDatabase` 保存所有非 Empty 格子。
- `Ground.walkable = false`。
- `Empty.walkable = true`。
- `GetColumn` 可以取一列。
- `GetLayer` 可以取一层。
- `MoveCell` 可以移动动态格子。
- `PlayerPositionTracker` 能得到玩家 Grid 坐标。
- `DebugGridVisualizer` 能显示真实 GridCell。
- Ground 上方一格显示 cyan 小球，代表未来音频播报 `y + 1`。

## 当前限制

- 这个目录当前不是 Git 仓库。
- 尚未在 Unity Editor 里实际运行验证。
- 下一步重点是把脚本挂到场景对象并配置 Tilemap 映射。
