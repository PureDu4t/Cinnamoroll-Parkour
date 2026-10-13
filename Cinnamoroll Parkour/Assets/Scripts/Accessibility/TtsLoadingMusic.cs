using System;
using System.Threading.Tasks;
using UnityEngine;
using AK.Wwise.Unity.WwiseAddressables;

/// <summary>Wwise owns musical timing; its custom Cue releases the loading screen.</summary>
[RequireComponent(typeof(AkGameObj))]
public class TtsLoadingMusic : MonoBehaviour
{
    private TtsLoadingAudioSettings settings;
    private uint playingId;
    private bool bankLoaded, destroyed, rtpcFailed, finishRequested, released, eventEnded;
    private int playbackVersion;
    private TaskCompletionSource<bool> finishedCue;
    private double releaseDeadline;
    private float sentProgress = float.NaN;
    private float progress;
    public bool HasMusic => settings != null && settings.HasLoadingEvent;

    public static TtsLoadingMusic Create()
    {
        var root = new GameObject("TTS Loading Music");
        DontDestroyOnLoad(root);
        return root.AddComponent<TtsLoadingMusic>();
    }

    private void Awake() => settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");

    public async Task BeginAsync()
    {
        int version = ++playbackVersion;
        finishRequested = released = eventEnded = rtpcFailed = false;
        finishedCue = new TaskCompletionSource<bool>();
        sentProgress = float.NaN;
        if (playingId != 0 && AkSoundEngine.IsInitialized()) AkSoundEngine.StopPlayingID(playingId);
        playingId = 0;
        if (!HasMusic) { SetProgress(0); return; }
        double deadline = Time.realtimeSinceStartupAsDouble + settings.initializationTimeout;
        while (!destroyed && !AkSoundEngine.IsInitialized() && Time.realtimeSinceStartupAsDouble < deadline)
            await Task.Yield();
        if (destroyed) throw new OperationCanceledException();
        if (!AkSoundEngine.IsInitialized()) throw new InvalidOperationException("Wwise 未初始化，加载音乐无法播放。");
        if (settings.soundBank != null && settings.soundBank.IsValid())
        {
            var bank = settings.soundBank.WwiseObjectReference.AddressableBank;
            if (!bankLoaded || bank.LoadState == BankLoadState.Unloaded)
            {
                settings.soundBank.Load();
                bankLoaded = true;
            }
            while (!destroyed && bank.LoadState != BankLoadState.Loaded && Time.realtimeSinceStartupAsDouble < deadline)
                await Task.Yield();
            if (destroyed) throw new OperationCanceledException();
            if (bank.LoadState != BankLoadState.Loaded) throw new InvalidOperationException("加载音乐的 SoundBank 未加载成功。");
        }
        GetComponent<AkGameObj>().Register();
        if (!string.IsNullOrWhiteSpace(settings.loadingStateGroup) && !string.IsNullOrWhiteSpace(settings.loadingInitialState))
        {
            var result = AkSoundEngine.SetState(settings.loadingStateGroup, settings.loadingInitialState);
            if (result != AKRESULT.AK_Success)
                throw new InvalidOperationException("无法重置 Loading State：" + settings.loadingStateGroup + "/" + settings.loadingInitialState);
        }
        SetProgress(0);
        // Register on the original Play Event: the transition Event only sets a State.
        playingId = AkSoundEngine.PostEvent(settings.loadingEvent.Id, gameObject,
            (uint)(AkCallbackType.AK_MusicSyncUserCue | AkCallbackType.AK_EndOfEvent),
            (cookie, type, info) => OnMusicCallback(version, type, info), null);
        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
            throw new InvalidOperationException("加载音乐 Event 无法播放：" + settings.loadingEvent.Name);
    }

    private void OnMusicCallback(int version, AkCallbackType type, AkCallbackInfo info)
    {
        if (destroyed || version != playbackVersion) return;
        if (type == AkCallbackType.AK_EndOfEvent) eventEnded = true;
        else if (finishRequested && type == AkCallbackType.AK_MusicSyncUserCue
            && info is AkMusicSyncCallbackInfo musicInfo
            && string.Equals(musicInfo.userCueName, settings.finishedCueName, StringComparison.Ordinal))
            finishedCue.TrySetResult(true);
    }

