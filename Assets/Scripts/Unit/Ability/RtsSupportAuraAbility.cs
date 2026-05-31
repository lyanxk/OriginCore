using Gameplay;
using System;
using System.Collections.Generic;
using Content;
using Modes;
using Unit.Movement;
using Unit.Selection;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class RtsSupportAuraAbility : RtsUnitAbility
    {
        const string AbilityIdValue = "ability.rts.supportAura";
        const string IconResourcePath = "AbilityIcons/Rts/rts_support_aura";

        [Header("Aura")]
        [SerializeField] LayerMask unitMask = ~0;
        [SerializeField] float radius = 5f;
        [SerializeField] float moveSpeedMultiplier = 1.2f;
        [SerializeField] float damageMultiplier = 1.2f;
        [SerializeField] float updateInterval = 0.25f;
        [SerializeField] bool affectSelf;

        readonly Collider[] _overlapBuffer = new Collider[48];
        readonly List<UnitBase> _bufferedUnits = new List<UnitBase>(16);
        readonly List<UnitBase> _buffedUnits = new List<UnitBase>(16);

        [NonSerialized] Sprite _icon;
        float _nextScanTime;

        public override string AbilityId => AbilityIdValue;
        public override string DisplayName => GameText.GetName(AbilityId, AbilityId);
        public override Sprite Icon => LoadIcon(ref _icon, IconResourcePath);
        public override string Tooltip => GameText.GetTooltip(AbilityId);
        public override RtsAbilityActivationType ActivationType => RtsAbilityActivationType.Passive;
        public override bool IsEnabled => true;

        public override void Tick(float deltaTime)
        {
            if (CachedTransform == null || !IsAvailableInCurrentMode)
            {
                ClearBuffs();
                return;
            }

            if (Time.time < _nextScanTime)
                return;

            _nextScanTime = Time.time + Mathf.Max(0.05f, updateInterval);
            RefreshBuffTargets();
        }

        protected override void OnUnbound()
        {
            ClearBuffs();
            _nextScanTime = 0f;
        }

        void RefreshBuffTargets()
        {
            _bufferedUnits.Clear();

            // The aura scans on a low fixed cadence and reuses buffers to keep RTS crowd costs predictable.
            int hitCount = Physics.OverlapSphereNonAlloc(
                CachedTransform.position,
                Mathf.Max(0.1f, radius),
                _overlapBuffer,
                unitMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit == null)
                    continue;

                UnitBase unit = hit.GetComponentInParent<UnitBase>();
                if (!IsValidAuraTarget(unit) || _bufferedUnits.Contains(unit))
                    continue;

                _bufferedUnits.Add(unit);
            }

            for (int i = _buffedUnits.Count - 1; i >= 0; i--)
            {
                UnitBase unit = _buffedUnits[i];
                if (unit != null && _bufferedUnits.Contains(unit))
                    continue;

                RemoveBuff(unit);
                _buffedUnits.RemoveAt(i);
            }

            for (int i = 0; i < _bufferedUnits.Count; i++)
            {
                UnitBase unit = _bufferedUnits[i];
                if (_buffedUnits.Contains(unit))
                    continue;

                ApplyBuff(unit);
                _buffedUnits.Add(unit);
            }
        }

        bool IsValidAuraTarget(UnitBase unit)
        {
            if (unit == null)
                return false;

            if (!affectSelf && unit == Motor)
                return false;

            TeamAffiliation ownerTeam = TeamAffiliation;
            TeamAffiliation targetTeam = ResolveTeamAffiliation(unit);
            if (ownerTeam != null && targetTeam != null)
                return ownerTeam.Team == targetTeam.Team;

            // Existing test units do not all carry explicit team components yet, so the aura falls back to
            // player-controllable ownership when team data is missing.
            Selectable ownerSelectable = Selectable;
            Selectable targetSelectable = unit.GetComponent<Selectable>();
            if (ownerSelectable != null && targetSelectable != null)
                return RtsOrderDispatcher.CanControlSelectable(ownerSelectable)
                    == RtsOrderDispatcher.CanControlSelectable(targetSelectable);

            if (ownerTeam == null && targetTeam == null)
                return true;

            if (ownerTeam != null)
                return ownerTeam.IsPlayerControllable;

            return targetTeam != null && targetTeam.IsPlayerControllable;
        }

        void ApplyBuff(UnitBase unit)
        {
            if (unit == null)
                return;

            unit.SetMoveSpeedMultiplier(this, moveSpeedMultiplier);
            unit.Combat?.SetDamageMultiplier(this, damageMultiplier);
        }

        void RemoveBuff(UnitBase unit)
        {
            if (unit == null)
                return;

            unit.ClearMoveSpeedMultiplier(this);
            unit.Combat?.ClearDamageMultiplier(this);
        }

        void ClearBuffs()
        {
            for (int i = 0; i < _buffedUnits.Count; i++)
                RemoveBuff(_buffedUnits[i]);

            _buffedUnits.Clear();
            _bufferedUnits.Clear();
        }

        static TeamAffiliation ResolveTeamAffiliation(UnitBase unit)
        {
            if (unit == null)
                return null;

            TeamAffiliation teamAffiliation = unit.GetComponent<TeamAffiliation>();
            if (teamAffiliation != null)
                return teamAffiliation;

            return unit.GetComponentInParent<TeamAffiliation>();
        }
    }
}
