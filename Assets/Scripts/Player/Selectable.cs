using UnityEngine;

public class Selectable : MonoBehaviour
{
    public bool IsSelected { get; private set; }

    Outline _outline;

    void Awake()
    {
        // ⚠️ Outline 在子物体上，所以用 GetComponentInChildren
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
}