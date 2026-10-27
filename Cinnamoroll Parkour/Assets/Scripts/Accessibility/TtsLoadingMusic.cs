using System;
using System.Threading.Tasks;
using UnityEngine;
using AK.Wwise.Unity.WwiseAddressables;

/// <summary>Wwise owns musical timing; its custom Cue releases the loading screen.</summary>
[RequireComponent(typeof(AkGameObj))]
public class TtsLoadingMusic : MonoBehaviour
{
    public static event Action<string, double> DestinationMusicEntered;
    public static bool HasDestinationMusicEntry { get; private set; }
    public static string LastDestinationMusicState { get; private set; }
    public static double LastDestinationMusicEntryDspTime { get; private set; }

    private TtsLoadingAudioSettings settings;
    private uint playingId;
    private bool bankLoaded, destroyed, rtpcFailed, finishRequested, released, eventEnded;
    private int playbackVersion;
    private TaskCompletionSource<bool> finishedCue;
    private TaskCompletionSource<bool> loadingStarted;
    private bool awaitingDestinationMusicEntry;
    private string requestedDestinationState;
    private float sentProgress = float.NaN;
    private float progress;
    private double diagnosticLastTime;
    private double diagnosticLastDsp;
    private double diagnosticUntil;
    public bool HasMusic => settings != null && settings.startGameEvent != null && settings.startGameEvent.IsValid();

    public static TtsLoadingMusic Create()
    {
        var existing = FindFirstObjectByType<TtsLoadingMusic>();
        if (existing != null) return existing;
        var root = new GameObject("TTS Loading Music");
        DontDestroyOnLoad(root);
        return root.AddComponent<TtsLoadingMusic>();
    }

    private void Awake() => settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");

