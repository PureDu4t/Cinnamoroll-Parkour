using UnityEngine;

[RequireComponent(typeof(AkGameObj))]
public class PlayerWallCollisionAudio : MonoBehaviour
{
    [Header("Wwise")]
    [SerializeField] private AK.Wwise.Event wallCollisionEvent;
    [SerializeField] private GameObject eventTarget;

    [Header("Rules")]
    [SerializeField] private CellType wallCellType = CellType.Wall;
    [SerializeField] private float minimumTimeBetweenHits = 0.05f;
    [SerializeField] private bool debugLogging;

    private float nextAllowedHitTime;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (Time.time < nextAllowedHitTime)
        {
            return;
        }

        GridCellReference cellReference =
            collision.collider.GetComponent<GridCellReference>();

        if (cellReference == null)
        {
            cellReference =
                collision.collider.GetComponentInParent<GridCellReference>();
        }

        if (cellReference == null)
        {
            return;
        }

        if (cellReference.CellType != wallCellType)
        {
            return;
        }

        if (!TryPlayWallCollisionEvent())
        {
            return;
        }

        nextAllowedHitTime = Time.time + minimumTimeBetweenHits;

        if (debugLogging)
        {
            Debug.Log(
                "Wall collision audio played at grid "
                + cellReference.GridPosition
                + "."
            );
        }
    }

    private bool TryPlayWallCollisionEvent()
    {
        if (wallCollisionEvent == null || !wallCollisionEvent.IsValid())
        {
            return false;
        }

        GameObject target = eventTarget != null ? eventTarget : gameObject;
        TtsText.TryPlay(gameObject);

        uint playingId = wallCollisionEvent.Post(target);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogWarning(name + " could not play wall collision Event.");
            return false;
        }

        return true;
    }
}
