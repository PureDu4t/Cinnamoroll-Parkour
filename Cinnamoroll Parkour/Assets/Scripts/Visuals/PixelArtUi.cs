using UnityEngine;

/// <summary>Shared pixel frames and bundled CJK typography for menus and loading screens.</summary>
public static class PixelArtUi
{
    private static Font font;
    public static GUIStyle Text(int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        if (font == null) font = Resources.Load<Font>("Fonts/CartoonUiFont") ?? Resources.Load<Font>("Fonts/SpeechUiFont");
        var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = anchor,
            wordWrap = true, padding = new RectOffset(0, 0, 0, 0) };
        style.normal.textColor = color;
        return style;
    }
    public static void Fill(Rect r, Color color)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
    }
    private static void Stepped(Rect r, Color color)
    {
        Fill(new Rect(r.x + 4, r.y, r.width - 8, r.height), color);
        Fill(new Rect(r.x, r.y + 4, 4, r.height - 8), color);
        Fill(new Rect(r.xMax - 4, r.y + 4, 4, r.height - 8), color);
    }
    public static void Frame(Rect r, Color fill, Color border)
    {
        var theme = PixelArtSettings.Current;
        if (theme != null && theme.enabled && theme.uiSkin != null && r.height >= 30)
        {
            // Cloud silhouettes cannot be nine-sliced without creating stretched lines.
            // Use the rectangular cloud-corner panel for every text container.
            Skin(r, theme.uiSkin, 0);
            return;
        }
        Stepped(r, border);
        Stepped(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), fill);
    }
    public static bool Button(Rect r, string label, PixelArtSettings settings, bool selected = false)
    {
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
        bool pressed = hover && Input.GetMouseButton(0);
        Rect face = new Rect(r.x, r.y + (pressed ? 3 : 0), r.width, r.height);
        if (settings.uiSkin != null) Skin(face, settings.uiSkin, selected || hover ? 2 : 1);
        else Frame(face, selected ? settings.accent : settings.panel, settings.border);
        GUI.Label(new Rect(face.x + 12, face.y + 2, face.width - 24, face.height - 6), label,
            Text(19, settings.ink, TextAnchor.MiddleCenter));
        return clicked;
    }
    // Preserve hand-drawn corners; stretch only the plain center and edge strips.
    public static void Skin(Rect target, Texture2D atlas, int index)
    {
        // Atlas frames have different transparent padding; use each painted frame's bounds.
        Rect[] frames = { new Rect(.016f,.52f,.468f,.439f), new Rect(.513f,.558f,.478f,.329f),
            new Rect(.017f,.070f,.471f,.388f), new Rect(.509f,.079f,.482f,.340f) };
        Rect source = frames[Mathf.Clamp(index, 0, 3)];
        source = Rect.MinMaxRect(Mathf.Round(source.xMin * atlas.width) / atlas.width,
            Mathf.Round(source.yMin * atlas.height) / atlas.height,
            Mathf.Round(source.xMax * atlas.width) / atlas.width,
            Mathf.Round(source.yMax * atlas.height) / atlas.height);
        float cornerX = source.width * .23f, cornerY = source.height * .23f;
        float cap = Mathf.Min(24, Mathf.Min(target.width, target.height) / 3);
        float[] dx = { target.x, target.x + cap, target.xMax - cap, target.xMax };
        float[] dy = { target.y, target.y + cap, target.yMax - cap, target.yMax };
        float[] sx = { source.x, source.x + cornerX, source.xMax - cornerX, source.xMax };
        float[] sy = { source.y, source.y + cornerY, source.yMax - cornerY, source.yMax };
        for (int i = 0; i < 4; i++)
        {
            sx[i] = Mathf.Round(sx[i] * atlas.width) / atlas.width;
            sy[i] = Mathf.Round(sy[i] * atlas.height) / atlas.height;
        }
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                GUI.DrawTextureWithTexCoords(new Rect(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]), atlas,
                    new Rect(sx[col], sy[2 - row], sx[col + 1] - sx[col], sy[3 - row] - sy[2 - row]));
    }
    public static void AtlasIcon(Rect r, Texture2D atlas, int index)
    {
        if (atlas == null) return;
        float left = Mathf.Round(index % 4 * atlas.width / 4f);
        float right = Mathf.Round((index % 4 + 1) * atlas.width / 4f);
        float bottom = Mathf.Round((3 - index / 4) * atlas.height / 4f);
        float top = Mathf.Round((4 - index / 4) * atlas.height / 4f);
        GUI.DrawTextureWithTexCoords(r, atlas,
            new Rect(left / atlas.width, bottom / atlas.height, (right - left) / atlas.width, (top - bottom) / atlas.height));
    }
    public static void Progress(Rect r, float value)
    {
        Frame(r, new Color(.95f, .91f, .86f), new Color(.55f, .48f, .65f));
        float available = r.width - 12;
        int count = Mathf.Max(1, Mathf.FloorToInt(available / 18));
        float step = available / count;
        int complete = Mathf.CeilToInt(count * Mathf.Clamp01(value));
        for (int i = 0; i < complete; i++)
        {
            Rect candy = new Rect(r.x + 6 + i * step, r.y + 6, step - 3, r.height - 12);
            Fill(candy, i % 2 == 0 ? new Color(.68f,.77f,.94f) : new Color(.89f,.67f,.76f));
            Fill(new Rect(candy.x + 2, candy.y + 1, candy.width - 4, 2), new Color(1,1,1,.65f));
        }
    }
    public static void FittedLabel(Rect r, string value, int size, Color color, TextAnchor alignment)
    {
        var style = Text(size, color, alignment);
        while (style.fontSize > 10 && style.CalcHeight(new GUIContent(value), r.width) > r.height)
            style.fontSize--;
        GUI.BeginGroup(r);
        GUI.Label(new Rect(0, 0, r.width, r.height), value, style);
        GUI.EndGroup();
    }
}
