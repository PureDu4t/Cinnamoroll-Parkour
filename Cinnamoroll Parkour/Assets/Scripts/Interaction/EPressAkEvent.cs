using UnityEngine;

[RequireComponent(typeof(AkEvent))]
[DisallowMultipleComponent]
public class EPressAkEvent : MonoBehaviour
{
    [SerializeField] private KeyCode triggerKey = KeyCode.E;

    private AkEvent akEvent;

    private void Awake()
    {
        akEvent = GetComponent<AkEvent>();
        akEvent.triggerList.Clear();
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        // Lore input is routed by PlayerInteraction inside the interaction area.
        var lore = GetComponent<LoreInteractable>();
        if (lore != null && lore.isActiveAndEnabled)
        {
            return;
        }

        if (!Input.GetKeyDown(triggerKey))
        {
            return;
        }

        Debug.Log(
            "EPressAkEvent received " + triggerKey
            + ". Posting Wwise Event from " + name + "."
        );

        akEvent.HandleEvent(gameObject);

        Debug.Log(
            "Wwise Event posted. Playing ID: "
            + akEvent.playingId
        );
    }
}
