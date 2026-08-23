using System;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilityLoadout), typeof(RuntimeStatBlock), typeof(VitalsComponent))]
    public sealed class HeroFormStateMachine : MonoBehaviour
    {
        [SerializeField] private HeroDefinition _heroDefinition;
        [SerializeField] private string _initialFormId;
        [SerializeField] private AbilityLoadout _loadout;
        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private FlightController _flight;
        [SerializeField] private AttackCapability _attack;

        private HeroFormDefinition _currentForm;

        public event Action<HeroFormStateMachine, HeroFormDefinition, HeroFormDefinition> FormChanged;

        public HeroDefinition HeroDefinition => _heroDefinition;
        public HeroFormDefinition CurrentForm => _currentForm;
        public string CurrentFormId => _currentForm != null ? _currentForm.ContentId : string.Empty;
        public float RespawnTimeMultiplier => _currentForm != null
            ? _currentForm.RespawnTimeMultiplier
            : 1f;

        private void Awake()
        {
            CacheComponents();
            if (_heroDefinition != null && _heroDefinition.Forms.Length > 0)
            {
                HeroFormDefinition initial = ResolveForm(_initialFormId) ?? _heroDefinition.Forms[0];
                if (initial != null)
                {
                    ApplyForm(initial, false, true, out _);
                }
            }
        }

        private void Update()
        {
            if (_currentForm != null && _currentForm.LockEnergyToMaximum && _vitals != null &&
                _vitals.Energy < _vitals.MaxEnergy)
            {
                _vitals.SetEnergy(_vitals.MaxEnergy);
            }
        }

        private void OnValidate()
        {
            CacheComponents();
            _initialFormId = ContentIdUtility.Normalize(_initialFormId);
        }

        public void Configure(HeroDefinition heroDefinition, string initialFormId = "")
        {
            _heroDefinition = heroDefinition;
            _initialFormId = ContentIdUtility.Normalize(initialFormId);
            _currentForm = null;
            CacheComponents();
        }

        public bool TryEnterForm(string formId, out string error)
        {
            HeroFormDefinition form = ResolveForm(formId);
            if (form == null)
            {
                error = "Unknown hero form '" + formId + "'.";
                return false;
            }

            return ApplyForm(form, true, false, out error);
        }

        public bool RestoreForm(string formId, out string error)
        {
            HeroFormDefinition form = ResolveForm(formId);
            if (form == null)
            {
                error = "Unknown saved hero form '" + formId + "'.";
                return false;
            }

            CacheComponents();
            if (_currentForm != null)
            {
                _stats?.RemoveModifiersBySource(_currentForm.ContentId);
                _currentForm = null;
            }

            return ApplyForm(form, false, true, out error);
        }

        public bool TryEnterNextForm(out string error)
        {
            if (!TryResolveNextForm(out HeroFormDefinition next, out error))
            {
                return false;
            }

            return ApplyForm(next, true, false, out error);
        }

        public bool CanEnterNextForm(out string error)
        {
            if (!TryResolveNextForm(out HeroFormDefinition next, out error))
            {
                return false;
            }

            CacheComponents();
            if (_loadout == null || _stats == null || _vitals == null)
            {
                error = "Hero form dependencies are incomplete.";
                return false;
            }

            if (_vitals.Energy + 0.0001f < next.EnergyCost)
            {
                error = "Not enough Energy to enter hero form.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryResolveNextForm(out HeroFormDefinition next, out string error)
        {
            next = null;
            if (_heroDefinition == null)
            {
                error = "Hero definition is unavailable.";
                return false;
            }

            HeroFormDefinition[] forms = _heroDefinition.Forms;
            for (int i = 0; i < forms.Length; i++)
            {
                HeroFormDefinition form = forms[i];
                if (form == null || form == _currentForm)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(form.RequiredPreviousFormId) && _currentForm == null ||
                    string.Equals(form.RequiredPreviousFormId, CurrentFormId, StringComparison.Ordinal))
                {
                    next = form;
                    error = string.Empty;
                    return true;
                }
            }

            error = "No next hero form is currently available.";
            return false;
        }

        private bool ApplyForm(
            HeroFormDefinition next,
            bool chargeEnergy,
            bool bypassPredecessor,
            out string error)
        {
            CacheComponents();
            if (next == null || _loadout == null || _stats == null || _vitals == null)
            {
                error = "Hero form dependencies are incomplete.";
                return false;
            }

            if (_currentForm == next)
            {
                error = string.Empty;
                return true;
            }

            if (!bypassPredecessor && !string.IsNullOrEmpty(next.RequiredPreviousFormId) &&
                !string.Equals(CurrentFormId, next.RequiredPreviousFormId, StringComparison.Ordinal))
            {
                error = "Hero form requires predecessor '" + next.RequiredPreviousFormId + "'.";
                return false;
            }

            if (chargeEnergy && _vitals.Energy + 0.0001f < next.EnergyCost)
            {
                error = "Not enough Energy to enter hero form.";
                return false;
            }

            HeroFormDefinition previous = _currentForm;
            float previousEnergy = _vitals.Energy;
            if (previous != null)
            {
                _stats.RemoveModifiersBySource(previous.ContentId);
            }

            StatModifierDefinition[] modifiers = next.StatModifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                StatModifierDefinition modifier = modifiers[i];
                StatModifierDefinition scoped = new StatModifierDefinition(
                    next.ContentId,
                    modifier.StatId,
                    modifier.Operation,
                    modifier.Value,
                    modifier.Priority,
                    modifier.Duration,
                    StatStackingRule.Stack);
                if (!_stats.AddModifier(scoped))
                {
                    _stats.RemoveModifiersBySource(next.ContentId);
                    RestorePreviousModifiers(previous);
                    _vitals.SetEnergy(previousEnergy);
                    error = "Hero form stat modifiers could not be applied.";
                    return false;
                }
            }

            if (chargeEnergy && next.EnergyCost > 0f)
            {
                _vitals.SetEnergy(previousEnergy - next.EnergyCost);
            }

            _currentForm = next;
            _loadout.Configure(
                next.RtsAbilities,
                next.ActAbilities,
                next.FpsAbilities,
                true);
            _vitals.ApplyMaximums(
                _stats.GetStat(RuntimeStatId.MaxHealth),
                _stats.GetStat(RuntimeStatId.MaxShield),
                _stats.GetStat(RuntimeStatId.MaxEnergy),
                true);
            if (next.LockEnergyToMaximum)
            {
                _vitals.SetEnergy(_vitals.MaxEnergy);
            }

            _flight?.SetFlightEnabled(next.FlightEnabled);
            _attack?.ConfigurePattern(
                next.BasicAttackHitCount,
                next.BasicAttackHitInterval,
                next.BasicAttackDamageType,
                next.BasicAttackDamageFlags);
            FormChanged?.Invoke(this, previous, next);
            error = string.Empty;
            return true;
        }

        private void RestorePreviousModifiers(HeroFormDefinition previous)
        {
            if (previous == null)
            {
                return;
            }

            StatModifierDefinition[] modifiers = previous.StatModifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                StatModifierDefinition modifier = modifiers[i];
                _stats.AddModifier(new StatModifierDefinition(
                    previous.ContentId,
                    modifier.StatId,
                    modifier.Operation,
                    modifier.Value,
                    modifier.Priority,
                    modifier.Duration,
                    StatStackingRule.Stack));
            }
        }

        private HeroFormDefinition ResolveForm(string formId)
        {
            string normalized = ContentIdUtility.Normalize(formId);
            if (_heroDefinition != null)
            {
                HeroFormDefinition[] forms = _heroDefinition.Forms;
                for (int i = 0; i < forms.Length; i++)
                {
                    if (forms[i] != null && string.Equals(
                            forms[i].ContentId,
                            normalized,
                            StringComparison.Ordinal))
                    {
                        return forms[i];
                    }
                }
            }

            if (AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                root.Services.ContentCatalog != null &&
                root.Services.ContentCatalog.Catalog.TryGetHeroForm(normalized, out HeroFormDefinition form))
            {
                return form;
            }

            return null;
        }

        private void CacheComponents()
        {
            if (_loadout == null) _loadout = GetComponent<AbilityLoadout>();
            if (_stats == null) _stats = GetComponent<RuntimeStatBlock>();
            if (_vitals == null) _vitals = GetComponent<VitalsComponent>();
            if (_flight == null) _flight = GetComponent<FlightController>();
            if (_attack == null) _attack = GetComponent<AttackCapability>();
        }
    }
}
