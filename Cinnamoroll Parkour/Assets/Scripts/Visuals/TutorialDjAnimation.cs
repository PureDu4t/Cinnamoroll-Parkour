using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Original eight pixel-art poses, with no interpolated frames.</summary>
public sealed class TutorialDjAnimation : MonoBehaviour
{
    private RawImage artwork;
    private RectTransform artworkRect;
    private float aspect = 16f / 9f;
    private float startedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Tutorial") return;
        var root = new GameObject("Tutorial DJ Animation");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<TutorialDjAnimation>();
    }

    private void Start()
    {
        var sheet = Resources.Load<Texture2D>("PixelArt/TutorialDjOriginalSheet");
        if (sheet == null) { Debug.LogError("Tutorial DJ: missing original pixel artwork.", this); return; }
        aspect = (sheet.width / 2f) / (sheet.height / 4f);
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        foreach (var oldCanvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (oldCanvas == canvas || oldCanvas.gameObject.scene != gameObject.scene) continue;
            var oldBackground = oldCanvas.GetComponent<Image>();
            if (oldCanvas.name == "Canvas" && oldBackground != null) oldBackground.enabled = false;
        }
        var imageObject = new GameObject("DJ Artwork", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(transform, false);
        artwork = imageObject.GetComponent<RawImage>();
        sheet.filterMode = FilterMode.Point;
        artwork.texture = sheet;
        artwork.raycastTarget = false;
        artworkRect = imageObject.GetComponent<RectTransform>();
        artworkRect.anchorMin = artworkRect.anchorMax = artworkRect.pivot = new Vector2(.5f, .5f);
        startedAt = Time.unscaledTime;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (artwork == null) return;
        int frame = (int)((Time.unscaledTime - startedAt) * 3f) % 8;
        artwork.uvRect = new Rect(frame % 2 * .5f, (3 - frame / 2) * .25f, .5f, .25f);

        // Preserve the image's aspect ratio and fill the Canvas. Top alignment
        // keeps the original quiet upper area behind the title and control hints.
        float height = Mathf.Max(Screen.height, Screen.width / aspect);
        artworkRect.sizeDelta = new Vector2(height * aspect, height);
        artworkRect.anchoredPosition = new Vector2(0, (Screen.height - height) * .5f);
    }
}
