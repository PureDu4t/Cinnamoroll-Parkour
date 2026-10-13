using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
public class KeyPressAkEvent : MonoBehaviour
{
    [SerializeField] private KeyCode triggerKey = KeyCode.E;
    [SerializeField] private AK.Wwise.Event eventToPlay;

    [Range(0f, 1f)]
    [Tooltip("1 means always play. 0.25 means 25% chance.")]
    [SerializeField] private float playChance = 1f;

    [Tooltip("Minimum time between successful plays.")]
    [SerializeField] private float cooldown = 0f;

    private float nextAllowedPlayTime;

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        // Lore input is routed by PlayerInteraction inside the interaction area.
        var lore = GetComponent<LoreInteractable>();
        if (lore != null && lore.isActiveAndEnabled)
        {
            return;
        }

        if (!Input.GetKeyDown(triggerKey))
        {
            return;
        }

        TryPlay();
    }

    private void TryPlay()
    {
        if (Time.time < nextAllowedPlayTime)
        {
            return;
        }

        if (Random.value > playChance)
        {
            return;
        }

        TtsText.TryPlay(gameObject);

        if (eventToPlay == null)
        {
            Debug.LogError(name + " has no Wwise Event configured.");
            return;
        }

        uint playingId = eventToPlay.Post(gameObject);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                "Wwise could not play key press Event on " + name + "."
            );
            return;
        }

        nextAllowedPlayTime = Time.time + cooldown;
    }
}
