using OriginCore.Content;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilityLoadout))]
    public sealed class AbilityChannelController : MonoBehaviour
    {
        [SerializeField] private AbilityDefinition _autoRepeatAbility;

        public AbilityDefinition AutoRepeatAbility => _autoRepeatAbility;

        public void Configure(AbilityDefinition autoRepeatAbility)
        {
            _autoRepeatAbility = autoRepeatAbility;
        }

        public bool Supports(AbilityDefinition definition)
        {
            return definition != null && _autoRepeatAbility == definition;
        }
    }
}
