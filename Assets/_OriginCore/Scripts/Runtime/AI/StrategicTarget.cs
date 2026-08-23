using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class StrategicTarget : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private bool _publiclyKnown = true;
        public EntityIdentity Identity => _identity != null ? _identity :
            (_identity = GetComponent<EntityIdentity>());
        public bool PubliclyKnown => _publiclyKnown;
    }
}
