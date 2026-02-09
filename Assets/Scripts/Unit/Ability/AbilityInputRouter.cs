using System.Collections.Generic;
using Unit.Ability;
using UnityEngine;

[DisallowMultipleComponent]
public class AbilityInputRouter : MonoBehaviour
{
    readonly List<IAbilityInput> _abilities = new List<IAbilityInput>(16);

    void Awake()
    {
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        _abilities.Clear();

        // 找到同物体上所有 MonoBehaviour，然后筛出实现了 IAbilityInput 的
        var mbs = GetComponents<MonoBehaviour>();
        for (int i = 0; i < mbs.Length; i++)
        {
            if (mbs[i] is IAbilityInput a)
                _abilities.Add(a);
        }
    }

    public void Process(InputIntent intent)
    {
        // 分发给所有能力
        for (int i = 0; i < _abilities.Count; i++)
            _abilities[i].ProcessInput(intent);
    }
}
