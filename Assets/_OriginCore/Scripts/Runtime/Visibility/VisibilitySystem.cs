using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Gameplay;
using OriginCore.Save;
using UnityEngine;

namespace OriginCore.Visibility
{
    [DisallowMultipleComponent]
    public sealed class VisibilitySystem : MonoBehaviour, IVisibilitySaveBridge
    {
        private static readonly int GlobalFogTextureId =
            Shader.PropertyToID("_OriginCoreFogTex");
        private static readonly int GlobalFogWorldRectId =
            Shader.PropertyToID("_OriginCoreFogWorldRect");
        private static readonly int GlobalFogVolumeBoundsId =
            Shader.PropertyToID("_OriginCoreFogVolumeBounds");
        private static readonly int GlobalFogActiveId =
            Shader.PropertyToID("_OriginCoreFogActive");

        private static VisibilitySystem _globalFogOwner;

        [SerializeField] private VisibilityConfig _config;
        [SerializeField] private Collider _boundsSource;
        [SerializeField] private CommanderVisibilityPolicy _visibilityPolicy;
        [SerializeField] private VisibilityElevationDefinition _elevationDefinition;

        private readonly List<VisionEmitter> _emitters = new List<VisionEmitter>(32);
        private readonly List<VisibilityTarget> _targets = new List<VisibilityTarget>(32);
        private readonly List<BuildingMemoryGhost> _buildingMemories =
            new List<BuildingMemoryGhost>(8);
        private VisibilityGrid _grid;
        private Texture2D _fogTexture;
        private Color32[] _fogPixels;
        private float _timeUntilUpdate;
        private readonly HeightAwareVisibilitySolver _solver =
            new HeightAwareVisibilitySolver();
        private bool _reportedFlatCompatibility;
        private bool _hasWorldBoundsOverride;
        private Bounds _worldBoundsOverride;

        public VisibilityConfig Config => _config;
        public Collider BoundsSource => _boundsSource;
        public VisibilityGrid Grid => _grid;
        public CommanderVisibilityPolicy VisibilityPolicy => _visibilityPolicy;
        public Texture2D FogTexture => _fogTexture;
        public IReadOnlyList<BuildingMemoryGhost> BuildingMemories => _buildingMemories;
        public FactionId ObserverFaction => _config != null
            ? _config.ObserverFaction
            : FactionId.Friendly;
        public int EmitterCount => CountLive(_emitters);
        public int TargetCount => CountLive(_targets);
        public int BuildingMemoryCount => CountLive(_buildingMemories);
        public int TickCount { get; private set; }
        public VisibilityElevationDefinition ElevationDefinition => _elevationDefinition;
        public bool UsesHeightAwareVisibility => _solver.IsHeightAware;
        public bool IsConfigured => _config != null &&
                                    (_hasWorldBoundsOverride || _boundsSource != null);
        public Bounds WorldBounds => _grid != null
            ? _grid.WorldBounds
            : ResolveWorldBounds();
        public static bool HasActiveFogOfWar =>
            _globalFogOwner != null && _globalFogOwner.isActiveAndEnabled &&
            _globalFogOwner._fogTexture != null && _globalFogOwner._grid != null;

        // Kept for serialized/editor integrations that still use the former name.
        public static bool HasActiveVolumetricFog => HasActiveFogOfWar;

        private void OnEnable()
        {
            EnsureInitialized();
            PublishFogShaderGlobals();
            _timeUntilUpdate = 0f;
        }

        private void Start()
        {
            TickNow();
        }

        private void Update()
        {
            if (_grid == null || _config == null || Time.deltaTime <= 0f)
            {
                return;
            }

            _timeUntilUpdate -= Time.deltaTime;
            if (_timeUntilUpdate > 0f)
            {
                return;
            }

            TickNow();
            _timeUntilUpdate = _config.UpdateInterval;
        }

        private void OnDisable()
        {
            ReleaseGlobalFogOwnership();
        }

        private void OnDestroy()
        {
            ReleaseGlobalFogOwnership();
            if (_fogTexture != null)
            {
                DestroyImmediateSafe(_fogTexture);
                _fogTexture = null;
            }
        }

        private void OnValidate()
        {
            if (_boundsSource == null)
            {
                _boundsSource = GetComponent<Collider>();
            }
        }

        public void Configure(
            VisibilityConfig config,
            Collider boundsSource,
            VisibilityElevationDefinition elevationDefinition = null)
        {
            _config = config;
            _boundsSource = boundsSource;
            _elevationDefinition = elevationDefinition;
            _hasWorldBoundsOverride = false;
            RebuildGrid();
        }

