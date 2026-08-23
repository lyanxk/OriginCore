using System;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    public enum DamageType
    {
        Physical = 0,
        Energy = 1,
        Explosive = 2,
        True = 3
    }

    [Flags]
    public enum DamageFlags
    {
        None = 0,
        IgnoreArmor = 1 << 0,
        IgnoreShield = 1 << 1,
        Area = 1 << 2,
        Periodic = 1 << 3
    }

    public readonly struct DamageInfo
    {
        public DamageInfo(float amount)
            : this(
                amount,
                null,
                FactionId.Neutral,
                DamageType.Physical,
                DamageFlags.None,
                Vector3.zero,
                false,
                string.Empty,
                string.Empty)
        {
        }

        public DamageInfo(
            float amount,
            EntityIdentity source,
            FactionId sourceFaction,
            Vector3 hitPoint,
            bool hasHitPoint = true)
            : this(
                amount,
                source,
                sourceFaction,
                DamageType.Physical,
                DamageFlags.None,
                hitPoint,
                hasHitPoint,
                string.Empty,
                string.Empty)
        {
        }

        public DamageInfo(
            float amount,
            EntityIdentity source,
            FactionId sourceFaction,
            DamageType damageType,
            DamageFlags flags,
            Vector3 hitPoint,
            bool hasHitPoint = true,
            string abilityId = "",
            string weaponId = "")
        {
            Amount = amount;
            Source = source;
            SourceFaction = sourceFaction;
            DamageType = damageType;
            Flags = flags;
            HitPoint = hitPoint;
            HasHitPoint = hasHitPoint;
            AbilityId = abilityId ?? string.Empty;
            WeaponId = weaponId ?? string.Empty;
        }

        public float Amount { get; }
        public EntityIdentity Source { get; }
        public FactionId SourceFaction { get; }
        public DamageType DamageType { get; }
        public DamageFlags Flags { get; }
        public Vector3 HitPoint { get; }
        public bool HasHitPoint { get; }
        public string AbilityId { get; }
        public string WeaponId { get; }

        public bool HasFlag(DamageFlags flag)
        {
            return flag != DamageFlags.None && (Flags & flag) == flag;
        }
    }

    public readonly struct DamageResult
    {
        public static DamageResult None => default(DamageResult);

        public DamageResult(
            float requestedAmount,
            float shieldDamage,
            float healthDamage,
            bool killed)
            : this(
                requestedAmount,
                requestedAmount,
                0f,
                shieldDamage,
                healthDamage,
                killed)
        {
        }

        public DamageResult(
            float requestedAmount,
            float modifiedAmount,
            float armorMitigation,
            float shieldDamage,
            float healthDamage,
            bool killed)
        {
            RequestedAmount = Mathf.Max(0f, requestedAmount);
            ModifiedAmount = Mathf.Max(0f, modifiedAmount);
            ArmorMitigation = Mathf.Max(0f, armorMitigation);
            ShieldDamage = Mathf.Max(0f, shieldDamage);
            HealthDamage = Mathf.Max(0f, healthDamage);
            Killed = killed;
        }

        public float RequestedAmount { get; }
        public float ModifiedAmount { get; }
        public float ArmorMitigation { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
        public float AppliedAmount => ShieldDamage + HealthDamage;
        public float PreventedAmount => Mathf.Max(0f, ModifiedAmount - AppliedAmount);
        public bool Killed { get; }
        public bool Changed => AppliedAmount > 0f;
    }
}
