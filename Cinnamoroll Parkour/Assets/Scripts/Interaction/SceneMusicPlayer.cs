using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(AkGameObj))]
public class SceneMusicPlayer : MonoBehaviour
{
    public enum PlaybackCondition
    {
        [InspectorName("Scene 加载时")] SceneLoaded,
        [InspectorName("加载完毕（加载页结束后）")] LoadingFinished
    }

    [Header("Playback")]
    [SerializeField] private PlaybackCondition condition = PlaybackCondition.LoadingFinished;
    [Tooltip("留空则使用当前物体。")]
    [SerializeField] private GameObject eventTarget;
    [Min(1f), SerializeField] private float initializationTimeout = 30f;

    [Header("Optional SoundBank")]
    [SerializeField] private AK.Wwise.Bank bankToLoad;
    [SerializeField] private bool unloadBankOnDestroy;

    [Header("Wwise Events")]
    [FormerlySerializedAs("eventToPlay")]
    [SerializeField] private AK.Wwise.Event playMusicEvent;
    [SerializeField] private string playMusicEventName;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    private uint playingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    private bool bankLoaded;

    protected IEnumerator Start()
    {
        if (condition == PlaybackCondition.LoadingFinished)
        {
            yield return null;
            while (isActiveAndEnabled && (GameSpeechSettings.IsChoosing
                || TtsSceneLoading.IsLoading(gameObject.scene))) yield return null;
        }
        double deadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(1f, initializationTimeout);
        while (isActiveAndEnabled && !AkSoundEngine.IsInitialized()
            && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        if (!isActiveAndEnabled) yield break;
        if (!AkSoundEngine.IsInitialized())
        {
            Debug.LogError(name + " 无法播放场景 Event：Wwise 初始化超时。", this);
            yield break;
        }

        if (bankToLoad != null && bankToLoad.IsValid())
        {
            bankToLoad.Load();
            bankLoaded = true;
            yield return null;
        }

        PlayMusic();
    }

    private GameObject PlaybackTarget => eventTarget != null ? eventTarget : gameObject;

    private void PlayMusic()
    {
        var emitter = PlaybackTarget.GetComponent<AkGameObj>();
        if (emitter == null) emitter = PlaybackTarget.AddComponent<AkGameObj>();
        emitter.Register();
        TtsText.TryPlay(gameObject);
        if (!string.IsNullOrWhiteSpace(playMusicEventName))
        {
            playingId = AkSoundEngine.PostEvent(playMusicEventName, PlaybackTarget);
        }
        else if (playMusicEvent != null && playMusicEvent.IsValid())
        {
            playingId = playMusicEvent.Post(PlaybackTarget);
        }
        else
        {
            if (debugLogging)
            {
                Debug.Log(name + " has no valid scene music Play Event.");
            }

            return;
        }

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning(name + " could not play scene music.");
            return;
        }

        if (debugLogging)
        {
            Debug.Log(
                name + " started scene music. Playing ID: " + playingId
            );
        }
    }

    protected void OnDestroy()
    {
        if (bankLoaded
            && unloadBankOnDestroy
            && bankToLoad != null
            && bankToLoad.IsValid()
            && AkSoundEngine.IsInitialized())
        {
            bankToLoad.Unload();
        }
    }
}
