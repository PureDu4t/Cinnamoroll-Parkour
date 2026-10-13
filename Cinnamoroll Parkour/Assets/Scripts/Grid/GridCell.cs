using UnityEngine;

[System.Serializable]
public class GridCell
{
    public Vector2Int position;
    public CellType type;
    public bool walkable;
    public bool interactable;
    public string audioID;

    public GridCell(Vector2Int position, CellType type, bool walkable, bool interactable, string audioID)
    {
        this.position = position;
        this.type = type;
        this.walkable = walkable;
        this.interactable = interactable;
        this.audioID = audioID;
    }
}