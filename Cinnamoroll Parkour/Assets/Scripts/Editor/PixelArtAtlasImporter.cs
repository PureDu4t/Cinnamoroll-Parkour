using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Clean atlas gutters at import time; no pixel processing is done during gameplay.</summary>
internal sealed class PixelArtAtlasImporter : AssetPostprocessor
{
    private static bool Layout(string path, out int columns, out int rows)
    {
        columns = rows = 1;
        if (!path.StartsWith("Assets/Resources/PixelArt/", StringComparison.Ordinal)) return false;
        switch (Path.GetFileNameWithoutExtension(path))
        {
            case "PixelTiles": case "PixelCafePropsV2": columns = rows = 4; return true;
            case "PixelCafeFurnitureV2": columns = 4; rows = 2; return true;
            case "PixelCafeTerrain": case "PixelForestTerrain": columns = 2; return true;
            case "PixelInvisibleWalls": case "PixelCafeHeightFurniture": columns = 3; return true;
            case "PixelCartoonUiSkin": columns = rows = 2; return true;
            case "PixelPlayer": case "PixelCloudBarrier": return true;
            default: return false;
        }
    }

    public override uint GetVersion() => 1;

    private void OnPreprocessTexture()
    {
        if (!Layout(assetPath, out _, out _)) return;
        var importer = (TextureImporter)assetImporter;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 4096;
        foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (!settings.overridden) continue;
            settings.maxTextureSize = 4096;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(settings);
        }
    }

    private void OnPostprocessTexture(Texture2D texture)
    {
        if (!Layout(assetPath, out int columns, out int rows)) return;
        Color32[] pixels = texture.GetPixels32();
        string name = Path.GetFileNameWithoutExtension(assetPath);
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int left = Mathf.RoundToInt(texture.width * column / (float)columns);
                int right = Mathf.RoundToInt(texture.width * (column + 1) / (float)columns);
                int bottom = Mathf.RoundToInt(texture.height * row / (float)rows);
                int top = Mathf.RoundToInt(texture.height * (row + 1) / (float)rows);
                // Only internal atlas boundaries need gutters. Keep outer silhouettes intact.
                int leftGutter = column > 0 ? 3 : 0;
                int rightGutter = column < columns - 1 ? 3 : 0;
                if ((name == "PixelForestTerrain" || name == "PixelCafeTerrain") && column == 1)
                    leftGutter = 10;
                // The tall cupboard overlaps the fridge column in the source sheet.
                if (name == "PixelCafeHeightFurniture" && column == 1) rightGutter = 12;
                for (int y = bottom; y < top; y++)
                    for (int x = left; x < right; x++)
                        if (x < left + leftGutter || x >= right - rightGutter
                            || (row > 0 && y < bottom + 3) || (row < rows - 1 && y >= top - 3))
                            pixels[y * texture.width + x] = default;
                CleanCell(pixels, texture.width, left, bottom, right - left, top - bottom);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private sealed class Region
    {
        public int count, left = int.MaxValue, bottom = int.MaxValue, right, top;
        public void Add(int x, int y)
        {
            count++; left = Math.Min(left, x); right = Math.Max(right, x);
            bottom = Math.Min(bottom, y); top = Math.Max(top, y);
        }
    }

    private static void CleanCell(Color32[] pixels, int stride, int left, int bottom, int width, int height)
    {
        var labels = new int[width * height];
        var queue = new int[labels.Length];
        var regions = new List<Region> { new Region() };
        int largest = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int start = y * width + x;
                if (labels[start] != 0 || pixels[(bottom + y) * stride + left + x].a < 8) continue;
                var region = new Region();
                int id = regions.Count, head = 0, tail = 1;
                regions.Add(region); queue[0] = start; labels[start] = id;
                while (head < tail)
                {
                    int position = queue[head++], px = position % width, py = position / width;
                    region.Add(px, py);
                    // Eight neighbours preserve diagonal pixel outlines and fine vine links.
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = px + dx, ny = py + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            int next = ny * width + nx;
                            if (labels[next] != 0 || pixels[(bottom + ny) * stride + left + nx].a < 8) continue;
                            labels[next] = id; queue[tail++] = next;
                        }
                }
                if (largest == 0 || region.count > regions[largest].count) largest = id;
            }
        if (largest == 0) return;
        var body = regions[largest];
        var remove = new bool[regions.Count];
        for (int id = 1; id < regions.Count; id++)
        {
            if (id == largest) continue;
            var region = regions[id];
            int gapX = Math.Max(0, Math.Max(body.left - region.right, region.left - body.right));
            int gapY = Math.Max(0, Math.Max(body.bottom - region.top, region.bottom - body.top));
            bool far = Math.Max(gapX, gapY) > Math.Min(width, height) * .04f;
            bool edgeLine = (region.right - region.left < 3 && (region.left < 4 || region.right >= width - 4))
                || (region.top - region.bottom < 3 && (region.bottom < 4 || region.top >= height - 4));
            remove[id] = edgeLine || region.count < Math.Max(8, body.count / 2000)
                || (far && region.count < body.count / 100);
        }
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int label = labels[y * width + x];
                int index = (bottom + y) * stride + left + x;
                if (label == 0 || remove[label]) pixels[index] = default;
            }
    }
}
