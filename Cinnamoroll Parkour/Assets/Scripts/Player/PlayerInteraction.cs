using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("Grid References")]
    [SerializeField] private GridDatabase gridDatabase;
    [SerializeField] private PlayerPositionTracker positionTracker;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerJump playerJump;

    [Header("Interaction Handlers")]
    [Tooltip("Add future Lever, Cannon, Battery, or Goal handlers here.")]
    [SerializeField] private List<MonoBehaviour> handlerBehaviours =
        new List<MonoBehaviour>();

    [Header("Wwise")]
    [Tooltip("Optional Event played when E successfully interacts.")]
    [SerializeField] private AK.Wwise.Event interactionConfirmEvent;

    private readonly List<IGridInteractionHandler> handlers =
        new List<IGridInteractionHandler>();

    private LoreInteractable currentLore;
    private Collider2D[] playerColliders;

    private void Awake()
    {
        CacheHandlers();
        CachePlayerColliders();
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (Input.GetKeyDown(interactionKey))
        {
            TryInteract();
        }
    }

    public void TryInteract()
    {
        LoreInteractable loreToInteract =
            IsLoreUsable(currentLore)
                ? currentLore
                : FindCurrentLoreByOverlap();

        if (loreToInteract != null)
        {
            currentLore = loreToInteract;
            loreToInteract.Interact(playerMovement, playerJump);
            PlayInteractionConfirm();
            return;
        }

        if (gridDatabase == null
            || positionTracker == null
            || playerMovement == null)
        {
            Debug.LogError(
                "PlayerInteraction is missing one or more grid references."
            );
            return;
        }

        Vector2Int currentPosition =
            positionTracker.CurrentGridPosition;

        if (TryInteractAt(currentPosition))
        {
            PlayInteractionConfirm();
            return;
        }

        Vector2Int facingPosition =
            currentPosition
            + Vector2Int.right * playerMovement.LastMoveDirection;

        if (TryInteractAt(facingPosition))
        {
            PlayInteractionConfirm();
        }
    }

    private bool TryInteractAt(Vector2Int gridPosition)
    {
        GridCell cell = gridDatabase.GetCell(gridPosition);

        if (!cell.interactable)
        {
            return false;
        }

        for (int i = 0; i < handlers.Count; i++)
        {
            if (handlers[i].CanHandle(cell.type))
            {
                handlers[i].Interact(gridPosition);
                return true;
            }
        }

        Debug.Log(
            "Interaction detected at " + gridPosition
            + " for " + cell.type
            + ", but its gameplay handler is not implemented yet."
        );

        return true;
    }

    private void PlayInteractionConfirm()
    {
        TtsText.TryPlay(gameObject);

        if (interactionConfirmEvent == null
            || !interactionConfirmEvent.IsValid())
        {
            return;
        }

        uint playingId = interactionConfirmEvent.Post(gameObject);

        if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                "Wwise could not play interaction confirm Event."
            );
        }
    }

    public void EnterLore(LoreInteractable lore)
    {
        currentLore = lore;
    }

    public void ExitLore(LoreInteractable lore)
    {
        if (currentLore == lore)
        {
            currentLore = null;
        }
    }

    private LoreInteractable FindCurrentLoreByOverlap()
    {
        if (playerColliders == null || playerColliders.Length == 0)
        {
            CachePlayerColliders();
        }

        if (playerColliders == null || playerColliders.Length == 0)
        {
            return null;
        }

        LoreInteractable[] loreInteractables =
            FindObjectsOfType<LoreInteractable>();

        for (int i = 0; i < loreInteractables.Length; i++)
        {
            LoreInteractable lore = loreInteractables[i];

            if (!IsLoreUsable(lore))
            {
                continue;
            }

            Collider2D loreCollider = lore.GetComponent<Collider2D>();

            if (loreCollider == null || !loreCollider.enabled)
            {
                continue;
            }

            if (PlayerOverlaps(loreCollider))
            {
                Debug.Log(
                    "PlayerInteraction found Lore by overlap: "
                    + lore.name
                );

                return lore;
            }
        }

        return null;
    }

    private static bool IsLoreUsable(LoreInteractable lore)
    {
        return lore != null
            && lore.isActiveAndEnabled
            && lore.gameObject.activeInHierarchy;
    }

    private bool PlayerOverlaps(Collider2D loreCollider)
    {
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];

            if (playerCollider == null
                || !playerCollider.enabled
                || playerCollider.isTrigger)
            {
                continue;
            }

            if (loreCollider.bounds.Intersects(playerCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void CachePlayerColliders()
    {
        playerColliders = GetComponentsInChildren<Collider2D>();
    }

    private void CacheHandlers()
    {
        handlers.Clear();

        for (int i = 0; i < handlerBehaviours.Count; i++)
        {
            IGridInteractionHandler handler =
                handlerBehaviours[i] as IGridInteractionHandler;

            if (handler != null)
            {
                handlers.Add(handler);
            }
            else if (handlerBehaviours[i] != null)
            {
                Debug.LogWarning(
                    handlerBehaviours[i].name
                    + " does not implement IGridInteractionHandler."
                );
            }
        }
    }
}
