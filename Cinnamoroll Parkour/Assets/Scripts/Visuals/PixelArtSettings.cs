using UnityEngine;

[CreateAssetMenu(menuName = "Cinnamoroll Parkour/Pixel Art Settings")]
public class PixelArtSettings : ScriptableObject
{
    public bool enabled = true;
    public bool replaceCharacter = true;
    public bool showLevelHud = true;
    public Texture2D sky;
    public Texture2D cafe;
    public Texture2D forest;
    public Texture2D tiles;
    [Tooltip("Two cells: wall on the left, road on the right.")]
    public Texture2D cafeTerrain;
    public Texture2D forestTerrain;
    [Tooltip("Three cells: café tablecloth, forest vines, cloud curtain.")]
    public Texture2D invisibleWallTiles;
    [Tooltip("4×2 café furniture atlas: four Wall variants on top; road and tablecloth at bottom left.")]
    public Texture2D cafeArchitecture;
    public Texture2D cafeProps;
    public Texture2D cloudInvisibleWall;
    [Tooltip("Three columns: table at bottom 1/3, fridge at bottom 2/3, tall cupboard at full height.")]
    public Texture2D cafeHeightFurniture;
    public Texture2D cafeCreamBoundary;
    public Texture2D uiSkin;
    public Texture2D character;
    public Color ink = new Color(.19f, .23f, .40f);
    public Color accent = new Color(.43f, .50f, .82f);
    public Color panel = new Color(1f, .99f, .96f, .97f);
    public Color border = new Color(.70f, .76f, .93f);
    public Color selected = new Color(.88f, .92f, 1f);

    public static PixelArtSettings Current => Resources.Load<PixelArtSettings>("PixelArt/PixelArtSettings");
    public Texture2D BackgroundForScene(string scene)
    {
        if (scene.StartsWith("Level ") && int.TryParse(scene.Substring(6), out int number))
        {
            if (number <= 5 && cafe != null) return cafe;
            if (number <= 10 && forest != null) return forest;
        }
        return sky;
    }
    public static Texture2D BackgroundOr(Texture2D fallback)
    {
        var settings = Current;
        return settings != null && settings.enabled && settings.sky != null ? settings.sky : fallback;
    }
}
