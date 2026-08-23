using OriginCore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriginCore.Visibility
{
    [DisallowMultipleComponent]
    public sealed class BuildingMemoryGhost : MonoBehaviour
    {
        private VisibilityTarget _sourceTarget;
        private bool _hasMemory;
        private Vector3 _lastKnownPosition;
        private Quaternion _lastKnownRotation;
        private Vector3 _lastKnownScale = Vector3.one;
        private FactionId _faction = FactionId.Neutral;
        private UnitRole _roles = UnitRole.Building;

        public VisibilityTarget SourceTarget => _sourceTarget;
        public bool HasMemory => _hasMemory;
        public bool IsGhostVisible => gameObject.activeSelf;
        public Vector3 LastKnownPosition => _lastKnownPosition;
        public FactionId Faction => _faction;
        public UnitRole Roles => _roles;

        public static BuildingMemoryGhost Create(VisibilityTarget sourceTarget)
        {
            if (sourceTarget == null)
            {
                return null;
            }

            GameObject root = new GameObject(
                "P15_Memory_" + sourceTarget.gameObject.name);
            Scene sourceScene = sourceTarget.gameObject.scene;
            if (sourceScene.IsValid() && sourceScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(root, sourceScene);
            }

            BuildingMemoryGhost memory = root.AddComponent<BuildingMemoryGhost>();
            memory._sourceTarget = sourceTarget;
            if (sourceTarget.Faction != null)
            {
                memory._faction = sourceTarget.Faction.Faction;
            }

            if (sourceTarget.Identity != null)
            {
                memory._roles = sourceTarget.Identity.Roles | UnitRole.Building;
            }

            root.SetActive(false);
            return memory;
        }

        public void Refresh(VisibilityGrid grid)
        {
            if (grid == null)
            {
                SetGhostVisible(false);
                return;
            }

            bool sourceAvailable = _sourceTarget != null &&
                                   _sourceTarget.gameObject.activeInHierarchy;
            if (sourceAvailable)
            {
                VisibilityCellState sourceState = grid.GetState(
                    _sourceTarget.transform.position);
                if (_sourceTarget.IsVisible &&
                    sourceState == VisibilityCellState.Visible)
                {
                    if (!_sourceTarget.IsAlive)
                    {
                        DestroyGhost();
                        return;
                    }

                    RecordSourceTransform();
                    SetGhostVisible(false);
                    return;
                }
            }

            if (!_hasMemory)
            {
                SetGhostVisible(false);
                return;
            }

            VisibilityCellState lastKnownState = grid.GetState(_lastKnownPosition);
            if (lastKnownState == VisibilityCellState.Visible)
            {
                if (!sourceAvailable)
                {
                    DestroyGhost();
                }
                else
                {
                    _hasMemory = false;
                    SetGhostVisible(false);
                }
                return;
            }

            ApplyLastKnownTransform();
            SetGhostVisible(lastKnownState == VisibilityCellState.Explored);
        }

        private void RecordSourceTransform()
        {
            Transform source = _sourceTarget.transform;
            _lastKnownPosition = source.position;
            _lastKnownRotation = source.rotation;
            _lastKnownScale = source.lossyScale;
            _hasMemory = true;
        }

        private void ApplyLastKnownTransform()
        {
            transform.SetPositionAndRotation(_lastKnownPosition, _lastKnownRotation);
            transform.localScale = _lastKnownScale;
        }

        private void SetGhostVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private void DestroyGhost()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

    }
}
