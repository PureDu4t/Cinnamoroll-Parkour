using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
public class KeyPressWwiseSceneAction : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode triggerKey = KeyCode.E;

    [Header("Wwise")]
    [SerializeField] private AK.Wwise.Event eventToPlay;

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        // PlayerInteraction handles Lore input only inside its interaction area.
        // A legacy global key listener must not play this object's TTS elsewhere.
        var lore = GetComponent<LoreInteractable>();
        if (lore != null && lore.isActiveAndEnabled)
        {
            return;
        }

        if (!Input.GetKeyDown(triggerKey))
        {
            return;
        }

        PlayEventIfConfigured();
    }

    private void PlayEventIfConfigured()
    {
        TtsText.TryPlay(gameObject);
        if (eventToPlay == null || !eventToPlay.IsValid())
        {
            return;
        }

        uint playingId = eventToPlay.Post(gameObject);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning(
                name + " tried to play a Wwise Event, but Wwise returned an invalid playing ID."
            );
        }
    }
}
