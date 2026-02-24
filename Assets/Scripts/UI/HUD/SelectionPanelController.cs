using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectionPanelController : MonoBehaviour
{
    [Header("Single View")]
    [SerializeField] GameObject singleView;
    [SerializeField] Image singlePortraitImage;
    [SerializeField] TMP_Text singleNameText;
    [SerializeField] Image singleHealthFillImage;
    [SerializeField] Image singleEnergyFillImage;

    [Header("Multi View")]
    [SerializeField] GameObject multiView;
    [SerializeField] Transform multiGridRoot;
    [SerializeField] UnitIconItemView unitIconItemPrefab;

    readonly List<UnitIconItemView> _iconItems = new List<UnitIconItemView>(32);

    SelectionManager _selection;
    Selectable _singleUnit;
    UnitUIDataSource _singleDataSource;

    void Awake()
    {
        SetPanelState(showSingle: false, showMulti: false);
    }

    void OnEnable()
    {
        BindSelectionManager();
        RefreshFromManager();
    }

    void OnDisable()
    {
        UnbindSelectionManager();
        ClearSingleState();
    }

    void Update()
    {
        if (_selection != SelectionManager.Instance)
        {
            UnbindSelectionManager();
            BindSelectionManager();
            RefreshFromManager();
            return;
        }

        RefreshSingleVitals();
    }

    void BindSelectionManager()
    {
        _selection = SelectionManager.Instance;
        if (_selection != null)
            _selection.OnSelectionChanged += HandleSelectionChanged;
    }

    void UnbindSelectionManager()
    {
        if (_selection != null)
            _selection.OnSelectionChanged -= HandleSelectionChanged;

        _selection = null;
    }

    void HandleSelectionChanged(IReadOnlyList<Selectable> selected, Selectable primary)
    {
        RefreshView(selected, primary);
    }

    void RefreshFromManager()
    {
        if (_selection == null)
        {
            RefreshView(null, null);
            return;
        }

        RefreshView(_selection.Selected, _selection.Primary);
    }

    void RefreshView(IReadOnlyList<Selectable> selected, Selectable primary)
    {
        int count = selected != null ? selected.Count : 0;

        if (count <= 0)
        {
            ClearSingleState();
            SetPanelState(showSingle: false, showMulti: false);
            return;
        }

        if (count == 1)
        {
            BuildSingleView(selected[0]);
            SetPanelState(showSingle: true, showMulti: false);
            return;
        }

        BuildMultiView(selected, primary);
        SetPanelState(showSingle: false, showMulti: true);
    }

    void BuildSingleView(Selectable selectable)
    {
        _singleUnit = selectable;
        _singleDataSource = selectable != null ? selectable.GetComponent<UnitUIDataSource>() : null;

        string displayName = selectable != null ? selectable.name : string.Empty;
        Sprite portrait = null;

        if (_singleDataSource != null)
        {
            displayName = _singleDataSource.DisplayName;
            portrait = _singleDataSource.Portrait;
        }

        if (singleNameText != null)
            singleNameText.text = displayName;

        if (singlePortraitImage != null)
        {
            singlePortraitImage.sprite = portrait;
            singlePortraitImage.enabled = portrait != null;
        }

        RefreshSingleVitals();
    }

    void BuildMultiView(IReadOnlyList<Selectable> selected, Selectable primary)
    {
        _singleUnit = null;
        _singleDataSource = null;

        EnsureIconCount(selected.Count);
        if (_iconItems.Count < selected.Count)
            return;

        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            UnitUIDataSource dataSource = selectable != null ? selectable.GetComponent<UnitUIDataSource>() : null;

            UnitIconItemView item = _iconItems[i];
            item.gameObject.SetActive(true);
            item.Bind(selectable, dataSource, selectable == primary, HandleUnitIconClicked);
        }

        for (int i = selected.Count; i < _iconItems.Count; i++)
            _iconItems[i].gameObject.SetActive(false);
    }

    void HandleUnitIconClicked(Selectable selectable)
    {
        if (selectable == null || _selection == null)
            return;

        _selection.SetPrimary(selectable);
    }

    void RefreshSingleVitals()
    {
        if (singleView == null || !singleView.activeSelf)
            return;

        float healthCurrent = 0f;
        float healthMax = 0f;
        float energyCurrent = 0f;
        float energyMax = 0f;

        if (_singleDataSource != null)
        {
            _singleDataSource.TryGetHealth(out healthCurrent, out healthMax);
            _singleDataSource.TryGetEnergy(out energyCurrent, out energyMax);
        }
        else if (_singleUnit != null)
        {
            Health health = _singleUnit.GetComponent<Health>();
            if (health != null)
            {
                healthCurrent = health.CurrentHp;
                healthMax = health.MaxHp;
            }
        }

        if (singleHealthFillImage != null)
            singleHealthFillImage.fillAmount = healthMax > 0f ? Mathf.Clamp01(healthCurrent / healthMax) : 0f;

        if (singleEnergyFillImage != null)
            singleEnergyFillImage.fillAmount = energyMax > 0f ? Mathf.Clamp01(energyCurrent / energyMax) : 0f;
    }

    void EnsureIconCount(int count)
    {
        if (multiGridRoot == null || unitIconItemPrefab == null)
            return;

        while (_iconItems.Count < count)
        {
            UnitIconItemView view = Instantiate(unitIconItemPrefab, multiGridRoot);
            view.gameObject.SetActive(false);
            _iconItems.Add(view);
        }
    }

    void SetPanelState(bool showSingle, bool showMulti)
    {
        if (singleView != null)
            singleView.SetActive(showSingle);

        if (multiView != null)
            multiView.SetActive(showMulti);
    }

    void ClearSingleState()
    {
        _singleUnit = null;
        _singleDataSource = null;

        if (singleNameText != null)
            singleNameText.text = string.Empty;

        if (singlePortraitImage != null)
        {
            singlePortraitImage.sprite = null;
            singlePortraitImage.enabled = false;
        }

        if (singleHealthFillImage != null)
            singleHealthFillImage.fillAmount = 0f;

        if (singleEnergyFillImage != null)
            singleEnergyFillImage.fillAmount = 0f;

        for (int i = 0; i < _iconItems.Count; i++)
            _iconItems[i].gameObject.SetActive(false);
    }
}
