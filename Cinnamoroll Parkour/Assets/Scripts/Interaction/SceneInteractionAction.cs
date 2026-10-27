using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneInteractionAction : MonoBehaviour, IInteractionAction
{
    [Header("Scene")]
    [Tooltip("Leave empty if this action should not load a Scene.")]
    [SerializeField] private string sceneName;

    [Tooltip("Optional delay before loading the Scene. Useful if a Wwise Event should be heard first.")]
    [SerializeField] private float delayBeforeLoad;

    private bool isLoading;

    public void Execute(GameObject interactor)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (delayBeforeLoad > 0f)
        {
            StartCoroutine(LoadSceneAfterDelay());
            return;
        }

        LoadScene();
    }

    private IEnumerator LoadSceneAfterDelay()
    {
        isLoading = true;
        yield return new WaitForSeconds(delayBeforeLoad);
        LoadScene();
    }

    private void LoadScene()
    {
        isLoading = true;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }
}
