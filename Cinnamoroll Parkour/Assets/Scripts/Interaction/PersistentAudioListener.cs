using UnityEngine;

/// <summary>Unity's listener stays alive independently of scenes and the speech engine.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioListener))]
public sealed class PersistentAudioListener : MonoBehaviour
{
    private static PersistentAudioListener instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void EnsureExists()
    {
        if (instance != null) return;
        var existing = FindFirstObjectByType<PersistentAudioListener>();
        if (existing != null)
        {
            instance = existing;
            return;
        }
        new GameObject("Persistent Audio Listener").AddComponent<PersistentAudioListener>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
