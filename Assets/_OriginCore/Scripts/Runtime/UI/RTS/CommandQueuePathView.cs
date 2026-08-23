using System.Collections.Generic;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class CommandQueuePathView : MonoBehaviour
    {
        private const float LineWidth = 0.055f;
        private const float LineYOffset = 0.12f;
        private const int DashTextureWidth = 32;
        private const int DashSolidPixels = 18;
        private const int LineRenderQueue = 5000;
        private const int LineSortingOrder = 64;
        private const string OverlayShaderName = "OriginCore/Command Queue Overlay";

        private static readonly Color StandardLineColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color AttackLineColor = new Color(1f, 0.12f, 0.08f, 0.95f);
        private static Material s_lineMaterial;
        private static Texture2D s_dashTexture;

        [SerializeField] private UnitCommandQueue _commandQueue;
        [SerializeField] private MonoBehaviour _routeSourceComponent;
        [SerializeField] private Selectable _selectable;

        private readonly List<Vector3> _routePoints =
            new List<Vector3>(UnitCommandQueue.WaitingCapacity + 2);
        private readonly List<RtsRouteSegment> _routeSegments =
            new List<RtsRouteSegment>(UnitCommandQueue.WaitingCapacity + 1);
        private readonly List<LineRenderer> _segmentRenderers =
            new List<LineRenderer>(UnitCommandQueue.WaitingCapacity + 1);
        private IRtsRouteSource _routeSource;
        private IRtsStyledRouteSource _styledRouteSource;

        public UnitCommandQueue CommandQueue => _commandQueue;
        public MonoBehaviour RouteSourceComponent => _routeSourceComponent;
        public IRtsRouteSource RouteSource => _routeSource;
        public Selectable Selectable => _selectable;
        public LineRenderer RuntimeLineRenderer => _segmentRenderers.Count > 0
            ? _segmentRenderers[0]
            : null;
        public static Color StandardRouteColor => StandardLineColor;
        public static Color AttackRouteColor => AttackLineColor;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
        }

        private void OnDisable()
        {
            HideRoute();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void LateUpdate()
        {
            RefreshRoute();
        }

        public void Configure(UnitCommandQueue commandQueue, Selectable selectable)
        {
            _commandQueue = commandQueue != null
                ? commandQueue
                : GetComponent<UnitCommandQueue>();
            _routeSourceComponent = _commandQueue;
            _routeSource = _commandQueue;
            _styledRouteSource = _commandQueue;
            _selectable = selectable != null
                ? selectable
                : GetComponent<Selectable>();
            RefreshRoute();
        }

        public bool Configure(MonoBehaviour routeSource, Selectable selectable)
        {
            if (routeSource != null && !(routeSource is IRtsRouteSource))
            {
                return false;
            }

            _routeSourceComponent = routeSource;
            _routeSource = routeSource as IRtsRouteSource;
            _styledRouteSource = routeSource as IRtsStyledRouteSource;
            _commandQueue = routeSource as UnitCommandQueue;
            _selectable = selectable != null
                ? selectable
                : GetComponent<Selectable>();
            RefreshRoute();
            return _routeSource != null;
        }

        public void RefreshRoute()
        {
            CacheComponents();
            if (_routeSource == null || _selectable == null ||
                _selectable.State != SelectionVisualState.Selected ||
                !_selectable.IsAlive || !TryBuildRenderSegments())
            {
                HideRoute();
                return;
            }

            for (int i = 0; i < _routeSegments.Count; i++)
            {
                LineRenderer renderer = EnsureSegmentRenderer(i);
                if (renderer == null)
                {
                    continue;
                }

                RtsRouteSegment segment = _routeSegments[i];
                Color color = GetRouteColor(segment.Style);
                renderer.startColor = color;
                renderer.endColor = color;
                renderer.positionCount = 2;
                renderer.SetPosition(0, segment.Start + Vector3.up * LineYOffset);
                renderer.SetPosition(1, segment.End + Vector3.up * LineYOffset);
                renderer.enabled = true;
            }

            for (int i = _routeSegments.Count; i < _segmentRenderers.Count; i++)
            {
                DisableRenderer(_segmentRenderers[i]);
            }
        }

        private void CacheComponents()
        {
            if (_routeSourceComponent == null && _commandQueue != null)
            {
                _routeSourceComponent = _commandQueue;
            }

            if (_routeSourceComponent == null)
            {
                UnitCommandQueue queue = GetComponent<UnitCommandQueue>();
                if (queue != null)
                {
                    _commandQueue = queue;
                    _routeSourceComponent = queue;
                }
                else
                {
                    MonoBehaviour[] components = GetComponents<MonoBehaviour>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] is IRtsRouteSource)
                        {
                            _routeSourceComponent = components[i];
                            break;
                        }
                    }
                }
            }

            _routeSource = _routeSourceComponent as IRtsRouteSource;
            _styledRouteSource = _routeSourceComponent as IRtsStyledRouteSource;
            _commandQueue = _routeSourceComponent as UnitCommandQueue;

            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }
        }

        private bool TryBuildRenderSegments()
        {
            _routeSegments.Clear();
            if (_styledRouteSource != null)
            {
                if (!_styledRouteSource.TryBuildStyledRoute(_routeSegments))
                {
                    return false;
                }

                int visibleCount = 0;
                for (int i = 0; i < _routeSegments.Count; i++)
                {
                    RtsRouteSegment segment = _routeSegments[i];
                    if (segment.Style == RtsRouteStyle.None)
                    {
                        continue;
                    }

                    _routeSegments[visibleCount++] = segment;
                }

                if (visibleCount < _routeSegments.Count)
                {
                    _routeSegments.RemoveRange(
                        visibleCount,
                        _routeSegments.Count - visibleCount);
                }

                return _routeSegments.Count > 0;
            }

            if (!_routeSource.TryBuildRoute(_routePoints) || _routePoints.Count < 2)
            {
                return false;
            }

            for (int i = 1; i < _routePoints.Count; i++)
            {
                _routeSegments.Add(new RtsRouteSegment(
                    _routePoints[i - 1],
                    _routePoints[i],
                    RtsRouteStyle.Standard));
            }

            return _routeSegments.Count > 0;
        }

        private LineRenderer EnsureSegmentRenderer(int index)
        {
            while (_segmentRenderers.Count <= index)
            {
                LineRenderer renderer = CreateSegmentRenderer(_segmentRenderers.Count);
                if (renderer == null)
                {
                    return null;
                }

                _segmentRenderers.Add(renderer);
            }

            return _segmentRenderers[index];
        }

        private LineRenderer CreateSegmentRenderer(int index)
        {
            string objectName = index == 0
                ? "CommandQueuePath"
                : "CommandQueuePath_" + (index + 1).ToString("00");
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Tile;
            renderer.loop = false;
            renderer.startWidth = LineWidth;
            renderer.endWidth = LineWidth;
            renderer.startColor = StandardLineColor;
            renderer.endColor = StandardLineColor;
            renderer.numCapVertices = 0;
            renderer.numCornerVertices = 2;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = LineSortingOrder;
            renderer.enabled = false;

            Material material = GetLineMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return renderer;
        }

        private void HideRoute()
        {
            _routePoints.Clear();
            _routeSegments.Clear();
            for (int i = 0; i < _segmentRenderers.Count; i++)
            {
                DisableRenderer(_segmentRenderers[i]);
            }
        }

        private static void DisableRenderer(LineRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.positionCount = 0;
            renderer.enabled = false;
        }

        private static Color GetRouteColor(RtsRouteStyle style)
        {
            return style == RtsRouteStyle.Attack
                ? AttackLineColor
                : StandardLineColor;
        }

        private static Material GetLineMaterial()
        {
            if (s_lineMaterial != null)
            {
                return s_lineMaterial;
            }

            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            s_lineMaterial = new Material(shader)
            {
                name = "CommandQueuePathDashedLine",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GetDashTexture(),
                renderQueue = LineRenderQueue
            };

            if (s_lineMaterial.HasProperty("_Color"))
            {
                s_lineMaterial.SetColor("_Color", Color.white);
            }

            return s_lineMaterial;
        }

        private static Texture2D GetDashTexture()
        {
            if (s_dashTexture != null)
            {
                return s_dashTexture;
            }

            Color32[] pixels = new Color32[DashTextureWidth];
            Color32 solid = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = i < DashSolidPixels ? solid : clear;
            }

            s_dashTexture = new Texture2D(
                DashTextureWidth,
                1,
                TextureFormat.RGBA32,
                false)
            {
                name = "CommandQueuePathDashPattern",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            s_dashTexture.SetPixels32(pixels);
            s_dashTexture.Apply(false, true);
            return s_dashTexture;
        }
    }
}
