using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [DefaultExecutionOrder(-11820)]
    [DisallowMultipleComponent]
    public sealed class EntityRegistry : MonoBehaviour
    {
        [SerializeField] private FactionRelationService _factionRelations;

        private readonly List<EntityIdentity> _entities = new List<EntityIdentity>(64);
        private readonly HashSet<EntityIdentity> _membership = new HashSet<EntityIdentity>();

        public event Action<EntityIdentity> EntityRegistered;
        public event Action<EntityIdentity> EntityUnregistered;

        public FactionRelationService FactionRelations => _factionRelations;

        public int Count
        {
            get
            {
                PruneInvalid();
                return _entities.Count;
            }
        }

        public IReadOnlyList<EntityIdentity> Entities
        {
            get
            {
                PruneInvalid();
                return _entities;
            }
        }

        public void Configure(FactionRelationService factionRelations)
        {
            _factionRelations = factionRelations;
        }

        public bool Register(EntityIdentity entity)
        {
            if (entity == null || !entity.isActiveAndEnabled || !_membership.Add(entity))
            {
                return false;
            }

            _entities.Add(entity);
            entity.NotifyRegistered(this);
            EntityRegistered?.Invoke(entity);
            return true;
        }

        public bool Unregister(EntityIdentity entity)
        {
            if (entity == null || !_membership.Remove(entity))
            {
                return false;
            }

            _entities.Remove(entity);
            entity.NotifyUnregistered(this);
            EntityUnregistered?.Invoke(entity);
            return true;
        }

        public bool Contains(EntityIdentity entity)
        {
            return entity != null && _membership.Contains(entity);
        }

        public int CollectByFaction(FactionId faction, List<EntityIdentity> results)
        {
            PrepareResults(results);
            for (int i = 0; i < _entities.Count; i++)
            {
                EntityIdentity entity = _entities[i];
                if (entity.TryGetComponent(out FactionMember member) && member.Faction == faction)
                {
                    results.Add(entity);
                }
            }

            return results.Count;
        }

        public int CollectAllied(FactionId observerFaction, List<EntityIdentity> results)
        {
            return CollectByRelation(observerFaction, FactionRelation.Allied, results);
        }

        public int CollectHostile(FactionId observerFaction, List<EntityIdentity> results)
        {
            return CollectByRelation(observerFaction, FactionRelation.Hostile, results);
        }

        public int CollectNeutral(FactionId observerFaction, List<EntityIdentity> results)
        {
            return CollectByRelation(observerFaction, FactionRelation.Neutral, results);
        }

        public int CollectByRole(UnitRole anyRole, List<EntityIdentity> results)
        {
            PrepareResults(results);
            if (anyRole == UnitRole.None)
            {
                return 0;
            }

            for (int i = 0; i < _entities.Count; i++)
            {
                EntityIdentity entity = _entities[i];
                if (entity.HasAnyRole(anyRole))
                {
                    results.Add(entity);
                }
            }

            return results.Count;
        }

        public int CollectSelectables(List<Selectable> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            PruneInvalid();
            for (int i = 0; i < _entities.Count; i++)
            {
                if (_entities[i].TryGetComponent(out Selectable selectable) && selectable.IsAlive)
                {
                    results.Add(selectable);
                }
            }

            return results.Count;
        }

        public int CollectCommandable(
            FactionId observerFaction,
            List<Selectable> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            PruneInvalid();
            if (_factionRelations == null)
            {
                return 0;
            }

            for (int i = 0; i < _entities.Count; i++)
            {
                if (_entities[i].TryGetComponent(out Selectable selectable) &&
                    selectable.CanBeCommandedBy(observerFaction, _factionRelations))
                {
                    results.Add(selectable);
                }
            }

            return results.Count;
        }

        [ContextMenu("Debug/Log Registry Summary")]
        private void LogRegistrySummary()
        {
            PruneInvalid();
            int friendly = 0;
            int enemy = 0;
            int neutral = 0;
            int selectableCount = 0;
            for (int i = 0; i < _entities.Count; i++)
            {
                EntityIdentity entity = _entities[i];
                if (entity.TryGetComponent(out FactionMember faction))
                {
                    switch (faction.Faction)
                    {
                        case FactionId.Friendly:
                            friendly++;
                            break;
                        case FactionId.Enemy:
                            enemy++;
                            break;
                        case FactionId.Neutral:
                            neutral++;
                            break;
                    }
                }

                if (entity.TryGetComponent(out Selectable selectable) && selectable.IsAlive)
                {
                    selectableCount++;
                }
            }

            Debug.Log(
                "[OriginCore P05] Registry total=" + _entities.Count +
                ", friendly=" + friendly +
                ", enemy=" + enemy +
                ", neutral=" + neutral +
                ", selectable=" + selectableCount + ".",
                this);
        }

        private int CollectByRelation(
            FactionId observerFaction,
            FactionRelation relation,
            List<EntityIdentity> results)
        {
            PrepareResults(results);
            if (_factionRelations == null)
            {
                return 0;
            }

            for (int i = 0; i < _entities.Count; i++)
            {
                EntityIdentity entity = _entities[i];
                if (entity.TryGetComponent(out FactionMember member) &&
                    _factionRelations.GetRelation(observerFaction, member.Faction) == relation)
                {
                    results.Add(entity);
                }
            }

            return results.Count;
        }

        private void PrepareResults(List<EntityIdentity> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            PruneInvalid();
        }

        private void PruneInvalid()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                EntityIdentity entity = _entities[i];
                if (entity != null && entity.isActiveAndEnabled)
                {
                    continue;
                }

                _entities.RemoveAt(i);
                _membership.Remove(entity);
                if (entity != null)
                {
                    entity.NotifyUnregistered(this);
                    EntityUnregistered?.Invoke(entity);
                }
            }
        }

        private void OnDisable()
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                EntityIdentity entity = _entities[i];
                if (entity != null)
                {
                    entity.NotifyUnregistered(this);
                    EntityUnregistered?.Invoke(entity);
                }
            }

            _entities.Clear();
            _membership.Clear();
        }
    }
}