    public void SetProgress(float normalized)
    {
        progress = Mathf.Clamp01(normalized);
        if (settings == null || released || rtpcFailed || !AkSoundEngine.IsInitialized() || string.IsNullOrWhiteSpace(settings.loadingRtpcName)) return;
        if (Mathf.Approximately(sentProgress, progress)) return;
        var result = AkSoundEngine.SetRTPCValue(settings.loadingRtpcName, progress * 100f);
        if (result == AKRESULT.AK_Success) sentProgress = progress;
        else
        {
            rtpcFailed = true;
            Debug.LogWarning("加载 RTPC 未设置成功，请在 Wwise 创建全局 0–100 参数：" + settings.loadingRtpcName);
        }
    }

    public async Task FinishAsync()
    {
        // A scene AkBank may force-unload the shared bank. Restore the music session
        // before requesting the transition instead of posting into an unloaded bank.
        if (HasMusic && settings.soundBank != null && settings.soundBank.IsValid()
            && settings.soundBank.WwiseObjectReference.AddressableBank.LoadState != BankLoadState.Loaded)
        {
            Debug.LogWarning("TTS Loading：共享 SoundBank 已被卸载，重新加载音乐后再请求过渡。");
            await BeginAsync();
        }
        SetProgress(1);
        if (!HasMusic) { Destroy(gameObject); return; }
        if (!settings.HasStingerEvent || string.IsNullOrWhiteSpace(settings.finishedCueName))
            throw new InvalidOperationException("请配置 Transition Event 和 Finished Cue Name。");
        if (!finishRequested)
        {
            finishRequested = true;
            uint id = AkSoundEngine.PostEvent(settings.transitionStingerEvent.Id, gameObject);
            if (id == AkSoundEngine.AK_INVALID_PLAYING_ID)
            {
                finishRequested = false;
                throw new InvalidOperationException("Loading Transition Event 无法触发。");
            }
        }
        double deadline = Time.realtimeSinceStartupAsDouble + settings.transitionTimeout;
        while (!destroyed && !finishedCue.Task.IsCompleted)
        {
            if (eventEnded) throw new InvalidOperationException("Loading Event 已结束，但未收到 Custom Cue：" + settings.finishedCueName);
            if (Time.realtimeSinceStartupAsDouble >= deadline)
                throw new TimeoutException("等待 Custom Cue 超时：" + settings.finishedCueName + "。请检查 Wwise Transition、Cue 名称及生成的 SoundBank。");
            await Task.Yield();
        }
        if (destroyed) throw new OperationCanceledException();
        await finishedCue.Task;
        // A persistent emitter carries post-exit audio across Scenes until the Event ends.
        released = true;
        releaseDeadline = Time.realtimeSinceStartupAsDouble + settings.tailTimeout;
        Debug.Log("TTS Loading：收到 " + settings.finishedCueName + "，允许进入场景。");
    }

    public void Cancel() { if (!destroyed) Destroy(gameObject); }

    private void Update()
    {
        if (released)
        {
            if (eventEnded || Time.realtimeSinceStartupAsDouble >= releaseDeadline) Destroy(gameObject);
            return;
        }
        if (!rtpcFailed && settings != null && AkSoundEngine.IsInitialized()) SetProgress(progress);
    }

    private void OnDestroy()
    {
        destroyed = true;
        ++playbackVersion;
        finishedCue?.TrySetCanceled();
        if (AkSoundEngine.IsInitialized())
        {
            if (playingId != 0 && !eventEnded) AkSoundEngine.StopPlayingID(playingId);
            // Bank.Unload() defaults to ignoreRefCount=true in this Addressables version.
            // Releasing an old stinger must not unload the next Scene's shared Main bank.
            if (bankLoaded && settings != null && settings.soundBank != null && settings.soundBank.IsValid())
                AkAddressableBankManager.Instance.UnloadBank(
                    settings.soundBank.WwiseObjectReference.AddressableBank,
                    ignoreRefCount: false, removeFromBankDictionary: false);
        }
    }
}
