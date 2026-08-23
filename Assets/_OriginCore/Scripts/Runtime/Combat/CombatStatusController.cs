using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    public readonly struct CombatDisplacementRequest
    {
        public CombatDisplacementRequest(
            WeaponStatusType type,
            EntityIdentity source,
            Vector3 direction,
            float magnitude,
            float duration)
        {
            Type = type;
            Source = source;
            Direction = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector3.zero;
            Magnitude = Mathf.Max(0f, magnitude);
            Duration = Mathf.Max(0f, duration);
        }

        public WeaponStatusType Type { get; }
        public EntityIdentity Source { get; }
        public Vector3 Direction { get; }
        public float Magnitude { get; }
        public float Duration { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CombatStatusController : MonoBehaviour
    {
        private sealed class TimedStatus
        {
            public int Handle;
            public WeaponStatusType Type;
            public EntityIdentity Source;
            public float Magnitude;
            public float Remaining;
            public WeaponStatusStackPolicy StackPolicy;
        }

        private readonly List<TimedStatus> _statuses = new List<TimedStatus>(8);
        private int _nextHandle = 1;

        public event Action<CombatStatusController, WeaponStatusType, int> StatusStarted;
        public event Action<CombatStatusController, WeaponStatusType, int> StatusEnded;
        public event Action<CombatStatusController, int> InterruptRequested;
        public event Action<CombatStatusController, CombatDisplacementRequest>
            DisplacementRequested;

        public bool IsTemporallyLocked => HasStatus(WeaponStatusType.TemporalLock);
        public bool IsDisplacementLocked => HasStatus(WeaponStatusType.DisplacementLock) ||
                                            IsTemporallyLocked;
        public bool BlocksMovement => IsTemporallyLocked;
        public bool BlocksActions => IsTemporallyLocked;

        private void OnDisable()
        {
            ClearAll();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                TimedStatus status = _statuses[i];
                status.Remaining = Mathf.Max(0f, status.Remaining - deltaTime);
                if (status.Remaining <= 0f)
                {
                    RemoveAt(i);
                }
            }
        }

        public int Apply(
            WeaponStatusDefinition definition,
            EntityIdentity source,
            Vector3 direction,
            int interruptPriority = 0)
        {
            if (definition == null || definition.Type == WeaponStatusType.None)
            {
                return 0;
            }

            switch (definition.Type)
            {
                case WeaponStatusType.Interrupt:
                    InterruptRequested?.Invoke(this, interruptPriority);
                    return 0;
                case WeaponStatusType.Push:
                case WeaponStatusType.Pull:
                case WeaponStatusType.Launch:
                case WeaponStatusType.LaunchPair:
                case WeaponStatusType.GroundSlam:
                    if (IsDisplacementLocked)
                    {
                        return 0;
                    }
                    DisplacementRequested?.Invoke(
                        this,
                        new CombatDisplacementRequest(
                            definition.Type,
                            source,
                            direction,
                            definition.Magnitude,
                            definition.Duration));
                    return 0;
                default:
                    return ApplyTimed(definition, source);
            }
        }

        public bool Remove(int handle)
        {
            if (handle <= 0)
            {
                return false;
            }

            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].Handle == handle)
                {
                    RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool HasStatus(WeaponStatusType type)
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public float GetMaximumMagnitude(WeaponStatusType type)
        {
            float maximum = 0f;
            for (int i = 0; i < _statuses.Count; i++)
            {
                TimedStatus status = _statuses[i];
                if (status.Type == type)
                {
                    maximum = Mathf.Max(maximum, status.Magnitude);
                }
            }

            return maximum;
        }

        public void ClearAll()
        {
            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                RemoveAt(i);
            }
        }

        private int ApplyTimed(
            WeaponStatusDefinition definition,
            EntityIdentity source)
        {
            if (definition.Duration <= 0f)
            {
                return 0;
            }

            if (definition.StackPolicy != WeaponStatusStackPolicy.Stack)
            {
                int existingIndex = FindStatus(definition.Type);
                if (existingIndex >= 0)
                {
                    TimedStatus existing = _statuses[existingIndex];
                    if (definition.StackPolicy == WeaponStatusStackPolicy.IgnoreIfPresent)
                    {
                        return existing.Handle;
                    }

                    if (definition.StackPolicy == WeaponStatusStackPolicy.Replace)
                    {
                        RemoveAt(existingIndex);
                    }
                    else
                    {
                        existing.Remaining = Mathf.Max(
                            existing.Remaining,
                            definition.Duration);
                        existing.Magnitude = Mathf.Max(
                            existing.Magnitude,
                            definition.Magnitude);
                        existing.Source = source;
                        return existing.Handle;
                    }
                }
            }

            int handle = _nextHandle++;
            if (_nextHandle <= 0)
            {
                _nextHandle = 1;
            }

            _statuses.Add(new TimedStatus
            {
                Handle = handle,
                Type = definition.Type,
                Source = source,
                Magnitude = definition.Magnitude,
                Remaining = definition.Duration,
                StackPolicy = definition.StackPolicy
            });
            StatusStarted?.Invoke(this, definition.Type, handle);
            return handle;
        }

        private int FindStatus(WeaponStatusType type)
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveAt(int index)
        {
            TimedStatus status = _statuses[index];
            _statuses.RemoveAt(index);
            StatusEnded?.Invoke(this, status.Type, status.Handle);
        }
    }
}
