using UnityEngine;

public class GridCellReference : MonoBehaviour
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private CellType cellType;

    public Vector2Int GridPosition
    {
        get { return gridPosition; }
    }

    public CellType CellType
    {
        get { return cellType; }
    }

    public void Initialize(GridCell cell)
    {
        if (cell == null)
        {
            return;
        }

        gridPosition = cell.position;
        cellType = cell.type;
    }
}
