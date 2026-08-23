using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeStatBlock), typeof(FactionMember))]
    public sealed class IsolationModifierController : MonoBehaviour
    {
        private const string ModifierSource = "passive.hero-y.isolation";

        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private HeroFormStateMachine _forms;
        [Min(0f), SerializeField] private float _radius = 6f;
        [Min(0.05f), SerializeField] private float _updateInterval = 0.25f;
        [Range(0f, 1f), SerializeField] private float _fallbackDamageBonus = 0.3f;
        [Range(0f, 1f), SerializeField] private float _fallbackDamageReduction = 0.3f;

        private float _remaining;
        private bool _isolated;

        public bool IsIsolated => _isolated;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
            _radius = Mathf.Max(0f, _radius);
            _updateInterval = Mathf.Max(0.05f, _updateInterval);
            _fallbackDamageBonus = Mathf.Clamp01(_fallbackDamageBonus);
            _fallbackDamageReduction = Mathf.Clamp01(_fallbackDamageReduction);
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            _remaining = _updateInterval;
            Evaluate();
        }

        public void Evaluate()
        {
            CacheComponents();
            bool isolated = !HasNearbyAlly();
            float bonus = _forms != null && _forms.CurrentForm != null
                ? _forms.CurrentForm.IsolationDamageBonus
                : _fallbackDamageBonus;
            float reduction = _forms != null && _forms.CurrentForm != null
                ? _forms.CurrentForm.IsolationDamageReduction
                : _fallbackDamageReduction;

            _stats.RemoveModifiersBySource(ModifierSource);
            if (isolated)
            {
                _stats.AddModifier(new StatModifierDefinition(
                    ModifierSource,
                    RuntimeStatId.DamageDealt,
                    StatModifierOperation.Multiply,
                    1f + bonus,
                    100,
                    0f,
                    StatStackingRule.ReplaceExisting));
                _stats.AddModifier(new StatModifierDefinition(
                    ModifierSource,
                    RuntimeStatId.DamageTaken,
                    StatModifierOperation.Multiply,
                    1f - reduction,
                    100,
                    0f,
                    StatStackingRule.ReplaceExisting));
            }

            _isolated = isolated;
        }

        private bool HasNearbyAlly()
        {
            if (_faction == null || !AppRoot.TryGetInstance(out AppRoot root) ||
                root.Services == null || root.Services.EntityRegistry == null)
            {
                return false;
            }

            var entities = root.Services.EntityRegistry.Entities;
            float radiusSquared = _radius * _radius;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity entity = entities[i];
                if (entity == null || entity.gameObject == gameObject)
                {
                    continue;
                }

                FactionMember member = entity.GetComponent<FactionMember>();
                if (member == null || member.Faction != _faction.Faction)
                {
                    continue;
                }

                Vector3 delta = entity.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheComponents()
        {
            if (_stats == null) _stats = GetComponent<RuntimeStatBlock>();
            if (_faction == null) _faction = GetComponent<FactionMember>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
        }
    }
}
