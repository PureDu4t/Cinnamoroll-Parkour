# Project Decisions

这里记录已经定稿的设计和技术规则，方便之后搜索和避免反复讨论。

## 玩家体验原则

```text
玩家不是“看见地图”，而是“聆听地图”。
```

Unity 负责世界逻辑、Grid、Query、交互；Wwise 只负责播放声音，不理解游戏地图。

## 架构原则

- `Tilemap` 是编辑器工具，不是真实游戏数据。
- 真实地图数据由 `GridDatabase` 保存。
- 核心链路是 `Grid -> Query -> Audio`。
- `AudioManager` 不扫描地图，只播放声音。
- `QuerySystem` 负责生成查询结果。
- `Interactable` 不读取输入，由 `PlayerInteraction` 读取输入后调用。
- Manager 不处理具体玩法逻辑。

## 坐标和尺寸

测试地图：

```text
16 x 10 tiles
```

每个 tile：

```text
16 x 16 pixels
```

Unity 设置：

```text
Sprite Pixels Per Unit = 16
Grid Cell Size = 1, 1, 0
```

坐标范围：

```text
X: 0 到 15
Y: 0 到 9
```

## Ground 规则

Ground 是真实地面实体，玩家站在 Ground 上方，Ground 本身不可进入。

真实 Grid：

```text
Ground at y = 0
```

音频播报：

```text
Walkable surface at y = 1
```

实现规则：

- `Ground.walkable = false`
- 播报 Ground 时使用 `GridAudioCoordinateUtility.GetAudioY(cell)`
- Ground 的音频 Y 值是 `cell.position.y + 1`

## Walkable 和 Interactable 规则

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

## 推荐 Unity Hierarchy

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

MVP 阶段也可以先把 Grid 相关脚本挂在同一个 `GridSystem` 空物体上，简单优先。
