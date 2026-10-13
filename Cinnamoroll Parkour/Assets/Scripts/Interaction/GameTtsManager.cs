using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using PonyuDev.SherpaOnnx.Tts;
using UnityEngine;

/// <summary>Scene-local speech generation, caching and Unity audio playback.</summary>
[DisallowMultipleComponent]
public class GameTtsManager : MonoBehaviour
{
    public readonly struct PreparationRequest
    {
        public readonly string Text, Profile;
        public readonly float Speed;
        public PreparationRequest(string text, float speed, string profile) { Text = text; Speed = speed; Profile = profile; }
    }
    private const string ChineseProfile = "matcha-icefall-zh-baker";
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private TtsService service;
    private AudioSource source;
    private Task initialization;
    private Task tail = Task.CompletedTask;
    private Task prewarming = Task.CompletedTask;
    private Task externalPreparation = Task.CompletedTask;
    private readonly SemaphoreSlim synthesisGate = new SemaphoreSlim(1, 1);
    private readonly Queue<(string text, float speed, string profile)> prewarmQueue =
        new Queue<(string, float, string)>();
    private int requestVersion;
    private bool destroyed;
    private UnityEngine.Object activeOwner;
    private PlayerMovement movement;
    private PlayerJump jump;
    private bool playerLocked;
    private TtsText lastText;
    private uint inlinePlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    public bool IsSpeakingFor(UnityEngine.Object owner) => activeOwner == owner;

    public static GameTtsManager GetOrCreate()
    {
        var manager = FindFirstObjectByType<GameTtsManager>();
        return manager != null ? manager : new GameObject("Game TTS Manager").AddComponent<GameTtsManager>();
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = false;

        // Wwise listeners do not replace Unity's AudioListener.
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            GameObject listenerObject = Camera.main != null ? Camera.main.gameObject : gameObject;
            listenerObject.AddComponent<AudioListener>();
        }

