using UnityEngine;
using System.Threading.Tasks;

/// <summary>Designer-facing introduction text, with skip and repeat controls.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GameTtsManager))]
public class TtsLevelIntro : MonoBehaviour
{
    [InspectorName("中文开场文本")]
    [Tooltip("进入关卡时自动朗读。在非运行模式修改并保存场景。")]
    [TextArea(4, 12)]
    [SerializeField] private string chineseText = "这是第一关的语音测试。请在这里填写你的开场说明。";

    [InspectorName("语速")]
    [Range(0.5f, 4f)]
    [SerializeField] private float speechSpeed = 2f;

    [InspectorName("播放时锁定玩家")]
    [SerializeField] private bool lockPlayerDuringPlayback = true;

    private GameTtsManager tts;
    private PlayerMovement movement;
    private PlayerJump jump;
    private bool playerLocked;
    private int playbackVersion;

    private void Awake()
    {
        tts = GetComponent<GameTtsManager>();
    }

    private void Start()
    {
        movement = FindFirstObjectByType<PlayerMovement>();
        jump = FindFirstObjectByType<PlayerJump>();
        PlayIntroduction();
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (Input.GetKeyDown(KeyCode.Q)) StopIntroduction();
        else if (Input.GetKeyDown(KeyCode.R)) PlayIntroduction();
    }

    [ContextMenu("播放开场 TTS（运行时）")]
    public async void PlayIntroduction()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        int version = ++playbackVersion;
        while (GameSpeechSettings.IsChoosing && this != null && version == playbackVersion) await Task.Yield();
        if (this == null || !isActiveAndEnabled || version != playbackVersion) return;
        if (lockPlayerDuringPlayback && !string.IsNullOrWhiteSpace(chineseText)) SetPlayerLocked(true);
        try
        {
            await tts.SpeakAsync(chineseText, speechSpeed);
        }
        finally
        {
            if (version == playbackVersion) SetPlayerLocked(false);
        }
    }

    public void StopIntroduction()
    {
        playbackVersion++;
        if (tts != null) tts.Stop();
        SetPlayerLocked(false);
    }

    private void SetPlayerLocked(bool locked)
    {
        if (!locked && !playerLocked) return;
        playerLocked = locked;
        if (movement != null) movement.SetControlEnabled(!locked);
        if (jump != null) jump.SetControlEnabled(!locked);
    }

    private void OnDisable() => StopIntroduction();
}
