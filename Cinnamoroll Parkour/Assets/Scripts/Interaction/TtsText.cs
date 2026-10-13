using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Text and voice settings shared by existing event triggers on this object.</summary>
[DisallowMultipleComponent]
public class TtsText : MonoBehaviour, IAkEventPlaybackOverride
{
    public enum SpeechLanguage { Chinese, English }

    [InspectorName("中文文本"), TextArea(4, 12)]
    [SerializeField] private string chineseText;
    [InspectorName("英文文本"), TextArea(4, 12)]
    [SerializeField] private string englishText;
    [InspectorName("语言")]
    [SerializeField] private SpeechLanguage language;
    [InspectorName("语速"), Range(0.5f, 4f)]
    [SerializeField] private float speechSpeed = 2f;
    [InspectorName("使用玩家语言和语速")]
    [Tooltip("默认使用玩家进入游戏时选择的设置。取消勾选可为此文本指定固定语言和语速。")]
    [SerializeField] private bool usePlayerSpeechSettings = true;
    [InspectorName("播放时锁定玩家")]
    [SerializeField] private bool lockPlayerDuringPlayback;

    [Serializable]
    public class InlineSound
    {
        [InspectorName("名称")]
        public string name;
        [InspectorName("Wwise Event")]
        public AK.Wwise.Event soundEvent;
        [InspectorName("发声物体（留空使用当前物体）")]
        public GameObject eventTarget;
        [InspectorName("最长等待秒数"), Min(0.1f)]
        [Tooltip("正常等待 Event 播放结束。循环音效超过此时间会停止，继续朗读。")]
        public float maximumWaitSeconds = 30f;
    }

    [InspectorName("音效插入列表")]
    [Tooltip("文本中填写 [sfx:名称]，在此选择对应的 Wwise Event。[pause:0.5] 表示停顿半秒。")]
    [SerializeField] private List<InlineSound> inlineSounds = new List<InlineSound>();

    public enum StepKind { Speech, Sound, Pause }
    public sealed class PlaybackStep
    {
        public StepKind kind;
        public string text;
        public AK.Wwise.Event soundEvent;
        public GameObject eventTarget;
        public float seconds;
    }

    private static readonly Regex InlineMarker = new Regex(
        @"\[(sfx|pause):([^\]\r\n]*)\]", RegexOptions.IgnoreCase);

    private GameTtsManager manager;
    private int version;
    private int lastTriggerFrame = -1;
    private readonly List<Action> completions = new List<Action>();
    public bool HasText => isActiveAndEnabled && !string.IsNullOrWhiteSpace(CurrentText);
    public bool IsPlaying => manager != null && manager.IsSpeakingFor(this);
    private SpeechLanguage CurrentLanguage => usePlayerSpeechSettings ? GameSpeechSettings.Language : language;
    private float CurrentSpeed => usePlayerSpeechSettings ? GameSpeechSettings.Speed : speechSpeed;
    private string CurrentText => CurrentLanguage == SpeechLanguage.Chinese ? chineseText : englishText;
    private string CurrentProfile => CurrentLanguage == SpeechLanguage.Chinese
        ? "matcha-icefall-zh-baker" : "vits-piper-en_US-lessac-medium";

    private void OnEnable()
    {
        if (Application.isPlaying) manager = GameTtsManager.GetOrCreate();
    }

    internal void QueuePrewarm(GameTtsManager sceneManager)
    {
        if (!string.IsNullOrWhiteSpace(CurrentText))
            sceneManager.QueuePrewarm(BuildSteps(CurrentText), CurrentSpeed, CurrentProfile);
    }

    internal void AddPreparationJobs(List<GameTtsManager.PreparationRequest> requests)
    {
        if (string.IsNullOrWhiteSpace(CurrentText)) return;
        foreach (var step in BuildSteps(CurrentText))
            if (step.kind == StepKind.Speech)
                requests.Add(new GameTtsManager.PreparationRequest(step.text, Mathf.Clamp(CurrentSpeed, 0.5f, 4f), CurrentProfile));
    }

    public static bool TryPlay(GameObject owner, Action finished = null, bool? lockPlayer = null)
    {
        var text = owner != null ? owner.GetComponent<TtsText>() : null;
        if (text == null || !text.isActiveAndEnabled) return false;
        text.Play(finished, lockPlayer);
        return true;
    }

    [ContextMenu("播放 TTS（运行时）")]
    public void Preview() => Play();

