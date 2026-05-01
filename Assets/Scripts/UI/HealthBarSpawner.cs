using Gameplay;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace UI
{
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "HealthBarSpawner")]
    public class HealthBarSpawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] Transform healthBarRoot;   // 拖 HealthBarRoot
        [SerializeField] HealthBarUI healthBarPrefab; // 拖你的血条 Prefab（带 HealthBarUI 的那个）

        Health _health;
        HealthBarUI _barInstance;

        void Awake()
        {
            _health = GetComponent<Health>();
        }

        void Start()
        {
            if (_health == null || healthBarRoot == null)
                return;

            _barInstance = healthBarRoot.GetComponentInChildren<HealthBarUI>(true);
            if (_barInstance == null)
            {
                if (healthBarPrefab == null)
                    return;

                _barInstance = Instantiate(healthBarPrefab, healthBarRoot);
                _barInstance.transform.localPosition = Vector3.zero;
                _barInstance.transform.localRotation = Quaternion.identity;
            }

            _barInstance.Bind(_health);
        }
    }
}
