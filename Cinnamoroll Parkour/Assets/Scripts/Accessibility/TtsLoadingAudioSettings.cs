using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cinnamoroll Parkour/TTS Loading Audio Settings")]
public class TtsLoadingAudioSettings : ScriptableObject
{
    [Header("Wwise")]
    public AK.Wwise.Bank soundBank;
    public AK.Wwise.Event loadingEvent;
    public AK.Wwise.Event transitionStingerEvent;
    [Tooltip("名称与 Wwise 完全一致，全局范围 0–100。")]
    public string loadingRtpcName = "loadingRTPC";
    [Header("Wwise 音乐同步")]
    public string finishedCueName = "LoadingFinished";
    [Tooltip("开始播放前重置 State，避免上次 Finish 状态影响新一次加载。")]
    public string loadingStateGroup = "Loading_State";
    public string loadingInitialState = "Loop";
    [Min(1)] public float transitionTimeout = 30f;
    [Tooltip("Cue 后等待 Event 自然结束的最长时间，保留音效尾音。")]
    [Min(1)] public float tailTimeout = 15f;
    [Header("参考节拍（切换时机由 Wwise Cue 决定）")]
    [Min(1)] public float bpm = 90f;
    [Min(1)] public int beatsPerBar = 4;
    [Header("跳转场景")]
    public string startupTargetScene = "Main Menu";
    [Serializable] public class SceneRoute
    {
        public string sourceScene;
        [Tooltip("留空表示加载结束后继续当前关卡。")]
        public string targetScene;
    }
    public List<SceneRoute> sceneRoutes = new List<SceneRoute>();
    [Min(1)] public float initializationTimeout = 30f;
    public float BarSeconds => 60f / Mathf.Max(1f, bpm) * Mathf.Max(1, beatsPerBar);
    public bool HasLoadingEvent => loadingEvent != null && loadingEvent.IsValid();
    public bool HasStingerEvent => transitionStingerEvent != null && transitionStingerEvent.IsValid();
    public string TargetFor(string scene)
    {
        var route = sceneRoutes.Find(item => item != null && item.sourceScene == scene);
        return route != null ? route.targetScene : "";
    }
}