    public async Task BeginAsync()
    {
        TraceAudioWindow("Begin loading");
        finishRequested = released = rtpcFailed = false;
        awaitingDestinationMusicEntry = false;
        requestedDestinationState = null;
        HasDestinationMusicEntry = false;
        LastDestinationMusicState = null;
        LastDestinationMusicEntryDspTime = 0;
        finishedCue = new TaskCompletionSource<bool>();
        loadingStarted = new TaskCompletionSource<bool>();
        sentProgress = float.NaN;
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
                if (bank.LoadState == BankLoadState.Unloaded)
                {
                    ++playbackVersion;
                    playingId = 0;
                }
                settings.soundBank.Load();
                bankLoaded = true;
            }
            while (!destroyed && bank.LoadState != BankLoadState.Loaded && Time.realtimeSinceStartupAsDouble < deadline)
                await Task.Yield();
            if (destroyed) throw new OperationCanceledException();
            if (bank.LoadState != BankLoadState.Loaded) throw new InvalidOperationException("加载音乐的 SoundBank 未加载成功。");
        }
        GameAudioSettings.ApplyWwiseVolumes();
        GetComponent<AkGameObj>().Register();
        if (!string.IsNullOrWhiteSpace(settings.loadingStateGroup) && !string.IsNullOrWhiteSpace(settings.loadingInitialState))
        {
            var result = AkSoundEngine.SetState(settings.loadingStateGroup, settings.loadingInitialState);
            if (result != AKRESULT.AK_Success)
                throw new InvalidOperationException("无法重置 Loading State：" + settings.loadingStateGroup + "/" + settings.loadingInitialState);
        }
        SetProgress(0);
        // Start_Game owns the persistent music instance and its Cue callbacks.
        // Play_Loading is a separate request and must not replace that PlayingID.
        if (playingId != 0 && AkSoundEngine.GetEventIDFromPlayingID(playingId) == 0)
            eventEnded = true;
        if (playingId == 0 || eventEnded)
        {
            eventEnded = false;
            int version = ++playbackVersion;
            playingId = AkSoundEngine.PostEvent(settings.startGameEvent.Id, gameObject,
                (uint)(AkCallbackType.AK_MusicSyncUserCue | AkCallbackType.AK_MusicSyncEntry | AkCallbackType.AK_EndOfEvent),
                (cookie, type, info) => OnMusicCallback(version, type, info), null);
            if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
                throw new InvalidOperationException("Start Game Event 无法播放：" + settings.startGameEvent.Name);
        }
        if (settings.HasLoadingEvent)
        {
            uint loadingRequestId = AkSoundEngine.PostEvent(settings.loadingEvent.Id, gameObject);
            if (loadingRequestId == AkSoundEngine.AK_INVALID_PLAYING_ID)
                throw new InvalidOperationException("Loading Event 无法播放：" + settings.loadingEvent.Name);
        }
        // A fast/cached load must not overwrite Loading before the audio thread
        // actually enters it. The loop's Cue acknowledges the requested state.
        double entryDeadline = Time.realtimeSinceStartupAsDouble + settings.transitionTimeout;
        while (!destroyed && !loadingStarted.Task.IsCompleted)
        {
            if (eventEnded) throw new InvalidOperationException("总音乐已结束，无法进入 Loading。");
            if (Time.realtimeSinceStartupAsDouble >= entryDeadline)
                throw new TimeoutException("未收到 " + settings.loadingStartedCueName + "，请检查 Loading 路径及 SoundBank。");
            await Task.Yield();
        }
        if (destroyed) throw new OperationCanceledException();
        await loadingStarted.Task;
        Debug.Log("TTS Loading：Loading 音乐已开始，可准备目标关卡。");
    }

    private void OnMusicCallback(int version, AkCallbackType type, AkCallbackInfo info)
    {
        if (destroyed || version != playbackVersion) return;
        if (type == AkCallbackType.AK_EndOfEvent) eventEnded = true;
        else if (type == AkCallbackType.AK_MusicSyncEntry && info is AkMusicSyncCallbackInfo
            && awaitingDestinationMusicEntry)
        {
            awaitingDestinationMusicEntry = false;
            HasDestinationMusicEntry = true;
            LastDestinationMusicState = requestedDestinationState;
            LastDestinationMusicEntryDspTime = AudioSettings.dspTime;
            Debug.Log("TTS Loading：目标音乐段已进入 State " + requestedDestinationState
                + "；Unity DSP 时间 " + LastDestinationMusicEntryDspTime.ToString("F3") + " 秒。");
            DestinationMusicEntered?.Invoke(LastDestinationMusicState, LastDestinationMusicEntryDspTime);
        }
        else if (type == AkCallbackType.AK_MusicSyncUserCue && info is AkMusicSyncCallbackInfo musicInfo)
        {
            string cueName = musicInfo.userCueName;
            Debug.Log("TTS Loading：Event " + settings.startGameEvent.Name + " 收到 Custom Cue：" + cueName);
            if (string.Equals(cueName?.Trim(), settings.loadingStartedCueName?.Trim(), StringComparison.Ordinal))
                loadingStarted?.TrySetResult(true);
            if (finishRequested && string.Equals(cueName?.Trim(), settings.finishedCueName?.Trim(), StringComparison.Ordinal))
            {
                awaitingDestinationMusicEntry = true;
                finishedCue.TrySetResult(true);
            }
        }
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

    public async Task FinishAsync(string targetState)
    {
        // A scene AkBank may force-unload the shared bank. Restore the music session
        // before requesting the transition instead of posting into an unloaded bank.
        if (HasMusic && settings.soundBank != null && settings.soundBank.IsValid()
            && settings.soundBank.WwiseObjectReference.AddressableBank.LoadState != BankLoadState.Loaded)
        {
            Debug.LogWarning("TTS Loading：共享 SoundBank 已被卸载，重新加载音乐后再请求过渡。");
            await BeginAsync();
        }
        if (HasMusic && (playingId == 0 || eventEnded))
            await BeginAsync();
        SetProgress(1);
        if (!HasMusic)
            throw new InvalidOperationException("请配置播放总 Music Switch Container 的 Start Game Event；收到 Custom Cue 前不会切换场景。");
        if (string.IsNullOrWhiteSpace(settings.finishedCueName)
            || string.IsNullOrWhiteSpace(settings.loadingStateGroup) || string.IsNullOrWhiteSpace(targetState))
            throw new InvalidOperationException("请配置 State Group、Target State 和 Finished Cue Name。");
        if (!finishRequested)
        {
            TraceAudioWindow("Request destination: " + targetState);
            // Arm the callback before requesting the destination State.
            finishRequested = true;
            awaitingDestinationMusicEntry = false;
            requestedDestinationState = targetState;
            var result = AkSoundEngine.SetState(settings.loadingStateGroup, targetState);
            if (result != AKRESULT.AK_Success)
            {
                finishRequested = false;
                throw new InvalidOperationException("无法切换音乐 State：" + settings.loadingStateGroup + "/" + targetState);
            }
            Debug.Log("TTS Loading：场景尚未激活，已请求音乐 State " + settings.loadingStateGroup + "/" + targetState
                + "；等待 " + settings.finishedCueName + "。监听 Event：" + settings.startGameEvent.Name);
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
        // The unified music emitter continues playing the destination music across Scenes.
        released = true;
        Debug.Log("TTS Loading：收到 " + settings.finishedCueName + "，允许进入场景。");
    }

    public void Cancel() { if (!destroyed) Destroy(gameObject); }

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        double dsp = AudioSettings.dspTime;
        if (diagnosticLastTime > 0 && now < diagnosticUntil && now - diagnosticLastTime > 0.1)
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, this,
                "[Music transition] Main thread gap={0:F0} ms; Unity DSP advance={1:F0} ms; scene={2}; focused={3}; music ended={4}",
                (now - diagnosticLastTime) * 1000, (dsp - diagnosticLastDsp) * 1000,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, Application.isFocused, eventEnded);
        diagnosticLastTime = now;
        diagnosticLastDsp = dsp;
        if (released) return;
        if (!rtpcFailed && settings != null && AkSoundEngine.IsInitialized()) SetProgress(progress);
    }

    private void TraceAudioWindow(string action)
    {
        diagnosticUntil = Time.realtimeSinceStartupAsDouble + 10;
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this,
            "[Music transition] {0}; realtime={1:F3}; DSP={2:F3}; focused={3}",
            action, Time.realtimeSinceStartupAsDouble, AudioSettings.dspTime, Application.isFocused);
    }

    private void OnApplicationFocus(bool focused) => TraceAudioWindow("Window focus: " + focused);
    private void OnApplicationPause(bool paused) => TraceAudioWindow("Application pause: " + paused);

    private void OnDestroy()
    {
        destroyed = true;
        DestinationMusicEntered = null;
        HasDestinationMusicEntry = false;
        LastDestinationMusicState = null;
        LastDestinationMusicEntryDspTime = 0;
        ++playbackVersion;
        finishedCue?.TrySetCanceled();
        loadingStarted?.TrySetCanceled();
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
