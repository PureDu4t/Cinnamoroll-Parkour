# Cinnamoroll Parkour Project Index

这是本项目在 Codex Projects 页面继续工作的入口。目标是让对话、决策和下一步任务更有条理，也更容易搜索。

## 快速入口

- 当前状态和验收标准：`PROJECT_STATUS.md`
- 已定稿设计决策：`PROJECT_DECISIONS.md`
- 下一步任务清单：`PROJECT_NEXT_STEPS.md`
- 历史交接全文：`PROJECT_HANDOFF.md`

## 项目关键词

搜索这些词可以快速定位上下文：

- `Grid World System`
- `Audio Query System`
- `Ground y+1`
- `TilemapImporter`
- `GridDatabase`
- `DebugGridVisualizer`
- `16 x 10`
- `Pixels Per Unit = 16`
- `Wwise`
- `盲人和视障玩家`

## 当前阶段

正在做开发计划第一步：`Grid World System`，也就是地图逻辑层。

这一阶段先保证 Unity 中的 Tilemap 能导入到真实数据层 `GridDatabase`，并且可以被之后的音频查询系统稳定读取。

## 和 Codex 继续对话时可以这样说

- “继续第一步，帮我检查 Grid World System。”
- “按照 `PROJECT_NEXT_STEPS.md` 带我在 Unity 里设置。”
- “帮我实现第二步 Audio Query System。”
- “查一下 Ground y+1 规则。”
- “把今天的进展更新到项目文档。”

## 维护规则

每次完成一个阶段后，优先更新：

1. `PROJECT_STATUS.md`
2. `PROJECT_DECISIONS.md`
3. `PROJECT_NEXT_STEPS.md`

`PROJECT_HANDOFF.md` 保留为历史完整上下文，不再作为唯一入口。
