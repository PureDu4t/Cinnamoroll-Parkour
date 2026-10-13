using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class PlayerJump : MonoBehaviour
{
    public event System.Action JumpStarted;
    public event System.Action Landed;

    [Header("Jump Settings")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Tooltip("刚按下跳跃键时立刻获得的向上速度")]
    [SerializeField] private float initialJumpVelocity = 6f;

    [Tooltip("按住跳跃键时，每秒继续增加的向上速度")]
    [SerializeField] private float holdJumpAcceleration = 18f;

    [Tooltip("最多允许持续加高跳跃的时间")]
    [SerializeField] private float maximumHoldDuration = 0.22f;

    [Tooltip("玩家向上移动的最大速度")]
    [SerializeField] private float maximumUpwardVelocity = 10f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.08f;
    [SerializeField] private LayerMask groundLayerMask;

    private Rigidbody2D rb;

    private bool jumpKeyHeld;
    private bool isExtendingJump;
    private bool controlEnabled = true;
    private bool hasGroundState;
    private bool wasGrounded;
    private bool hasBeenAirborne;
    private float jumpStartTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!controlEnabled || GameSpeechSettings.BlocksGameInput)
        {
            jumpKeyHeld = false;
            isExtendingJump = false;
            return;
        }

        if (Input.GetKeyDown(jumpKey) && IsGrounded())
        {
            StartJump();
        }

        jumpKeyHeld = Input.GetKey(jumpKey);

        if (Input.GetKeyUp(jumpKey))
        {
            isExtendingJump = false;
        }

        UpdateGroundState();
    }

    private void FixedUpdate()
    {
        ExtendJump();
    }

    private void StartJump()
    {
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            initialJumpVelocity
        );

        jumpStartTime = Time.time;
        isExtendingJump = true;
        hasBeenAirborne = false;
        JumpStarted?.Invoke();
    }

    private void ExtendJump()
    {
        if (!isExtendingJump || !jumpKeyHeld)
        {
            return;
        }

        float heldDuration = Time.time - jumpStartTime;

        if (heldDuration >= maximumHoldDuration || rb.linearVelocity.y <= 0f)
        {
            isExtendingJump = false;
            return;
        }

        float newVerticalVelocity =
            rb.linearVelocity.y
            + holdJumpAcceleration * Time.fixedDeltaTime;

        newVerticalVelocity = Mathf.Min(
            newVerticalVelocity,
            maximumUpwardVelocity
        );

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            newVerticalVelocity
        );
    }

    public bool IsGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayerMask
        ) != null;
    }

    private void UpdateGroundState()
    {
        bool isGrounded = IsGrounded();

        if (!hasGroundState)
        {
            hasGroundState = true;
            wasGrounded = isGrounded;
            hasBeenAirborne = !isGrounded;
            return;
        }

        if (!isGrounded)
        {
            hasBeenAirborne = true;
        }

        if (isGrounded && !wasGrounded && hasBeenAirborne)
        {
            hasBeenAirborne = false;
            Landed?.Invoke();
        }

        wasGrounded = isGrounded;
    }

    public void SetControlEnabled(bool isEnabled)
    {
        controlEnabled = isEnabled;

        if (!controlEnabled)
        {
            jumpKeyHeld = false;
            isExtendingJump = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}