    public async void Play(Action finished = null, bool? lockPlayer = null)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) { finished?.Invoke(); return; }
        int waitingVersion = version;
        while (this != null && isActiveAndEnabled && waitingVersion == version
            && (GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene)))
            await Task.Yield();
        if (this == null || !isActiveAndEnabled || waitingVersion != version)
        {
            finished?.Invoke();
            return;
        }
        if (!HasText)
        {
            Debug.LogWarning(name + " 的 TTS Text 没有填写当前语言的文本，已跳过语音。");
            finished?.Invoke();
            return;
        }
        manager = GameTtsManager.GetOrCreate();
        // Some old objects have both AkEvent Start and WwiseStartEvent.
        if (lastTriggerFrame == Time.frameCount && manager.IsSpeakingFor(this))
        {
            if (finished != null) completions.Add(finished);
            return;
        }
        NotifyStopped();
        string profile = CurrentProfile;
        try
        {
            var operation = manager.SpeakSequenceAsync(BuildSteps(CurrentText), CurrentSpeed, profile,
                lockPlayerDuringPlayback || (lockPlayer ?? false), this);
            int current = ++version;
            lastTriggerFrame = Time.frameCount;
            if (finished != null) completions.Add(finished);
            await operation;
            if (current == version) NotifyStopped();
        }
        catch (Exception exception)
        {
            Debug.LogError("TTS 文本播放失败：" + exception.Message);
            NotifyStopped();
        }
    }

    private List<PlaybackStep> BuildSteps(string text)
    {
        var steps = new List<PlaybackStep>();
        int position = 0;
        foreach (Match marker in InlineMarker.Matches(text))
        {
            AddSpeech(steps, text.Substring(position, marker.Index - position));
            string argument = marker.Groups[2].Value.Trim();
            if (string.Equals(marker.Groups[1].Value, "pause", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                    && !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f)
                    steps.Add(new PlaybackStep { kind = StepKind.Pause, seconds = seconds });
                else
                    Debug.LogWarning(name + " 的停顿标记无效：" + marker.Value + "，请使用 [pause:0.5] 格式。");
            }
            else
            {
                var sound = inlineSounds.Find(item => item != null
                    && string.Equals(item.name?.Trim(), argument, StringComparison.OrdinalIgnoreCase));
                if (sound != null && sound.soundEvent != null && sound.soundEvent.IsValid())
                    steps.Add(new PlaybackStep
                    {
                        kind = StepKind.Sound, text = argument, soundEvent = sound.soundEvent,
                        eventTarget = sound.eventTarget,
                        seconds = float.IsNaN(sound.maximumWaitSeconds) || float.IsInfinity(sound.maximumWaitSeconds)
                            ? 30f : Mathf.Max(0.1f, sound.maximumWaitSeconds)
                    });
                else
                    Debug.LogWarning(name + " 的音效标记 " + marker.Value + " 未绑定有效 Event，请检查音效插入列表。");
            }
            position = marker.Index + marker.Length;
        }
        AddSpeech(steps, text.Substring(position));
        return steps;
    }

    private static void AddSpeech(List<PlaybackStep> steps, string text)
    {
        // Do not synthesize punctuation left between two audio markers.
        if (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text, @"[\p{L}\p{N}]")) return;
        // Start reading after the first sentence instead of synthesizing the entire paragraph.
        // Keep punctuation with its sentence so pauses and intonation remain natural.
        foreach (Match sentence in Regex.Matches(text, @"[^。！？!?\r\n]+?(?:[。！？!?]+|\.(?=\s|$)|\r?\n|$)"))
        {
            string segment = sentence.Value.Trim();
            if (Regex.IsMatch(segment, @"[\p{L}\p{N}]"))
                steps.Add(new PlaybackStep { kind = StepKind.Speech, text = segment });
        }
    }

    public void Stop()
    {
        if (manager != null) manager.StopFor(this);
        if (GameSpeechSettings.IsChoosing) NotifyStopped();
    }

    public void NotifyStopped()
    {
        version++;
        var pending = completions.ToArray();
        completions.Clear();
        foreach (var completion in pending)
        {
            try { completion(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    bool IAkEventPlaybackOverride.TryPlay(AkEvent trigger)
    {
        if (!isActiveAndEnabled) return false;
        var playerLock = GetComponent<AkEventPlayerLock>();
        // Native AkEvent callbacks come from Wwise only, not from TTS completion.
        Play(lockPlayer: playerLock != null && playerLock.enabled
            ? playerLock.LockPlayerDuringPlayback : (bool?)null);
        return true;
    }

    bool IAkEventPlaybackOverride.TryStop(AkEvent trigger)
    {
        if (!isActiveAndEnabled) return false;
        Stop();
        return true;
    }

    private void OnDisable() => Stop();
}
