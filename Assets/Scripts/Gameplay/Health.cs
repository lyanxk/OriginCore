using System;
using UnityEngine;

namespace Gameplay
{
    public class Health : MonoBehaviour
    {
        [Min(1f)] public float maxHp = 100f;
        [SerializeField] float _currentHp;
        [SerializeField, HideInInspector] TeamAffiliation teamAffiliation;
        float _invulnerableTimer;

        public float CurrentHp => _currentHp;
        public float MaxHp => maxHp;
        public TeamAffiliation TeamAffiliation => teamAffiliation;

        [Range(0f, 1f)]
        public float startHpPercent = 1f;

        public event Action<float, float> OnHpChanged; // (current, max)

        void Awake()
        {
            CacheComponents();
            _currentHp = maxHp * startHpPercent;
            OnHpChanged?.Invoke(_currentHp, maxHp);
        }

        void OnValidate()
        {
            CacheComponents();
        }

        void Update()
        {
            _invulnerableTimer = Mathf.Max(0f, _invulnerableTimer - Time.deltaTime);
        }

        public void SetHp(float value)
        {
            float newHp = Mathf.Clamp(value, 0f, maxHp);
            if (Mathf.Approximately(newHp, _currentHp)) return;

            _currentHp = newHp;
            OnHpChanged?.Invoke(_currentHp, maxHp);

            if (_currentHp <= 0f) Die();
        }

        public void TakeDamage(float dmg)
        {
            if (_invulnerableTimer > 0f)
                return;

            SetHp(_currentHp - Mathf.Max(0f, dmg));
        }

        public void Heal(float v) => SetHp(_currentHp + Mathf.Max(0f, v));

        public void AddInvulnerability(float duration)
        {
            if (duration <= 0f)
                return;

            _invulnerableTimer = Mathf.Max(_invulnerableTimer, duration);
        }

        void Die()
        {
            Destroy(gameObject);
        }

        void CacheComponents()
        {
            teamAffiliation ??= GetComponent<TeamAffiliation>() ?? GetComponentInParent<TeamAffiliation>();
        }

        public static bool TryResolve(Component hitComponent, out Health health)
        {
            health = null;
            if (hitComponent == null)
                return false;

            if (hitComponent.TryGetComponent(out health))
                return true;

            health = hitComponent.GetComponentInParent<Health>() ?? hitComponent.GetComponentInChildren<Health>(true);
            return health != null;
        }
    }
}
