using System.Collections.Generic;
using UnityEngine;

public class GridDatabase : MonoBehaviour
{
    public static GridDatabase Instance { get; private set; }

    private readonly Dictionary<Vector2Int, GridCell> cells = new Dictionary<Vector2Int, GridCell>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GridDatabase instances found. Keeping the first one.");
            return;
        }

        Instance = this;
    }

    public void Clear()
    {
        cells.Clear();
    }

    public bool HasCell(Vector2Int position)
    {
        return cells.ContainsKey(position);
    }

    public GridCell GetCell(Vector2Int position)
    {
        GridCell cell;
        if (cells.TryGetValue(position, out cell))
        {
            return cell;
        }

        return GridCellFactory.Create(position, CellType.Empty);
    }

    public CellType GetCellType(Vector2Int position)
    {
        return GetCell(position).type;
    }

    public bool IsWalkable(Vector2Int position)
    {
        return GetCell(position).walkable;
    }

    public bool IsInteractable(Vector2Int position)
    {
        return GetCell(position).interactable;
    }

    public void SetCell(Vector2Int position, GridCell cell)
    {
        if (cell.type == CellType.Empty)
        {
            RemoveCell(position);
            return;
        }

        cell.position = position;
        cells[position] = cell;
    }

    public void SetCellType(Vector2Int position, CellType type)
    {
        if (type == CellType.Empty)
        {
            RemoveCell(position);
            return;
        }

        SetCell(position, GridCellFactory.Create(position, type));
    }

    public void RemoveCell(Vector2Int position)
    {
        if (cells.ContainsKey(position))
        {
            cells.Remove(position);
        }
    }

    public void MoveCell(Vector2Int from, Vector2Int to)
    {
        if (!cells.ContainsKey(from))
        {
            Debug.LogWarning("Cannot move cell because source position is empty: " + from);
            return;
        }

        GridCell cell = cells[from];
        cells.Remove(from);

        cell.position = to;
        cells[to] = cell;
    }

    public List<GridCell> GetColumn(int x)
    {
        List<GridCell> result = new List<GridCell>();

        foreach (KeyValuePair<Vector2Int, GridCell> entry in cells)
        {
            if (entry.Key.x == x)
            {
                result.Add(entry.Value);
            }
        }

        result.Sort((a, b) => a.position.y.CompareTo(b.position.y));
        return result;
    }

    public List<GridCell> GetLayer(int y)
    {
        List<GridCell> result = new List<GridCell>();

        foreach (KeyValuePair<Vector2Int, GridCell> entry in cells)
        {
            if (entry.Key.y == y)
            {
                result.Add(entry.Value);
            }
        }

        result.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        return result;
    }

    public List<GridCell> GetAllCells()
    {
        return new List<GridCell>(cells.Values);
    }

    public int Count
    {
        get { return cells.Count; }
    }
}