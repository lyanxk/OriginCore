using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Min(1f)] public float maxHp = 100f;
    [SerializeField] float _currentHp;

    public float CurrentHp => _currentHp;
    public float MaxHp => maxHp;
    
    [Range(0f, 1f)]
    public float startHpPercent = 1f;

    public event Action<float, float> OnHpChanged; // (current, max)

    void Awake()
    {
        _currentHp = maxHp * startHpPercent;
        OnHpChanged?.Invoke(_currentHp, maxHp);
    }

    public void SetHp(float value)
    {
        float newHp = Mathf.Clamp(value, 0f, maxHp);
        if (Mathf.Approximately(newHp, _currentHp)) return;

        _currentHp = newHp;
        OnHpChanged?.Invoke(_currentHp, maxHp);

        if (_currentHp <= 0f) Die();
    }

    public void TakeDamage(float dmg) => SetHp(_currentHp - Mathf.Max(0f, dmg));
    public void Heal(float v) => SetHp(_currentHp + Mathf.Max(0f, v));

    void Die()
    {
        Destroy(gameObject);
    }
}