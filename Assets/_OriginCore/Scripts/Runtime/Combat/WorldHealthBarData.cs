using UnityEngine;

namespace OriginCore.Combat
{
    public readonly struct WorldHealthBarData
    {
        public WorldHealthBarData(
            float health,
            float maxHealth,
            float shield,
            float maxShield,
            bool isAlive)
        {
            Health = health;
            MaxHealth = maxHealth;
            Shield = shield;
            MaxShield = maxShield;
            IsAlive = isAlive;
        }

        public float Health { get; }
        public float MaxHealth { get; }
        public float Shield { get; }
        public float MaxShield { get; }
        public bool IsAlive { get; }
        public float HealthNormalized => MaxHealth <= 0f ? 0f : Mathf.Clamp01(Health / MaxHealth);
        public float ShieldNormalized => MaxShield <= 0f ? 0f : Mathf.Clamp01(Shield / MaxShield);
    }
}
