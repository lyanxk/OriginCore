using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] Image fill; // 拖 Fill 的 Image

    Health _health;
    Transform _camTr;

    void Awake()
    {
        var cam = Camera.main;
        _camTr = cam != null ? cam.transform : null;
    }

    public void Bind(Health health)
    {
        if (_health != null)
            _health.OnHpChanged -= OnHpChanged;

        _health = health;
        if (_health == null) return;

        _health.OnHpChanged += OnHpChanged;
        OnHpChanged(_health.CurrentHp, _health.MaxHp);
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnHpChanged -= OnHpChanged;
    }

    void LateUpdate()
    {
        if (_camTr == null) return;
        // 让血条面向摄像机（Billboard）
        transform.forward = _camTr.forward;
    }

    void OnHpChanged(float current, float max)
    {
        if (fill == null) return;
        fill.fillAmount = (max <= 0f) ? 0f : current / max;
    }
}