        public void SetWorldBounds(Bounds worldBounds)
        {
            Vector3 size = worldBounds.size;
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            size.z = Mathf.Max(0.1f, size.z);
            worldBounds.size = size;

            if (_hasWorldBoundsOverride &&
                Approximately(_worldBoundsOverride, worldBounds))
            {
                return;
            }

            _worldBoundsOverride = worldBounds;
            _hasWorldBoundsOverride = true;
            RebuildGrid();
        }

        public void SetVisibilityPolicy(CommanderVisibilityPolicy visibilityPolicy)
        {
            if (_visibilityPolicy == visibilityPolicy)
            {
                return;
            }

            _visibilityPolicy = visibilityPolicy;
            if (_grid == null)
            {
                return;
            }

            if (_visibilityPolicy == CommanderVisibilityPolicy.ExploredRemainsObserved)
            {
                _grid.PromoteExploredToVisible();
            }
            else
            {
                _grid.ClearVisible();
            }

            TickNow();
        }

        public void SetElevationDefinition(
            VisibilityElevationDefinition elevationDefinition)
        {
            if (_elevationDefinition == elevationDefinition && _grid != null)
            {
                return;
            }
            _elevationDefinition = elevationDefinition;
            _reportedFlatCompatibility = false;
            RebuildGrid();
        }

        public bool RegisterEmitter(VisionEmitter emitter)
        {
            if (emitter == null || _emitters.Contains(emitter))
            {
                return false;
            }

            _emitters.Add(emitter);
            return true;
        }

        public bool UnregisterEmitter(VisionEmitter emitter)
        {
            return emitter != null && _emitters.Remove(emitter);
        }

        public bool RegisterTarget(VisibilityTarget target)
        {
            if (target == null)
            {
                return false;
            }

            bool added = false;
            if (!_targets.Contains(target))
            {
                _targets.Add(target);
                added = true;
            }

            EnsureInitialized();
            ApplyTargetVisibility(target);
            EnsureBuildingMemory(target);
            return added;
        }

        public bool UnregisterTarget(VisibilityTarget target)
        {
            return target != null && _targets.Remove(target);
        }

        public bool TickNow()
        {
            if (!EnsureInitialized())
            {
                return false;
            }

            if (_visibilityPolicy == CommanderVisibilityPolicy.StandardFogOfWar)
            {
                _grid.ClearVisible();
            }
            else
            {
                _grid.PromoteExploredToVisible();
            }
            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                VisionEmitter emitter = _emitters[i];
                if (emitter == null)
                {
                    _emitters.RemoveAt(i);
                    continue;
                }

                if (emitter.ContributesTo(ObserverFaction))
                {
                    _solver.MarkVisible(_grid, emitter);
                }
            }

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                VisibilityTarget target = _targets[i];
                if (target == null)
                {
                    _targets.RemoveAt(i);
                    continue;
                }

                ApplyTargetVisibility(target);
                EnsureBuildingMemory(target);
            }

            for (int i = _buildingMemories.Count - 1; i >= 0; i--)
            {
                BuildingMemoryGhost memory = _buildingMemories[i];
                if (memory == null)
                {
                    _buildingMemories.RemoveAt(i);
                    continue;
                }

                memory.Refresh(_grid);
            }

