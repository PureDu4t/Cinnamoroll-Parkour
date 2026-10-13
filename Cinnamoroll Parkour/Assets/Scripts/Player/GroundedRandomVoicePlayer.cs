using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
public class GroundedRandomVoicePlayer : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event randomVoiceEvent;
    [SerializeField] private AK.Wwise.Event stopRandomVoiceEvent;
    [SerializeField] private PlayerJump playerJump;

    [Tooltip("Seconds before the first random voice attempt.")]
    [SerializeField] private float initialDelay = 5f;

    [Tooltip("Shortest wait between random voice attempts.")]
    [SerializeField] private float minimumDelay = 5f;

    [Tooltip("Longest wait between random voice attempts.")]
    [SerializeField] private float maximumDelay = 10f;

    [Tooltip("Used only when Stop Random Voice Event is empty.")]
    [SerializeField] private int stopFadeMilliseconds = 100;

    private uint activePlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    private float nextAttemptTime;

    private void Awake()
    {
        if (playerJump == null)
        {
            playerJump = GetComponentInParent<PlayerJump>();
        }
    }

    private void OnEnable()
    {
        if (playerJump != null)
        {
            playerJump.JumpStarted += StopActiveVoice;
        }

        ScheduleNextAttempt(initialDelay);
    }

    private void OnDisable()
    {
        if (playerJump != null)
        {
            playerJump.JumpStarted -= StopActiveVoice;
        }

        StopActiveVoice();
    }

    private void Update()
    {
        if (playerJump != null && !playerJump.IsGrounded())
        {
            StopActiveVoice();
            return;
        }

        if (Time.time < nextAttemptTime)
        {
            return;
        }

        TryPlayRandomVoice();
        ScheduleNextAttempt(Random.Range(minimumDelay, maximumDelay));
    }

    private void TryPlayRandomVoice()
    {
        if (randomVoiceEvent == null)
        {
            return;
        }

        if (playerJump != null && !playerJump.IsGrounded())
        {
            return;
        }

        TtsText.TryPlay(gameObject);
        activePlayingId = randomVoiceEvent.Post(gameObject);
    }

    private void ScheduleNextAttempt(float delay)
    {
        float safeDelay = Mathf.Max(0f, delay);
        nextAttemptTime = Time.time + safeDelay;
    }

    private void StopActiveVoice()
    {
        var text = GetComponent<TtsText>();
        if (text != null) text.Stop();
        if (stopRandomVoiceEvent != null)
        {
            stopRandomVoiceEvent.Post(gameObject);
            activePlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
            return;
        }

        if (activePlayingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            return;
        }

        AkSoundEngine.StopPlayingID(
            activePlayingId,
            stopFadeMilliseconds,
            AkCurveInterpolation.AkCurveInterpolation_Linear
        );
        activePlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }
}
