using System;
using System.Collections.Generic;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Weapons;

namespace OriginCore.Save
{
    internal sealed class InventoryAbilitySaveParticipant : ISaveParticipant
    {
        private readonly Dictionary<string, EntitySaveData> _entityDataById =
            new Dictionary<string, EntitySaveData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InventoryAbilitySaveData> _abilityDataById =
            new Dictionary<string, InventoryAbilitySaveData>(StringComparer.Ordinal);

        public string ParticipantKey => "inventory-abilities";
        public int RestoreOrder => 180;

        public void Capture(SaveOperationContext context, SaveGameData data)
        {
            data.inventoryAbilities.Clear();
            AbilityLoadout[] loadouts = EntitySaveParticipant.CollectSceneComponents<AbilityLoadout>(
                context.SceneContext.Scene,
                true);
            for (int i = 0; i < loadouts.Length; i++)
            {
                AbilityLoadout loadout = loadouts[i];
                EntityIdentity identity = loadout != null ? loadout.Identity : null;
                if (identity == null || string.IsNullOrEmpty(identity.RuntimeId))
                {
                    continue;
                }

                InventoryAbilitySaveData snapshot = new InventoryAbilitySaveData
                {
                    ownerRuntimeId = identity.RuntimeId
                };
                IReadOnlyList<AbilityRuntime> runtimes = loadout.Runtimes;
                for (int runtimeIndex = 0; runtimeIndex < runtimes.Count; runtimeIndex++)
                {
                    AbilityRuntime runtime = runtimes[runtimeIndex];
                    if (runtime == null || runtime.Definition == null)
                    {
                        continue;
                    }

                    snapshot.abilities.Add(new AbilityStateSaveData
                    {
                        abilityId = runtime.Definition.ContentId,
                        cooldownRemaining = runtime.CooldownRemaining,
                        charges = runtime.Charges,
                        sharedCooldownGroup = runtime.Definition.SharedCooldownGroup
                    });
                }

                data.inventoryAbilities.Add(snapshot);
            }

            WeaponInventory[] inventories =
                EntitySaveParticipant.CollectSceneComponents<WeaponInventory>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < inventories.Length; i++)
            {
                WeaponInventory inventory = inventories[i];
                EntityIdentity identity = inventory != null
                    ? inventory.GetComponent<EntityIdentity>()
                    : null;
                if (identity == null || string.IsNullOrEmpty(identity.RuntimeId))
                {
                    continue;
                }

                InventoryAbilitySaveData snapshot = FindOrCreateSnapshot(
                    data.inventoryAbilities,
                    identity.RuntimeId);
                snapshot.equippedPrimaryId = inventory.FpsPrimary != null
                    ? inventory.FpsPrimary.ContentId
                    : string.Empty;
                snapshot.equippedSecondaryId = inventory.FpsSecondary != null
                    ? inventory.FpsSecondary.ContentId
                    : string.Empty;
                snapshot.equippedMeleeId = inventory.FpsMelee != null
                    ? inventory.FpsMelee.ContentId
                    : string.Empty;
                snapshot.activeActWeaponId = inventory.ActiveWeapon != null &&
                    inventory.Mode == GameMode.ACT
                    ? inventory.ActiveWeapon.ContentId
                    : string.Empty;
                snapshot.auxiliaryActWeaponId = inventory.ActAuxiliary != null
                    ? inventory.ActAuxiliary.ContentId
                    : string.Empty;
                snapshot.weapons.Clear();
                IReadOnlyList<WeaponRuntimeState> states = inventory.States;
                for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
                {
                    WeaponRuntimeState state = states[stateIndex];
                    if (state == null || state.Definition == null)
                    {
                        continue;
                    }

                    snapshot.weapons.Add(new WeaponStateSaveData
                    {
                        weaponId = state.Definition.ContentId,
                        magazineAmmo = state.MagazineAmmo,
                        reserveAmmo = state.ReserveAmmo
                    });
                }
            }
        }

