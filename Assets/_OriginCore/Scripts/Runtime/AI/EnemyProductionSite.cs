using OriginCore.Buildings;
using OriginCore.Combat;
using UnityEngine;

namespace OriginCore.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProductionQueue))]
    public sealed class EnemyProductionSite : MonoBehaviour
    {
        [SerializeField] private ProductionQueue _queue;
        public ProductionQueue Queue => _queue != null ? _queue :
            (_queue = GetComponent<ProductionQueue>());
        public bool IsOperational
        {
            get
            {
                ProductionQueue queue = Queue;
                if (!isActiveAndEnabled || queue == null || !queue.isActiveAndEnabled)
                {
                    return false;
                }

                VitalsComponent vitals = GetComponent<VitalsComponent>();
                return vitals == null || vitals.IsAlive;
            }
        }

        public void ApplyContext(ProductionContext context)
        {
            Queue?.SetProductionContext(context);
        }
    }
}
