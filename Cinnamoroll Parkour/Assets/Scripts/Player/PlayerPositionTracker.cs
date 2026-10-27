using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerPositionTracker : MonoBehaviour
{
    [SerializeField] private Tilemap referenceTilemap;

    public Vector2Int CurrentGridPosition { get; private set; }
    /// <summary>Four fixed horizontal regions per Tilemap column, aligned to its cell boundaries.</summary>
    public int GetFootstepRegion(Vector3 worldPosition)
    {
        if (referenceTilemap == null) return Mathf.FloorToInt(worldPosition.x * 4f);
        Vector3Int cell = referenceTilemap.WorldToCell(worldPosition);
        Vector3 origin = referenceTilemap.CellToWorld(cell);
        Vector3 cellRight = referenceTilemap.CellToWorld(cell + Vector3Int.right) - origin;
        float fraction = Vector3.Dot(worldPosition - origin, cellRight)
            / Mathf.Max(cellRight.sqrMagnitude, 0.000001f);
        return cell.x * 4 + Mathf.Clamp(Mathf.FloorToInt(fraction * 4f), 0, 3);
    }

    private void Awake()
    {
        RefreshPosition();
    }

    private void Update()
    {
        RefreshPosition();
    }

    public void RefreshPosition()
    {
        if (referenceTilemap == null)
        {
            return;
        }

        CurrentGridPosition = GridCoordinateUtility.WorldToGrid(referenceTilemap, transform.position);
    }

    public Vector2Int GetNearestGridPosition(Vector3 worldPosition)
    {
        if (referenceTilemap == null)
        {
            return Vector2Int.RoundToInt(worldPosition);
        }

        Vector3Int cellPosition = referenceTilemap.WorldToCell(worldPosition);
        Vector2Int nearestGridPosition =
            new Vector2Int(cellPosition.x, cellPosition.y);
        float nearestDistance = float.MaxValue;

        for (int xOffset = -1; xOffset <= 1; xOffset++)
        {
            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                Vector3Int candidateCell = new Vector3Int(
                    cellPosition.x + xOffset,
                    cellPosition.y + yOffset,
                    cellPosition.z
                );
                Vector3 candidateCenter =
                    referenceTilemap.GetCellCenterWorld(candidateCell);
                float distance =
                    (candidateCenter - worldPosition).sqrMagnitude;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestGridPosition = new Vector2Int(
                        candidateCell.x,
                        candidateCell.y
                    );
                }
            }
        }

        return nearestGridPosition;
    }
}
