using OriginCore.Core;
using UnityEngine;

namespace OriginCore.Economy
{
    [DisallowMultipleComponent]
    public sealed class AutomaticResourceExtractor : MonoBehaviour
    {
        [Min(0.1f), SerializeField] private float _searchRadius = 2f;
        [Min(0.05f), SerializeField] private float _unitsPerSecond = 1f;

        private ResourceNode _node;
        private float _accumulator;
        private float _searchCooldown;

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f ||
                !AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                !TryResolveNode(root.Services, deltaTime))
            {
                return;
            }

            _accumulator += _unitsPerSecond * deltaTime;
            int requested = Mathf.FloorToInt(_accumulator);
            if (requested <= 0)
            {
                return;
            }

            int extracted = _node.Extract(requested);
            _accumulator -= requested;
            if (extracted > 0)
            {
                root.Services.ResourceService.Add(_node.ResourceType, extracted);
            }
        }

        public void Configure(float searchRadius, float unitsPerSecond)
        {
            _searchRadius = Mathf.Max(0.1f, searchRadius);
            _unitsPerSecond = Mathf.Max(0.05f, unitsPerSecond);
            _node = null;
            _searchCooldown = 0f;
        }

        private bool TryResolveNode(GameServices services, float deltaTime)
        {
            if (_node != null && !_node.IsDepleted)
            {
                return true;
            }

            _searchCooldown = Mathf.Max(0f, _searchCooldown - deltaTime);
            if (_searchCooldown > 0f)
            {
                return false;
            }

            _searchCooldown = 0.5f;

            float bestDistance = _searchRadius * _searchRadius;
            _node = null;
            var entities = services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                ResourceNode candidate = entities[i] != null
                    ? entities[i].GetComponent<ResourceNode>()
                    : null;
                if (candidate == null || candidate.IsDepleted)
                {
                    continue;
                }

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    _node = candidate;
                }
            }

            return _node != null;
        }
    }
}
