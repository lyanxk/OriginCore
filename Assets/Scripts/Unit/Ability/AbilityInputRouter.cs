using System;
using System.Collections.Generic;
using Unit.Ability;
using UnityEngine;

[DisallowMultipleComponent]
public class AbilityInputRouter : MonoBehaviour
{
    readonly List<IAbilityInput> _abilities = new List<IAbilityInput>(16);
    readonly List<IActivatableAbility> _activatableAbilities = new List<IActivatableAbility>(16);
    readonly Dictionary<string, IActivatableAbility> _abilityMap =
        new Dictionary<string, IActivatableAbility>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IActivatableAbility> ActivatableAbilities => _activatableAbilities;

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
        _activatableAbilities.Clear();
        _abilityMap.Clear();

        MonoBehaviour[] mbs = GetComponents<MonoBehaviour>();
        for (int i = 0; i < mbs.Length; i++)
        {
            MonoBehaviour mono = mbs[i];
            if (mono == null) continue;

            if (mono is IAbilityInput input)
                _abilities.Add(input);

            if (mono is not IActivatableAbility activatable)
                continue;

            if (string.IsNullOrWhiteSpace(activatable.AbilityId))
                continue;

            if (_abilityMap.ContainsKey(activatable.AbilityId))
            {
                Debug.LogWarning(
                    $"Duplicate ability id '{activatable.AbilityId}' on '{name}'. Keeping first registration.",
                    this);
                continue;
            }

            _activatableAbilities.Add(activatable);
            _abilityMap.Add(activatable.AbilityId, activatable);
        }
    }

    public void Process(InputIntent intent)
    {
        for (int i = 0; i < _abilities.Count; i++)
            _abilities[i].ProcessInput(intent);
    }

    public bool TryActivate(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        if (!_abilityMap.TryGetValue(abilityId, out IActivatableAbility ability))
            return false;

        return ability.TryActivate();
    }

    public bool HasAbility(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return false;

        return _abilityMap.ContainsKey(abilityId);
    }
}
