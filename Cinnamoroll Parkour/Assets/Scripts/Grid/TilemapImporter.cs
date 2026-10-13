using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapImporter : MonoBehaviour
{
    [SerializeField] private Tilemap sourceTilemap;
    [SerializeField] private GridDatabase gridDatabase;
    [SerializeField] private List<TileTypeMapping> tileMappings = new List<TileTypeMapping>();
    public Tilemap SourceTilemap => sourceTilemap;

    public void Import()
    {
        if (sourceTilemap == null)
        {
            Debug.LogError("TilemapImporter missing sourceTilemap.");
            return;
        }

        if (gridDatabase == null)
        {
            Debug.LogError("TilemapImporter missing gridDatabase.");
            return;
        }

        gridDatabase.Clear();

        BoundsInt bounds = sourceTilemap.cellBounds;
        int importedCount = 0;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            TileBase tile = sourceTilemap.GetTile(cellPosition);

            if (tile == null)
            {
                continue;
            }

            CellType type = GetCellTypeForTile(tile);
            Vector2Int gridPosition = new Vector2Int(cellPosition.x, cellPosition.y);

            gridDatabase.SetCellType(gridPosition, type);
            importedCount++;
        }

        Debug.Log("Tilemap import complete. Imported cells: " + importedCount);
    }

    public CellType GetCellTypeForTile(TileBase tile, bool logMissing = true)
    {
        for (int i = 0; i < tileMappings.Count; i++)
        {
            if (tileMappings[i].tile == tile)
            {
                return tileMappings[i].type;
            }
        }

        if (tile.name.Contains("AirWall"))
        {
            return CellType.AirWall;
        }

        if (logMissing) Debug.LogWarning("No CellType mapping found for tile: " + tile.name + ". Defaulting to Wall.");
        return CellType.Wall;
    }
}
