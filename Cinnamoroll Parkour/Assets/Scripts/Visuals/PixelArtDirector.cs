using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Visual presentation only: original Tilemaps remain the source of physics and audio cells.</summary>
[DefaultExecutionOrder(900)]
public class PixelArtDirector : MonoBehaviour
{
    private PixelArtSettings settings;
    private string sceneName;
    private bool level;
    private Camera viewCamera;
    private SpriteRenderer skyRenderer;
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private readonly List<Tile> ownedTiles = new List<Tile>();
    private readonly Dictionary<Tilemap, (Tilemap visual, TilemapImporter importer)> maps =
        new Dictionary<Tilemap, (Tilemap, TilemapImporter)>();
    private readonly HashSet<Tilemap> dirtyMaps = new HashSet<Tilemap>();
    private Tile[] tileSet;
    private Tile chapterWall, chapterRoad, invisibleWall;
    private Tile[] cafeWalls;
    private Tile cafeBoundary;
    private Texture2D boundaryTexture;
    private Sprite[] heightFurniture;
    private float invisibleWallAspect = 1;
    private Sprite loreBook;
    private LevelSelectController selection;
    private SceneKeyboardNavigator navigator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static bool IsLevel(string name) => name == "Tutorial"
        || name.StartsWith("Level ") && int.TryParse(name.Substring(6), out _);
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var config = PixelArtSettings.Current;
        if (config == null || !config.enabled || !(IsLevel(scene.name) || scene.name == "Main Menu"
            || scene.name == "Level Select" || scene.name == "Credits")) return;
        var root = new GameObject("Pixel Art Presentation");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<PixelArtDirector>();
    }
    private IEnumerator Start()
    {
        settings = PixelArtSettings.Current;
        sceneName = gameObject.scene.name;
        level = IsLevel(sceneName);
        // Let existing GridWorldManager import the original tiles before presenting new art.
        yield return null;
        viewCamera = Camera.main;
        foreach (var item in FindObjectsByType<SceneKeyboardNavigator>(FindObjectsSortMode.None))
            if (item.gameObject.scene == gameObject.scene) navigator = item;
        foreach (var item in FindObjectsByType<LevelSelectController>(FindObjectsSortMode.None))
            if (item.gameObject.scene == gameObject.scene) selection = item;
        if (level)
        {
            CreateSky();
            if (settings.tiles != null)
            {
                MakeTileSet();
                foreach (var importer in FindObjectsByType<TilemapImporter>(FindObjectsSortMode.None))
                    if (importer.gameObject.scene == gameObject.scene && importer.SourceTilemap != null)
                        CreateVisualMap(importer);
                Tilemap.tilemapTileChanged += OnTilesChanged;
            }
            ReplaceCharacter();
        }
    }
    private Sprite MakeSprite(Texture2D texture, Rect rect, float pixelsPerUnit)
    {
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        ownedSprites.Add(sprite);
        return sprite;
    }
    private void CreateSky()
    {
        var background = settings.BackgroundForScene(sceneName);
        if (viewCamera == null || background == null) return;
        var root = new GameObject("Pixel Sky");
        root.transform.SetParent(transform);
        skyRenderer = root.AddComponent<SpriteRenderer>();
        Rect skyRect = new Rect(0, 0, background.width, background.height);
        skyRenderer.sprite = MakeSprite(background, skyRect, 100);
        skyRenderer.sortingOrder = -1000;
        FitSky();
    }
    private void FitSky()
    {
        if (skyRenderer == null || viewCamera == null || !viewCamera.orthographic) return;
        float height = viewCamera.orthographicSize * 2;
        skyRenderer.transform.position = new Vector3(viewCamera.transform.position.x, viewCamera.transform.position.y, 5);
        Vector2 size = skyRenderer.sprite.bounds.size;
        skyRenderer.transform.localScale = new Vector3(height * viewCamera.aspect / size.x, height / size.y, 1);
    }
    private void MakeTileSet()
    {
        tileSet = new Tile[16];
        bool cafeChapter = int.TryParse(sceneName.Replace("Level ", ""), out int chapterNumber) && chapterNumber <= 5;
        var props = cafeChapter && settings.cafeProps != null ? settings.cafeProps : settings.tiles;
        float width = props.width / 4f, height = props.height / 4f;
        for (int i = 0; i < 16; i++)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "Pixel visual " + i;
            tile.sprite = MakeSprite(props, new Rect(i % 4 * width, (3 - i / 4) * height, width, height), width);
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None;
            tileSet[i] = tile;
            ownedTiles.Add(tile);
        }
        // Crop only the book above the existing road, preserving its placement.
        width = settings.tiles.width / 4f;
        height = settings.tiles.height / 4f;
        loreBook = Sprite.Create(settings.tiles, new Rect(2 * width, 3 * height + height * .2f, width, height * .8f),
            new Vector2(.5f, .375f), width, 0, SpriteMeshType.FullRect);
        ownedSprites.Add(loreBook);
        Texture2D terrain = null;
        if (int.TryParse(sceneName.Replace("Level ", ""), out int number))
            terrain = number <= 5 ? settings.cafeTerrain : number <= 10 ? settings.forestTerrain : null;
        if (terrain != null)
        {
            float cell = terrain.width / 2f;
            chapterWall = MakeTerrainTile(terrain, new Rect(0, 0, cell, terrain.height), cell);
            chapterRoad = MakeTerrainTile(terrain, new Rect(cell, 0, cell, terrain.height), cell);
        }
        if (settings.invisibleWallTiles != null)
        {
            var atlas = settings.invisibleWallTiles;
            int chapter = 2;
            if (int.TryParse(sceneName.Replace("Level ", ""), out int levelNumber))
                chapter = levelNumber <= 5 ? 0 : levelNumber <= 10 ? 1 : 2;
            float cell = atlas.width / 3f;
            invisibleWall = MakeTerrainTile(atlas, new Rect(chapter * cell, 0, cell, atlas.height), cell);
            invisibleWallAspect = cell / atlas.height;
        }
        if (cafeChapter && settings.cafeArchitecture != null)
        {
            var atlas = settings.cafeArchitecture;
            float cell = atlas.width / 4f, heightOfCell = atlas.height / 2f;
            cafeWalls = new Tile[4];
            for (int i = 0; i < cafeWalls.Length; i++)
                cafeWalls[i] = MakeTerrainTile(atlas, new Rect(i * cell, heightOfCell, cell, heightOfCell), cell);
            chapterWall = cafeWalls[0];
            // Exclude neighboring atlas-row pixels leaking into the transparent area.
            chapterRoad = MakeTerrainTile(atlas, new Rect(0, 0, cell, heightOfCell), cell, .20f);
            invisibleWall = MakeTerrainTile(atlas, new Rect(cell, 0, cell, heightOfCell), cell, .90f);
            invisibleWallAspect = cell / heightOfCell;
            MakeCafeBoundary();
            if (settings.cafeHeightFurniture != null)
            {
                var furniture = settings.cafeHeightFurniture;
                float widthOfColumn = furniture.width / 3f;
                heightFurniture = new Sprite[3];
                // Visible bounds of the generated objects (including the fridge's rounded top).
                float[] visibleHeights = { .34f, .76f, 1f };
                for (int i = 0; i < 3; i++)
                    heightFurniture[i] = MakeSprite(furniture,
                        new Rect(i * widthOfColumn, 0, widthOfColumn, furniture.height * visibleHeights[i]), widthOfColumn);
            }
        }
        else if (!cafeChapter && (chapterNumber > 10 || sceneName == "Tutorial") && settings.cloudInvisibleWall != null)
        {
            var cloud = settings.cloudInvisibleWall;
            invisibleWall = MakeTerrainTile(cloud, new Rect(0, 0, cloud.width, cloud.height), cloud.width);
            invisibleWallAspect = (float)cloud.width / cloud.height;
        }
    }
    private Tile MakeTerrainTile(Texture2D texture, Rect rect, float ppu, float visibleFraction = 1f)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        float originalHeight = rect.height;
        rect.height *= visibleFraction;
        tile.sprite = MakeSprite(texture, rect, ppu);
        tile.transform = Matrix4x4.Translate(new Vector3(0, -(originalHeight - rect.height) / (2 * ppu), 0));
        tile.colliderType = Tile.ColliderType.None;
        tile.flags = TileFlags.None;
        ownedTiles.Add(tile);
        return tile;
    }
    private void MakeCafeBoundary()
    {
        if (settings.cafeCreamBoundary != null)
        {
            var texture = settings.cafeCreamBoundary;
            cafeBoundary = MakeTerrainTile(texture, new Rect(0, 0, texture.width, texture.height), texture.width);
            return;
        }
        // Solid border; decorations are relocated into empty cells separately.
        boundaryTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        boundaryTexture.filterMode = FilterMode.Point;
        var pixels = new Color[32 * 32];
        var line = new Color(.64f, .51f, .38f, 1);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = x == 0 || y == 0 || x == 31 || y == 31
                    ? line : new Color(.94f, .87f, .75f, 1);
        boundaryTexture.SetPixels(pixels);
        boundaryTexture.Apply();
        cafeBoundary = MakeTerrainTile(boundaryTexture, new Rect(0, 0, 32, 32), 32);
    }
    private static bool OnBoundary(Vector3Int position, BoundsInt bounds) =>
        position.x == bounds.xMin || position.x == bounds.xMax - 1
        || position.y == bounds.yMin || position.y == bounds.yMax - 1;
    private static bool IsWallAt(Tilemap source, TilemapImporter importer, Vector3Int position)
    {
        var tile = source.GetTile(position);
        return tile != null && importer.GetCellTypeForTile(tile, false) == CellType.Wall;
    }
    private void DrawCafeFurniture(Tilemap visual, Vector3Int bottom, int height)
    {
        var sprite = heightFurniture[Mathf.Min(height, 3) - 1];
        var root = new GameObject("Cafe Furniture " + height + " cells");
        root.transform.SetParent(visual.transform, false);
        Vector3 low = visual.CellToLocalInterpolated((Vector3)bottom);
        Vector3 high = visual.CellToLocalInterpolated((Vector3)bottom + new Vector3(1, height, 0));
        root.transform.localPosition = (low + high) * .5f;
        root.transform.localScale = new Vector3((high.x - low.x) / sprite.bounds.size.x,
            (high.y - low.y) / sprite.bounds.size.y, 1);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        var mapRenderer = visual.GetComponent<TilemapRenderer>();
        renderer.sharedMaterial = mapRenderer.sharedMaterial;
        renderer.sortingLayerID = mapRenderer.sortingLayerID;
        renderer.sortingOrder = mapRenderer.sortingOrder;
    }
    private void CreateVisualMap(TilemapImporter importer)
    {
        var source = importer.SourceTilemap;
        if (maps.ContainsKey(source)) return;
        var sourceRenderer = source.GetComponent<TilemapRenderer>();
        if (sourceRenderer == null) return;
        var root = new GameObject("Pixel Visual Tilemap");
        root.transform.SetParent(source.transform.parent, false);
        root.transform.localPosition = source.transform.localPosition;
        root.transform.localRotation = source.transform.localRotation;
        root.transform.localScale = source.transform.localScale;
        var visual = root.AddComponent<Tilemap>();
        visual.tileAnchor = source.tileAnchor;
        visual.orientation = source.orientation;
        visual.orientationMatrix = source.orientationMatrix;
        var renderer = root.AddComponent<TilemapRenderer>();
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sourceRenderer.sortingOrder;
        sourceRenderer.enabled = false;
        maps.Add(source, (visual, importer));
        RefreshMap(source);
    }
    private static int TileIndex(CellType type, Vector3Int position)
    {
        switch (type)
        {
            case CellType.Empty: case CellType.Death: return -1;
            case CellType.AirWall: return 0;
            case CellType.Ground: return 1;
            case CellType.Lore: return 2;
            case CellType.Goal: return 3;
            case CellType.Door: return 4;
            case CellType.CrackedWall: return 5;
            case CellType.Lever: return 6;
            case CellType.Cannon: return 7;
            case CellType.Battery: return 8;
            case CellType.Enemy: return 9;
            case CellType.Background: return 11;
            case CellType.SmallJumpHintRight: case CellType.SmallJumpHintLeft: case CellType.SmallJumpHintBoth: return 12;
            case CellType.BigJumpHintRight: case CellType.BigJumpHintLeft: case CellType.BigJumpHintBoth: return 13;
            default: return (position.x * 3 + position.y * 7) % 11 == 0 ? 15 : 0;
        }
    }
    private void RefreshMap(Tilemap source)
    {
        if (source == null || !maps.TryGetValue(source, out var map) || map.visual == null) return;
        map.visual.ClearAllTiles();
        // Book decorations belong to this map and are rebuilt when its cells change.
        for (int i = map.visual.transform.childCount - 1; i >= 0; i--)
            Destroy(map.visual.transform.GetChild(i).gameObject);
        var furnishedCells = new HashSet<Vector3Int>();
        foreach (Vector3Int position in source.cellBounds.allPositionsWithin)
        {
            TileBase original = source.GetTile(position);
            if (original == null) continue;
            CellType type = map.importer.GetCellTypeForTile(original, false);
            int index = TileIndex(type, position);
            if (index < 0) continue;
            bool wall = type == CellType.Wall;
            if (wall && cafeBoundary != null)
            {
                if (OnBoundary(position, source.cellBounds))
                {
                    map.visual.SetTile(position, cafeBoundary);
                    continue;
                }
                if (heightFurniture != null)
                {
                    if (furnishedCells.Contains(position)) continue;
                    Vector3Int bottom = position;
                    while (!OnBoundary(bottom + Vector3Int.down, source.cellBounds)
                        && IsWallAt(source, map.importer, bottom + Vector3Int.down)) bottom += Vector3Int.down;
                    int height = 0;
                    Vector3Int cell = bottom;
                    while (!OnBoundary(cell, source.cellBounds) && IsWallAt(source, map.importer, cell))
                    {
                        furnishedCells.Add(cell);
                        height++;
                        cell += Vector3Int.up;
                    }
                    if (height > 0) DrawCafeFurniture(map.visual, bottom, height);
                    continue;
                }
            }
            // Stable visual variation only: original source tile and CellType stay untouched.
            int furniturePattern = Mathf.Abs((position.x * 3 + position.y * 7) % 12);
            Tile wallVisual = cafeWalls != null
                ? cafeWalls[furniturePattern < 2 ? furniturePattern + 2 : furniturePattern % 2] : chapterWall;
            bool bookOnRoad = type == CellType.Lore && chapterRoad != null;
            Tile displayed = type == CellType.AirWall && invisibleWall != null ? invisibleWall
                : wall && wallVisual != null ? wallVisual
                : (type == CellType.Ground || bookOnRoad) && chapterRoad != null ? chapterRoad : tileSet[index];
            map.visual.SetTile(position, displayed);
            if (bookOnRoad)
            {
                var book = new GameObject("Lore Book");
                book.transform.SetParent(map.visual.transform, false);
                book.transform.localPosition = map.visual.CellToLocalInterpolated((Vector3)position + map.visual.tileAnchor);
                var renderer = book.AddComponent<SpriteRenderer>();
                renderer.sprite = loreBook;
                var mapRenderer = map.visual.GetComponent<TilemapRenderer>();
                renderer.sharedMaterial = mapRenderer.sharedMaterial;
                renderer.sortingLayerID = mapRenderer.sortingLayerID;
                renderer.sortingOrder = mapRenderer.sortingOrder + 1;
            }
            Matrix4x4 matrix = source.GetTransformMatrix(position);
            if (type == CellType.AirWall && invisibleWall != null)
                matrix *= Matrix4x4.Scale(new Vector3(1, invisibleWallAspect, 1));
            matrix *= displayed.transform;
            if (type == CellType.SmallJumpHintLeft || type == CellType.BigJumpHintLeft)
                matrix = Matrix4x4.Scale(new Vector3(-1, 1, 1));
            map.visual.SetTransformMatrix(position, matrix);
            if (type == CellType.Background) map.visual.SetColor(position, new Color(1, 1, 1, .28f));
        }
    }
    private void OnTilesChanged(Tilemap source, Tilemap.SyncTile[] changes)
    {
        if (maps.ContainsKey(source)) dirtyMaps.Add(source);
    }
    private void ReplaceCharacter()
    {
        if (!settings.replaceCharacter || settings.character == null) return;
        foreach (var player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != gameObject.scene) continue;
            var renderer = player.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) continue;
            // Preserve existing Transform, facing direction and physics dimensions.
            float oldHeight = renderer.sprite.bounds.size.y;
            renderer.sprite = MakeSprite(settings.character, new Rect(0, 0, settings.character.width, settings.character.height),
                settings.character.height / Mathf.Max(.01f, oldHeight));
            renderer.color = Color.white;
        }
    }
    private void LateUpdate()
    {
        FitSky();
        if (dirtyMaps.Count == 0) return;
        var changed = new List<Tilemap>(dirtyMaps);
        dirtyMaps.Clear();
        foreach (var source in changed) RefreshMap(source);
    }
    private bool Chinese => GameSpeechSettings.Language == TtsText.SpeechLanguage.Chinese;
    private void OnGUI()
    {
        if (settings == null || !settings.enabled || GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene)) return;
        if (level)
        {
            if (settings.showLevelHud) DrawLevelHud();
            return;
        }
        if (settings.sky != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), settings.sky, ScaleMode.ScaleAndCrop);
        Matrix4x4 saved = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1200f, Screen.height / 760f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1200 * scale) / 2, (Screen.height - 760 * scale) / 2), Quaternion.identity, Vector3.one * scale);
        if (sceneName == "Main Menu") DrawMainMenu();
        else if (sceneName == "Level Select") DrawLevelSelect();
        else if (sceneName == "Credits") DrawCredits();
        GUI.matrix = saved;
    }
    private void DrawMainMenu()
    {
        PixelArtUi.Frame(new Rect(80, 80, 620, 610), settings.panel, settings.border);
        GUI.Label(new Rect(120, 110, 540, 30), "CINNAMOROLL PARKOUR", PixelArtUi.Text(18, settings.accent));
        GUI.Label(new Rect(120, 165, 540, 95), Chinese ? "玉桂狗跑酷" : "Cinnamoroll Parkour", PixelArtUi.Text(48, settings.ink));
        GUI.Label(new Rect(120, 275, 540, 65), Chinese ? "听见每一步，探索云朵之间的小世界。" : "Follow each sound. Find your way through the clouds.", PixelArtUi.Text(20, settings.ink));
        if (PixelArtUi.Button(new Rect(120, 385, 540, 68), Chinese ? "开始冒险   E" : "Start adventure   E", settings, true)) navigator?.NavigateFromUI("Level Select");
        if (PixelArtUi.Button(new Rect(120, 470, 260, 58), Chinese ? "教学关卡   T" : "Tutorial   T", settings)) navigator?.NavigateFromUI("Tutorial");
        if (PixelArtUi.Button(new Rect(400, 470, 260, 58), Chinese ? "语音设置   F2" : "Voice settings   F2", settings)) SpeechSettingsMenu.OpenFromMenu();
        GUI.Label(new Rect(120, 555, 540, 54), Chinese ? "方向键移动 · 空格跳跃 · E 交互" : "Arrows to move · Space to jump · E to interact", PixelArtUi.Text(15, settings.ink));
        if (PixelArtUi.Button(new Rect(120, 620, 200, 40), Chinese ? "退出   Esc" : "Quit   Esc", settings)) navigator?.QuitFromUI();
        DrawHero(new Rect(740, 195, 390, 420));
    }
    private void DrawHero(Rect rect)
    {
        if (settings.character != null) GUI.DrawTexture(rect, settings.character, ScaleMode.ScaleToFit);
    }
    private void DrawLevelSelect()
    {
        PixelArtUi.Frame(new Rect(65, 55, 1070, 650), settings.panel, settings.border);
        GUI.Label(new Rect(105, 87, 980, 55), Chinese ? "挑选一段云端冒险" : "Choose your adventure", PixelArtUi.Text(32, settings.ink));
        GUI.Label(new Rect(105, 151, 980, 35), Chinese ? "← / → 切换 · ↑ / ↓ 跨五关 · E 出发" : "← / → select · ↑ / ↓ move five · E to start", PixelArtUi.Text(16, settings.accent));
        if (selection != null)
        {
            for (int i = 0; i < selection.LevelCount; i++)
            {
                Rect card = new Rect(105 + i % 5 * 198, 225 + i / 5 * 108, 180, 88);
                bool selected = selection.SelectedLevel == i + 1;
                string label = (i + 1).ToString("00") + "\n" + (Chinese ? "第 " + (i + 1) + " 关" : "LEVEL " + (i + 1));
                if (PixelArtUi.Button(card, label, settings, selected)) selection.SelectFromUI(i);
            }
            string chapter = selection.SelectedLevel <= 5 ? "Cinnamon Café" : selection.SelectedLevel <= 10 ? "Forest" : "Clouds";
            GUI.Label(new Rect(105, 570, 620, 38), (Chinese ? "第 " + selection.SelectedLevel + " 关 · " : "Level " + selection.SelectedLevel + " · ") + chapter, PixelArtUi.Text(21, settings.ink));
            if (PixelArtUi.Button(new Rect(745, 565, 350, 65), Chinese ? "进入关卡   E" : "Enter level   E", settings, true)) selection.ConfirmFromUI();
            if (PixelArtUi.Button(new Rect(105, 635, 250, 40), Chinese ? "返回主菜单   Esc" : "Back to menu   Esc", settings)) selection.BackFromUI();
        }
    }
    private void DrawCredits()
    {
        PixelArtUi.Frame(new Rect(160, 70, 880, 620), settings.panel, settings.border);
        GUI.Label(new Rect(210, 125, 780, 65), Chinese ? "谢谢你来陪玉桂狗冒险" : "Thank you for playing!", PixelArtUi.Text(34, settings.ink, TextAnchor.MiddleCenter));
        DrawHero(new Rect(425, 230, 350, 290));
        GUI.Label(new Rect(210, 535, 780, 45), Chinese ? "期待下一次在云朵之间相遇。" : "See you on the next adventure.", PixelArtUi.Text(20, settings.accent, TextAnchor.MiddleCenter));
        if (PixelArtUi.Button(new Rect(390, 610, 420, 50), Chinese ? "返回主菜单   E / Esc" : "Back to menu   E / Esc", settings, true)) navigator?.NavigateFromUI("Main Menu");
    }
    private void DrawLevelHud()
    {
        float scale = Mathf.Min(Screen.width / 1200f, Screen.height / 760f);
        Matrix4x4 saved = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        PixelArtUi.Frame(new Rect(22, 20, 280, 90), settings.panel, settings.border);
        string levelTitle = sceneName == "Tutorial" ? (Chinese ? "教学关卡" : "Tutorial")
            : Chinese ? "第 " + sceneName.Replace("Level ", "") + " 关" : sceneName;
        PixelArtUi.FittedLabel(new Rect(54, 41, 216, 28), levelTitle, 22, settings.ink, TextAnchor.MiddleLeft);
        string chapter = "Clouds";
        if (int.TryParse(sceneName.Replace("Level ", ""), out int number))
            chapter = number <= 5 ? "Cinnamon Café" : number <= 10 ? "Forest" : "Clouds";
        PixelArtUi.FittedLabel(new Rect(54, 73, 216, 18), chapter, 14, settings.accent, TextAnchor.MiddleLeft);
        PixelArtUi.Frame(new Rect(324, 20, 570, 76), settings.panel, settings.border);
        PixelArtUi.FittedLabel(new Rect(356, 42, 506, 30),
            Chinese ? "← → 移动   空格 跳跃   E 交互   S 播报" : "← → Move   Space Jump   E Interact   S Broadcast",
            16, settings.ink, TextAnchor.MiddleCenter);
        GUI.matrix = saved;
    }
    private void OnDestroy()
    {
        Tilemap.tilemapTileChanged -= OnTilesChanged;
        foreach (var sprite in ownedSprites) if (sprite != null) Destroy(sprite);
        foreach (var tile in ownedTiles) if (tile != null) Destroy(tile);
        if (boundaryTexture != null) Destroy(boundaryTexture);
        foreach (var entry in maps) if (entry.Value.visual != null) Destroy(entry.Value.visual.gameObject);
    }
}
