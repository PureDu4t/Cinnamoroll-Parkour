using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AkEvent))]
[DisallowMultipleComponent]
public class AkEventPlayerLock : MonoBehaviour
{
    [SerializeField] private bool lockPlayerDuringPlayback = true;

    private AkEvent akEvent;
    private PlayerMovement playerMovement;
    private PlayerJump playerJump;
    private bool playerLocked;
    public bool LockPlayerDuringPlayback => lockPlayerDuringPlayback;

    private void Awake()
    {
        akEvent = GetComponent<AkEvent>();

        var text = GetComponent<TtsText>();
        if (text != null && text.isActiveAndEnabled) return;

        Debug.Log(
            name + " AkEventPlayerLock Awake. Event: "
            + (akEvent.data != null ? akEvent.data.Name : "null")
            + ", Trigger count: " + akEvent.triggerList.Count
            + ", Has Start: "
            + akEvent.triggerList.Contains(
                AkTriggerHandler.START_TRIGGER_ID
            )
        );

        akEvent.useCallbacks = true;
        akEvent.Callbacks.Add(new AkEvent.CallbackData
        {
            Flags = new AK.Wwise.CallbackFlags
            {
                value = (uint)AkCallbackType.AK_EndOfEvent
            },
            FunctionName = nameof(OnWwiseEventEnded),
            GameObject = gameObject
        });
    }

    private void Start()
    {
        var text = GetComponent<TtsText>();
        if (text != null && text.isActiveAndEnabled) return;
        if (!lockPlayerDuringPlayback
            || !akEvent.triggerList.Contains(
                AkTriggerHandler.START_TRIGGER_ID
            ))
        {
            return;
        }

        StartCoroutine(LockAfterEventStarts());
    }

    private IEnumerator LockAfterEventStarts()
    {
        yield return null;

        if (akEvent.playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                name + " did not start its configured Wwise Event."
            );
            yield break;
        }

        playerMovement = FindObjectOfType<PlayerMovement>();
        playerJump = FindObjectOfType<PlayerJump>();
        SetPlayerControlEnabled(false);

        Debug.Log(
            name + " started its native AkEvent. Playing ID: "
            + akEvent.playingId
        );
    }

    private void OnWwiseEventEnded(AkEventCallbackMsg callbackMessage)
    {
        SetPlayerControlEnabled(true);
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (!isEnabled)
        {
            playerLocked = true;
        }
        else if (!playerLocked)
        {
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.SetControlEnabled(isEnabled);
        }

        if (playerJump != null)
        {
            playerJump.SetControlEnabled(isEnabled);
        }

        playerLocked = !isEnabled;
    }

    private void OnDisable()
    {
        SetPlayerControlEnabled(true);
    }
}
