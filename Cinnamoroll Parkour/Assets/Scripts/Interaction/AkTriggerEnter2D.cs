using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AkGameObj))]
public class AkTriggerEnter2D : MonoBehaviour
{
    [Header("Wwise")]
    [Tooltip("Wwise Event played when the player enters this 2D trigger.")]
    [SerializeField] private AK.Wwise.Event eventToPlay;

    [Tooltip("仅用于 Stage1 等音乐初始化区域：资源准备完成后、请求目标音乐 State 前发送此 Event。普通碰撞区域不要开启。")]
    [SerializeField] private bool initializeBeforeMusicTransition;

    [Header("TTS")]
    [Tooltip("开启时，同时触发当前物体上的 TtsText；关闭时只播放 Wwise Event。")]
    [SerializeField] private bool triggerTts = true;

    [Header("Player Filter")]
    [Tooltip("Only objects with this tag can trigger the Event.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("If enabled, the Event can play only once.")]
    [SerializeField] private bool playOnlyOnce;

    private bool hasPlayed;
    private bool musicInitialized;
    private readonly HashSet<Collider2D> loadingOverlaps = new HashSet<Collider2D>();

    public void InitializeMusicForLoading()
    {
        if (!initializeBeforeMusicTransition || musicInitialized || !isActiveAndEnabled) return;
        if (eventToPlay == null || !eventToPlay.IsValid())
        {
            Debug.LogError(name + " 的音乐初始化 Event 未配置。", this);
            return;
        }
        uint id = eventToPlay.Post(gameObject);
        if (id == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(name + " 的音乐初始化 Event 播放失败。", this);
            return;
        }
        musicInitialized = true;
        hasPlayed = true;
        Debug.Log(name + "：已在请求目标音乐 State 前发送初始化 Event。Playing ID: " + id, this);
    }

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                name
                + " uses AkTriggerEnter2D, so its Collider2D "
                + "has been changed to Is Trigger."
            );

            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (initializeBeforeMusicTransition && GameSpeechSettings.BlocksGameInput)
        {
            loadingOverlaps.Add(other);
            return;
        }
        if (musicInitialized && loadingOverlaps.Remove(other)) return;
        if (LoadingInteractionGate.TryDefer(this, other.gameObject,
            () => { if (other != null) OnTriggerEnter2D(other); })) return;
        if (playOnlyOnce && hasPlayed)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        // Spawn overlaps happen while the loading overlay is still active.
        // Do not queue a collision narration that would replace the level intro.
        bool ttsTriggered = triggerTts && !GameSpeechSettings.BlocksGameInput
            && TtsText.TryPlay(gameObject);
        if (ttsTriggered) hasPlayed = true;

        if (eventToPlay == null || !eventToPlay.IsValid())
        {
            if (!ttsTriggered)
                Debug.LogError(name + " is missing a valid Wwise Event.");
            return;
        }

        uint playingId = eventToPlay.Post(gameObject);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                "Wwise could not play the 2D trigger Event on "
                + name + "."
            );
            return;
        }

        hasPlayed = true;

        Debug.Log(
            "AkTriggerEnter2D played an Event on " + name
            + ". Playing ID: " + playingId
        );
    }

    private void OnTriggerExit2D(Collider2D other) => loadingOverlaps.Remove(other);
}
