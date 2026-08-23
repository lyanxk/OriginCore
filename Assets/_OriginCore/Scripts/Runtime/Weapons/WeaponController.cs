using System;
using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.FPS;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInventory), typeof(EntityIdentity))]
    public sealed class WeaponController : MonoBehaviour
    {
        private const int RaycastCapacity = 32;
        private const int MeleeCapacity = 32;

        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private HybridPawnMotor _motor;
        [SerializeField] private FpsMovementController _fpsMovement;
        [SerializeField] private ThrowableController _throwables;
        [SerializeField] private WeaponActionController _weaponActions;
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private Transform _worldWeaponAnchor;
        [SerializeField] private Transform _firstPersonWeaponAnchor;
        [SerializeField] private LayerMask _hitMask = ~0;
        [Min(0.1f), SerializeField] private float _projectileSpeed = 40f;
        [Min(0.1f), SerializeField] private float _projectileLifetime = 5f;
        [Min(0), SerializeField] private int _actWeaponSwitchPriority = 10;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[RaycastCapacity];
        private readonly Collider[] _meleeHits = new Collider[MeleeCapacity];
        private readonly HashSet<EntityIdentity> _meleeTargets =
            new HashSet<EntityIdentity>();
        private InputRouter _inputRouter;
        private float _fireCooldownRemaining;
        private float _reloadRemaining;
        private WeaponDefinition _reloadingWeapon;
        private GameObject _presentationInstance;
        private WeaponDefinition _presentedWeapon;
        private GameMode _presentedMode = GameMode.RTS;

        public event Action<WeaponDefinition> ShotFired;
        public event Action<WeaponDefinition> ReloadStarted;
        public event Action<WeaponDefinition> ReloadCompleted;

        public WeaponInventory Inventory => _inventory;
        public float ReloadRemaining => Mathf.Max(0f, _reloadRemaining);
        public bool IsReloading => _reloadingWeapon != null && _reloadRemaining > 0f;

        private void OnEnable()
        {
            CacheComponents();
            BindInventory();
            TryBindInput();
            RefreshPresentation();
        }

        private void Start()
        {
            TryBindInput();
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
                _inputRouter = null;
            }

            if (_inventory != null)
            {
                _inventory.ActiveWeaponChanged -= HandleActiveWeaponChanged;
            }

            DestroyPresentation();
            CancelReload();
        }

        private void OnValidate()
        {
            CacheComponents();
            _projectileSpeed = Mathf.Max(0.1f, _projectileSpeed);
            _projectileLifetime = Mathf.Max(0.1f, _projectileLifetime);
            _actWeaponSwitchPriority = Mathf.Max(0, _actWeaponSwitchPriority);
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            _fireCooldownRemaining = Mathf.Max(0f, _fireCooldownRemaining - deltaTime);
            TickReload(deltaTime);

            if (snapshot.GameplaySuppressed || !IsPossessedPawn())
            {
                return;
            }

            if (snapshot.ActiveMap == GameplayInputMap.ACT)
            {
                _throwables?.Unequip();
                _inventory.SetMode(GameMode.ACT);
                if (snapshot.ACT.WeaponPrevious.WasPressedThisFrame)
                {
                    if (_weaponActions == null ||
                        _weaponActions.CanAuthorizeExternalAction(
                            _actWeaponSwitchPriority,
                            true))
                    {
                        _inventory.CycleAct(-1);
                    }
                }
                if (snapshot.ACT.WeaponNext.WasPressedThisFrame)
                {
                    if (_weaponActions == null ||
                        _weaponActions.CanAuthorizeExternalAction(
                            _actWeaponSwitchPriority,
                            true))
                    {
                        _inventory.CycleAct(1);
                    }
                }
                if (snapshot.ACT.Primary.IsPressed)
                {
                    WeaponDefinition active = _inventory.ActiveWeapon;
                    if (active == null || active.ActActionSet == null)
                    {
                        TryFire(false, snapshot.ACT.Move);
                    }
                }
            }
            else if (snapshot.ActiveMap == GameplayInputMap.FPS)
            {
                _inventory.SetMode(GameMode.FPS);
                if (snapshot.FPS.Slot1.WasPressedThisFrame)
                {
                    _throwables?.Unequip();
                    _inventory.SelectFpsSlot(WeaponSlotMask.Primary);
                }
                if (snapshot.FPS.Slot2.WasPressedThisFrame)
                {
                    _throwables?.Unequip();
                    _inventory.SelectFpsSlot(WeaponSlotMask.Secondary);
                }
                if (snapshot.FPS.Slot3.WasPressedThisFrame)
                {
                    _throwables?.Unequip();
                    _inventory.SelectFpsSlot(WeaponSlotMask.Melee);
                }
                if ((_throwables == null || !_throwables.IsEquipped) &&
                    snapshot.FPS.Primary.IsPressed)
                {
                    TryFire(snapshot.FPS.Aim.IsPressed, snapshot.FPS.Move);
                }
            }
            else
            {
                _throwables?.Unequip();
                _inventory.SetMode(GameMode.RTS);
            }
        }

        private void TryFire(bool aiming, Vector2 movementInput)
        {
            WeaponDefinition weapon = _inventory.ActiveWeapon;
            WeaponRuntimeState state = _inventory.ActiveState;
            if (weapon == null || state == null || _fireCooldownRemaining > 0f ||
                IsReloading)
            {
                return;
            }

            if (!state.TryConsumeRound())
            {
                StartReload(weapon, state);
                return;
            }

            _fireCooldownRemaining = weapon.SecondsPerShot;
            Camera camera = ResolveAimCamera();
            Vector3 origin = camera != null
                ? camera.transform.position
                : transform.position + Vector3.up;
            Vector3 direction = camera != null ? camera.transform.forward : transform.forward;
            if (weapon.FireType != WeaponFireType.Melee)
            {
                direction = ApplySpread(
                    direction,
                    camera,
                    ResolveSpread(weapon, aiming, movementInput));
            }

            switch (weapon.FireType)
            {
                case WeaponFireType.Melee:
                    FireMelee(weapon, direction);
                    break;
                case WeaponFireType.Projectile:
                case WeaponFireType.Thrown:
                    FireProjectile(weapon, origin, direction);
                    break;
                default:
                    FireHitscan(weapon, origin, direction);
                    break;
            }

            _inventory.NotifyRuntimeStateChanged();
            ShotFired?.Invoke(weapon);
            if (state.MagazineAmmo == 0)
            {
                StartReload(weapon, state);
            }
        }

        private void FireHitscan(
            WeaponDefinition weapon,
            Vector3 origin,
            Vector3 direction)
        {
            int count = Physics.RaycastNonAlloc(
                origin,
                direction,
                _raycastHits,
                weapon.Range,
                _hitMask,
                QueryTriggerInteraction.Collide);
            RaycastHit nearest = default(RaycastHit);
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _raycastHits[i];
                if (candidate.collider == null ||
                    candidate.collider.transform.IsChildOf(transform) ||
                    candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearest = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }

            if (!found)
            {
                return;
            }

            DamageReceiver receiver = nearest.collider.GetComponentInParent<DamageReceiver>();
            EntityIdentity target = receiver != null
                ? receiver.GetComponent<EntityIdentity>()
                : null;
            if (receiver != null && IsHostile(target))
            {
                receiver.TryReceiveDamage(BuildDamage(weapon, nearest.point), out _);
            }
        }

        private void FireMelee(WeaponDefinition weapon, Vector3 aimDirection)
        {
            Vector3 forward = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = transform.forward;
            }
            forward.Normalize();
            float radius = Mathf.Max(0.5f, weapon.Range * 0.5f);
            Vector3 center = transform.position + Vector3.up + forward * radius;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _meleeHits,
                _hitMask,
                QueryTriggerInteraction.Collide);
            _meleeTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider collider = _meleeHits[i];
                DamageReceiver receiver = collider != null
                    ? collider.GetComponentInParent<DamageReceiver>()
                    : null;
                EntityIdentity target = receiver != null
                    ? receiver.GetComponent<EntityIdentity>()
                    : null;
                if (target == null || target == _identity || !_meleeTargets.Add(target) ||
                    !IsHostile(target))
                {
                    continue;
                }

                Vector3 toTarget = Vector3.ProjectOnPlane(
                    target.transform.position - transform.position,
                    Vector3.up);
                if (toTarget.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(forward, toTarget.normalized) < 0f)
                {
                    continue;
                }

                receiver.TryReceiveDamage(
                    BuildDamage(weapon, target.transform.position),
                    out _);
            }
        }

        private void FireProjectile(
            WeaponDefinition weapon,
            Vector3 origin,
            Vector3 direction)
        {
            if (weapon.ProjectilePrefab == null)
            {
                FireHitscan(weapon, origin, direction);
                return;
            }

            GameObject instance = Instantiate(
                weapon.ProjectilePrefab,
                origin + direction * 0.25f,
                Quaternion.LookRotation(direction));
            WeaponProjectile projectile = instance.GetComponent<WeaponProjectile>();
            if (projectile == null)
            {
                projectile = instance.AddComponent<WeaponProjectile>();
            }

            projectile.Launch(
                _identity,
                _faction != null ? _faction.Faction : FactionId.Neutral,
                weapon,
                direction,
                _projectileSpeed,
                _projectileLifetime,
                _hitMask);
        }

        private DamageInfo BuildDamage(WeaponDefinition weapon, Vector3 point)
        {
            return new DamageInfo(
                weapon.Damage,
                _identity,
                _faction != null ? _faction.Faction : FactionId.Neutral,
                weapon.DamageType,
                weapon.DamageFlags,
                point,
                true,
                string.Empty,
                weapon.ContentId);
        }

        private bool IsHostile(EntityIdentity target)
        {
            if (target == null || !target.TryGetComponent(out FactionMember targetFaction) ||
                _faction == null || !AppRoot.TryGetInstance(out AppRoot root) ||
                root.Services == null)
            {
                return false;
            }

            return root.Services.FactionRelations.GetRelation(
                       _faction.Faction,
                       targetFaction.Faction) == FactionRelation.Hostile;
        }

        private float ResolveSpread(
            WeaponDefinition weapon,
            bool aiming,
            Vector2 movementInput)
        {
            float spread = aiming ? weapon.AdsSpreadDegrees : weapon.HipSpreadDegrees;
            if (!aiming && movementInput.sqrMagnitude > 0.01f)
            {
                spread += 3f;
            }
            if (_motor != null && !_motor.IsGrounded)
            {
                spread += 5f;
            }
            return spread;
        }

        private static Vector3 ApplySpread(
            Vector3 direction,
            Camera camera,
            float spreadDegrees)
        {
            if (spreadDegrees <= 0f)
            {
                return direction.normalized;
            }

            Vector2 random = UnityEngine.Random.insideUnitCircle *
                             Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;
            return (direction + right * random.x + up * random.y).normalized;
        }

        private void StartReload(WeaponDefinition weapon, WeaponRuntimeState state)
        {
            if (weapon == null || state == null || weapon.FireType == WeaponFireType.Melee ||
                state.ReserveAmmo <= 0 || state.MagazineAmmo >= weapon.MagazineSize)
            {
                return;
            }

            _reloadingWeapon = weapon;
            _reloadRemaining = weapon.ReloadSeconds;
            ReloadStarted?.Invoke(weapon);
            if (_reloadRemaining <= 0f)
            {
                TickReload(0f);
            }
        }

        private void TickReload(float deltaTime)
        {
            if (_reloadingWeapon == null)
            {
                return;
            }

            _reloadRemaining = Mathf.Max(0f, _reloadRemaining - deltaTime);
            if (_reloadRemaining > 0f)
            {
                return;
            }

            WeaponDefinition completed = _reloadingWeapon;
            WeaponRuntimeState state = _inventory.FindState(completed);
            state?.CompleteReload();
            _reloadingWeapon = null;
            _inventory.NotifyRuntimeStateChanged();
            ReloadCompleted?.Invoke(completed);
        }

        private void CancelReload()
        {
            _reloadingWeapon = null;
            _reloadRemaining = 0f;
        }

        private void HandleActiveWeaponChanged(
            WeaponDefinition previous,
            WeaponDefinition current)
        {
            CancelReload();
            _fireCooldownRemaining = 0f;
            if (_fpsMovement != null)
            {
                _fpsMovement.Aim.SetAdsFieldOfView(
                    current != null ? current.AdsFieldOfView : 0f);
            }
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (_inventory == null)
            {
                return;
            }

            WeaponDefinition weapon = _inventory.ActiveWeapon;
            GameMode mode = _inventory.Mode;
            if (_presentedWeapon == weapon && _presentedMode == mode)
            {
                return;
            }

            DestroyPresentation();
            _presentedWeapon = weapon;
            _presentedMode = mode;
            if (weapon == null || mode == GameMode.RTS)
            {
                return;
            }

            bool firstPerson = mode == GameMode.FPS && weapon.FirstPersonPrefab != null;
            GameObject prefab = firstPerson ? weapon.FirstPersonPrefab : weapon.WorldPrefab;
            if (prefab == null)
            {
                return;
            }

            Transform anchor = firstPerson
                ? EnsureAnchor(ref _firstPersonWeaponAnchor, "FirstPersonWeaponAnchor")
                : EnsureAnchor(ref _worldWeaponAnchor, "WorldWeaponAnchor");
            _presentationInstance = Instantiate(prefab, anchor, false);
            _presentationInstance.name = prefab.name + "_Runtime";
        }

        private Transform EnsureAnchor(ref Transform anchor, string anchorName)
        {
            if (anchor != null)
            {
                return anchor;
            }

            GameObject value = new GameObject(anchorName);
            value.transform.SetParent(transform, false);
            value.transform.localPosition = new Vector3(0.35f, 1.1f, 0.45f);
            anchor = value.transform;
            return anchor;
        }

        private void DestroyPresentation()
        {
            if (_presentationInstance != null)
            {
                Destroy(_presentationInstance);
                _presentationInstance = null;
            }

            _presentedWeapon = null;
            _presentedMode = GameMode.RTS;
        }

        private Camera ResolveAimCamera()
        {
            if (_aimCamera != null)
            {
                return _aimCamera;
            }

            if (AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                root.Services.CurrentSceneContext != null)
            {
                _aimCamera = root.Services.CurrentSceneContext.MainCamera;
            }

            return _aimCamera;
        }

        private bool IsPossessedPawn()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.PossessionService.CurrentPawn != null &&
                   root.Services.PossessionService.CurrentPawn.gameObject == gameObject;
        }

        private void TryBindInput()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.InputRouter == null)
            {
                return;
            }

            InputRouter next = root.Services.InputRouter;
            if (_inputRouter == next)
            {
                return;
            }

            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
            }

            _inputRouter = next;
            _inputRouter.SnapshotReady += HandleSnapshot;
        }

        private void BindInventory()
        {
            if (_inventory == null)
            {
                return;
            }

            _inventory.ActiveWeaponChanged -= HandleActiveWeaponChanged;
            _inventory.ActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        private void CacheComponents()
        {
            if (_inventory == null) _inventory = GetComponent<WeaponInventory>();
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_faction == null) _faction = GetComponent<FactionMember>();
            if (_motor == null) _motor = GetComponent<HybridPawnMotor>();
            if (_fpsMovement == null) _fpsMovement = GetComponent<FpsMovementController>();
            if (_throwables == null) _throwables = GetComponent<ThrowableController>();
            if (_weaponActions == null) _weaponActions = GetComponent<WeaponActionController>();
        }
    }
}
