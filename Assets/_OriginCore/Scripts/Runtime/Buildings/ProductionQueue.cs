using System;
using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Progression;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.Buildings
{
    public enum ProductionRejectionReason
    {
        None = 0,
        InvalidRecipe = 1,
        RecipeNotAvailable = 2,
        QueueFull = 3,
        ResourceServiceUnavailable = 4,
        InsufficientResources = 5,
        InfluenceCapReached = 6
    }

    public readonly struct ProductionOrderSnapshot
    {
        public ProductionOrderSnapshot(
            ProductionRecipe recipe,
            float remainingTime,
            float normalizedProgress)
        {
            Recipe = recipe;
            RemainingTime = Mathf.Max(0f, remainingTime);
            NormalizedProgress = Mathf.Clamp01(normalizedProgress);
        }

        public ProductionRecipe Recipe { get; }
        public float RemainingTime { get; }
        public float NormalizedProgress { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitSpawner), typeof(RallyPointController))]
    public sealed class ProductionQueue : MonoBehaviour
    {
        public const int DefaultCapacity = 5;

        private sealed class ProductionOrder
        {
            public ProductionOrder(ProductionRecipe recipe, float totalTime)
            {
                Recipe = recipe;
                TotalTime = Mathf.Max(0.05f, totalTime);
                RemainingTime = recipe != null ? TotalTime : 0f;
            }

            public ProductionRecipe Recipe { get; }
            public float TotalTime { get; }
            public float RemainingTime { get; set; }
        }

        [SerializeField] private ProductionRecipe[] _recipes =
            Array.Empty<ProductionRecipe>();
        [SerializeField] private UnitSpawner _unitSpawner;
        [SerializeField] private RallyPointController _rallyPointController;
        [SerializeField] private VitalsComponent _producerVitals;
        [Min(1), SerializeField] private int _capacity = DefaultCapacity;
        [Min(0.05f), SerializeField] private float _spawnRetryInterval = 0.5f;

        private readonly Queue<ProductionOrder> _waiting =
            new Queue<ProductionOrder>(DefaultCapacity);
        private ProductionContext _context;
        private ProductionOrder _current;
        private float _spawnRetryRemaining;
        private bool _awaitingWorldDeployment;
        private bool _eventsBound;

        public event Action<ProductionQueue> QueueChanged;
        public event Action<ProductionQueue, ProductionOrderSnapshot> ProgressChanged;
        public event Action<ProductionQueue, ProductionRecipe, ProductionRejectionReason, string>
            ProductionRejected;
        public event Action<ProductionQueue, ProductionRecipe, EntityIdentity> UnitProduced;

        public IReadOnlyList<ProductionRecipe> Recipes => _recipes;
        public UnitSpawner UnitSpawner => _unitSpawner;
        public RallyPointController RallyPointController => _rallyPointController;
        public VitalsComponent ProducerVitals => _producerVitals;
        public ResourceService ResourceService => _context?.Account as ResourceService;
        public IProductionResourceAccount ResourceAccount => _context?.Account;
        public ProductionContext Context => _context;
        public int Capacity => _capacity;
        public int WaitingCount => _waiting.Count;
        public int TotalCount => (_current != null ? 1 : 0) + _waiting.Count;
        public ProductionRecipe CurrentRecipe => _current != null ? _current.Recipe : null;
        public float CurrentRemainingTime => _current != null
            ? Mathf.Max(0f, _current.RemainingTime)
            : 0f;
        public float NormalizedProgress => GetNormalizedProgress(_current);
        public string LastMessage { get; private set; } = string.Empty;
        public bool AwaitingWorldDeployment => _awaitingWorldDeployment &&
                                               _current != null &&
                                               _current.RemainingTime <= 0f;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            TryBindResourceService();
            BindEvents();
        }

        private void Start()
        {
            if (_context == null)
            {
                TryBindResourceService();
            }
        }

        private void OnDisable()
        {
            UnbindEvents();
            if (Application.isPlaying)
            {
                CancelAllAndRefund();
            }
        }

        private void OnDestroy()
        {
            UnbindEvents();
            if (Application.isPlaying)
            {
                CancelAllAndRefund();
            }
        }

        private void OnValidate()
        {
            CacheComponents();
            _capacity = Mathf.Max(1, _capacity);
            _spawnRetryInterval = Mathf.Max(0.05f, _spawnRetryInterval);
        }

        private void Update()
        {
            TickProduction(Time.deltaTime);
        }

        public void Configure(
            ProductionRecipe[] recipes,
            UnitSpawner unitSpawner,
            RallyPointController rallyPointController,
            VitalsComponent producerVitals,
            int capacity = DefaultCapacity,
            float spawnRetryInterval = 0.5f,
            ResourceService resourceService = null)
        {
            UnbindEvents();
            _recipes = recipes != null
                ? (ProductionRecipe[])recipes.Clone()
                : Array.Empty<ProductionRecipe>();
            _unitSpawner = unitSpawner != null
                ? unitSpawner
                : GetComponent<UnitSpawner>();
            _rallyPointController = rallyPointController != null
                ? rallyPointController
                : GetComponent<RallyPointController>();
            _producerVitals = producerVitals != null
                ? producerVitals
                : GetComponent<VitalsComponent>();
            _capacity = Mathf.Max(1, capacity);
            _spawnRetryInterval = Mathf.Max(0.05f, spawnRetryInterval);
            _context = resourceService != null
                ? new ProductionContext(
                    ResolveProducerFaction(),
                    resourceService,
                    new PlayerProductionUnlockPolicy())
                : null;
            if (isActiveAndEnabled)
            {
                TryBindResourceService();
                BindEvents();
            }
        }

        public void SetProductionContext(ProductionContext context)
        {
            _context = context;
        }

        public bool TryEnqueue(ProductionRecipe recipe)
        {
            return TryEnqueue(recipe, out ProductionRejectionReason rejection) &&
                   rejection == ProductionRejectionReason.None;
        }

        public bool TryEnqueue(
            ProductionRecipe recipe,
            out ProductionRejectionReason rejection)
        {
            rejection = ProductionRejectionReason.None;
            if (recipe == null || !recipe.IsValid)
            {
                return Reject(
                    recipe,
                    ProductionRejectionReason.InvalidRecipe,
                    "The production recipe is invalid.",
                    out rejection);
            }

            if (!ContainsRecipe(recipe))
            {
                return Reject(
                    recipe,
                    ProductionRejectionReason.RecipeNotAvailable,
                    "This producer does not offer that recipe.",
                    out rejection);
            }

            if (_context != null && _context.UnlockPolicy != null &&
                !_context.UnlockPolicy.CanProduce(recipe))
            {
                return Reject(
                    recipe,
                    ProductionRejectionReason.RecipeNotAvailable,
                    "This production recipe is still locked.",
                    out rejection);
            }

            if (TotalCount >= _capacity)
            {
                return Reject(
                    recipe,
                    ProductionRejectionReason.QueueFull,
                    "Production queue is full.",
                    out rejection);
            }

            if ((_context == null || !_context.IsValid) && !TryBindResourceService())
            {
                return Reject(
                    recipe,
                    ProductionRejectionReason.ResourceServiceUnavailable,
                    "ResourceService is unavailable.",
                    out rejection);
            }

            if (!_context.Account.TrySpend(recipe.Cost))
            {
                ResourceSnapshot snapshot = _context.Account.Snapshot;
                bool influenceBlocked = snapshot.InfluenceAvailable < recipe.Cost.Influence;
                return Reject(
                    recipe,
                    influenceBlocked
                        ? ProductionRejectionReason.InfluenceCapReached
                        : ProductionRejectionReason.InsufficientResources,
                    influenceBlocked
                        ? "Influence cap reached."
                        : "Insufficient resources.",
                    out rejection);
            }

            ProductionOrder order = new ProductionOrder(
                recipe,
                GetEffectiveProductionTime(recipe));
            if (_current == null)
            {
                _current = order;
                _spawnRetryRemaining = 0f;
            }
            else
            {
                _waiting.Enqueue(order);
            }

            LastMessage = recipe.DisplayName + " queued.";
            QueueChanged?.Invoke(this);
            PublishProgress();
            return true;
        }

        public void TickProduction(float deltaTime)
        {
            if (_current == null)
            {
                StartNext();
                return;
            }

            float scaledDelta = Mathf.Max(0f, deltaTime);
            if (_current.RemainingTime > 0f)
            {
                if (scaledDelta <= 0f)
                {
                    return;
                }

                _current.RemainingTime = Mathf.Max(
                    0f,
                    _current.RemainingTime - scaledDelta);
                PublishProgress();
                if (_current.RemainingTime > 0f)
                {
                    return;
                }
            }

            if (_spawnRetryRemaining > 0f)
            {
                _spawnRetryRemaining = Mathf.Max(
                    0f,
                    _spawnRetryRemaining - scaledDelta);
                return;
            }

            TryCompleteCurrent();
        }

        public int CancelAllAndRefund()
        {
            int refunded = 0;
            if (_current != null)
            {
                if (RefundOrder(_current))
                {
                    refunded++;
                }

                _current = null;
            }

            while (_waiting.Count > 0)
            {
                if (RefundOrder(_waiting.Dequeue()))
                {
                    refunded++;
                }
            }

            _spawnRetryRemaining = 0f;
            _awaitingWorldDeployment = false;
            if (refunded > 0)
            {
                LastMessage = "Production cancelled; " + refunded + " order(s) refunded.";
                QueueChanged?.Invoke(this);
                PublishProgress();
            }

            return refunded;
        }

        public int CaptureOrders(List<ProductionOrderSnapshot> destination)
        {
            if (destination == null)
            {
                return 0;
            }

            destination.Clear();
            if (_current != null && _current.Recipe != null)
            {
                destination.Add(new ProductionOrderSnapshot(
                    _current.Recipe,
                    _current.RemainingTime,
                    GetNormalizedProgress(_current)));
            }

            foreach (ProductionOrder order in _waiting)
            {
                if (order != null && order.Recipe != null)
                {
                    destination.Add(new ProductionOrderSnapshot(
                        order.Recipe,
                        order.RemainingTime,
                        GetNormalizedProgress(order)));
                }
            }

            return destination.Count;
        }

        public bool RestoreOrders(
            IReadOnlyList<ProductionOrderSnapshot> snapshots,
            out string error)
        {
            _current = null;
            _waiting.Clear();
            _spawnRetryRemaining = 0f;
            _awaitingWorldDeployment = false;
            LastMessage = string.Empty;

            int count = snapshots != null ? snapshots.Count : 0;
            if (count > _capacity)
            {
                error = "Saved production queue exceeds this producer's capacity.";
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                ProductionOrderSnapshot snapshot = snapshots[i];
                if (snapshot.Recipe == null || !ContainsRecipe(snapshot.Recipe))
                {
                    _current = null;
                    _waiting.Clear();
                    error = "Saved production queue contains an unavailable recipe.";
                    return false;
                }

                ProductionOrder order = new ProductionOrder(
                    snapshot.Recipe,
                    GetEffectiveProductionTime(snapshot.Recipe))
                {
                    RemainingTime = Mathf.Clamp(
                        snapshot.RemainingTime,
                        0f,
                        GetEffectiveProductionTime(snapshot.Recipe))
                };
                if (_current == null)
                {
                    _current = order;
                }
                else
                {
                    _waiting.Enqueue(order);
                }
            }

            _awaitingWorldDeployment = _current != null &&
                                       _current.RemainingTime <= 0f &&
                                       _current.Recipe.SpawnPolicy ==
                                       ProductionSpawnPolicy.WorldTarget;
            LastMessage = count > 0 ? "Production queue restored." : string.Empty;
            QueueChanged?.Invoke(this);
            PublishProgress();
            error = string.Empty;
            return true;
        }

        private void TryCompleteCurrent()
        {
            if (_current == null || _unitSpawner == null || _context?.Account == null)
            {
                LastMessage = "Production is waiting for its spawn dependencies.";
                _spawnRetryRemaining = _spawnRetryInterval;
                QueueChanged?.Invoke(this);
                return;
            }

            ProductionRecipe completedRecipe = _current.Recipe;
            if (completedRecipe.SpawnPolicy == ProductionSpawnPolicy.WorldTarget)
            {
                if (!_awaitingWorldDeployment)
                {
                    _awaitingWorldDeployment = true;
                    LastMessage = completedRecipe.DisplayName +
                                  " is ready for visible deployment.";
                    QueueChanged?.Invoke(this);
                }

                return;
            }

            if (!_unitSpawner.TrySpawnBatch(
                    completedRecipe,
                    _context.Account,
                    out List<EntityIdentity> spawnedIdentities,
                    out string error))
            {
                LastMessage = string.IsNullOrWhiteSpace(error)
                    ? "SpawnPoint is temporarily blocked."
                    : error;
                _spawnRetryRemaining = _spawnRetryInterval;
                QueueChanged?.Invoke(this);
                return;
            }

            FinishProduced(completedRecipe, spawnedIdentities);
        }

        public bool TryDeployCurrent(Vector3 worldPosition, out string error)
        {
            error = string.Empty;
            if (!AwaitingWorldDeployment || _unitSpawner == null || _context?.Account == null)
            {
                error = "No completed production batch is awaiting deployment.";
                return false;
            }

            ProductionRecipe completedRecipe = _current.Recipe;
            if (!_unitSpawner.TrySpawnBatchAt(
                    completedRecipe,
                    _context.Account,
                    worldPosition,
                    out List<EntityIdentity> spawnedIdentities,
                    out error))
            {
                LastMessage = error;
                QueueChanged?.Invoke(this);
                return false;
            }

            FinishProduced(completedRecipe, spawnedIdentities);
            return true;
        }

        private void FinishProduced(
            ProductionRecipe completedRecipe,
            List<EntityIdentity> spawnedIdentities)
        {
            _current = null;
            _spawnRetryRemaining = 0f;
            _awaitingWorldDeployment = false;
            ProductionResourceConverter converter =
                GetComponent<ProductionResourceConverter>();
            converter?.GrantForCompletedProduction(completedRecipe);
            LastMessage = completedRecipe.DisplayName + " produced x" +
                          spawnedIdentities.Count + ".";
            for (int i = 0; i < spawnedIdentities.Count; i++)
            {
                EntityIdentity spawnedIdentity = spawnedIdentities[i];
                _rallyPointController?.TryDispatchSpawnedUnit(spawnedIdentity);
                UnitProduced?.Invoke(this, completedRecipe, spawnedIdentity);
            }
            QueueChanged?.Invoke(this);
            StartNext();
        }

        private void StartNext()
        {
            if (_current != null || _waiting.Count == 0)
            {
                return;
            }

            _current = _waiting.Dequeue();
            _spawnRetryRemaining = 0f;
            _awaitingWorldDeployment = false;
            QueueChanged?.Invoke(this);
            PublishProgress();
        }

        private bool ContainsRecipe(ProductionRecipe recipe)
        {
            for (int i = 0; i < _recipes.Length; i++)
            {
                if (_recipes[i] == recipe)
                {
                    return true;
                }
            }

            return false;
        }

        private bool RefundOrder(ProductionOrder order)
        {
            return order != null && order.Recipe != null &&
                   _context?.Account != null &&
                   _context.Account.Refund(order.Recipe.Cost);
        }

        private bool Reject(
            ProductionRecipe recipe,
            ProductionRejectionReason reason,
            string message,
            out ProductionRejectionReason rejection)
        {
            rejection = reason;
            LastMessage = message;
            ProductionRejected?.Invoke(this, recipe, reason, message);
            QueueChanged?.Invoke(this);
            return false;
        }

        private void PublishProgress()
        {
            ProgressChanged?.Invoke(this, new ProductionOrderSnapshot(
                CurrentRecipe,
                CurrentRemainingTime,
                NormalizedProgress));
        }

        public float GetEffectiveProductionTime(ProductionRecipe recipe)
        {
            float definitionSeconds = recipe != null
                ? recipe.ProductionTime
                : 0.05f;
            if (AppRoot.TryGetInstance(out AppRoot appRoot) &&
                appRoot.Services != null && appRoot.Services.MatchSession != null)
            {
                return appRoot.Services.MatchSession.ResolveUnitProductionSeconds(
                    definitionSeconds);
            }

            return Mathf.Max(0.05f, definitionSeconds);
        }

        private static float GetNormalizedProgress(ProductionOrder order)
        {
            if (order == null || order.Recipe == null || order.TotalTime <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                1f - order.RemainingTime / order.TotalTime);
        }

        private bool TryBindResourceService()
        {
            if (_context != null && _context.IsValid)
            {
                return true;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            _context = new ProductionContext(
                FactionId.Friendly,
                appRoot.Services.ResourceService,
                new PlayerProductionUnlockPolicy());
            return _context.IsValid;
        }

        private FactionId ResolveProducerFaction()
        {
            FactionMember faction = GetComponent<FactionMember>();
            return faction != null ? faction.Faction : FactionId.Friendly;
        }

        private void HandleProducerDied(VitalsComponent vitals)
        {
            CancelAllAndRefund();
        }

        private void CacheComponents()
        {
            if (_unitSpawner == null)
            {
                _unitSpawner = GetComponent<UnitSpawner>();
            }

            if (_rallyPointController == null)
            {
                _rallyPointController = GetComponent<RallyPointController>();
            }

            if (_producerVitals == null)
            {
                _producerVitals = GetComponent<VitalsComponent>();
            }
        }

        private void BindEvents()
        {
            if (_eventsBound)
            {
                return;
            }

            if (_producerVitals != null)
            {
                _producerVitals.Died += HandleProducerDied;
            }

            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
            {
                return;
            }

            if (_producerVitals != null)
            {
                _producerVitals.Died -= HandleProducerDied;
            }

            _eventsBound = false;
        }
    }
}
