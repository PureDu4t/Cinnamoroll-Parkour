using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DeathSceneResetTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    [Header("Wwise")]
    [SerializeField] private AK.Wwise.Event deathStingerEvent;
    [SerializeField] private GameObject deathStingerEventTarget;
    [SerializeField] private float resetDelay;

    private bool respawnStarted;

    public void ConfigureAudio(
        AK.Wwise.Event stingerEvent,
        GameObject eventTarget,
        float sceneResetDelay
    )
    {
        deathStingerEvent = stingerEvent;
        deathStingerEventTarget = eventTarget;
        resetDelay = Mathf.Max(0f, sceneResetDelay);
    }

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (respawnStarted || !other.CompareTag(playerTag))
        {
            return;
        }

        PlayerMovement playerMovement =
            other.GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
        {
            Debug.LogWarning(
                name
                + " detected a death trigger, but no PlayerMovement "
                + "was found on the player object."
            );
            return;
        }

        respawnStarted = true;
        PlayDeathStinger(other.gameObject);

        if (resetDelay > 0f)
        {
            StartCoroutine(RespawnPlayerAfterDelay(playerMovement));
            return;
        }

        RespawnPlayer(playerMovement);
    }

    private void PlayDeathStinger(GameObject playerObject)
    {
        TtsText.TryPlay(gameObject);

        if (deathStingerEvent == null || !deathStingerEvent.IsValid())
        {
            return;
        }

        GameObject target =
            deathStingerEventTarget != null
                ? deathStingerEventTarget
                : playerObject;

        uint playingId = deathStingerEvent.Post(target);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning(name + " could not play death stinger Event.");
        }
    }

    private IEnumerator RespawnPlayerAfterDelay(
        PlayerMovement playerMovement
    )
    {
        yield return new WaitForSeconds(resetDelay);
        RespawnPlayer(playerMovement);
    }

    private void RespawnPlayer(PlayerMovement playerMovement)
    {
        if (playerMovement != null)
        {
            playerMovement.ResetToStartPosition();
        }

        respawnStarted = false;
    }
}
