using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Prepares both menu scenes together and retains only their clips across scenes.</summary>
public static class SharedMenuSpeech
{
    [Serializable] public class Entry
    {
        public string chineseText, englishText;
        public bool usePlayerSpeechSettings = true;
        public int language;
        public float speechSpeed = 2f;
    }
    [Serializable] public class Catalog { public List<Entry> entries = new List<Entry>(); }
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public static bool IsMenu(string scene) => scene == "Main Menu" || scene == "Level Select";
    internal static bool TryGet(string key, out AudioClip clip) => clips.TryGetValue(key, out clip) && clip != null;
    internal static bool Owns(AudioClip clip) => clips.ContainsValue(clip);
    internal static void Store(string key, AudioClip clip) { if (clip != null) clips[key] = clip; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (var clip in clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
        clips.Clear();
    }

    private static List<GameTtsManager.PreparationRequest> Jobs(TtsText.SpeechLanguage language, float speed)
    {
        var asset = Resources.Load<TextAsset>("SharedMenuSpeech");
        if (asset == null) throw new InvalidOperationException("缺少菜单语音目录 SharedMenuSpeech，请在 Unity 中保存菜单场景后重试。");
        var catalog = JsonUtility.FromJson<Catalog>(asset.text);
        var jobs = new List<GameTtsManager.PreparationRequest>();
        foreach (var entry in catalog.entries)
        {
            bool chinese = entry.usePlayerSpeechSettings ? language == TtsText.SpeechLanguage.Chinese : entry.language == 0;
            TtsText.AddSpeechPreparationJobs(chinese ? entry.chineseText : entry.englishText,
                entry.usePlayerSpeechSettings ? speed : entry.speechSpeed,
                chinese ? "matcha-icefall-zh-baker" : "vits-piper-en_US-lessac-medium", jobs);
        }
        var unique = new List<GameTtsManager.PreparationRequest>();
        var keys = new HashSet<string>();
        foreach (var job in jobs)
            if (keys.Add(GameTtsManager.ClipKey(job.Text, job.Speed, job.Profile))) unique.Add(job);
        return unique;
    }

    public static bool IsReady()
    {
        if (Resources.Load<TextAsset>("SharedMenuSpeech") == null) return false;
        foreach (var job in Jobs(GameSpeechSettings.Language, GameSpeechSettings.Speed))
            if (!TryGet(GameTtsManager.ClipKey(job.Text, job.Speed, job.Profile), out _)) return false;
        return true;
    }

    public static async Task PrepareAsync(GameTtsManager manager, TtsText.SpeechLanguage language,
        float speed, Action<float> progress)
    {
        var jobs = Jobs(language, speed);
        progress?.Invoke(0);
        for (int i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            string key = GameTtsManager.ClipKey(job.Text, job.Speed, job.Profile);
            if (!TryGet(key, out _))
            {
                await manager.PrepareSpeechAsync(job.Text, job.Speed, job.Profile);
                if (manager == null) throw new OperationCanceledException("菜单语音加载场景已关闭。");
                manager.SharePreparedSpeech(job.Text, job.Speed, job.Profile);
            }
            progress?.Invoke((i + 1f) / jobs.Count);
        }
        progress?.Invoke(1);
    }
}
