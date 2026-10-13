using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DebugGridVisualizer : MonoBehaviour
{
    [SerializeField] private GridDatabase gridDatabase;
    [SerializeField] private Tilemap referenceTilemap;
    [SerializeField] private bool drawGridCells = true;

    private void OnDrawGizmos()
    {
        if (!drawGridCells || gridDatabase == null || referenceTilemap == null)
        {
            return;
        }

        List<GridCell> cells = gridDatabase.GetAllCells();

        for (int i = 0; i < cells.Count; i++)
        {
            DrawCell(cells[i]);
        }
    }

    private void DrawCell(GridCell cell)
    {
        Vector3 worldPosition = GridCoordinateUtility.GridToWorldCenter(referenceTilemap, cell.position);

        Gizmos.color = GetColor(cell.type);
        Gizmos.DrawWireCube(worldPosition, Vector3.one * 0.9f);
    }

    private Color GetColor(CellType type)
    {
        switch (type)
        {
            case CellType.Ground:
                return new Color(0.55f, 0.28f, 0.12f);
            case CellType.Wall:
                return Color.gray;
            case CellType.Door:
                return Color.blue;
            case CellType.CrackedWall:
                return new Color(0.65f, 0.25f, 0.15f);
            case CellType.Lever:
                return Color.gray;
            case CellType.Cannon:
                return Color.magenta;
            case CellType.Battery:
                return new Color(0.55f, 0f, 0.8f);
            case CellType.Lore:
                return Color.green;
            case CellType.Goal:
                return Color.yellow;
            case CellType.SmallJumpHintRight:
            case CellType.SmallJumpHintLeft:
            case CellType.SmallJumpHintBoth:
                return Color.white;
            case CellType.BigJumpHintRight:
            case CellType.BigJumpHintLeft:
            case CellType.BigJumpHintBoth:
                return new Color(1f, 0.75f, 0f);
            case CellType.Enemy:
                return Color.black;
            case CellType.Background:
                return new Color(0.25f, 0.25f, 0.25f, 0.35f);
            case CellType.AirWall:
                return new Color(0.25f, 0.8f, 1f, 0.45f);
            case CellType.Death:
                return Color.red;
            default:
                return Color.white;
        }
    }
}
