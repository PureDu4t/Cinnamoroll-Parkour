using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AkGameObj))]
public class AkTriggerEnter2D : MonoBehaviour
{
    [Header("Wwise")]
    [Tooltip("Wwise Event played when the player enters this 2D trigger.")]
    [SerializeField] private AK.Wwise.Event eventToPlay;

    [Header("TTS")]
    [Tooltip("开启时，同时触发当前物体上的 TtsText；关闭时只播放 Wwise Event。")]
    [SerializeField] private bool triggerTts = true;

    [Header("Player Filter")]
    [Tooltip("Only objects with this tag can trigger the Event.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("If enabled, the Event can play only once.")]
    [SerializeField] private bool playOnlyOnce;

    private bool hasPlayed;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                name
                + " uses AkTriggerEnter2D, so its Collider2D "
                + "has been changed to Is Trigger."
            );

            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playOnlyOnce && hasPlayed)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        bool ttsTriggered = triggerTts && TtsText.TryPlay(gameObject);
        if (ttsTriggered) hasPlayed = true;

        if (eventToPlay == null || !eventToPlay.IsValid())
        {
            if (!ttsTriggered)
                Debug.LogError(name + " is missing a valid Wwise Event.");
            return;
        }

        uint playingId = eventToPlay.Post(gameObject);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                "Wwise could not play the 2D trigger Event on "
                + name + "."
            );
            return;
        }

        hasPlayed = true;

        Debug.Log(
            "AkTriggerEnter2D played an Event on " + name
            + ". Playing ID: " + playingId
        );
    }
}
