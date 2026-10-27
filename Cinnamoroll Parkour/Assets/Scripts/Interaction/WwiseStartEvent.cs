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

    [Header("TTS")]
    [Tooltip("开启时，关卡加载完成后播放同一物体上的 TtsText。关闭时只播放 Wwise Event。")]
    [SerializeField] private bool triggerTts = true;

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

        // TTS is independent of the Wwise bank/event configuration.
        if (bankToLoad != null && bankToLoad.IsValid())
        {
            bankToLoad.Load();
            bankLoaded = true;
        }
        else
        {
            Debug.LogWarning(name + " is missing a valid Wwise SoundBank. TTS can still play.", this);
        }

        yield return null;
        while (GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene))
            yield return null;

        var configuredText = GetComponent<TtsText>();
        bool usesTts = triggerTts && configuredText != null && configuredText.HasText;
        // A native Start trigger may already have started the same TTS.
        if (usesTts && !configuredText.IsPlaying)
            TtsText.TryPlay(gameObject, lockPlayer: lockPlayerDuringPlayback);
        else if (triggerTts && !usesTts)
            Debug.LogWarning(name + " 的开场 TTS 没有可用的当前语言文本，请检查同一物体上的 TtsText。", this);

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
