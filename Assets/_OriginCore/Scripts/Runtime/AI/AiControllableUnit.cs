using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using UnityEngine;

namespace OriginCore.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(UnitCommandQueue))]
    public sealed class AiControllableUnit : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private UnitCommandQueue _commands;
        public EntityIdentity Identity => _identity != null ? _identity :
            (_identity = GetComponent<EntityIdentity>());
        public UnitCommandQueue Commands => _commands != null ? _commands :
            (_commands = GetComponent<UnitCommandQueue>());
    }
}
