using UnityEngine;

public class Selectable : MonoBehaviour
{
    public bool IsSelected { get; private set; }

    Outline _outline;
    bool _registered;

    void Awake()
    {
        _outline = GetComponentInChildren<Outline>();

        if (_outline != null)
        {
            _outline.enabled = false;
            _outline.OutlineColor = Color.green;
            _outline.OutlineWidth = 4f;
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (_outline != null)
        {
            _outline.enabled = selected;
        }
    }
    void OnEnable()
    {
        TryRegister();
    }

    void Start()
    {
        // SelectionManager may initialize after this component's OnEnable.
        TryRegister();
    }

    void Update()
    {
        if (_registered)
            return;

        TryRegister();
    }

    void OnDisable()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.Unregister(this);

        _registered = false;
    }

    void TryRegister()
    {
        if (_registered)
            return;

        SelectionManager manager = SelectionManager.Instance;
        if (manager == null)
            return;

        manager.Register(this);
        _registered = true;
    }

}
