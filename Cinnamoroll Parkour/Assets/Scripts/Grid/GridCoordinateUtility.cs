using UnityEngine;
using UnityEngine.Tilemaps;

public static class GridCoordinateUtility
{
    public static Vector2Int WorldToGrid(Tilemap tilemap, Vector3 worldPosition)
    {
        Vector3Int cellPosition = tilemap.WorldToCell(worldPosition);
        return new Vector2Int(cellPosition.x, cellPosition.y);
    }

    public static Vector3 GridToWorldCenter(Tilemap tilemap, Vector2Int gridPosition)
    {
        Vector3Int cellPosition = new Vector3Int(gridPosition.x, gridPosition.y, 0);
        return tilemap.GetCellCenterWorld(cellPosition);
    }
}