        public void Restore(SaveOperationContext context, SaveGameData data)
        {
            BuildIndexes(data);
            AbilityLoadout[] loadouts = EntitySaveParticipant.CollectSceneComponents<AbilityLoadout>(
                context.SceneContext.Scene,
                true);
            for (int i = 0; i < loadouts.Length; i++)
            {
                AbilityLoadout loadout = loadouts[i];
                EntityIdentity identity = loadout != null ? loadout.Identity : null;
                if (identity == null || string.IsNullOrEmpty(identity.RuntimeId))
                {
                    continue;
                }

                _entityDataById.TryGetValue(identity.RuntimeId, out EntitySaveData entityData);
                RestoreFormAndModifiers(context, identity, entityData);
                if (_abilityDataById.TryGetValue(
                        identity.RuntimeId,
                        out InventoryAbilitySaveData abilityData))
                {
                    RestoreAbilityStates(context, loadout, abilityData);
                }

                if (entityData != null && entityData.hasVitals &&
                    identity.TryGetComponent(out VitalsComponent vitals))
                {
                    vitals.Restore(entityData.ToVitalsSnapshot());
                }

                if (entityData != null && entityData.isRespawning &&
                    identity.TryGetComponent(out RespawnController respawn) &&
                    !respawn.RestoreRespawnCountdown(entityData.respawnRemainingSeconds))
                {
                    context.Warn(
                        "Respawn countdown could not be restored for '" +
                        identity.RuntimeId + "'.");
                }
            }

            WeaponInventory[] inventories =
                EntitySaveParticipant.CollectSceneComponents<WeaponInventory>(
                    context.SceneContext.Scene,
                    true);
            for (int i = 0; i < inventories.Length; i++)
            {
                WeaponInventory inventory = inventories[i];
                EntityIdentity identity = inventory != null
                    ? inventory.GetComponent<EntityIdentity>()
                    : null;
                if (identity == null || !_abilityDataById.TryGetValue(
                        identity.RuntimeId,
                        out InventoryAbilitySaveData snapshot))
                {
                    continue;
                }

                snapshot.EnsureCollections();
                for (int weaponIndex = 0; weaponIndex < snapshot.weapons.Count; weaponIndex++)
                {
                    WeaponStateSaveData weapon = snapshot.weapons[weaponIndex];
                    if (weapon != null && !inventory.RestoreAmmo(
                            weapon.weaponId,
                            weapon.magazineAmmo,
                            weapon.reserveAmmo))
                    {
                        context.Warn(
                            "Saved weapon '" + weapon.weaponId +
                            "' is unavailable on '" + identity.RuntimeId + "'.");
                    }
                }

                inventory.RestoreEquipped(
                    snapshot.equippedPrimaryId,
                    snapshot.equippedSecondaryId,
                    snapshot.equippedMeleeId);
                inventory.RestoreActState(
                    snapshot.activeActWeaponId,
                    snapshot.auxiliaryActWeaponId);
            }
        }

        private static InventoryAbilitySaveData FindOrCreateSnapshot(
            List<InventoryAbilitySaveData> snapshots,
            string runtimeId)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                InventoryAbilitySaveData snapshot = snapshots[i];
                if (snapshot != null && string.Equals(
                        snapshot.ownerRuntimeId,
                        runtimeId,
                        StringComparison.Ordinal))
                {
                    return snapshot;
                }
            }

            InventoryAbilitySaveData created = new InventoryAbilitySaveData
            {
                ownerRuntimeId = runtimeId
            };
            snapshots.Add(created);
            return created;
        }

        private void BuildIndexes(SaveGameData data)
        {
            _entityDataById.Clear();
            for (int i = 0; i < data.entities.Count; i++)
            {
                EntitySaveData entity = data.entities[i];
                if (entity != null && !string.IsNullOrEmpty(entity.runtimeId))
                {
                    _entityDataById[entity.runtimeId] = entity;
                }
            }

            _abilityDataById.Clear();
            for (int i = 0; i < data.inventoryAbilities.Count; i++)
            {
                InventoryAbilitySaveData abilities = data.inventoryAbilities[i];
                if (abilities != null && !string.IsNullOrEmpty(abilities.ownerRuntimeId))
                {
                    _abilityDataById[abilities.ownerRuntimeId] = abilities;
                }
            }
        }

        private static void RestoreFormAndModifiers(
            SaveOperationContext context,
            EntityIdentity identity,
            EntitySaveData entityData)
        {
            if (entityData == null)
            {
                return;
            }

            RuntimeStatBlock stats = identity.GetComponent<RuntimeStatBlock>();
            HeroFormStateMachine forms = identity.GetComponent<HeroFormStateMachine>();
            if (forms != null && !string.IsNullOrEmpty(entityData.heroFormId))
            {
                stats?.ClearModifiers();
                if (!forms.RestoreForm(entityData.heroFormId, out string error))
                {
                    context.Warn(
                        "Hero form could not be restored for '" + identity.RuntimeId +
                        "': " + error);
                }
            }

            if (stats == null)
            {
                return;
            }

            entityData.EnsureCollections();
            for (int i = 0; i < entityData.persistentModifiers.Count; i++)
            {
                PersistentStatModifierSaveData modifier = entityData.persistentModifiers[i];
                if (modifier != null)
                {
                    stats.AddModifier(modifier.ToDefinition());
                }
            }
        }

        private static void RestoreAbilityStates(
            SaveOperationContext context,
            AbilityLoadout loadout,
            InventoryAbilitySaveData snapshot)
        {
            for (int i = 0; i < snapshot.abilities.Count; i++)
            {
                AbilityStateSaveData saved = snapshot.abilities[i];
                if (saved == null || !loadout.TryGetRuntime(
                        saved.abilityId,
                        out AbilityRuntime runtime))
                {
                    context.Warn(
                        "Saved ability '" + (saved != null ? saved.abilityId : string.Empty) +
                        "' is unavailable on '" + snapshot.ownerRuntimeId + "'.");
                    continue;
                }

                runtime.Restore(saved.charges, saved.cooldownRemaining);
            }
        }
    }
}
