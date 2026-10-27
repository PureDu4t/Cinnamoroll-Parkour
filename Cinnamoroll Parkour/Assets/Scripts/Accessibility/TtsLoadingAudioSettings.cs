using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cinnamoroll Parkour/TTS Loading Audio Settings")]
public class TtsLoadingAudioSettings : ScriptableObject
{
    [Header("Wwise")]
    public AK.Wwise.Bank soundBank;
    [Tooltip("启动总音乐容器的 Event；游戏启动时播放一次，并监听加载 Custom Cue 与目标音乐段 Entry。")]
    public AK.Wwise.Event startGameEvent;
    [Tooltip("每次开始加载时播放的独立 Event，例如 Play_Loading。")]
    public AK.Wwise.Event loadingEvent;
    [Tooltip("由菜单、选择、确认、返回等 UI 操作主动触发的通用 UI Event。")]
    public AK.Wwise.Event uiActionEvent;
    [Tooltip("名称与 Wwise 完全一致，全局范围 0–100。")]
    public string loadingRtpcName = "loadingRTPC";
    [Header("Wwise 音乐同步")]
    public string loadingStartedCueName = "LoadingStarted";
    public string finishedCueName = "LoadingFinished";
    [Tooltip("音乐 Switch Container 使用的 State Group。")]
    public string loadingStateGroup = "MusicStates";
    public string loadingInitialState = "Loading";
    [Min(1)] public float transitionTimeout = 30f;
    [Header("跳转场景")]
    public string startupTargetScene = "Main Menu";
    public string startupTargetState = "MainTitle";
    [Serializable] public class SceneRoute
    {
        public string sourceScene;
        [Tooltip("留空表示加载结束后继续当前关卡。")]
        public string targetScene;
        [Tooltip("加载完成后立即请求的音乐 State；Wwise 负责过渡，Custom Cue 负责放行场景。") ]
        public string targetState;
    }
    public List<SceneRoute> sceneRoutes = new List<SceneRoute>();
    [Min(1)] public float initializationTimeout = 30f;
    public bool HasLoadingEvent => loadingEvent != null && loadingEvent.IsValid();
    public string StateFor(string scene)
    {
        var route = sceneRoutes.Find(item => item != null && item.sourceScene == scene);
        if (route == null || string.IsNullOrWhiteSpace(route.targetState))
            throw new InvalidOperationException("请在 Tts Loading Audio Settings 配置场景的 Target State：" + scene);
        return route.targetState;
    }
    public string TargetFor(string scene)
    {
        var route = sceneRoutes.Find(item => item != null && item.sourceScene == scene);
        return route != null ? route.targetScene : "";
    }
}
