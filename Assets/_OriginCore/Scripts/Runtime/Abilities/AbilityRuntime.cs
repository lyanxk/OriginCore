using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Abilities
{
    public enum AbilityCastFailure
    {
        None = 0,
        MissingDefinition = 1,
        WrongMode = 2,
        InvalidTarget = 3,
        OutOfRange = 4,
        OnCooldown = 5,
        NoCharges = 6,
        InsufficientEnergy = 7,
        InsufficientResources = 8,
        Locked = 9,
        EffectUnavailable = 10,
        CasterDead = 11
    }

    public readonly struct AbilityTarget
    {
        private AbilityTarget(
            EntityIdentity entity,
            Vector3 point,
            Vector3 direction,
            bool hasPoint,
            bool hasDirection)
        {
            Entity = entity;
            Point = point;
            Direction = direction;
            HasPoint = hasPoint;
            HasDirection = hasDirection;
        }

        public EntityIdentity Entity { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public bool HasPoint { get; }
        public bool HasDirection { get; }

        public static AbilityTarget None => default(AbilityTarget);

        public static AbilityTarget ForEntity(EntityIdentity entity)
        {
            return new AbilityTarget(
                entity,
                entity != null ? entity.transform.position : Vector3.zero,
                Vector3.zero,
                entity != null,
                false);
        }

        public static AbilityTarget ForPoint(Vector3 point)
        {
            return new AbilityTarget(null, point, Vector3.zero, true, false);
        }

        public static AbilityTarget ForDirection(Vector3 direction)
        {
            return new AbilityTarget(
                null,
                Vector3.zero,
                direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
                false,
                true);
        }
    }

    public readonly struct AbilityCastContext
    {
        public AbilityCastContext(
            AbilityLoadout loadout,
            AbilityDefinition definition,
            EntityIdentity caster,
            FactionId casterFaction,
            GameMode mode,
            AbilityTarget target,
            Vector3 origin,
            int frame)
        {
            Loadout = loadout;
            Definition = definition;
            Caster = caster;
            CasterFaction = casterFaction;
            Mode = mode;
            Target = target;
            Origin = origin;
            Frame = frame;
        }

        public AbilityLoadout Loadout { get; }
        public AbilityDefinition Definition { get; }
        public EntityIdentity Caster { get; }
        public FactionId CasterFaction { get; }
        public GameMode Mode { get; }
        public AbilityTarget Target { get; }
        public Vector3 Origin { get; }
        public int Frame { get; }
    }

    public readonly struct AbilityCastResult
    {
        public AbilityCastResult(
            bool succeeded,
            AbilityCastFailure failure,
            string message,
            string abilityId)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message ?? string.Empty;
            AbilityId = abilityId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public AbilityCastFailure Failure { get; }
        public string Message { get; }
        public string AbilityId { get; }

        public static AbilityCastResult Success(string abilityId)
        {
            return new AbilityCastResult(true, AbilityCastFailure.None, string.Empty, abilityId);
        }

        public static AbilityCastResult Fail(
            string abilityId,
            AbilityCastFailure failure,
            string message)
        {
            return new AbilityCastResult(false, failure, message, abilityId);
        }
    }

    internal sealed class AbilityChargeState
    {
        public int MaximumCharges;
        public int Charges;
        public float RechargeRemaining;

        public AbilityChargeState(int maximumCharges)
        {
            MaximumCharges = Mathf.Max(1, maximumCharges);
            Charges = MaximumCharges;
        }
    }

    public sealed class SharedAbilityChargePool
    {
        private readonly Dictionary<string, AbilityChargeState> _states =
            new Dictionary<string, AbilityChargeState>(StringComparer.Ordinal);

        internal AbilityChargeState GetOrCreate(string groupId, int maximumCharges)
        {
            string normalized = ContentIdUtility.Normalize(groupId);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            if (!_states.TryGetValue(normalized, out AbilityChargeState state))
            {
                state = new AbilityChargeState(maximumCharges);
                _states.Add(normalized, state);
            }
            else if (maximumCharges > state.MaximumCharges)
            {
                int delta = maximumCharges - state.MaximumCharges;
                state.MaximumCharges = maximumCharges;
                state.Charges = Mathf.Min(state.MaximumCharges, state.Charges + delta);
            }

            return state;
        }

        public void Tick(float deltaTime, IReadOnlyList<AbilityRuntime> runtimes)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<string, AbilityChargeState> pair in _states)
            {
                AbilityChargeState state = pair.Value;
                if (state.Charges >= state.MaximumCharges || state.RechargeRemaining <= 0f)
                {
                    continue;
                }

                state.RechargeRemaining -= deltaTime;
                while (state.RechargeRemaining <= 0f && state.Charges < state.MaximumCharges)
                {
                    state.Charges++;
                    if (state.Charges < state.MaximumCharges)
                    {
                        state.RechargeRemaining += ResolveCooldown(pair.Key, runtimes);
                    }
                    else
                    {
                        state.RechargeRemaining = 0f;
                    }
                }
            }
        }

        public void Reset()
        {
            foreach (AbilityChargeState state in _states.Values)
            {
                state.Charges = state.MaximumCharges;
                state.RechargeRemaining = 0f;
            }
        }

        private static float ResolveCooldown(string groupId, IReadOnlyList<AbilityRuntime> runtimes)
        {
            float cooldown = 0.01f;
            for (int i = 0; i < runtimes.Count; i++)
            {
                AbilityRuntime runtime = runtimes[i];
                if (runtime != null && string.Equals(
                        ContentIdUtility.Normalize(runtime.Definition.SharedCooldownGroup),
                        groupId,
                        StringComparison.Ordinal))
                {
                    cooldown = Mathf.Max(cooldown, runtime.Definition.Cooldown);
                }
            }

            return cooldown;
        }
    }

    public sealed class AbilityRuntime
    {
        private readonly SharedAbilityChargePool _sharedPool;
        private readonly AbilityChargeState _localState;
        private readonly AbilityChargeState _sharedState;

        public AbilityRuntime(AbilityDefinition definition, SharedAbilityChargePool sharedPool)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _sharedPool = sharedPool;
            if (!string.IsNullOrEmpty(ContentIdUtility.Normalize(definition.SharedCooldownGroup)) &&
                sharedPool != null)
            {
                _sharedState = sharedPool.GetOrCreate(
                    definition.SharedCooldownGroup,
                    definition.MaximumCharges);
            }
            else
            {
                _localState = new AbilityChargeState(definition.MaximumCharges);
            }
        }

        public AbilityDefinition Definition { get; }
        public int Charges => State.Charges;
        public int MaximumCharges => State.MaximumCharges;
        public float CooldownRemaining => Mathf.Max(0f, State.RechargeRemaining);
        public bool IsReady => Charges > 0;
        public bool UsesSharedPool => _sharedState != null;

        private AbilityChargeState State => _sharedState ?? _localState;

        public void CommitCast()
        {
            AbilityChargeState state = State;
            if (state.Charges <= 0)
            {
                return;
            }

            state.Charges--;
            if (state.RechargeRemaining <= 0f)
            {
                state.RechargeRemaining = Definition.Cooldown;
            }
        }

        public void TickLocal(float deltaTime)
        {
            if (UsesSharedPool || deltaTime <= 0f)
            {
                return;
            }

            AbilityChargeState state = State;
            if (state.Charges >= state.MaximumCharges || state.RechargeRemaining <= 0f)
            {
                return;
            }

            state.RechargeRemaining -= deltaTime;
            while (state.RechargeRemaining <= 0f && state.Charges < state.MaximumCharges)
            {
                state.Charges++;
                if (state.Charges < state.MaximumCharges)
                {
                    state.RechargeRemaining += Mathf.Max(0.01f, Definition.Cooldown);
                }
                else
                {
                    state.RechargeRemaining = 0f;
                }
            }
        }

        public void Refresh()
        {
            State.Charges = State.MaximumCharges;
            State.RechargeRemaining = 0f;
        }

        public void Restore(int charges, float cooldownRemaining)
        {
            State.Charges = Mathf.Clamp(charges, 0, State.MaximumCharges);
            State.RechargeRemaining = State.Charges < State.MaximumCharges
                ? Mathf.Max(0f, cooldownRemaining)
                : 0f;
        }
    }
}
