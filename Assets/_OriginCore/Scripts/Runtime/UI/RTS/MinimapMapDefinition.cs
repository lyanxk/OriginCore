using System;
using UnityEngine;

namespace OriginCore.UI.RTS
{
    [Serializable]
    public struct MinimapMapRegion
    {
        [SerializeField] private Rect _normalizedRect;
        [SerializeField] private Color _color;
        [SerializeField] private bool _ellipse;

        public MinimapMapRegion(Rect normalizedRect, Color color, bool ellipse = false)
        {
            _normalizedRect = normalizedRect;
            _color = color;
            _ellipse = ellipse;
        }

        public Rect NormalizedRect => _normalizedRect;
        public Color Color => _color;
        public bool Ellipse => _ellipse;
    }

    [CreateAssetMenu(
        fileName = "SO_MinimapMap",
        menuName = "OriginCore/RTS/Minimap Map Definition")]
    public sealed class MinimapMapDefinition : ScriptableObject
    {
        [SerializeField] private Texture2D _authoredTexture;
        [Range(64, 1024), SerializeField] private int _resolution = 256;
        [SerializeField] private Color _backgroundColor =
            new Color(0.035f, 0.065f, 0.065f, 1f);
        [SerializeField] private Color _gridColor =
            new Color(0.12f, 0.22f, 0.20f, 1f);
        [SerializeField] private Color _borderColor =
            new Color(0.55f, 0.78f, 0.70f, 1f);
        [Range(0, 32), SerializeField] private int _gridDivisions = 10;
        [Range(1, 8), SerializeField] private int _lineThickness = 1;
        [SerializeField] private MinimapMapRegion[] _regions =
            Array.Empty<MinimapMapRegion>();

        public Texture2D AuthoredTexture => _authoredTexture;
        public int Resolution => _resolution;
        public Color BackgroundColor => _backgroundColor;
        public Color GridColor => _gridColor;
        public Color BorderColor => _borderColor;
        public int GridDivisions => _gridDivisions;
        public int LineThickness => _lineThickness;
        public MinimapMapRegion[] Regions => _regions;
        public bool UsesAuthoredTexture => _authoredTexture != null;

        public void Configure(
            Texture2D authoredTexture,
            int resolution,
            Color backgroundColor,
            Color gridColor,
            Color borderColor,
            int gridDivisions,
            int lineThickness,
            MinimapMapRegion[] regions)
        {
            _authoredTexture = authoredTexture;
            _resolution = Mathf.Clamp(resolution, 64, 1024);
            _backgroundColor = backgroundColor;
            _gridColor = gridColor;
            _borderColor = borderColor;
            _gridDivisions = Mathf.Clamp(gridDivisions, 0, 32);
            _lineThickness = Mathf.Clamp(lineThickness, 1, 8);
            _regions = regions ?? Array.Empty<MinimapMapRegion>();
        }

        public Texture2D CreateGeneratedTexture()
        {
            int resolution = Mathf.Clamp(_resolution, 64, 1024);
            Color32[] pixels = new Color32[resolution * resolution];
            Color32 background = _backgroundColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }

            for (int i = 0; i < _regions.Length; i++)
            {
                DrawRegion(pixels, resolution, _regions[i]);
            }

            int thickness = Mathf.Clamp(_lineThickness, 1, 8);
            int divisions = Mathf.Clamp(_gridDivisions, 0, 32);
            if (divisions > 1)
            {
                Color32 grid = _gridColor;
                for (int i = 1; i < divisions; i++)
                {
                    int coordinate = Mathf.RoundToInt(
                        i / (float)divisions * (resolution - 1));
                    DrawVerticalLine(pixels, resolution, coordinate, thickness, grid);
                    DrawHorizontalLine(pixels, resolution, coordinate, thickness, grid);
                }
            }

            Color32 border = _borderColor;
            for (int i = 0; i < thickness; i++)
            {
                DrawVerticalLine(pixels, resolution, i, 1, border);
                DrawVerticalLine(pixels, resolution, resolution - 1 - i, 1, border);
                DrawHorizontalLine(pixels, resolution, i, 1, border);
                DrawHorizontalLine(pixels, resolution, resolution - 1 - i, 1, border);
            }

            Texture2D texture = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = name + "_RuntimeMap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnValidate()
        {
            _resolution = Mathf.Clamp(_resolution, 64, 1024);
            _gridDivisions = Mathf.Clamp(_gridDivisions, 0, 32);
            _lineThickness = Mathf.Clamp(_lineThickness, 1, 8);
            if (_regions == null)
            {
                _regions = Array.Empty<MinimapMapRegion>();
            }
        }

        private static void DrawRegion(
            Color32[] pixels,
            int resolution,
            MinimapMapRegion region)
        {
            Rect rect = region.NormalizedRect;
            float xMinNormalized = Mathf.Clamp01(Mathf.Min(rect.xMin, rect.xMax));
            float xMaxNormalized = Mathf.Clamp01(Mathf.Max(rect.xMin, rect.xMax));
            float yMinNormalized = Mathf.Clamp01(Mathf.Min(rect.yMin, rect.yMax));
            float yMaxNormalized = Mathf.Clamp01(Mathf.Max(rect.yMin, rect.yMax));
            int xMin = Mathf.FloorToInt(xMinNormalized * (resolution - 1));
            int xMax = Mathf.CeilToInt(xMaxNormalized * (resolution - 1));
            int yMin = Mathf.FloorToInt(yMinNormalized * (resolution - 1));
            int yMax = Mathf.CeilToInt(yMaxNormalized * (resolution - 1));
            float centerX = (xMin + xMax) * 0.5f;
            float centerY = (yMin + yMax) * 0.5f;
            float radiusX = Mathf.Max(0.5f, (xMax - xMin) * 0.5f);
            float radiusY = Mathf.Max(0.5f, (yMax - yMin) * 0.5f);
            Color32 color = region.Color;

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (region.Ellipse)
                    {
                        float dx = (x - centerX) / radiusX;
                        float dy = (y - centerY) / radiusY;
                        if (dx * dx + dy * dy > 1f)
                        {
                            continue;
                        }
                    }

                    pixels[y * resolution + x] = color;
                }
            }
        }

        private static void DrawVerticalLine(
            Color32[] pixels,
            int resolution,
            int x,
            int thickness,
            Color32 color)
        {
            int start = Mathf.Clamp(x - (thickness - 1) / 2, 0, resolution - 1);
            int end = Mathf.Clamp(start + thickness - 1, 0, resolution - 1);
            for (int lineX = start; lineX <= end; lineX++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    pixels[y * resolution + lineX] = color;
                }
            }
        }

        private static void DrawHorizontalLine(
            Color32[] pixels,
            int resolution,
            int y,
            int thickness,
            Color32 color)
        {
            int start = Mathf.Clamp(y - (thickness - 1) / 2, 0, resolution - 1);
            int end = Mathf.Clamp(start + thickness - 1, 0, resolution - 1);
            for (int lineY = start; lineY <= end; lineY++)
            {
                int row = lineY * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    pixels[row + x] = color;
                }
            }
        }
    }
}
