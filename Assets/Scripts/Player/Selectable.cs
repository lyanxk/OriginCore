using UnityEngine;

public class Selectable : MonoBehaviour
{
    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        // 选中逻辑 此处用缩小暂时替代
        transform.localScale = selected ? Vector3.one * 1.15f : Vector3.one;
    }
}