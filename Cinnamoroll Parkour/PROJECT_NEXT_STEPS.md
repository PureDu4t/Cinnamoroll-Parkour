# Project Next Steps

## 当前目标

继续完成开发计划第一步：`Grid World System`。

目标不是先做声音，而是先让地图逻辑层稳定跑通。

## Unity Editor 设置清单

1. 检查 tile 图片 `Pixels Per Unit = 16`。
2. 如果是 tileset，设置：
   - `Sprite Mode = Multiple`
   - `Filter Mode = Point (no filter)`
   - `Compression = None`
   - `Sprite Editor -> Slice -> Grid By Cell Size -> 16 x 16`
3. 创建 Tile Palette。
4. 创建 Rectangular Tilemap。
5. 确认 Hierarchy 中 `Grid` 物体的 `Cell Size = 1, 1, 0`。
6. 画一个 `16 x 10` 测试地图。
7. 创建 `GridSystem` 空物体。
8. 挂载或引用：
   - `GridDatabase`
   - `TilemapImporter`
   - `GridWorldManager`
   - `DebugGridVisualizer`
9. 给 `TilemapImporter` 配置 Tile 到 `CellType` 的映射。
10. 开启 Gizmos，进入 Play Mode，检查 Debug Grid 是否显示。

## Inspector 连接关系

`TilemapImporter`：

- `Source Tilemap` 指向测试关卡的 Tilemap。
- `Grid Database` 指向场景里的 `GridDatabase`。
- `Tile Mappings` 填入每种 tile 对应的 `CellType`。

`GridWorldManager`：

- `Tilemap Importer` 指向场景里的 `TilemapImporter`。

`DebugGridVisualizer`：

- `Grid Database` 指向场景里的 `GridDatabase`。
- `Reference Tilemap` 指向同一个测试关卡 Tilemap。
- `Draw Grid Cells` 开启。
- `Draw Audio Y For Ground` 开启。

`PlayerPositionTracker`：

- `Reference Tilemap` 指向测试关卡 Tilemap。

## 下一次和 Codex 继续时

推荐直接说：

```text
按照 PROJECT_NEXT_STEPS.md 带我完成 Unity Editor 设置。
```

或者：

```text
我已经完成 Tilemap 设置，帮我检查第一阶段验收。
```

## 第一阶段完成后的第二步

第二步建议进入：`Audio Query System`。

它应该读取 `GridDatabase`，把玩家附近、前方列、当前层、Ground 的 `y+1` 音频坐标等信息转换为可播放的查询结果。Wwise 仍然只负责播放。
