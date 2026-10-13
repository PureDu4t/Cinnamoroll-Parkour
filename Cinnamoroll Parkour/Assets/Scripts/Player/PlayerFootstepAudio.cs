using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
[RequireComponent(typeof(PlayerJump))]
[RequireComponent(typeof(PlayerPositionTracker))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFootstepAudio : MonoBehaviour
{
    [Header("Wwise Events")]
    [SerializeField] private AK.Wwise.Event jumpStartEvent;
    [SerializeField] private AK.Wwise.Event landingEvent;

    [Header("Footstep Loop Events")]
    [SerializeField] private AK.Wwise.Event footstepLoopPlayEvent;
    [SerializeField] private AK.Wwise.Event footstepLoopStopEvent;

    [Header("Footstep Input")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode moveRightKey = KeyCode.RightArrow;

    [Tooltip("If enabled, footsteps only play while Rigidbody2D has horizontal movement.")]
    [SerializeField] private bool requireHorizontalMovement = true;

    [SerializeField] private float minimumHorizontalSpeed = 0.05f;

    [Header("Position Tone Events")]
    [Tooltip("Played when the player becomes airborne. Wwise should make the jump/fall position tone audible.")]
    [SerializeField] private AK.Wwise.Event positionToneShowEvent;

    [Tooltip("Played when the player lands. Wwise should hide the jump/fall position tone again.")]
    [SerializeField] private AK.Wwise.Event positionToneHideEvent;

    [Tooltip("Played when the player enters a new X grid cell while grounded.")]
    [SerializeField] private AK.Wwise.Event positionTonePulseEvent;

    [Tooltip("Jump/fall tone target. Use the emitter that plays the jump/fall PlayerPositionTone.")]
    [SerializeField] private Transform airbornePositionToneEventTarget;

    [Tooltip("Horizontal pulse target. Use the emitter that plays the copied X-pulse PlayerPositionTone.")]
    [SerializeField] private Transform horizontalPositionToneEventTarget;

    [Tooltip("Legacy fallback target. Used when the dedicated targets above are empty.")]
    [SerializeField] private Transform positionToneEventTarget;

    [Header("Step Rules")]
    [Tooltip("Minimum time between grid-step sounds while walking.")]
    [SerializeField] private float stepCooldown = 0.08f;

    [Tooltip("If true, stepping into a new X column only plays while the player is grounded.")]
    [SerializeField] private bool onlyStepWhenGrounded = true;

    [Tooltip("Skip the first detected grid X so the player does not step immediately on scene load.")]
    [SerializeField] private bool ignoreInitialGridX = true;

    private PlayerJump playerJump;
    private PlayerPositionTracker positionTracker;
    private Rigidbody2D rb;
    private bool hasLastGridX;
    private int lastGridX;
    private float nextAllowedStepTime;
    private bool footstepLoopPlaying;
    private bool airbornePositionToneVisible;

    private void Awake()
    {
        playerJump = GetComponent<PlayerJump>();
        positionTracker = GetComponent<PlayerPositionTracker>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (playerJump == null)
        {
            return;
        }

        playerJump.JumpStarted += PlayJumpStart;
        playerJump.Landed += PlayLanding;
    }

    private void OnDisable()
    {
        if (playerJump == null)
        {
            return;
        }

        if (footstepLoopPlaying)
        {
            StopFootstepLoop();
        }

        playerJump.JumpStarted -= PlayJumpStart;
        playerJump.Landed -= PlayLanding;
    }

    private void LateUpdate()
    {
        UpdateAirbornePositionTone();
        UpdateFootstepLoop();
        UpdateStep();
    }

    private void UpdateAirbornePositionTone()
    {
        bool isAirborne = !playerJump.IsGrounded();

        if (isAirborne && !airbornePositionToneVisible)
        {
            ShowAirbornePositionTone();
            return;
        }

        if (!isAirborne && airbornePositionToneVisible)
        {
            HideAirbornePositionTone();
        }
    }

    private void UpdateFootstepLoop()
    {
        bool shouldPlayFootsteps = ShouldPlayFootstepLoop();

        if (shouldPlayFootsteps && !footstepLoopPlaying)
        {
            if (TryPostEvent(footstepLoopPlayEvent, "footstep loop play"))
            {
                footstepLoopPlaying = true;
            }

            return;
        }

        if (!shouldPlayFootsteps && footstepLoopPlaying)
        {
            StopFootstepLoop();
        }
    }

    private bool ShouldPlayFootstepLoop()
    {
        if (!playerJump.IsGrounded())
        {
            return false;
        }

        bool leftPressed = Input.GetKey(moveLeftKey);
        bool rightPressed = Input.GetKey(moveRightKey);

        if (leftPressed == rightPressed)
        {
            return false;
        }

        if (!requireHorizontalMovement)
        {
            return true;
        }

        return Mathf.Abs(rb.linearVelocity.x) >= minimumHorizontalSpeed;
    }

    private void UpdateStep()
    {
        int currentGridX = positionTracker.CurrentGridPosition.x;

        if (!hasLastGridX)
        {
            hasLastGridX = true;
            lastGridX = currentGridX;

            if (ignoreInitialGridX)
            {
                return;
            }
        }

        if (currentGridX == lastGridX)
        {
            return;
        }

        lastGridX = currentGridX;

        if (onlyStepWhenGrounded && !playerJump.IsGrounded())
        {
            return;
        }

        if (Time.time < nextAllowedStepTime)
        {
            return;
        }

        bool playedPositionTonePulse =
            TryPostEvent(
                positionTonePulseEvent,
                "position tone pulse",
                GetHorizontalPositionToneEventGameObject()
            );

        if (playedPositionTonePulse)
        {
            nextAllowedStepTime = Time.time + stepCooldown;
        }
    }

    private void PlayJumpStart()
    {
        TryPostEvent(jumpStartEvent, "jump start");
        ShowAirbornePositionTone();
    }

    private void PlayLanding()
    {
        TryPostEvent(landingEvent, "landing");
        HideAirbornePositionTone();
    }

    private void ShowAirbornePositionTone()
    {
        if (airbornePositionToneVisible)
        {
            return;
        }

        if (TryPostEvent(
            positionToneShowEvent,
            "position tone show",
            GetAirbornePositionToneEventGameObject()
        ))
        {
            airbornePositionToneVisible = true;
        }
    }

    private void HideAirbornePositionTone()
    {
        if (!airbornePositionToneVisible)
        {
            return;
        }

        TryPostEvent(
            positionToneHideEvent,
            "position tone hide",
            GetAirbornePositionToneEventGameObject()
        );
        airbornePositionToneVisible = false;
    }

    private void StopFootstepLoop()
    {
        TryPostEvent(footstepLoopStopEvent, "footstep loop stop");
        footstepLoopPlaying = false;
    }

    private void OnDestroy()
    {
        if (footstepLoopPlaying)
        {
            StopFootstepLoop();
        }
    }

    private GameObject GetAirbornePositionToneEventGameObject()
    {
        if (airbornePositionToneEventTarget != null)
        {
            return airbornePositionToneEventTarget.gameObject;
        }

        return GetLegacyPositionToneEventGameObject();
    }

    private GameObject GetHorizontalPositionToneEventGameObject()
    {
        if (horizontalPositionToneEventTarget != null)
        {
            return horizontalPositionToneEventTarget.gameObject;
        }

        return GetLegacyPositionToneEventGameObject();
    }

    private GameObject GetLegacyPositionToneEventGameObject()
    {
        if (positionToneEventTarget != null)
        {
            return positionToneEventTarget.gameObject;
        }

        return gameObject;
    }

    private bool TryPostEvent(
        AK.Wwise.Event eventToPlay,
        string debugName,
        GameObject targetGameObject = null
    )
    {
        if (eventToPlay == null || !eventToPlay.IsValid())
        {
            return false;
        }

        GameObject eventTarget =
            targetGameObject != null ? targetGameObject : gameObject;

        TtsText.TryPlay(gameObject);

        uint playingId = eventToPlay.Post(eventTarget);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning(
                name + " could not play " + debugName + " Wwise Event."
            );
            return false;
        }

        return true;
    }
}
