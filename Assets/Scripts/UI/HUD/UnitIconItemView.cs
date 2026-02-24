using System;
using UnityEngine;
using UnityEngine.UI;

public class UnitIconItemView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image portraitImage;
    [SerializeField] Image healthFillImage;
    [SerializeField] GameObject primaryHighlight;

    Selectable _unit;
    UnitUIDataSource _dataSource;
    Health _health;
    Action<Selectable> _onClick;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    void Update()
    {
        RefreshHealthFill();
    }

    public void Bind(Selectable unit, UnitUIDataSource dataSource, bool isPrimary, Action<Selectable> onClick)
    {
        _unit = unit;
        _dataSource = dataSource;
        _onClick = onClick;

        _health = _dataSource != null
            ? _dataSource.HealthComponent
            : (_unit != null ? _unit.GetComponent<Health>() : null);

        if (portraitImage != null)
        {
            Sprite portrait = _dataSource != null ? _dataSource.Portrait : null;
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (button != null)
            button.interactable = _unit != null;

        SetPrimary(isPrimary);
        RefreshHealthFill();
    }

    public void SetPrimary(bool isPrimary)
    {
        if (primaryHighlight != null)
            primaryHighlight.SetActive(isPrimary);
    }

    void HandleClick()
    {
        if (_unit == null) return;
        _onClick?.Invoke(_unit);
    }

    void RefreshHealthFill()
    {
        if (healthFillImage == null)
            return;

        float current = 0f;
        float max = 0f;

        if (_dataSource != null)
            _dataSource.TryGetHealth(out current, out max);
        else if (_health != null)
        {
            current = _health.CurrentHp;
            max = _health.MaxHp;
        }

        healthFillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }
}
