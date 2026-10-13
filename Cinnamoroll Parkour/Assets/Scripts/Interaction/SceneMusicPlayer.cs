using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
[DisallowMultipleComponent]
public class SceneMusicPlayer : MonoBehaviour
{
    [Header("Optional SoundBank")]
    [SerializeField] private AK.Wwise.Bank bankToLoad;
    [SerializeField] private bool unloadBankOnDestroy;

    [Header("Wwise Events")]
    [SerializeField] private AK.Wwise.Event playMusicEvent;
    [SerializeField] private AK.Wwise.Event stopMusicEvent;
    [SerializeField] private string playMusicEventName;
    [SerializeField] private string stopMusicEventName;

    [Header("Stop Fallback")]
    [SerializeField] private int stopFadeMilliseconds = 200;
    [SerializeField] private AkCurveInterpolation stopFadeCurve =
        AkCurveInterpolation.AkCurveInterpolation_Linear;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    private uint playingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    private bool bankLoaded;
    private bool musicStarted;
    private bool musicStopped;

    private IEnumerator Start()
    {
        while (GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene)) yield return null;
        while (!AkSoundEngine.IsInitialized())
        {
            yield return null;
        }

        if (bankToLoad != null && bankToLoad.IsValid())
        {
            bankToLoad.Load();
            bankLoaded = true;
            yield return null;
        }

        PlayMusic();
    }

    private void PlayMusic()
    {
        TtsText.TryPlay(gameObject);
        if (!string.IsNullOrWhiteSpace(playMusicEventName))
        {
            playingId = AkSoundEngine.PostEvent(playMusicEventName, gameObject);
        }
        else if (playMusicEvent != null && playMusicEvent.IsValid())
        {
            playingId = playMusicEvent.Post(gameObject);
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

        musicStarted = true;
        musicStopped = false;

        if (debugLogging)
        {
            Debug.Log(
                name + " started scene music. Playing ID: " + playingId
            );
        }
    }

    private void StopMusic()
    {
        var text = GetComponent<TtsText>();
        if (text != null) text.Stop();
        if (!AkSoundEngine.IsInitialized())
        {
            return;
        }

        if (!musicStarted || musicStopped)
        {
            return;
        }

        musicStopped = true;

        if (!string.IsNullOrWhiteSpace(stopMusicEventName))
        {
            uint stopPlayingId = AkSoundEngine.PostEvent(
                stopMusicEventName,
                gameObject
            );

            if (debugLogging)
            {
                Debug.Log(
                    name + " posted scene music Stop Event by name. Playing ID: "
                    + stopPlayingId
                );
            }

            playingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
            return;
        }

        if (stopMusicEvent != null && stopMusicEvent.IsValid())
        {
            uint stopPlayingId = stopMusicEvent.Post(gameObject);

            if (debugLogging)
            {
                Debug.Log(
                    name + " posted scene music Stop Event. Playing ID: "
                    + stopPlayingId
                );
            }

            playingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
            return;
        }

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            return;
        }

        AkSoundEngine.StopPlayingID(
            playingId,
            stopFadeMilliseconds,
            stopFadeCurve
        );

        if (debugLogging)
        {
            Debug.Log(name + " stopped scene music by Playing ID.");
        }

        playingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }

    private void OnDisable()
    {
        StopMusic();
    }

    private void OnDestroy()
    {
        StopMusic();

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
