using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    [Header("Input")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode moveRightKey = KeyCode.RightArrow;

    [Header("Visual Facing")]
    [SerializeField] private SpriteRenderer characterSprite;

    [Tooltip("Use the value that makes the sprite face right in its idle/default setup.")]
    [SerializeField] private bool flipXWhenFacingRight = true;

    private Rigidbody2D rb;
    private float horizontalInput;
    private bool controlEnabled = true;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public int LastMoveDirection { get; private set; } = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (characterSprite == null)
        {
            characterSprite = GetComponentInChildren<SpriteRenderer>(true);
        }

        startPosition = transform.position;
        startRotation = transform.rotation;
        UpdateVisualFacing();
    }

    private void Update()
    {
        if (!controlEnabled || GameSpeechSettings.BlocksGameInput)
        {
            horizontalInput = 0f;
            return;
        }

        horizontalInput = GetHorizontalInput();

        if (horizontalInput > 0f)
        {
            LastMoveDirection = 1;
            UpdateVisualFacing();
        }
        else if (horizontalInput < 0f)
        {
            LastMoveDirection = -1;
            UpdateVisualFacing();
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    public void SetControlEnabled(bool isEnabled)
    {
        controlEnabled = isEnabled;

        if (!controlEnabled)
        {
            horizontalInput = 0f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    public void ResetToStartPosition()
    {
        horizontalInput = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = startPosition;
        rb.rotation = startRotation.eulerAngles.z;
        transform.SetPositionAndRotation(startPosition, startRotation);
        UpdateVisualFacing();
    }

    private void UpdateVisualFacing()
    {
        if (characterSprite == null)
        {
            return;
        }

        characterSprite.flipX =
            LastMoveDirection >= 0
                ? flipXWhenFacingRight
                : !flipXWhenFacingRight;
    }

    private float GetHorizontalInput()
    {
        bool leftPressed = Input.GetKey(moveLeftKey);
        bool rightPressed = Input.GetKey(moveRightKey);

        if (leftPressed == rightPressed)
        {
            return 0f;
        }

        return rightPressed ? 1f : -1f;
    }
}
