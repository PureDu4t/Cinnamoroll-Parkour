using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
public class PlayerVoicePanController : MonoBehaviour
{
    [Header("Position References")]
    [SerializeField] private PlayerPositionTracker positionTracker;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform voiceEmitter;
    [SerializeField] private Transform listenerReference;

    [Header("Grid Range")]
    [SerializeField] private int minimumGridX = 1;
    [SerializeField] private int maximumGridX = 15;
    [SerializeField] private float panWorldWidth = 30f;
    [SerializeField] private bool useCameraViewportPan = true;
    [Range(0f, 1f)]
    [SerializeField] private float viewportPanLeftEdge = 0.125f;
    [Range(0f, 1f)]
    [SerializeField] private float viewportPanRightEdge = 0.875f;
    [SerializeField] private float panResponseCurve = 1.25f;
    [SerializeField] private float emitterForwardDistance = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;
    [SerializeField] private bool drawDebugPanLine = true;

    private Camera listenerCamera;
    private float nextDebugLogTime;

    private void Awake()
    {
        AutoAssignMissingReferences();
    }

    private void Reset()
    {
        AutoAssignMissingReferences();
    }

    private void LateUpdate()
    {
        if (!ValidateConfiguration())
        {
            return;
        }

        MoveEmitterForPlayerX();
    }

    private void AutoAssignMissingReferences()
    {
        if (voiceEmitter == null)
        {
            voiceEmitter = transform;
        }

        if (positionTracker == null)
        {
            positionTracker = GetComponentInParent<PlayerPositionTracker>();
        }

        if (playerTransform == null && positionTracker != null)
        {
            playerTransform = positionTracker.transform;
        }

        if (playerTransform == null)
        {
            playerTransform = transform.root;
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

    private bool ValidateConfiguration()
    {
        if (voiceEmitter == null || listenerReference == null)
        {
            AutoAssignMissingReferences();
        }

        return voiceEmitter != null
            && listenerReference != null
            && playerTransform != null;
    }

    private void MoveEmitterForPlayerX()
    {
        float normalizedX = GetNormalizedPanX();
        float localPanX = Mathf.Lerp(
            -panWorldWidth * 0.5f,
            panWorldWidth * 0.5f,
            normalizedX
        );
        float signedPan = normalizedX * 2f - 1f;

        Vector3 listenerPosition = listenerReference.position;
        Vector3 forwardOffset =
            listenerReference.forward * emitterForwardDistance;

        voiceEmitter.position = new Vector3(
            listenerPosition.x + localPanX,
            listenerPosition.y,
            listenerPosition.z
        ) + forwardOffset;

        AkSoundEngine.SetObjectPosition(
            voiceEmitter.gameObject,
            voiceEmitter.position,
            voiceEmitter.forward,
            voiceEmitter.up
        );

        if (drawDebugPanLine)
        {
            Debug.DrawLine(
                listenerReference.position,
                voiceEmitter.position,
                GetPanDebugColor(signedPan)
            );
        }

        if (debugLogging && Time.time >= nextDebugLogTime)
        {
            Debug.Log(
                "Player VO pan: "
                + signedPan.ToString("0.00")
                + ", emitter offset X: "
                + localPanX.ToString("0.00")
            );
            nextDebugLogTime = Time.time + 0.5f;
        }
    }

    private float GetNormalizedPanX()
    {
        float signedPan;

        if (useCameraViewportPan && listenerCamera != null)
        {
            Vector3 viewportPosition =
                listenerCamera.WorldToViewportPoint(playerTransform.position);

            float leftEdge = Mathf.Clamp01(viewportPanLeftEdge);
            float rightEdge = Mathf.Clamp01(viewportPanRightEdge);

            if (rightEdge <= leftEdge)
            {
                rightEdge = Mathf.Min(1f, leftEdge + 0.01f);
            }

            signedPan = Mathf.InverseLerp(
                leftEdge,
                rightEdge,
                viewportPosition.x
            ) * 2f - 1f;
        }
        else
        {
            float designX = playerTransform.position.x;

            if (positionTracker != null)
            {
                designX = positionTracker
                    .GetNearestGridPosition(playerTransform.position)
                    .x;
            }

            signedPan = Mathf.InverseLerp(
                minimumGridX,
                maximumGridX,
                designX
            ) * 2f - 1f;
        }

        signedPan = GridAudioParameterUtility.ApplyPanResponseCurve(
            signedPan,
            panResponseCurve
        );
        return (signedPan + 1f) * 0.5f;
    }

    private Color GetPanDebugColor(float signedPan)
    {
        if (signedPan < -0.1f)
        {
            return Color.cyan;
        }

        if (signedPan > 0.1f)
        {
            return Color.magenta;
        }

        return Color.white;
    }
}
