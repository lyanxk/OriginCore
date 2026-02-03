using UnityEngine;

public class Selectable : MonoBehaviour
{
    public bool IsSelected { get; private set; }

    Outline _outline;

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
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.Unregister(this);
    }

}