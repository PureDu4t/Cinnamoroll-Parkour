using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PlayerPositionToneController : MonoBehaviour
{
    [Header("Position References")]
    [SerializeField] private GridDatabase gridDatabase;
    [SerializeField] private PlayerPositionTracker positionTracker;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform positionToneEmitter;
    [SerializeField] private Transform listenerReference;

    [Header("Grid Range")]
    [Tooltip("Design X at the player's start position, usually 1.")]
    [SerializeField] private int minimumGridX = 1;

    [Tooltip("The rightmost design X used for panning.")]
    [SerializeField] private int maximumGridX = 15;

    [Tooltip("Unity world units per design grid cell.")]
    [SerializeField] private float worldUnitsPerGridCell = 1f;

    [Tooltip("How far left/right the tone emitter moves from the listener.")]
    [SerializeField] private float panWorldWidth = 8f;

    [Tooltip("Maps screen center to center pan and screen edges to hard left/right.")]
    [SerializeField] private bool useCameraViewportPan = true;

    [Range(0f, 1f)]
    [Tooltip("Viewport X treated as hard left. 0 is screen left, 1 is screen right.")]
    [SerializeField] private float viewportPanLeftEdge = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Viewport X treated as hard right. 0 is screen left, 1 is screen right.")]
    [SerializeField] private float viewportPanRightEdge = 0.9f;

    [Tooltip("Values above 1 make panning gentler near the center.")]
    [SerializeField] private float panResponseCurve = 1.25f;

    [Tooltip("Keeps the emitter in front of the listener for Wwise spatialization.")]
    [SerializeField] private float emitterForwardDistance = 5f;

    [Header("Y Pitch")]
    [Tooltip("Design Y at the player's start position, usually 1.")]
    [SerializeField] private int minimumGridY = 1;

    [Tooltip("Use the player's lowest non-trigger Collider2D point for pitch. This avoids reading the body center as a higher grid row.")]
    [SerializeField] private bool useFeetPositionForPitch = true;

    [Tooltip("Small downward offset from the player's feet when sampling the grid row.")]
    [SerializeField] private float feetPitchSampleOffset = 0.05f;

    [Tooltip("Use the lowest walkable/interactable grid row as Minimum Grid Y. This lets levels start above row 1 and still report the correct pitch.")]
    [SerializeField] private bool inferPitchOriginFromGridDatabase = true;

    [Tooltip("Added to the inferred lowest playable Y. Use 1 when the bottom row is invisible physics support, 0 when it is real gameplay ground.")]
    [SerializeField] private int inferredPitchOriginRowOffset = 1;

    [Tooltip("Wait for one physics update before establishing the pitch origin. This prevents ground physics from being interpreted as an initial pitch movement.")]
    [SerializeField] private bool waitForPhysicsBeforePitchOrigin = true;

    [Tooltip("Pitch values, in cents, for C D E F G A B.")]
    [SerializeField] private int[] scaleCents =
    {
        0, 200, 400, 500, 700, 900, 1100
    };

    [Tooltip("Caps sent pitch to this value.")]
    [SerializeField] private float maximumPitchCents = 2400f;

    [Header("Debug")]
    [Tooltip("Print calculated X/Y/pitch/pan for debugging.")]
    [SerializeField] private bool debugLogging;

    [Tooltip("Draws the listener-to-emitter line in the Scene view.")]
    [SerializeField] private bool drawDebugPanLine = true;

    [Tooltip("Draws viewport left/center/right pan guide lines in the Scene view.")]
    [SerializeField] private bool drawViewportPanGuides = true;

    [Header("Wwise Events")]
    [SerializeField] private AK.Wwise.Event playToneEvent;
    [SerializeField] private AK.Wwise.Event stopToneEvent;

    [Header("Wwise RTPC")]
    [SerializeField] private AK.Wwise.RTPC yPitchRtpc;
    [SerializeField] private AK.Wwise.RTPC volumeRtpc;

    [Tooltip("Enable only if Unity should continuously drive position tone volume. Leave off when Wwise Events handle show/hide/pulse volume.")]
    [SerializeField] private bool updateVolumeRtpc;

    [Tooltip("Sets the starting volume once before Play. This keeps startup audible without overriding later Wwise show/hide/pulse Events every frame.")]
    [SerializeField] private bool setInitialVolumeRtpc = true;

    [Tooltip("0 dB means audible. -96 dB is effectively muted.")]
    [SerializeField] private float activeVolumeDb = 0f;

    [Tooltip("How long to retry Play_PlayerPositionTone while Wwise/SoundBank startup settles.")]
    [SerializeField] private float startupRetryDuration = 2f;

    [SerializeField] private float startupRetryInterval = 0.1f;

    private bool toneStarted;
    private Coroutine startToneCoroutine;
    private Vector3 originPitchSampleWorldPosition;
    private Vector2 originGridPosition;
    private float pitchOriginGridY;
    private bool pitchOriginGridYInitialized;
    private Collider2D[] playerColliders;
    private Camera listenerCamera;
    private float nextDebugLogTime;
    private int startupDebugSamples = 6;
    private float lastSignedPan;
    private float lastLocalPanX;

    private void Awake()
    {
        AutoAssignMissingReferences();
    }

    private void Reset()
    {
        AutoAssignMissingReferences();
    }

    private void AutoAssignMissingReferences()
    {
        if (gridDatabase == null)
        {
            gridDatabase = GridDatabase.Instance;
        }

        if (positionTracker == null)
        {
            positionTracker = GetComponent<PlayerPositionTracker>();
        }

        if (playerTransform == null)
        {
            playerTransform = transform;
        }

        if (listenerReference == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                listenerReference = mainCamera.transform;
                listenerCamera = mainCamera;
            }
        }

        if (listenerCamera == null && listenerReference != null)
        {
            listenerCamera = listenerReference.GetComponent<Camera>();
        }
    }

    private IEnumerator Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            yield break;
        }

        /*
         * Wait for Unity's physics system to resolve the player's
         * starting position on the ground before establishing the
         * pitch origin.
         *
         * Without this, the upward movement caused by ground physics
         * can be interpreted as an actual musical movement.
         */
        if (waitForPhysicsBeforePitchOrigin)
        {
            yield return new WaitForFixedUpdate();
        }

        positionTracker.RefreshPosition();

        playerColliders = GetTrackedTransform()
            .GetComponentsInChildren<Collider2D>();

        originPitchSampleWorldPosition = GetPitchSampleWorldPosition();

        originGridPosition =
            positionTracker.GetNearestGridPosition(
                originPitchSampleWorldPosition
            );

        RefreshPitchOriginGridY();

        UpdateTone();

        startToneCoroutine = StartCoroutine(StartToneWhenReady());
    }

    private void Update()
    {
        UpdateTone();
    }

    private void OnDrawGizmos()
    {
        DrawViewportPanGuides();
    }

    private void UpdateTone()
    {
        Vector2 continuousDesignPosition =
            GetContinuousDesignPosition();

        MoveEmitterForX(continuousDesignPosition.x);
        SetPitchForY(continuousDesignPosition.y);

        if (updateVolumeRtpc && volumeRtpc != null)
        {
            SetVolumeRtpc(activeVolumeDb);
        }

        if ((debugLogging || startupDebugSamples > 0)
            && Time.time >= nextDebugLogTime)
        {
            Debug.Log(
                "Position tone X/Y: " + continuousDesignPosition
                + ", pitch cents: "
                + GetPitchCentsForY(continuousDesignPosition.y)
                + ", pan target: "
                + GetPanLabel(lastSignedPan)
                + " (" + lastSignedPan.ToString("0.00") + ")"
                + ", emitter offset X: "
                + lastLocalPanX.ToString("0.00")
                + ", pitch origin grid Y: "
                + pitchOriginGridY.ToString("0.00")
            );

            nextDebugLogTime = Time.time + 0.5f;
            startupDebugSamples--;
        }
    }

    private IEnumerator StartToneWhenReady()
    {
        float stopTryingAt = Time.time + startupRetryDuration;

        while (!AkSoundEngine.IsInitialized() && Time.time < stopTryingAt)
        {
            yield return null;
        }

        if (setInitialVolumeRtpc)
        {
            SetVolumeRtpc(activeVolumeDb);
        }

        TtsText.TryPlay(gameObject);

        while (!toneStarted && Time.time <= stopTryingAt)
        {
            uint playingId = playToneEvent.Post(
                positionToneEmitter.gameObject
            );

            if (playingId != AkSoundEngine.AK_INVALID_PLAYING_ID)
            {
                toneStarted = true;

                if (debugLogging)
                {
                    Debug.Log(
                        "PlayerPositionTone started. Playing ID: "
                        + playingId
                    );
                }

                yield break;
            }

            yield return new WaitForSeconds(startupRetryInterval);
        }

        Debug.LogError(
            "Wwise could not play PlayerPositionTone after retrying. "
            + "Check that the bank containing Play_PlayerPositionTone is loaded."
        );
    }

    private void SetVolumeRtpc(float volumeDb)
    {
        if (volumeRtpc == null || positionToneEmitter == null)
        {
            return;
        }

        volumeRtpc.SetValue(
            positionToneEmitter.gameObject,
            volumeDb
        );

        volumeRtpc.SetGlobalValue(volumeDb);
    }

    private Vector2 GetContinuousDesignPosition()
    {
        if (!pitchOriginGridYInitialized)
        {
            RefreshPitchOriginGridY();
        }

        Vector3 delta =
            GetPitchSampleWorldPosition()
            - originPitchSampleWorldPosition;

        Vector2 continuousGridPosition = new Vector2(
            originGridPosition.x
                + delta.x / worldUnitsPerGridCell,

            originGridPosition.y
                + delta.y / worldUnitsPerGridCell
        );

        return new Vector2(
            continuousGridPosition.x,
            minimumGridY
                + continuousGridPosition.y
                - pitchOriginGridY
        );
    }

    private Vector3 GetPitchSampleWorldPosition()
    {
        if (!useFeetPositionForPitch)
        {
            return GetTrackedTransform().position;
        }

        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetTrackedTransform()
                .GetComponentsInChildren<Collider2D>();
        }

        bool foundCollider = false;

        Bounds lowestBounds = new Bounds(
            GetTrackedTransform().position,
            Vector3.zero
        );

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];

            if (playerCollider == null
                || !playerCollider.enabled
                || playerCollider.isTrigger)
            {
                continue;
            }

            if (!foundCollider
                || playerCollider.bounds.min.y < lowestBounds.min.y)
            {
                lowestBounds = playerCollider.bounds;
                foundCollider = true;
            }
        }

        if (!foundCollider)
        {
            return GetTrackedTransform().position;
        }

        return new Vector3(
            lowestBounds.center.x,
            lowestBounds.min.y
                - Mathf.Abs(feetPitchSampleOffset),
            GetTrackedTransform().position.z
        );
    }

    private void RefreshPitchOriginGridY()
    {
        pitchOriginGridY = originGridPosition.y;

        if (!inferPitchOriginFromGridDatabase)
        {
            pitchOriginGridYInitialized = true;
            return;
        }

        if (gridDatabase == null)
        {
            gridDatabase = GridDatabase.Instance;
        }

        if (gridDatabase == null || gridDatabase.Count == 0)
        {
            pitchOriginGridYInitialized = false;
            return;
        }

        bool foundPlayableRow = false;
        int lowestPlayableY = 0;

        List<GridCell> cells =
            gridDatabase.GetAllCells();

        for (int i = 0; i < cells.Count; i++)
        {
            GridCell cell = cells[i];

            if (cell == null
                || (!cell.walkable && !cell.interactable))
            {
                continue;
            }

            if (!foundPlayableRow
                || cell.position.y < lowestPlayableY)
            {
                lowestPlayableY = cell.position.y;
                foundPlayableRow = true;
            }
        }

        if (foundPlayableRow)
        {
            pitchOriginGridY =
                lowestPlayableY
                + inferredPitchOriginRowOffset;

            pitchOriginGridYInitialized = true;
            return;
        }

        pitchOriginGridYInitialized = false;
    }

    private void MoveEmitterForX(float designX)
    {
        float normalizedX =
            GetNormalizedPanX(designX);

        float localPanX = Mathf.Lerp(
            -panWorldWidth * 0.5f,
            panWorldWidth * 0.5f,
            normalizedX
        );

        lastSignedPan =
            normalizedX * 2f - 1f;

        lastLocalPanX = localPanX;

        Vector3 listenerPosition =
            listenerReference.position;

        Vector3 forwardOffset =
            listenerReference.forward
            * emitterForwardDistance;

        positionToneEmitter.position =
            new Vector3(
                listenerPosition.x + localPanX,
                listenerPosition.y,
                listenerPosition.z
            )
            + forwardOffset;

        AkSoundEngine.SetObjectPosition(
            positionToneEmitter.gameObject,
            positionToneEmitter.position,
            positionToneEmitter.forward,
            positionToneEmitter.up
        );

        if (drawDebugPanLine)
        {
            Debug.DrawLine(
                listenerReference.position,
                positionToneEmitter.position,
                GetPanDebugColor(lastSignedPan)
            );
        }
    }

    private float GetNormalizedPanX(float designX)
    {
        float signedPan;

        if (useCameraViewportPan
            && listenerCamera != null)
        {
            Vector3 viewportPosition =
                listenerCamera.WorldToViewportPoint(
                    GetTrackedTransform().position
                );

            float leftEdge =
                Mathf.Clamp01(viewportPanLeftEdge);

            float rightEdge =
                Mathf.Clamp01(viewportPanRightEdge);

            if (rightEdge <= leftEdge)
            {
                rightEdge =
                    Mathf.Min(
                        1f,
                        leftEdge + 0.01f
                    );
            }

            signedPan =
                Mathf.InverseLerp(
                    leftEdge,
                    rightEdge,
                    viewportPosition.x
                )
                * 2f
                - 1f;
        }
        else
        {
            signedPan =
                Mathf.InverseLerp(
                    minimumGridX,
                    maximumGridX,
                    designX
                )
                * 2f
                - 1f;
        }

        signedPan =
            ApplyPanResponseCurve(signedPan);

        return (signedPan + 1f) * 0.5f;
    }

    private float ApplyPanResponseCurve(float signedPan)
    {
        return GridAudioParameterUtility.ApplyPanResponseCurve(
            signedPan,
            panResponseCurve
        );
    }

    private void DrawViewportPanGuides()
    {
        if (!drawViewportPanGuides
            || !useCameraViewportPan)
        {
            return;
        }

        Camera guideCamera = GetGuideCamera();

        if (guideCamera == null)
        {
            return;
        }

        float leftEdge =
            Mathf.Clamp01(viewportPanLeftEdge);

        float rightEdge =
            Mathf.Clamp01(viewportPanRightEdge);

        if (rightEdge <= leftEdge)
        {
            rightEdge =
                Mathf.Min(
                    1f,
                    leftEdge + 0.01f
                );
        }

        float center =
            (leftEdge + rightEdge) * 0.5f;

        DrawViewportGuideLine(
            guideCamera,
            leftEdge,
            new Color(1f, 0.85f, 0.15f),
            "Pan Left " + leftEdge.ToString("0.00")
        );

        DrawViewportGuideLine(
            guideCamera,
            center,
            Color.white,
            "Pan Center " + center.ToString("0.00")
        );

        DrawViewportGuideLine(
            guideCamera,
            rightEdge,
            new Color(1f, 0.85f, 0.15f),
            "Pan Right " + rightEdge.ToString("0.00")
        );
    }

    private Camera GetGuideCamera()
    {
        if (listenerCamera != null)
        {
            return listenerCamera;
        }

        if (listenerReference != null)
        {
            Camera cameraFromReference =
                listenerReference.GetComponent<Camera>();

            if (cameraFromReference != null)
            {
                return cameraFromReference;
            }
        }

        return Camera.main;
    }

    private void DrawViewportGuideLine(
        Camera guideCamera,
        float viewportX,
        Color color,
        string label
    )
    {
        float depth =
            GetGuideDepth(guideCamera);

        Vector3 bottom =
            guideCamera.ViewportToWorldPoint(
                new Vector3(
                    viewportX,
                    0f,
                    depth
                )
            );

        Vector3 top =
            guideCamera.ViewportToWorldPoint(
                new Vector3(
                    viewportX,
                    1f,
                    depth
                )
            );

        Gizmos.color = color;
        Gizmos.DrawLine(bottom, top);

#if UNITY_EDITOR
        Handles.color = color;
        Handles.Label(
            Vector3.Lerp(
                bottom,
                top,
                0.92f
            ),
            label
        );
#endif
    }

    private float GetGuideDepth(
        Camera guideCamera
    )
    {
        Transform trackedTransform =
            GetTrackedTransform();

        if (trackedTransform == null)
        {
            return Mathf.Max(
                guideCamera.nearClipPlane + 0.1f,
                1f
            );
        }

        float depth =
            Vector3.Dot(
                trackedTransform.position
                - guideCamera.transform.position,
                guideCamera.transform.forward
            );

        return Mathf.Max(
            depth,
            guideCamera.nearClipPlane + 0.1f
        );
    }

    private void SetPitchForY(float designY)
    {
        if (yPitchRtpc == null)
        {
            return;
        }

        float pitchCents =
            GetPitchCentsForY(designY);

        yPitchRtpc.SetValue(
            positionToneEmitter.gameObject,
            pitchCents
        );

        yPitchRtpc.SetGlobalValue(
            pitchCents
        );
    }

    private float GetPitchCentsForY(float designY)
    {
        return GridAudioParameterUtility.GetPitchCentsForY(
            designY,
            minimumGridY,
            scaleCents,
            maximumPitchCents
        );
    }

    private bool ValidateConfiguration()
    {
        if (positionTracker == null)
        {
            Debug.LogError(
                "PlayerPositionToneController needs a PlayerPositionTracker."
            );

            return false;
        }

        if (playerTransform == null)
        {
            playerTransform =
                GetTrackedTransform();
        }

        if (positionToneEmitter == null)
        {
            Debug.LogError(
                "PlayerPositionToneController needs a tone emitter."
            );

            return false;
        }

        if (listenerReference == null)
        {
            Debug.LogError(
                "PlayerPositionToneController needs a listener reference."
            );

            return false;
        }

        if (minimumGridX == maximumGridX)
        {
            Debug.LogError(
                "Minimum Grid X and Maximum Grid X cannot be the same."
            );

            return false;
        }

        if (worldUnitsPerGridCell <= 0f)
        {
            Debug.LogError(
                "World Units Per Grid Cell must be greater than zero."
            );

            return false;
        }

        if (scaleCents == null
            || scaleCents.Length == 0)
        {
            Debug.LogError(
                "PlayerPositionToneController needs at least one pitch value."
            );

            return false;
        }

        if (playToneEvent == null)
        {
            Debug.LogError(
                "PlayerPositionToneController needs a Play Tone Event."
            );

            return false;
        }

        return true;
    }

    private Transform GetTrackedTransform()
    {
        if (positionTracker != null)
        {
            return positionTracker.transform;
        }

        return playerTransform;
    }

    private static string GetPanLabel(
        float signedPan
    )
    {
        if (signedPan < -0.15f)
        {
            return "Left";
        }

        if (signedPan > 0.15f)
        {
            return "Right";
        }

        return "Center";
    }

    private static Color GetPanDebugColor(
        float signedPan
    )
    {
        if (signedPan < -0.15f)
        {
            return Color.blue;
        }

        if (signedPan > 0.15f)
        {
            return Color.red;
        }

        return Color.green;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugPanLine
            || listenerReference == null
            || positionToneEmitter == null)
        {
            return;
        }

        Gizmos.color =
            GetPanDebugColor(lastSignedPan);

        Gizmos.DrawLine(
            listenerReference.position,
            positionToneEmitter.position
        );

        Gizmos.DrawWireSphere(
            positionToneEmitter.position,
            0.2f
        );
    }

    private void OnDisable()
    {
        if (startToneCoroutine != null)
        {
            StopCoroutine(startToneCoroutine);
            startToneCoroutine = null;
        }

        if (!toneStarted
            || stopToneEvent == null
            || positionToneEmitter == null)
        {
            return;
        }

        var text = GetComponent<TtsText>();
        if (text != null) text.Stop();

        stopToneEvent.Post(
            positionToneEmitter.gameObject
        );

        toneStarted = false;
    }
}