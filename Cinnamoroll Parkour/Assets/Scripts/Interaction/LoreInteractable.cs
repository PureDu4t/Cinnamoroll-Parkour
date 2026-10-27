using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class LoreInteractable : MonoBehaviour
{
    [Tooltip("Disable player movement and jumping while this Lore plays.")]
    [SerializeField] private bool lockPlayerDuringPlayback;

    [Header("TTS")]
    [Tooltip("开启时，交互同时触发当前物体上的 TtsText；关闭时只触发 Wwise Event 和交互动作。")]
    [SerializeField] private bool triggerTts = true;

    [Header("Actions")]
    [Tooltip("Optional scripts to run when the player interacts inside this trigger.")]
    [SerializeField] private List<MonoBehaviour> actionBehaviours =
        new List<MonoBehaviour>();

    private AkEvent[] loreEvents;
    private readonly List<IInteractionAction> actions =
        new List<IInteractionAction>();

    private BoxCollider2D interactionArea;
    private PlayerInteraction currentPlayer;
    private PlayerMovement activePlayerMovement;
    private PlayerJump activePlayerJump;
    private bool isPlaying;
    private int pendingEventCount;
    private bool usingTts;

    private void Awake()
    {
        loreEvents = GetComponents<AkEvent>();
        interactionArea = GetComponent<BoxCollider2D>();

        interactionArea.isTrigger = true;
        CacheActions();
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (Input.GetKeyDown(KeyCode.Q))
        {
            StopLore();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (LoadingInteractionGate.TryDefer(this, other.gameObject,
            () => { if (other != null) OnTriggerEnter2D(other); })) return;
        PlayerInteraction player =
            other.GetComponentInParent<PlayerInteraction>();

        if (player == null)
        {
            return;
        }

        currentPlayer = player;
        player.EnterLore(this);

        Debug.Log("Player entered Lore area: " + name);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerInteraction player =
            other.GetComponentInParent<PlayerInteraction>();

        if (player == null || player != currentPlayer)
        {
            return;
        }

        player.ExitLore(this);
        currentPlayer = null;

        Debug.Log("Player exited Lore area: " + name);
    }

    public void Interact(
        PlayerMovement playerMovement,
        PlayerJump playerJump
    )
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        var text = GetComponent<TtsText>();
        if (isPlaying)
        {
            if (triggerTts && usingTts && text != null && text.isActiveAndEnabled && text.IsPlaying)
            {
                // Reserve the replacement before Stop completes the previous TTS callback.
                // Existing Wwise Events and interaction Actions continue independently.
                pendingEventCount++;
                text.Stop();
                text.Play(OnTtsFinished, lockPlayerDuringPlayback);
            }
            return;
        }

        usingTts = triggerTts && text != null && text.isActiveAndEnabled;
        // Count both playback paths before starting either (empty TTS completes immediately).
        pendingEventCount = usingTts ? 1 : 0;

        for (int i = 0; i < loreEvents.Length; i++)
        {
            AkEvent loreEvent = loreEvents[i];

            if (CanPlayOnInteraction(loreEvent))
            {
                pendingEventCount++;
            }
        }

        if (pendingEventCount == 0 && actions.Count == 0)
        {
            Debug.LogError(
                name
                + " has no AkEvent with Trigger On set to Nothing "
                + "and no interaction Actions."
            );
            return;
        }

        activePlayerMovement = playerMovement;
        activePlayerJump = playerJump;
        isPlaying = true;

        if (lockPlayerDuringPlayback && !usingTts)
        {
            SetPlayerControlEnabled(false);
        }

        if (usingTts) text.Play(OnTtsFinished, lockPlayerDuringPlayback);

        for (int i = 0; i < loreEvents.Length; i++)
        {
            AkEvent loreEvent = loreEvents[i];

            if (!CanPlayOnInteraction(loreEvent))
            {
                continue;
            }

            uint playingId = loreEvent.data.Post(
                gameObject,
                (uint)AkCallbackType.AK_EndOfEvent,
                OnWwiseCallback,
                this
            );

            if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
            {
                pendingEventCount--;
                Debug.LogError(
                    "Wwise could not play a Lore Event on " + name + "."
                );
                continue;
            }

            Debug.Log(
                "Playing Lore Event on " + name
                + ". Wwise Playing ID: " + playingId
            );
        }

        ExecuteActions(playerMovement != null
            ? playerMovement.gameObject
            : gameObject);

        if (pendingEventCount == 0)
        {
            FinishPlayback();
        }
    }

    private void StopLore()
    {
        if (!isPlaying)
        {
            return;
        }

        Debug.Log("Stopping Lore Event on " + name + " with Q.");
        if (usingTts)
        {
            var text = GetComponent<TtsText>();
            if (text != null) text.Stop();
        }

        for (int i = 0; i < loreEvents.Length; i++)
        {
            AkEvent loreEvent = loreEvents[i];

            if (!CanPlayOnInteraction(loreEvent))
            {
                continue;
            }

            AkSoundEngine.ExecuteActionOnEvent(
                loreEvent.data.Id,
                AkActionOnEventType.AkActionOnEventType_Stop,
                gameObject,
                0,
                AkCurveInterpolation.AkCurveInterpolation_Linear
            );
        }

        FinishPlayback();
    }

    private void OnWwiseCallback(
        object cookie,
        AkCallbackType callbackType,
        AkCallbackInfo callbackInfo
    )
    {
        if (callbackType == AkCallbackType.AK_EndOfEvent)
        {
            pendingEventCount--;

            if (pendingEventCount <= 0)
            {
                FinishPlayback();
            }
        }
    }

    private void OnTtsFinished()
    {
        if (!isPlaying) return;
        pendingEventCount--;
        if (pendingEventCount <= 0) FinishPlayback();
    }

    private void FinishPlayback()
    {
        if (lockPlayerDuringPlayback)
        {
            SetPlayerControlEnabled(true);
        }

        activePlayerMovement = null;
        activePlayerJump = null;
        pendingEventCount = 0;
        isPlaying = false;
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (activePlayerMovement != null)
        {
            activePlayerMovement.SetControlEnabled(isEnabled);
        }

        if (activePlayerJump != null)
        {
            activePlayerJump.SetControlEnabled(isEnabled);
        }
    }

    private void ExecuteActions(GameObject interactor)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            actions[i].Execute(interactor);
        }
    }

    private static bool CanPlayOnInteraction(AkEvent loreEvent)
    {
        return loreEvent != null
            && loreEvent.enabled
            && loreEvent.triggerList.Count == 0
            && loreEvent.data != null;
    }

    private void CacheActions()
    {
        actions.Clear();

        for (int i = 0; i < actionBehaviours.Count; i++)
        {
            IInteractionAction action =
                actionBehaviours[i] as IInteractionAction;

            if (action != null)
            {
                actions.Add(action);
            }
            else if (actionBehaviours[i] != null)
            {
                Debug.LogWarning(
                    actionBehaviours[i].name
                    + " does not implement IInteractionAction."
                );
            }
        }
    }

    private void OnDisable()
    {
        if (usingTts)
        {
            var text = GetComponent<TtsText>();
            if (text != null) text.Stop();
        }
        if (currentPlayer != null)
        {
            currentPlayer.ExitLore(this);
            currentPlayer = null;
        }

        FinishPlayback();
    }
}
