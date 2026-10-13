using UnityEngine;

public static class GridCellFactory
{
    public static GridCell Create(Vector2Int position, CellType type)
    {
        return new GridCell(
            position,
            type,
            IsWalkable(type),
            IsInteractable(type),
            type.ToString()
        );
    }

    private static bool IsWalkable(CellType type)
    {
        switch (type)
        {
            case CellType.Empty:
            case CellType.Ground:
            case CellType.Lever:
            case CellType.Cannon:
            case CellType.Battery:
            case CellType.Goal:
            case CellType.Lore:
            case CellType.SmallJumpHintRight:
            case CellType.SmallJumpHintLeft:
            case CellType.SmallJumpHintBoth:
            case CellType.BigJumpHintRight:
            case CellType.BigJumpHintLeft:
            case CellType.BigJumpHintBoth:
                return true;

            case CellType.Wall:
            case CellType.Door:
            case CellType.CrackedWall:
            case CellType.Enemy:
            case CellType.Background:
            case CellType.AirWall:
            case CellType.Death:
                return false;

            default:
                return false;
        }
    }

    private static bool IsInteractable(CellType type)
    {
        switch (type)
        {
            case CellType.Lever:
            case CellType.Cannon:
            case CellType.Battery:
            case CellType.Goal:
            case CellType.Lore:
                return true;

            default:
                return false;
        }
    }
}
