using UnityEngine;

[RequireComponent(typeof(PlayerJump))]
[RequireComponent(typeof(AkGameObj))]
public class JumpVoicePlayer : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event jumpVoiceEvent;
    [SerializeField] private GameObject eventTarget;

    [Range(0f, 1f)]
    [Tooltip("1 means every successful jump plays a voice. 0.25 means 25% chance.")]
    [SerializeField] private float playChance = 0.25f;

    [Tooltip("Prevents voices from triggering too frequently.")]
    [SerializeField] private float cooldown = 0.2f;

    private PlayerJump playerJump;
    private float nextAllowedPlayTime;

    private void Awake()
    {
        playerJump = GetComponent<PlayerJump>();

        if (eventTarget == null)
        {
            Transform voiceChild = transform.Find("Vo");

            if (voiceChild != null)
            {
                eventTarget = voiceChild.gameObject;
            }
        }
    }

    private void OnEnable()
    {
        if (playerJump != null)
        {
            playerJump.JumpStarted += TryPlayJumpVoice;
        }
    }

    private void OnDisable()
    {
        if (playerJump != null)
        {
            playerJump.JumpStarted -= TryPlayJumpVoice;
        }
    }

    private void TryPlayJumpVoice()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (jumpVoiceEvent == null)
        {
            Debug.LogError(name + " has no jump voice Event configured.");
            return;
        }

        if (Time.time < nextAllowedPlayTime)
        {
            return;
        }

        if (Random.value > playChance)
        {
            return;
        }

        GameObject target = eventTarget != null ? eventTarget : gameObject;
        TtsText.TryPlay(gameObject);

        uint playingId = jumpVoiceEvent.Post(target);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                "Wwise could not play jump voice Event on "
                + target.name
                + "."
            );
            return;
        }

        nextAllowedPlayTime = Time.time + cooldown;
    }
}
