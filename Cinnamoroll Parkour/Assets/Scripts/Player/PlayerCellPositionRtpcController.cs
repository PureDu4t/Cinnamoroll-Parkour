using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(AkGameObj))]
public class PlayerCellPositionRtpcController : MonoBehaviour
{
    [Header("Position References")]
    [SerializeField] private Tilemap referenceTilemap;
    [SerializeField] private Transform trackedTransform;
    [SerializeField] private GameObject rtpcTarget;

    [Header("Wwise RTPC")]
    [Tooltip("Recommended RTPC range: 0 at left edge, 50 at cell center, 100 at right edge.")]
    [SerializeField] private AK.Wwise.RTPC cellXPositionRtpc;

    [SerializeField] private float leftEdgeValue = 0f;
    [SerializeField] private float rightEdgeValue = 100f;

    [Tooltip("Also set the RTPC globally. Useful if the Wwise object does not read game-object scoped values.")]
    [SerializeField] private bool setGlobalValue = true;

    [Header("Smoothing")]
    [Tooltip("0 means instant updates. Higher values smooth sudden RTPC changes.")]
    [SerializeField] private float smoothingSpeed = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;
    [SerializeField] private bool drawCellEdgeGuides = true;

    private bool hasCurrentValue;
    private float currentRtpcValue;
    private float nextDebugLogTime;
    private Vector3 lastCellCenter;
    private float lastCellWidth = 1f;

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Update()
    {
        UpdateRtpc();
    }

    private void AutoAssignReferences()
    {
        if (trackedTransform == null)
        {
            trackedTransform = transform;
        }

        if (rtpcTarget == null)
        {
            rtpcTarget = gameObject;
        }
    }

    private void UpdateRtpc()
    {
        if (!ValidateRuntimeReferences())
        {
            return;
        }

        float normalizedCellX = GetNormalizedXInsideCurrentCell();
        float targetValue = Mathf.Lerp(
            leftEdgeValue,
            rightEdgeValue,
            normalizedCellX
        );

        if (!hasCurrentValue || smoothingSpeed <= 0f)
        {
            currentRtpcValue = targetValue;
            hasCurrentValue = true;
        }
        else
        {
            currentRtpcValue = Mathf.MoveTowards(
                currentRtpcValue,
                targetValue,
                smoothingSpeed * Time.deltaTime
            );
        }

        cellXPositionRtpc.SetValue(rtpcTarget, currentRtpcValue);

        if (setGlobalValue)
        {
            cellXPositionRtpc.SetGlobalValue(currentRtpcValue);
        }

        LogDebugIfNeeded(normalizedCellX);
    }

    private float GetNormalizedXInsideCurrentCell()
    {
        Vector3Int cellPosition =
            referenceTilemap.WorldToCell(trackedTransform.position);

        lastCellCenter =
            referenceTilemap.GetCellCenterWorld(cellPosition);

        Vector3Int rightCellPosition =
            new Vector3Int(
                cellPosition.x + 1,
                cellPosition.y,
                cellPosition.z
            );

        Vector3 rightCellCenter =
            referenceTilemap.GetCellCenterWorld(rightCellPosition);

        lastCellWidth = Mathf.Abs(rightCellCenter.x - lastCellCenter.x);

        if (lastCellWidth <= Mathf.Epsilon)
        {
            lastCellWidth =
                Mathf.Abs(referenceTilemap.layoutGrid.cellSize.x);
        }

        if (lastCellWidth <= Mathf.Epsilon)
        {
            lastCellWidth = 1f;
        }

        float leftEdgeX = lastCellCenter.x - lastCellWidth * 0.5f;
        float rightEdgeX = lastCellCenter.x + lastCellWidth * 0.5f;

        return Mathf.Clamp01(
            Mathf.InverseLerp(
                leftEdgeX,
                rightEdgeX,
                trackedTransform.position.x
            )
        );
    }

    private bool ValidateRuntimeReferences()
    {
        if (referenceTilemap == null
            || trackedTransform == null
            || rtpcTarget == null
            || cellXPositionRtpc == null
            || !cellXPositionRtpc.IsValid())
        {
            return false;
        }

        return true;
    }

    private void LogDebugIfNeeded(float normalizedCellX)
    {
        if (!debugLogging || Time.time < nextDebugLogTime)
        {
            return;
        }

        Debug.Log(
            name
            + " cell X RTPC: "
            + currentRtpcValue.ToString("0.00")
            + " / normalized cell X: "
            + normalizedCellX.ToString("0.00")
        );

        nextDebugLogTime = Time.time + 0.25f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawCellEdgeGuides || referenceTilemap == null)
        {
            return;
        }

        Transform target = trackedTransform != null
            ? trackedTransform
            : transform;

        Vector3Int cellPosition =
            referenceTilemap.WorldToCell(target.position);

        Vector3 cellCenter =
            referenceTilemap.GetCellCenterWorld(cellPosition);

        Vector3 rightCellCenter =
            referenceTilemap.GetCellCenterWorld(
                new Vector3Int(
                    cellPosition.x + 1,
                    cellPosition.y,
                    cellPosition.z
                )
            );

        float cellWidth = Mathf.Abs(rightCellCenter.x - cellCenter.x);
        if (cellWidth <= Mathf.Epsilon)
        {
            cellWidth = lastCellWidth;
        }

        float halfWidth = cellWidth * 0.5f;
        float guideHeight = referenceTilemap.layoutGrid.cellSize.y;

        DrawGuideLine(
            new Vector3(cellCenter.x - halfWidth, cellCenter.y, cellCenter.z),
            guideHeight,
            Color.red
        );

        DrawGuideLine(
            cellCenter,
            guideHeight,
            Color.green
        );

        DrawGuideLine(
            new Vector3(cellCenter.x + halfWidth, cellCenter.y, cellCenter.z),
            guideHeight,
            Color.red
        );
    }

    private static void DrawGuideLine(
        Vector3 center,
        float height,
        Color color
    )
    {
        Gizmos.color = color;
        Gizmos.DrawLine(
            center + Vector3.down * height * 0.5f,
            center + Vector3.up * height * 0.5f
        );
    }
}
