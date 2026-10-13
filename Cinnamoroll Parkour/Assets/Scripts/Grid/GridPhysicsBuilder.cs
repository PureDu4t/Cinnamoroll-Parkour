using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridPhysicsBuilder : MonoBehaviour
{
    [SerializeField] private GridDatabase gridDatabase;
    [SerializeField] private Tilemap referenceTilemap;
    private Vector3 gridCellSize;

    [SerializeField] private Transform supportColliderParent;
    [SerializeField] private Transform obstacleColliderParent;
    [SerializeField] private Transform deathTriggerParent;

    [SerializeField] private float supportThickness = 0.08f;
    [SerializeField] private string supportLayerName = "Default";
    [SerializeField] private string obstacleLayerName = "Default";
    [SerializeField] private string deathTriggerLayerName = "Default";

    [Header("Death Audio")]
    [SerializeField] private AK.Wwise.Event deathStingerEvent;
    [SerializeField] private GameObject deathStingerEventTarget;
    [SerializeField] private float deathResetDelay;

    public void Rebuild()
    {
        if (gridDatabase == null)
        {
            Debug.LogError("GridPhysicsBuilder missing gridDatabase.");
            return;
        }

        if (referenceTilemap == null)
        {
            Debug.LogError("GridPhysicsBuilder missing referenceTilemap.");
            return;
        }

        // layoutGrid can be unavailable while a newly activated Tilemap resolves its parent.
        // Resolve the actual Grid component as well, and validate before creating colliders.
        GridLayout layout = referenceTilemap.layoutGrid;
        if (layout == null) layout = referenceTilemap.GetComponentInParent<Grid>();
        if (layout == null)
        {
            Debug.LogError("GridPhysicsBuilder: referenceTilemap 没有父级 Grid，无法生成碰撞。", this);
            return;
        }
        gridCellSize = layout.cellSize;

        EnsureParents();
        ClearChildren(supportColliderParent);
        ClearChildren(obstacleColliderParent);
        ClearChildren(deathTriggerParent);

        List<GridCell> cells = gridDatabase.GetAllCells();

        for (int i = 0; i < cells.Count; i++)
        {
            GridCell cell = cells[i];

            if (ShouldCreateSupport(cell))
            {
                CreateSupportCollider(cell);
            }

            if (ShouldCreateObstacle(cell))
            {
                CreateObstacleCollider(cell);
            }

            if (ShouldCreateDeathTrigger(cell))
            {
                CreateDeathTrigger(cell);
            }
        }

        Debug.Log("Grid physics rebuilt. Cells processed: " + cells.Count);
    }

    private bool ShouldCreateSupport(GridCell cell)
    {
        return cell.walkable && cell.type != CellType.Empty;
    }

    private bool ShouldCreateObstacle(GridCell cell)
    {
        switch (cell.type)
        {
            case CellType.Wall:
            case CellType.Door:
            case CellType.CrackedWall:
            case CellType.AirWall:
                return true;

            default:
                return false;
        }
    }

    private bool ShouldCreateDeathTrigger(GridCell cell)
    {
        return cell.type == CellType.Death;
    }

    private void CreateSupportCollider(GridCell cell)
    {
        Vector3 center =
            GridCoordinateUtility.GridToWorldCenter(
                referenceTilemap,
                cell.position
            );

        Vector3 cellSize = gridCellSize;

        GameObject colliderObject = new GameObject(
            "Support_" + cell.position.x + "_" + cell.position.y
        );

        colliderObject.transform.SetParent(supportColliderParent);

        colliderObject.transform.position = new Vector3(
            center.x,
            center.y - cellSize.y * 0.5f
                + supportThickness * 0.5f,
            center.z
        );

        SetLayer(colliderObject, supportLayerName);

        BoxCollider2D box =
            colliderObject.AddComponent<BoxCollider2D>();

        box.size = new Vector2(
            cellSize.x,
            supportThickness
        );

        box.isTrigger = false;

        AttachGridCellReference(colliderObject, cell);
    }

    private void CreateObstacleCollider(GridCell cell)
    {
        Vector3 center =
            GridCoordinateUtility.GridToWorldCenter(
                referenceTilemap,
                cell.position
            );

        Vector3 cellSize = gridCellSize;

        GameObject colliderObject = new GameObject(
            "Obstacle_" + cell.position.x + "_" + cell.position.y
        );

        colliderObject.transform.SetParent(obstacleColliderParent);
        colliderObject.transform.position = center;

        SetLayer(colliderObject, obstacleLayerName);

        BoxCollider2D box =
            colliderObject.AddComponent<BoxCollider2D>();

        box.size = new Vector2(
            cellSize.x,
            cellSize.y
        );

        box.isTrigger = false;

        AttachGridCellReference(colliderObject, cell);
    }

    private void CreateDeathTrigger(GridCell cell)
    {
        Vector3 center =
            GridCoordinateUtility.GridToWorldCenter(
                referenceTilemap,
                cell.position
            );

        Vector3 cellSize = gridCellSize;

        GameObject triggerObject = new GameObject(
            "Death_" + cell.position.x + "_" + cell.position.y
        );

        triggerObject.transform.SetParent(deathTriggerParent);
        triggerObject.transform.position = center;

        SetLayer(triggerObject, deathTriggerLayerName);

        BoxCollider2D box =
            triggerObject.AddComponent<BoxCollider2D>();

        box.size = new Vector2(
            cellSize.x,
            cellSize.y
        );

        box.isTrigger = true;

        DeathSceneResetTrigger deathTrigger =
            triggerObject.AddComponent<DeathSceneResetTrigger>();

        deathTrigger.ConfigureAudio(
            deathStingerEvent,
            deathStingerEventTarget,
            deathResetDelay
        );
    }

    private void EnsureParents()
    {
        if (supportColliderParent == null)
        {
            GameObject parent =
                new GameObject("GeneratedSupportColliders");

            parent.transform.SetParent(transform);
            supportColliderParent = parent.transform;
        }

        if (obstacleColliderParent == null)
        {
            GameObject parent =
                new GameObject("GeneratedObstacleColliders");

            parent.transform.SetParent(transform);
            obstacleColliderParent = parent.transform;
        }

        if (deathTriggerParent == null)
        {
            GameObject parent =
                new GameObject("GeneratedDeathTriggers");

            parent.transform.SetParent(transform);
            deathTriggerParent = parent.transform;
        }
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void SetLayer(
        GameObject target,
        string layerName
    )
    {
        int layer = LayerMask.NameToLayer(layerName);

        if (layer >= 0)
        {
            target.layer = layer;
        }
    }

    private void AttachGridCellReference(
        GameObject target,
        GridCell cell
    )
    {
        GridCellReference cellReference =
            target.AddComponent<GridCellReference>();

        cellReference.Initialize(cell);
    }
}
