using OriginCore.Buildings;
using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriginCore.Matches
{
    [DisallowMultipleComponent]
    public sealed class CommanderBaseSite : MonoBehaviour
    {
        [Min(0.1f), SerializeField] private Vector2 _footprint =
            new Vector2(3f, 3f);

        public Vector2 Footprint => new Vector2(
            Mathf.Max(0.1f, _footprint.x),
            Mathf.Max(0.1f, _footprint.y));

        public void Configure(Vector2 footprint)
        {
            _footprint = new Vector2(
                Mathf.Max(0.1f, footprint.x),
                Mathf.Max(0.1f, footprint.y));
        }

        public bool TryReplace(
            CommanderDefinition commander,
            out BuildingRuntime resolvedBase,
            out string error)
        {
            resolvedBase = null;
            BuildingDefinition definition = commander != null
                ? commander.BaseBuilding
                : null;
            if (definition == null || definition.Prefab == null)
            {
                error = "Commander base site has no valid base building for commander '" +
                        (commander != null ? commander.ContentId : "<null>") + "'.";
                return false;
            }

            if (Vector2.Distance(Footprint, definition.Footprint) > 0.01f)
            {
                error = "Commander base site footprint does not match base building '" +
                        definition.ContentId + "'.";
                return false;
            }

            resolvedBase = FindExistingBase(definition);
            if (resolvedBase != null)
            {
                RemoveSite();
                error = string.Empty;
                return true;
            }

            GameObject instance = Instantiate(
                definition.Prefab,
                transform.position,
                transform.rotation);
            Scene targetScene = gameObject.scene;
            if (targetScene.IsValid() && instance.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(instance, targetScene);
            }

            instance.name = definition.Prefab.name + "_CommanderBase";
            resolvedBase = instance.GetComponent<BuildingRuntime>();
            EntityIdentity identity = instance.GetComponent<EntityIdentity>();
            if (resolvedBase == null || identity == null)
            {
                Destroy(instance);
                resolvedBase = null;
                error = "Commander base prefab is missing BuildingRuntime or EntityIdentity.";
                return false;
            }

            identity.MarkRuntimeSpawned();
            FactionMember faction = instance.GetComponent<FactionMember>();
            faction?.SetFaction(FactionId.Friendly);

            // Initial bases are complete at match start. Toggling through the normal
            // operational transition also preserves commander-specific automatic spawns.
            resolvedBase.Configure(definition, false);
            resolvedBase.SetOperational(true);
            RemoveSite();
            error = string.Empty;
            return true;
        }

        private BuildingRuntime FindExistingBase(BuildingDefinition definition)
        {
            BuildingRuntime[] buildings =
                Resources.FindObjectsOfTypeAll<BuildingRuntime>();
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingRuntime candidate = buildings[i];
                FactionMember faction = candidate != null
                    ? candidate.GetComponent<FactionMember>()
                    : null;
                if (candidate != null && candidate.gameObject.scene == gameObject.scene &&
                    candidate.Definition == definition && faction != null &&
                    faction.Faction == FactionId.Friendly)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RemoveSite()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnValidate()
        {
            Configure(_footprint);
        }
    }
}
