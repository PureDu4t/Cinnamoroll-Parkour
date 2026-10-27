using System;
using System.Threading;
using System.Threading.Tasks;
using PonyuDev.SherpaOnnx.Tts;
using UnityEngine;

/// <summary>Keeps the native speech engine alive while scene-local audio players change.</summary>
[DisallowMultipleComponent]
public sealed class SharedTtsRuntime : MonoBehaviour
{
    private static SharedTtsRuntime instance;
    public TtsService Service { get; private set; }
    public Task Initialization { get; private set; } = Task.CompletedTask;
    public SemaphoreSlim SynthesisGate { get; } = new SemaphoreSlim(1, 1);

    public static SharedTtsRuntime GetOrCreate()
    {
        if (instance != null) return instance;
        var root = new GameObject("Shared TTS Runtime");
        DontDestroyOnLoad(root);
        return root.AddComponent<SharedTtsRuntime>();
    }

    private void Awake()
    {
        instance = this;
        Service = new TtsService();
        Initialization = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try { await Service.InitializeAsync(); }
        catch (Exception exception) { Debug.LogError("TTS 初始化失败：" + exception.Message); }
    }

    private async void OnDestroy()
    {
        if (instance == this) instance = null;
        // Scene changes never dispose the native models. Release them only with this runtime,
        // after initialization and any in-flight synthesis have left the shared gate.
        try
        {
            await Initialization;
            await SynthesisGate.WaitAsync();
            try { Service?.Dispose(); }
            finally { SynthesisGate.Release(); }
        }
        catch (Exception exception) { Debug.LogWarning("TTS 引擎清理：" + exception.Message); }
    }
}
