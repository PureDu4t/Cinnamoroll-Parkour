using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public class TtsSceneLoading : MonoBehaviour
{
    private static readonly HashSet<int> loadingScenes = new HashSet<int>();
    private static int closedFrame = -1;
    public static bool BlocksInput => loadingScenes.Count > 0 || Time.frameCount == closedFrame;
    public static bool IsLoading(Scene scene) => loadingScenes.Contains(scene.handle);
    private GameTtsManager manager;
    private TtsLoadingMusic music;
    private Texture2D background;
    private GUIStyle textStyle;
    private float progress;
    private bool failed, destroyed, finished;
    private string status = "Loading...";
    private int sceneHandle;
    private readonly TtsSceneTransition sceneTransition = new TtsSceneTransition();
    private readonly List<AkEvent> deferredStartVoices = new List<AkEvent>();
    [Tooltip("填写则覆盖全局配置。留空使用 TtsLoadingAudioSettings 的 Scene Routes。")]
    [SerializeField] private string sceneAfterLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        loadingScenes.Clear();
        closedFrame = -1;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool level = scene.name.StartsWith("Level ", StringComparison.Ordinal)
            && int.TryParse(scene.name.Substring(6), out _);
        if (!level && scene.name != "Tutorial") return;
        loadingScenes.Add(scene.handle);
        foreach (var existing in FindObjectsByType<TtsSceneLoading>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.gameObject.scene == scene && existing.isActiveAndEnabled) return;
        var root = new GameObject("TTS Level Loading");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<TtsSceneLoading>();
    }

    private void Awake()
    {
        sceneHandle = gameObject.scene.handle;
        loadingScenes.Add(sceneHandle);
        DeferStartVoices();
        background = PixelArtSettings.BackgroundOr(Resources.Load<Texture2D>("SpeechSetupBackground"));
        var art = PixelArtSettings.Current;
        if (art != null && art.enabled && art.BackgroundForScene(gameObject.scene.name) != null)
            background = art.BackgroundForScene(gameObject.scene.name);
        music = TtsLoadingMusic.Create();
    }

    private void Start() => LoadLevel();

    private void DeferStartVoices()
    {
        // sceneLoaded runs before AkEvent.Start. Remove only this Event's automatic
        // Start trigger in memory; its other triggers and Inspector settings remain usable.
        foreach (var voice in FindObjectsByType<AkEvent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (voice.gameObject.scene.handle != sceneHandle || !voice.isActiveAndEnabled
                || voice.data == null || voice.data.Name != "Play_StartVo"
                || !voice.triggerList.Contains(AkTriggerHandler.START_TRIGGER_ID)) continue;
            voice.triggerList.RemoveAll(trigger => trigger == AkTriggerHandler.START_TRIGGER_ID);
            deferredStartVoices.Add(voice);
        }
    }

    private void PlayDeferredStartVoices()
    {
        foreach (var voice in deferredStartVoices)
        {
            if (voice == null || !voice.isActiveAndEnabled) continue;
            voice.HandleEvent(null);
            Debug.Log("TTS Loading：关卡加载完成后播放 Play_StartVo。", voice);
        }
        deferredStartVoices.Clear();
    }

    private async void LoadLevel()
    {
        failed = false;
        status = "Loading...";
        progress = 0;
        try
        {
            while (!destroyed && GameSpeechSettings.IsChoosing) await Task.Yield();
            if (destroyed) return;
            manager = GameTtsManager.GetOrCreate();
            await music.BeginAsync();
            if (destroyed) return;
            var jobs = new List<GameTtsManager.PreparationRequest>();
            foreach (var text in FindObjectsByType<TtsText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.gameObject.scene.handle == sceneHandle) text.AddPreparationJobs(jobs);
            var unique = new List<GameTtsManager.PreparationRequest>();
            var keys = new HashSet<string>();
            foreach (var job in jobs)
                if (keys.Add(job.Profile + "\n" + job.Speed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\n" + job.Text)) unique.Add(job);
            if (unique.Count == 0) { progress = 1; music.SetProgress(1); }
            for (int i = 0; i < unique.Count; i++)
            {
                var job = unique[i];
                await manager.PrepareSpeechAsync(job.Text, job.Speed, job.Profile);
                if (destroyed) return;
                progress = (i + 1f) / unique.Count;
                music.SetProgress(progress);
            }
            var settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");
            string target = !string.IsNullOrWhiteSpace(sceneAfterLoading) ? sceneAfterLoading
                : settings != null ? settings.TargetFor(gameObject.scene.name) : "";
            status = "正在准备场景 / Preparing scene…";
            await sceneTransition.PrepareAsync(target);
            if (destroyed) return;
            status = "加载完成，等待音乐过渡 / Finishing music transition…";
            await music.FinishAsync();
            if (destroyed) return;
            finished = true;
            loadingScenes.Remove(sceneHandle);
            closedFrame = Time.frameCount;
            if (!sceneTransition.WillChangeScene) PlayDeferredStartVoices();
            sceneTransition.Activate();
        }
        catch (Exception exception)
        {
            if (destroyed) return;
            failed = true;
            status = "加载失败，按 R 重试 / Loading failed. Press R to retry.";
            Debug.LogError("关卡 TTS 加载失败：" + exception.Message);
        }
    }

    private void Update()
    {
        if (failed && Input.GetKeyDown(KeyCode.R)) LoadLevel();
    }

    private void OnGUI()
    {
        if (finished) return;
        if (background != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background, ScaleMode.ScaleAndCrop);
        var theme = PixelArtSettings.Current;
        if (textStyle == null) textStyle = PixelArtUi.Text(24, theme != null ? theme.ink : new Color(.15f, .25f, .4f), TextAnchor.MiddleCenter);
        float width = Mathf.Min(650, Screen.width * 0.8f);
        float x = (Screen.width - width) / 2;
        float y = Screen.height * 0.4f;
        PixelArtUi.Frame(new Rect(x - 30, y - 35, width + 60, 235),
            theme != null ? theme.panel : Color.white, theme != null ? theme.border : new Color(.7f, .8f, .95f));
        GUI.Label(new Rect(x, y, width, 55), "Loading... " + Mathf.FloorToInt(progress * 100) + "%", textStyle);
        PixelArtUi.Progress(new Rect(x, y + 75, width, 32), progress);
        GUI.Label(new Rect(x, y + 115, width, 75), status, textStyle);
    }

    private void OnDestroy()
    {
        destroyed = true;
        loadingScenes.Remove(sceneHandle);
        if (!finished && music != null) music.Cancel();
    }
}

/// <summary>Keep a prepared Scene pending across retries; activate only after the music Cue.</summary>
internal sealed class TtsSceneTransition
{
    private AsyncOperation operation;
    private string preparedTarget;
    public bool WillChangeScene => operation != null;

    public async Task PrepareAsync(string target)
    {
        target = target?.Trim();
        var current = SceneManager.GetActiveScene();
        // A route to this same Scene means reveal the prepared level, not reload it forever.
        if (string.IsNullOrWhiteSpace(target) || target == current.name || target == current.path) return;
        if (!Application.CanStreamedLevelBeLoaded(target))
            throw new InvalidOperationException("目标 Scene 未加入构建列表：" + target);
        if (operation == null)
        {
            preparedTarget = target;
            operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("无法预加载 Scene：" + target);
            operation.allowSceneActivation = false;
        }
        else if (preparedTarget != target)
            throw new InvalidOperationException("已有等待激活的 Scene：" + preparedTarget + "。请停止运行后更换目标。");
        while (!operation.isDone && operation.progress < 0.9f) await Task.Yield();
    }

    public void Activate()
    {
        if (operation != null) operation.allowSceneActivation = true;
    }
}