        service = new TtsService();
        initialization = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await service.InitializeAsync();
        }
        catch (Exception exception)
        {
            Debug.LogError("TTS 初始化失败：" + exception.Message);
        }
    }

    private void Start()
    {
        prewarming = PrewarmSceneAsync();
    }

    private async Task PrewarmSceneAsync()
    {
        while (!destroyed && (GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene))) await Task.Yield();
        if (destroyed) return;
        foreach (var text in FindObjectsByType<TtsText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (text.gameObject.scene == gameObject.scene) text.QueuePrewarm(this);
        await PrewarmAsync();
    }

    internal void QueuePrewarm(List<TtsText.PlaybackStep> steps, float speed, string profile)
    {
        foreach (var step in steps)
            if (step.kind == TtsText.StepKind.Speech)
                prewarmQueue.Enqueue((step.text, Mathf.Clamp(speed, 0.5f, 4f), profile));
    }

    public void RefreshScenePrewarm()
    {
        if (destroyed) return;
        prewarmQueue.Clear();
        foreach (var text in FindObjectsByType<TtsText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (text.gameObject.scene == gameObject.scene) text.QueuePrewarm(this);
        if (prewarming.IsCompleted) prewarming = PrewarmAsync();
    }

    private async Task PrewarmAsync()
    {
        await initialization;
        while (!destroyed && prewarmQueue.Count > 0)
        {
            // Foreground playback takes priority over the next background sentence.
            while (!destroyed && (activeOwner != null || GameSpeechSettings.IsChoosing)) await Task.Yield();
            if (destroyed) break;
            var item = prewarmQueue.Dequeue();
            try
            {
                await GetOrGenerateClipAsync(item.text, item.speed, item.profile,
                    () => !destroyed && activeOwner == null && !GameSpeechSettings.IsChoosing);
                if (!destroyed && !clips.ContainsKey(ClipKey(item.text, item.speed, item.profile)))
                    prewarmQueue.Enqueue(item);
            }
            catch (Exception exception)
            {
                if (!destroyed) Debug.LogWarning("TTS 预生成失败：" + exception.Message);
            }
            await Task.Yield();
        }
    }

    /// <summary>Replaces previous speech; completes when playback finishes or stops.</summary>
    public Task<bool> SpeakAsync(string text, float speed, string profile = ChineseProfile,
        bool lockPlayer = false, UnityEngine.Object owner = null)
    {
        var steps = new List<TtsText.PlaybackStep>();
        if (!string.IsNullOrWhiteSpace(text))
            steps.Add(new TtsText.PlaybackStep { kind = TtsText.StepKind.Speech, text = text });
        return SpeakSequenceAsync(steps, speed, profile, lockPlayer, owner);
    }

    public Task<bool> SpeakSequenceAsync(List<TtsText.PlaybackStep> steps, float speed,
        string profile = ChineseProfile, bool lockPlayer = false, UnityEngine.Object owner = null)
    {
        Stop();
        if (destroyed || steps == null || steps.Count == 0)
            return Task.FromResult(false);

        int version = requestVersion;
        activeOwner = owner;
        if (owner is TtsText textComponent) lastText = textComponent;
        SetPlayerLocked(lockPlayer);
        Task previous = tail;
        Task<bool> operation = SpeakAfterAsync(previous, steps, Mathf.Clamp(speed, 0.5f, 4f), profile, version, owner);
        tail = operation;
        return operation;
    }

    private string ClipKey(string text, float speed, string profile) => profile + "\n"
        + speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\n" + text;

    /// <summary>Prepare startup prompts without playing them; shares the normal speech cache.</summary>
    public Task PrepareSpeechAsync(string text, float speed, string profile)
    {
        Task operation = PrepareSpeechCoreAsync(externalPreparation, text, Mathf.Clamp(speed, 0.5f, 4f), profile);
        externalPreparation = operation;
        return operation;
    }

    private async Task PrepareSpeechCoreAsync(Task previous, string text, float speed, string profile)
    {
        try { await previous; } catch (Exception) { /* A failed prompt may be retried. */ }
        await initialization;
        if (destroyed) return;
        AudioClip clip = await GetOrGenerateClipAsync(text, speed, profile, () => !destroyed);
        if (!destroyed && clip == null) throw new InvalidOperationException("TTS 提示语音未能生成。");
    }

    private async Task<bool> SpeakAfterAsync(Task previous, List<TtsText.PlaybackStep> steps,
        float speed, string profile, int version, UnityEngine.Object owner)
    {
        Task preparation = Task.CompletedTask;
        var preparedClips = new Dictionary<TtsText.PlaybackStep, Task<AudioClip>>();
        try
        {
            // Serialize synthesis/model changes, including work canceled during native generation.
            await previous;
            if (!IsCurrent(version)) return false;
            bool hasSpeech = steps.Exists(step => step.kind == TtsText.StepKind.Speech);
            if (hasSpeech)
            {
                await initialization;
                if (!IsCurrent(version)) return false;
                // Keep scene-local audio for replay. Generate in order while earlier clips play.
                foreach (var step in steps)
                {
                    if (step.kind != TtsText.StepKind.Speech) continue;
                    Task<AudioClip> clipTask = PrepareClipAfterAsync(preparation, step.text, speed, profile, version);
                    preparedClips.Add(step, clipTask);
                    preparation = clipTask;
                }
            }

            foreach (var step in steps)
            {
                while (IsCurrent(version) && owner is TtsText && TtsSceneLoading.IsLoading(gameObject.scene)) await Task.Yield();
                if (!IsCurrent(version)) return false;
                switch (step.kind)
                {
                    case TtsText.StepKind.Speech:
                        AudioClip readyClip = await preparedClips[step];
                        if (!IsCurrent(version) || readyClip == null) return false;
                        source.clip = readyClip;
                        source.Play();
                        Debug.Log("TTS 开始播放：" + step.text);
                        await Task.Yield();
                        while (IsCurrent(version) && source.isPlaying) await Task.Yield();
                        if (!IsCurrent(version)) return false;
                        source.clip = null;
                        break;
                    case TtsText.StepKind.Pause:
                        double resumeAt = Time.realtimeSinceStartupAsDouble + step.seconds;
                        while (IsCurrent(version) && Time.realtimeSinceStartupAsDouble < resumeAt)
                            await Task.Yield();
                        break;
                    case TtsText.StepKind.Sound:
                        await PlayInlineSoundAsync(step, version, owner);
                        break;
                }
            }
            return IsCurrent(version);
        }
        catch (Exception exception)
        {
            if (!destroyed) Debug.LogError("TTS 播放失败：" + exception.Message);
            return false;
        }
        finally
        {
            // Native generation cannot be disposed or switched until its current work returns.
            try { await preparation; }
            catch (Exception) { /* The playback await reports generation errors. */ }
            if (IsCurrent(version))
            {
                StopInlineSound();
                SetPlayerLocked(false);
                activeOwner = null;
            }
        }
    }

    private async Task<AudioClip> PrepareClipAfterAsync(Task previous, string text, float speed,
        string profile, int version)
    {
        await previous;
        if (!IsCurrent(version)) return null;
        return await GetOrGenerateClipAsync(text, speed, profile, () => IsCurrent(version));
    }

    private async Task<AudioClip> GetOrGenerateClipAsync(string text, float speed, string profile,
        Func<bool> stillWanted)
    {
        string key = ClipKey(text, speed, profile);
        if (clips.TryGetValue(key, out AudioClip cached)) return cached;
        await synthesisGate.WaitAsync();
        try
        {
            if (destroyed || !stillWanted()) return null;
            if (clips.TryGetValue(key, out cached)) return cached;
            if (service.IsReady && service.ActiveProfile.profileName != profile)
                await service.SwitchProfileAsync(profile);
            if (destroyed || !stillWanted()) return null;
            if (!service.IsReady || service.ActiveProfile.profileName != profile)
                throw new InvalidOperationException("TTS 未就绪，请检查模型：" + profile);
            using (var result = await service.GenerateAsync(text, speed, 0))
            {
                if (destroyed) return null;
                if (result == null || !result.IsValid)
                    throw new InvalidOperationException("TTS 未生成有效音频，请检查文本和模型。");
                AudioClip clip = result.ToAudioClip("Game TTS");
                clips.Add(key, clip);
                return clip;
            }
        }
        finally { synthesisGate.Release(); }
    }

    private async Task PlayInlineSoundAsync(TtsText.PlaybackStep step, int version, UnityEngine.Object owner)
    {
        if (!AkSoundEngine.IsInitialized() || step.soundEvent == null || !step.soundEvent.IsValid())
        {
            Debug.LogWarning("TTS 插入音效无法播放：" + step.text + "，请检查 Wwise 初始化与 Event。");
            return;
        }
        var component = owner as Component;
        GameObject target = step.eventTarget != null ? step.eventTarget
            : component != null ? component.gameObject : gameObject;
        if (target.GetComponent<AkGameObj>() == null) target.AddComponent<AkGameObj>();
        var ended = new TaskCompletionSource<bool>();
        // Inline effects need an actual PlayingID. The Addressables Event.Post wrapper can
        // return a pending sentinel and leave an uncancellable Event queued after Q/timeout.
        // Scene AkBank components load the banks; post directly to the initialized engine.
        uint playingId = AkSoundEngine.PostEvent(step.soundEvent.Id, target, (uint)AkCallbackType.AK_EndOfEvent,
            (cookie, type, info) =>
            {
                if (type == AkCallbackType.AK_EndOfEvent) ended.TrySetResult(true);
            }, null);
        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning("TTS 插入音效播放失败：" + step.text + "，请检查 SoundBank 是否已加载。");
            return;
        }
        inlinePlayingId = playingId;
        double deadline = Time.realtimeSinceStartupAsDouble + step.seconds;
        while (IsCurrent(version) && !ended.Task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
            await Task.Yield();
        if (!IsCurrent(version)) return; // Stop() has already stopped this exact instance.
        if (!ended.Task.IsCompleted)
        {
            Debug.LogWarning("TTS 插入音效超过最长等待时间：" + step.text + "，已停止并继续朗读。");
            StopInlineSound();
        }
        else
            inlinePlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }

    private void StopInlineSound()
    {
        if (inlinePlayingId == AkSoundEngine.AK_INVALID_PLAYING_ID) return;
        if (AkSoundEngine.IsInitialized()) AkSoundEngine.StopPlayingID(inlinePlayingId);
        inlinePlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }

    private bool IsCurrent(int version) => !destroyed && version == requestVersion;

    public void Stop()
    {
        var stoppedText = activeOwner as TtsText;
        requestVersion++;
        StopInlineSound();
        SetPlayerLocked(false);
        activeOwner = null;
        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }
        if (stoppedText != null) stoppedText.NotifyStopped();
    }

    public void StopFor(UnityEngine.Object owner)
    {
        if (activeOwner == owner) Stop();
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (Input.GetKeyDown(KeyCode.Q)) Stop();
        else if (Input.GetKeyDown(KeyCode.R) && lastText != null) lastText.Play();
    }

    private void SetPlayerLocked(bool locked)
    {
        if (!locked && !playerLocked) return;
        if (locked)
        {
            movement = FindFirstObjectByType<PlayerMovement>();
            jump = FindFirstObjectByType<PlayerJump>();
        }
        playerLocked = locked;
        if (movement != null) movement.SetControlEnabled(!locked);
        if (jump != null) jump.SetControlEnabled(!locked);
    }

    private void ClearClips()
    {
        foreach (AudioClip clip in clips.Values)
        {
            if (clip != null) Destroy(clip);
        }
        clips.Clear();
    }

    private void OnDisable() => Stop();

    private async void OnDestroy()
    {
        destroyed = true;
        Stop();
        ClearClips();
        // Avoid freeing native ONNX objects during initialization or synthesis.
        try
        {
            await initialization;
            await tail;
            await prewarming;
            try { await externalPreparation; } catch (Exception) { /* Reported by the loading UI. */ }
        }
        finally
        {
            service?.Dispose();
        }
    }
}