            RefreshFogTexture();
            TickCount++;
            return true;
        }

        public VisibilityCellState GetState(Vector3 worldPosition)
        {
            return _grid != null
                ? _grid.GetState(worldPosition)
                : VisibilityCellState.Hidden;
        }

        public void CaptureExploredState(VisibilitySaveData data)
        {
            if (data == null)
            {
                return;
            }

            data.EnsureCollections();
            if (_grid == null)
            {
                data.exploredCellIds.Clear();
                return;
            }

            _grid.CaptureExploredCellIds(data.exploredCellIds);
        }

        public void RestoreExploredState(VisibilitySaveData data)
        {
            if (!EnsureInitialized())
            {
                return;
            }

            data?.EnsureCollections();
            _grid.RestoreExploredCellIds(data != null ? data.exploredCellIds : null);
            if (_visibilityPolicy == CommanderVisibilityPolicy.ExploredRemainsObserved)
            {
                _grid.PromoteExploredToVisible();
            }

            TickNow();
        }

        private bool EnsureInitialized()
        {
            if (_grid != null)
            {
                return true;
            }

            if (!IsConfigured)
            {
                return false;
            }

            RebuildGrid();
            return _grid != null;
        }

        private void RebuildGrid()
        {
            if (!IsConfigured)
            {
                _grid = null;
                return;
            }

            _grid = new VisibilityGrid(ResolveWorldBounds(), _config.CellSize);
            bool heightAware = _solver.Configure(
                _grid,
                _elevationDefinition,
                out string elevationError);
            if (!heightAware && !_reportedFlatCompatibility)
            {
                Debug.LogWarning("[OriginCore Visibility] " + elevationError, this);
                _reportedFlatCompatibility = true;
            }
            else if (_elevationDefinition != null &&
                     _elevationDefinition.IsFlatCompatibility &&
                     !_reportedFlatCompatibility)
            {
                Debug.LogWarning(
                    "[OriginCore Visibility] Map uses explicit flat compatibility elevation '" +
                    _elevationDefinition.Revision + "'.",
                    this);
                _reportedFlatCompatibility = true;
            }
            if (_fogTexture != null)
            {
                DestroyImmediateSafe(_fogTexture);
            }

            _fogTexture = new Texture2D(
                _grid.Width,
                _grid.Height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "P15_RuntimeFogTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _fogPixels = new Color32[_grid.CellCount];
            RefreshFogTexture();
            PublishFogShaderGlobals();
        }

        private void ApplyTargetVisibility(VisibilityTarget target)
        {
            bool allied = target.Faction != null &&
                          target.Faction.Faction == ObserverFaction;
            bool visible = allied || _grid.GetState(target.transform.position) ==
                           VisibilityCellState.Visible;
            target.ApplyVisibility(visible);
        }

        private void EnsureBuildingMemory(VisibilityTarget target)
        {
            if (target == null || !target.RememberAsBuilding)
            {
                return;
            }

            for (int i = 0; i < _buildingMemories.Count; i++)
            {
                BuildingMemoryGhost existing = _buildingMemories[i];
                if (existing != null && existing.SourceTarget == target)
                {
                    return;
                }
            }

            BuildingMemoryGhost memory = BuildingMemoryGhost.Create(target);
            if (memory != null)
            {
                _buildingMemories.Add(memory);
            }
        }

        public bool TryGetBuildingMemory(
            VisibilityTarget target,
            out BuildingMemoryGhost memory)
        {
            memory = null;
            if (target == null)
            {
                return false;
            }

            for (int i = _buildingMemories.Count - 1; i >= 0; i--)
            {
                BuildingMemoryGhost candidate = _buildingMemories[i];
                if (candidate == null)
                {
                    _buildingMemories.RemoveAt(i);
                    continue;
                }

                if (candidate.SourceTarget == target)
                {
                    memory = candidate;
                    return true;
                }
            }

            return false;
        }

        private void RefreshFogTexture()
        {
            if (_grid == null || _config == null || _fogTexture == null ||
                _fogPixels == null || _fogPixels.Length != _grid.CellCount)
            {
                return;
            }

            byte hidden = ToAlpha(_config.HiddenAlpha);
            byte explored = ToAlpha(_config.ExploredAlpha);
            byte visible = ToAlpha(_config.VisibleAlpha);
            int index = 0;
            for (int z = 0; z < _grid.Height; z++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    byte alpha;
                    switch (_grid.GetState(x, z))
                    {
                        case VisibilityCellState.Visible:
                            alpha = visible;
                            break;
                        case VisibilityCellState.Explored:
                            alpha = explored;
                            break;
                        default:
                            alpha = hidden;
                            break;
                    }

                    _fogPixels[index++] = new Color32(255, 255, 255, alpha);
                }
            }

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply(false, false);
            PublishFogShaderGlobals();
        }

        private void PublishFogShaderGlobals()
        {
            if (_fogTexture == null || _grid == null)
            {
                return;
            }

            Bounds bounds = _grid.WorldBounds;
            _globalFogOwner = this;
            Shader.SetGlobalTexture(GlobalFogTextureId, _fogTexture);
            Shader.SetGlobalVector(
                GlobalFogWorldRectId,
                new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
            Shader.SetGlobalVector(
                GlobalFogVolumeBoundsId,
                new Vector4(bounds.min.y, bounds.max.y, 0f, 0f));
            Shader.SetGlobalFloat(GlobalFogActiveId, 1f);
        }

        private void ReleaseGlobalFogOwnership()
        {
            if (_globalFogOwner != this)
            {
                return;
            }

            _globalFogOwner = null;
            Shader.SetGlobalFloat(GlobalFogActiveId, 0f);
            Shader.SetGlobalTexture(GlobalFogTextureId, Texture2D.blackTexture);
        }

        private static byte ToAlpha(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private Bounds ResolveWorldBounds()
        {
            if (_hasWorldBoundsOverride)
            {
                return _worldBoundsOverride;
            }

            return _boundsSource != null
                ? _boundsSource.bounds
                : new Bounds(Vector3.zero, Vector3.one);
        }

        private static bool Approximately(Bounds left, Bounds right)
        {
            return (left.center - right.center).sqrMagnitude <= 0.0001f &&
                   (left.size - right.size).sqrMagnitude <= 0.0001f;
        }

        private static int CountLive<T>(List<T> values) where T : Object
        {
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void DestroyImmediateSafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
