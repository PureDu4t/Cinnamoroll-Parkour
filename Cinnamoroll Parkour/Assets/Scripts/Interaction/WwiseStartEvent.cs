using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
[DisallowMultipleComponent]
public class WwiseStartEvent : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Bank bankToLoad;
    [SerializeField] private AK.Wwise.Event eventToPlay;
    [Tooltip("Controls only the Wwise start Event. TtsText on this object still plays at scene start.")]
    [SerializeField] private bool playStartEvent = true;
    [SerializeField] private bool lockPlayerDuringPlayback = true;

    private bool bankLoaded;
    private PlayerMovement playerMovement;
    private PlayerJump playerJump;
    private bool playerLocked;

    private IEnumerator Start()
    {
        while (!AkSoundEngine.IsInitialized())
        {
            yield return null;
        }

        if (bankToLoad == null || !bankToLoad.IsValid())
        {
            Debug.LogError(name + " is missing a valid Wwise SoundBank.");
            yield break;
        }

        var configuredText = GetComponent<TtsText>();
        bool usesTts = configuredText != null && configuredText.isActiveAndEnabled;
        if (playStartEvent && !usesTts && (eventToPlay == null || !eventToPlay.IsValid()))
        {
            Debug.LogError(name + " is missing a valid Wwise start Event.");
            yield break;
        }

        bankToLoad.Load();
        bankLoaded = true;

        yield return null;

        while (GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene)) yield return null;

        // A native Start trigger may already have started the same TTS.
        if (!usesTts || !configuredText.IsPlaying)
            TtsText.TryPlay(gameObject, lockPlayer: lockPlayerDuringPlayback);

        if (!playStartEvent)
        {
            yield break;
        }

        if (eventToPlay == null || !eventToPlay.IsValid()) yield break;

        // The TTS manager owns the player lock while speech is active.
        if (lockPlayerDuringPlayback && !usesTts)
        {
            playerMovement = FindObjectOfType<PlayerMovement>();
            playerJump = FindObjectOfType<PlayerJump>();
            SetPlayerControlEnabled(false);
        }

        uint playingId = eventToPlay.Post(
            gameObject,
            (uint)AkCallbackType.AK_EndOfEvent,
            OnWwiseCallback,
            this
        );

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(name + " could not play its Wwise start Event.");
            SetPlayerControlEnabled(true);
            yield break;
        }

        Debug.Log(
            name + " loaded its SoundBank and played its start Event. "
            + "Playing ID: " + playingId
        );
    }

    private void OnWwiseCallback(
        object cookie,
        AkCallbackType callbackType,
        AkCallbackInfo callbackInfo
    )
    {
        if (callbackType == AkCallbackType.AK_EndOfEvent)
        {
            SetPlayerControlEnabled(true);
        }
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (!isEnabled)
        {
            playerLocked = true;
        }
        else if (!playerLocked)
        {
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.SetControlEnabled(isEnabled);
        }

        if (playerJump != null)
        {
            playerJump.SetControlEnabled(isEnabled);
        }

        playerLocked = !isEnabled;
    }

    private void OnDestroy()
    {
        SetPlayerControlEnabled(true);

        if (bankLoaded && AkSoundEngine.IsInitialized())
        {
            bankToLoad.Unload();
        }
    }
}
