using UnityEngine;

public interface IGridInteractionHandler
{
    bool CanHandle(CellType cellType);
    void Interact(Vector2Int gridPosition);
}
