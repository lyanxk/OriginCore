using UnityEngine;

namespace OriginCore.Content
{
    [DefaultExecutionOrder(-11650)]
    [DisallowMultipleComponent]
    public sealed class ContentCatalogService : MonoBehaviour
    {
        [SerializeField] private ContentCatalog _catalog;

        public ContentCatalog Catalog => _catalog;
        public bool IsReady { get; private set; }

        public void Configure(ContentCatalog catalog)
        {
            _catalog = catalog;
            IsReady = false;
        }

        public bool Initialize(out string error)
        {
            if (_catalog == null)
            {
                error = "Content catalog is not assigned.";
                IsReady = false;
                return false;
            }

            if (!_catalog.TryValidate(out error))
            {
                IsReady = false;
                return false;
            }

            IsReady = true;
            return true;
        }

        public void Shutdown()
        {
            IsReady = false;
        }
    }
}
