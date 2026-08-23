using System;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    public interface IIncomingDamageModifier
    {
        float ModifyIncomingDamage(DamageInfo damageInfo, float currentAmount);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(VitalsComponent))]
    public sealed class DamageReceiver : MonoBehaviour
    {
        public static event Action<DamageReceiver, DamageInfo, DamageResult> AnyDamageApplied;

        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private RuntimeStatBlock _statBlock;
        [SerializeField] private EntityIdentity _identity;

        private IIncomingDamageModifier[] _incomingModifiers =
            Array.Empty<IIncomingDamageModifier>();

        public event Action<DamageReceiver, DamageInfo, DamageResult> DamageApplied;

        public VitalsComponent Vitals => _vitals;
        public RuntimeStatBlock StatBlock => _statBlock;

        private void Awake()
        {
            EnsureCombatControlPipeline();
            CacheVitals();
            CacheIncomingModifiers();
        }

        private void OnValidate()
        {
            CacheVitals();
        }

        public void Configure(VitalsComponent vitals)
        {
            _vitals = vitals;
        }

        public bool TryReceiveDamage(DamageInfo damageInfo, out DamageResult result)
        {
            CacheVitals();
            if (_vitals == null)
            {
                result = DamageResult.None;
                return false;
            }

            float requestedAmount = damageInfo.Amount;
            if (requestedAmount <= 0f || float.IsNaN(requestedAmount) ||
                float.IsInfinity(requestedAmount) || !_vitals.IsAlive)
            {
                result = DamageResult.None;
                return false;
            }

            float modifiedAmount = requestedAmount;
            for (int i = 0; i < _incomingModifiers.Length; i++)
            {
                IIncomingDamageModifier modifier = _incomingModifiers[i];
                if (modifier != null)
                {
                    modifiedAmount = modifier.ModifyIncomingDamage(
                        damageInfo,
                        modifiedAmount);
                }
            }
            if (damageInfo.Source != null)
            {
                RuntimeStatBlock sourceStats = damageInfo.Source.GetComponent<RuntimeStatBlock>();
                if (sourceStats != null)
                {
                    modifiedAmount *= sourceStats.GetStat(RuntimeStatId.DamageDealt);
                }
            }

            if (_statBlock != null)
            {
                modifiedAmount *= _statBlock.GetStat(RuntimeStatId.DamageTaken);
            }
            else if (_identity != null && _identity.Definition != null)
            {
                modifiedAmount *= _identity.Definition.DamageTakenMultiplier;
            }

            if (modifiedAmount <= 0f || float.IsNaN(modifiedAmount) ||
                float.IsInfinity(modifiedAmount))
            {
                result = DamageResult.None;
                return false;
            }

            float armor = damageInfo.HasFlag(DamageFlags.IgnoreArmor)
                ? 0f
                : ResolveArmor();
            float armorMitigation = Mathf.Min(Mathf.Max(0f, modifiedAmount), armor);
            result = _vitals.ApplyResolvedDamage(
                requestedAmount,
                modifiedAmount,
                armorMitigation,
                damageInfo.HasFlag(DamageFlags.IgnoreShield));
            if (!result.Changed)
            {
                return false;
            }

            DamageApplied?.Invoke(this, damageInfo, result);
            AnyDamageApplied?.Invoke(this, damageInfo, result);
            return true;
        }

        [ContextMenu("Debug/Apply 25 Damage")]
        private void DebugApplyDamage()
        {
            TryReceiveDamage(new DamageInfo(25f), out DamageResult _);
        }

        [ContextMenu("Debug/Reset Vitals")]
        private void DebugResetVitals()
        {
            CacheVitals();
            _vitals?.ResetToMaximum();
        }

        private void CacheVitals()
        {
            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }

            if (_statBlock == null)
            {
                _statBlock = GetComponent<RuntimeStatBlock>();
            }

            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }
        }

        private float ResolveArmor()
        {
            if (_statBlock != null)
            {
                return _statBlock.GetStat(RuntimeStatId.Armor);
            }

            return _identity != null && _identity.Definition != null
                ? _identity.Definition.Armor
                : 0f;
        }

        private void CacheIncomingModifiers()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            var modifiers = new System.Collections.Generic.List<IIncomingDamageModifier>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IIncomingDamageModifier modifier)
                {
                    modifiers.Add(modifier);
                }
            }
            _incomingModifiers = modifiers.ToArray();
        }

        private void EnsureCombatControlPipeline()
        {
            if (GetComponent<CombatStatusController>() == null)
            {
                gameObject.AddComponent<CombatStatusController>();
            }
            if (GetComponent<CombatMotionController>() == null)
            {
                gameObject.AddComponent<CombatMotionController>();
            }
        }
    }
}
