using UnityEngine;

/// <summary>Keeps this object and its children alive when changing scenes.</summary>
[AddComponentMenu("Utilities/Dont Destroy On Load")]
[DisallowMultipleComponent]
public class PersistAcrossScenes : MonoBehaviour
{
    private void Awake()
    {
        // Unity requires a root object for DontDestroyOnLoad.
        // Detach only this object; preserve its world position and children.
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }
}
