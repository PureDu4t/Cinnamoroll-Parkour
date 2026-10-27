using UnityEngine;

/// <summary>Wwise feedback for explicit UI actions, never for raw key presses.</summary>
[DisallowMultipleComponent]
public sealed class UiActionSfx : MonoBehaviour
{
    private static UiActionSfx instance;
    private AK.Wwise.Event actionEvent;
    private AkGameObj emitter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var root = new GameObject("UI Action SFX");
        DontDestroyOnLoad(root);
        // Create AkGameObj only after Wwise is ready. Its startup registration is
        // not retried automatically in standalone builds when initialization was early.
        instance = root.AddComponent<UiActionSfx>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        var settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");
        if (settings == null)
        {
            Debug.LogWarning("UI action sound is not configured: TtsLoadingAudioSettings was not found.", this);
            return;
        }

        actionEvent = settings.uiActionEvent;
        if (actionEvent == null || !actionEvent.IsValid())
            Debug.LogWarning("UI action sound is not configured in TtsLoadingAudioSettings.", this);
    }

    public static void Play()
    {
        if (instance == null || !AkSoundEngine.IsInitialized()) return;
        if (instance.actionEvent != null && instance.actionEvent.IsValid())
        {
            if (!instance.EnsureEmitter()) return;
            uint playingId = instance.actionEvent.Post(instance.gameObject);
            if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
                Debug.LogWarning("UI action sound could not post Event: " + instance.actionEvent.Name, instance);
        }
    }

    private bool EnsureEmitter()
    {
        if (emitter == null)
            emitter = GetComponent<AkGameObj>() ?? gameObject.AddComponent<AkGameObj>();
        if (emitter.GameObjIsRegistered()) return true;
        var result = emitter.Register();
        if (result == AKRESULT.AK_Success) return true;
        Debug.LogWarning("UI action sound emitter could not register with Wwise: " + result, this);
        return false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
