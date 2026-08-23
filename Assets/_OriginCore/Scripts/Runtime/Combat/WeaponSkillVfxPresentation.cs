using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Gameplay;
using OriginCore.Weapons;
using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponActionController))]
    public sealed class WeaponSkillVfxPresentation : MonoBehaviour
    {
        private enum Recipe
        {
            None,
            TimeslotSlash,
            TimeslotReverseSlash,
            TimeslotCross,
            TimeslotHeavySlash,
            TimeslotSpin,
            TimeslotDash,
            TimeslotWave,
            TimeslotRift,
            TimeslotTeleportSlash,
            TimeslotLaunch,
            TimeslotSlam,
            SequenceSlash,
            SequenceReverseSlash,
            SequenceDoubleSpin,
            SequencePhantoms,
            SequenceVerticalSpin,
            SequenceSlam,
            SequenceThrow,
            SequenceOrbit,
            SequenceTripleVertical,
            SequenceHook,
            SequenceHeavyCleave,
            FinalWingVolley,
            FinalWingFocus,
            FinalWingRearBurst,
            FinalWingLaunch,
            FinalWingTimeslotFinisher,
            FinalWingSequenceFinisher
        }

        private enum Shape
        {
            Bezier,
            Arc,
            Polygon
        }

        private sealed class EffectLine
        {
            public GameObject GameObject;
            public LineRenderer Renderer;
            public Shape Shape;
            public Color Color;
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public Vector3 AxisA;
            public Vector3 AxisB;
            public float RadiusStart;
            public float RadiusEnd;
            public float StartAngle;
            public float Sweep;
            public float StartClock;
            public float Duration;
            public float Width;
            public float SpinDegrees;
            public int PointCount;
            public int Sides;
            public bool Loop;
            public bool Reverse;
            public bool Closing;
            public bool Ending;
            public float EndClock;
        }

        private static readonly Color TimeslotColor =
            new Color(0.16f, 0.78f, 1f, 1f);
        private static readonly Color TimeslotAccent =
            new Color(0.72f, 0.96f, 1f, 1f);
        private static readonly Color SequenceColor =
            new Color(0.82f, 0.2f, 1f, 1f);
        private static readonly Color SequenceAccent =
            new Color(1f, 0.55f, 0.96f, 1f);
        private static readonly Color WingColor =
            new Color(1f, 0.78f, 0.2f, 1f);
        private static readonly Color WingAccent =
            new Color(1f, 0.97f, 0.72f, 1f);

        [SerializeField] private WeaponActionController _actions;
        [SerializeField] private CombatHitboxResolver _hitboxResolver;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Material _effectMaterial;
        [Min(0.01f), SerializeField] private float _baseWidth = 0.075f;
        [Min(0.1f), SerializeField] private float _defaultReach = 3f;
        [Min(8), SerializeField] private int _curveSegments = 24;

        private static Material _fallbackMaterial;
        private readonly List<EffectLine> _lines = new List<EffectLine>();
        private float _clock;
        private string _activeActionId;

        public Material EffectMaterial => _effectMaterial;
        public int ActiveLineCount => _lines.Count;
        public int RecipeBuildCount { get; private set; }
        public string ActiveActionId => _activeActionId ?? string.Empty;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
            ClearAll();
            _activeActionId = null;
        }

        private void OnValidate()
        {
            _baseWidth = Mathf.Max(0.01f, _baseWidth);
            _defaultReach = Mathf.Max(0.1f, _defaultReach);
            _curveSegments = Mathf.Max(8, _curveSegments);
            CacheComponents();
        }

        private void Update()
        {
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (deltaTime <= 0f)
            {
                return;
            }

            _clock += deltaTime;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                EffectLine line = _lines[i];
                if (!UpdateLine(line))
                {
                    DestroyLine(line);
                    _lines.RemoveAt(i);
                }
            }
        }

        public void Configure(Material effectMaterial, Transform visualRoot = null)
        {
            _effectMaterial = effectMaterial;
            _visualRoot = visualRoot != null
                ? visualRoot
                : transform.Find("VisualRoot");
            CacheComponents();
        }

        public static bool SupportsAction(string actionId)
        {
            return ResolveRecipe(ContentIdUtility.Normalize(actionId)) != Recipe.None;
        }

        private void HandleActionStarted(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action)
        {
            if (!Application.isPlaying || action == null)
            {
                return;
            }

            BeginEndingAll(0.08f);
            _activeActionId = action.ActionId;
            Recipe recipe = ResolveRecipe(_activeActionId);
            if (recipe == Recipe.None)
            {
                return;
            }

            BuildRecipe(recipe, action, controller.CurrentIntent);
            RecipeBuildCount++;
        }

        private void HandleActionEnded(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponActionEndReason reason)
        {
            if (action == null || action.ActionId != _activeActionId)
            {
                return;
            }

            _activeActionId = null;
            BeginEndingAll(0.16f);
        }

        private void HandleHitApplied(
            CombatHitboxResolver resolver,
            WeaponActionHitResult result)
        {
            if (!Application.isPlaying || result.Action == null ||
                !SupportsAction(result.Action.ActionId) || result.Target == null)
            {
                return;
            }

            Vector3 center = ResolveTargetPoint(result.Target);
            Color color = ResolveColor(result.Action.ActionId, true);
            AddArc(
                center,
                transform.right,
                Vector3.up,
                0.12f,
                0.72f,
                0f,
                360f,
                0f,
                0.22f,
                _baseWidth * 0.75f,
                0f,
                false,
                false,
                color);
        }

        private void BuildRecipe(
            Recipe recipe,
            WeaponActionDefinition action,
            WeaponActionIntent intent)
        {
            Vector3 origin = ResolveOrigin();
            Vector3 target = intent.HasTarget
                ? ResolveTargetPoint(intent.Target)
                : origin + transform.forward * ResolveReach(action);
            Vector3 forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = transform.forward;
            }
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float reach = Mathf.Max(1.5f, Vector3.Distance(origin, target));
            Vector3 middle = origin + forward * Mathf.Min(reach * 0.55f, 2.2f);

            switch (recipe)
            {
                case Recipe.TimeslotSlash:
                    AddDiagonalSlash(middle, forward, right, false, 1f,
                        TimeslotColor, 0f);
                    break;
                case Recipe.TimeslotReverseSlash:
                    AddDiagonalSlash(middle, forward, right, true, 1f,
                        TimeslotColor, 0f);
                    break;
                case Recipe.TimeslotCross:
                    AddDashTrails(origin, target, right, TimeslotColor, 0f);
                    AddDashTrails(target, origin, right, TimeslotAccent, 0.2f);
                    AddDiagonalSlash(target, forward, right, false, 0.85f,
                        TimeslotAccent, 0.12f);
                    break;
                case Recipe.TimeslotHeavySlash:
                    AddDiagonalSlash(middle, forward, right, false, 1.65f,
                        TimeslotAccent, 0f);
                    AddDiagonalSlash(middle, forward, right, false, 1.15f,
                        TimeslotColor, 0.05f);
                    break;
                case Recipe.TimeslotSpin:
                    AddArc(origin, right, forward, 0.45f, 1.55f, 0f, 360f,
                        0f, 0.5f, _baseWidth, 360f, false, false,
                        TimeslotColor);
                    break;
                case Recipe.TimeslotDash:
                    AddDashTrails(origin, target, right, TimeslotColor, 0f);
                    AddBezier(origin - right * 0.7f, middle, target + right * 0.7f,
                        0f, 0.35f, _baseWidth * 0.7f, false, TimeslotAccent);
                    break;
                case Recipe.TimeslotWave:
                    BuildSwordWave(origin, forward, right, reach);
                    break;
                case Recipe.TimeslotRift:
                    BuildTemporalRift(target, forward, right);
                    break;
                case Recipe.TimeslotTeleportSlash:
                    BuildTemporalRift(target, forward, right);
                    AddDiagonalSlash(target, forward, right, false, 1.35f,
                        TimeslotAccent, 0.08f);
                    break;
                case Recipe.TimeslotLaunch:
                    AddBezier(origin - right * 0.7f, origin + Vector3.up * 1.8f,
                        middle + Vector3.up * 2.4f, 0f, 0.5f,
                        _baseWidth * 1.2f, false, TimeslotAccent);
                    AddArc(middle, right, Vector3.up, 0.35f, 1.3f, 180f,
                        180f, 0.05f, 0.42f, _baseWidth, 0f, false, false,
                        TimeslotColor);
                    break;
                case Recipe.TimeslotSlam:
                    BuildGroundSlam(origin, right, forward, TimeslotColor, 1f);
                    break;
                case Recipe.SequenceSlash:
                    AddScytheSlash(middle, forward, right, false, 1f, 0f);
                    break;
                case Recipe.SequenceReverseSlash:
                    AddScytheSlash(middle, forward, right, true, 1f, 0f);
                    break;
                case Recipe.SequenceDoubleSpin:
                    AddArc(origin, right, forward, 0.45f, 1.8f, 0f, 720f,
                        0f, 0.7f, _baseWidth * 1.2f, 720f, false, false,
                        SequenceColor);
                    AddArc(origin + Vector3.up * 0.2f, right, forward, 0.5f,
                        1.5f, 180f, -720f, 0.08f, 0.65f, _baseWidth * 0.7f,
                        -720f, false, true, SequenceAccent);
                    break;
                case Recipe.SequencePhantoms:
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * 72f;
                        Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * right;
                        Vector3 phantomCenter = origin + radial * 1.15f;
                        AddScytheSlash(phantomCenter, forward, radial, (i & 1) != 0,
                            0.8f, i * 0.06f);
                    }
                    break;
                case Recipe.SequenceVerticalSpin:
                    AddArc(middle, forward, Vector3.up, 0.4f, 1.5f, 0f, 1080f,
                        0f, 0.75f, _baseWidth * 1.15f, 1080f, false, false,
                        SequenceColor);
                    break;
                case Recipe.SequenceSlam:
                    BuildGroundSlam(origin, right, forward, SequenceColor, 1.45f);
                    break;
                case Recipe.SequenceThrow:
                    BuildScytheThrow(origin, target, right);
                    break;
                case Recipe.SequenceOrbit:
                    BuildPersistentScythe(origin + forward * 3f, right, forward);
                    break;
                case Recipe.SequenceTripleVertical:
                    for (int i = 0; i < 3; i++)
                    {
                        AddArc(middle, forward, Vector3.up, 0.4f,
                            1.3f + i * 0.25f, i * 40f, 360f,
                            i * 0.12f, 0.45f, _baseWidth, 360f,
                            false, false, i == 2 ? SequenceAccent : SequenceColor);
                    }
                    break;
                case Recipe.SequenceHook:
                    AddArc(target, right, forward, 0.2f, 1.5f, 0f, 360f,
                        0f, 0.62f, _baseWidth * 1.15f, 720f, false, false,
                        SequenceAccent);
                    AddBezier(origin, target + right * 1.2f, target,
                        0f, 0.5f, _baseWidth, false, SequenceColor);
                    break;
                case Recipe.SequenceHeavyCleave:
                    AddScytheSlash(middle, forward, right, false, 1.75f, 0f);
                    AddBezier(origin, middle + Vector3.up * 1.1f, target,
                        0.04f, 0.5f, _baseWidth * 1.35f, false, SequenceAccent);
                    break;
                case Recipe.FinalWingVolley:
                    BuildWingVolley(origin, target, right, forward, false);
                    break;
                case Recipe.FinalWingFocus:
                    BuildWingVolley(origin, target, right, forward, true);
                    break;
                case Recipe.FinalWingRearBurst:
                    BuildRearWingBurst(origin, right, forward);
                    break;
                case Recipe.FinalWingLaunch:
                    BuildWingLaunch(origin, target, right);
                    break;
                case Recipe.FinalWingTimeslotFinisher:
                    BuildFrozenHexagon(
                        target,
                        right,
                        forward,
                        ResolveMaximumHitRadius(action));
                    break;
                case Recipe.FinalWingSequenceFinisher:
                    BuildOpposedOrbit(
                        target,
                        right,
                        forward,
                        ResolveMaximumHitRadius(action));
                    break;
            }
        }

        private void AddDiagonalSlash(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            bool reverse,
            float scale,
            Color color,
            float delay)
        {
            Vector3 start = center + Vector3.up * scale + right * (reverse ? scale : -scale);
            Vector3 end = center - Vector3.up * scale + right * (reverse ? -scale : scale);
            AddBezier(start, center + forward * (0.55f * scale), end,
                delay, 0.34f, _baseWidth * (1.2f * scale), false, color);
        }

        private void AddScytheSlash(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            bool reverse,
            float scale,
            float delay)
        {
            float start = reverse ? 20f : 160f;
            float sweep = reverse ? 190f : -190f;
            AddArc(center, right, Vector3.up, 0.35f * scale, 1.45f * scale,
                start, sweep, delay, 0.42f, _baseWidth * 1.35f,
                0f, false, reverse, SequenceColor);
            AddBezier(
                center - forward * 0.2f,
                center + forward * 0.8f + Vector3.up * 0.5f,
                center + right * (reverse ? -1f : 1f) * scale,
                delay + 0.04f,
                0.34f,
                _baseWidth * 0.75f,
                false,
                SequenceAccent);
        }

        private void AddDashTrails(
            Vector3 start,
            Vector3 end,
            Vector3 right,
            Color color,
            float delay)
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector3 offset = right * (i * 0.32f) + Vector3.up * (0.2f + Mathf.Abs(i) * 0.16f);
                AddBezier(start + offset, Vector3.Lerp(start, end, 0.5f) + offset,
                    end + offset, delay + Mathf.Abs(i) * 0.025f, 0.38f,
                    _baseWidth * (i == 0 ? 1f : 0.55f), false, color);
            }
        }

        private void BuildSwordWave(
            Vector3 origin,
            Vector3 forward,
            Vector3 right,
            float reach)
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(i * 22.5f, Vector3.up) * forward;
                Vector3 end = origin + direction * Mathf.Max(reach, 4f);
                AddBezier(origin + Vector3.up * 0.45f,
                    Vector3.Lerp(origin, end, 0.5f) + right * i * 0.3f,
                    end + Vector3.up * 0.45f, Mathf.Abs(i) * 0.03f,
                    0.58f, _baseWidth * (i == 0 ? 1.3f : 0.7f),
                    false, i == 0 ? TimeslotAccent : TimeslotColor);
            }
        }

        private void BuildTemporalRift(
            Vector3 target,
            Vector3 forward,
            Vector3 right)
        {
            Vector3 center = target + Vector3.up * 1.4f;
            AddArc(center, right, Vector3.up, 0.9f, 1.15f, 0f, 360f,
                0f, 0.9f, _baseWidth * 1.2f, 180f, true, false,
                TimeslotAccent);
            for (int i = 0; i < 3; i++)
            {
                AddArc(target + Vector3.up * (0.1f + i * 0.3f), right, forward,
                    1.8f - i * 0.25f, 0.2f, 0f, 360f, i * 0.08f,
                    0.55f, _baseWidth * 0.7f, -180f, false, true,
                    TimeslotColor);
            }
        }

        private void BuildGroundSlam(
            Vector3 origin,
            Vector3 right,
            Vector3 forward,
            Color color,
            float scale)
        {
            AddBezier(origin + Vector3.up * 2.2f, origin + Vector3.up * 0.8f,
                origin, 0f, 0.34f, _baseWidth * 1.5f, false, color);
            for (int i = 0; i < 3; i++)
            {
                AddArc(origin + Vector3.up * 0.05f, right, forward,
                    0.12f, scale * (1.4f + i * 0.65f), 0f, 360f,
                    0.18f + i * 0.06f, 0.5f, _baseWidth * (1f - i * 0.15f),
                    0f, false, false, color);
            }
        }

        private void BuildScytheThrow(
            Vector3 origin,
            Vector3 target,
            Vector3 right)
        {
            Vector3 control = Vector3.Lerp(origin, target, 0.5f) +
                              right * 1.35f + Vector3.up * 0.7f;
            AddBezier(origin, control, target, 0f, 0.48f,
                _baseWidth * 1.2f, false, SequenceColor);
            AddBezier(target, control - right * 2.7f, origin, 0.42f, 0.48f,
                _baseWidth * 1.2f, false, SequenceAccent);
            AddArc(target, right, Vector3.up, 0.25f, 0.8f, 0f, 720f,
                0.2f, 0.55f, _baseWidth, 720f, false, false, SequenceAccent);
        }

        private void BuildPersistentScythe(
            Vector3 center,
            Vector3 right,
            Vector3 forward)
        {
            AddArc(center, right, forward, 0.65f, 1.3f, 0f, 360f,
                0f, 0.7f, _baseWidth * 1.4f, 540f, true, false,
                SequenceColor);
            AddArc(center + Vector3.up * 0.2f, right, Vector3.up,
                0.25f, 0.85f, 0f, 270f, 0f, 0.55f,
                _baseWidth * 0.8f, -720f, true, true, SequenceAccent);
        }

        private void BuildWingVolley(
            Vector3 origin,
            Vector3 target,
            Vector3 right,
            Vector3 forward,
            bool sustained)
        {
            Vector3[] offsets =
            {
                -right * 0.85f + Vector3.up * 1.05f,
                -right * 1.05f + Vector3.up * 0.45f,
                -right * 0.9f - Vector3.up * 0.15f,
                right * 0.85f + Vector3.up * 1.05f,
                right * 1.05f + Vector3.up * 0.45f,
                right * 0.9f - Vector3.up * 0.15f
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 start = origin + offsets[i];
                Vector3 through = target + forward * 1.1f;
                float delay = i * 0.1f;
                AddBezier(start, Vector3.Lerp(start, target, 0.5f) +
                    offsets[i] * 0.35f, through, delay,
                    sustained ? 0.42f : 0.48f, _baseWidth,
                    sustained, i % 2 == 0 ? WingColor : WingAccent);
                if (!sustained)
                {
                    AddBezier(through, target - offsets[i] * 0.2f, start,
                        delay + 0.42f, 0.4f, _baseWidth * 0.65f,
                        false, WingAccent);
                }
            }
        }

        private void BuildRearWingBurst(
            Vector3 origin,
            Vector3 right,
            Vector3 forward)
        {
            Vector3 rear = -forward;
            for (int i = 0; i < 6; i++)
            {
                float side = (i - 2.5f) / 2.5f;
                Vector3 start = origin + right * side * 0.8f +
                                Vector3.up * (0.15f + (i % 3) * 0.42f);
                Vector3 end = start + rear * 5f + right * side * 1.4f;
                AddBezier(start, Vector3.Lerp(start, end, 0.5f) +
                    Vector3.up * 0.7f, end, i * 0.035f, 0.55f,
                    _baseWidth * 1.15f, false,
                    i % 2 == 0 ? WingColor : WingAccent);
            }
        }

        private void BuildWingLaunch(
            Vector3 origin,
            Vector3 target,
            Vector3 right)
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector3 start = origin + right * i * 0.7f - Vector3.up * 0.45f;
                Vector3 control = Vector3.Lerp(start, target, 0.5f) +
                                  Vector3.down * 0.8f + right * i * 0.45f;
                AddBezier(start, control, target + Vector3.up * 1.15f,
                    (i + 1) * 0.055f, 0.52f, _baseWidth * 1.25f,
                    false, i == 0 ? WingAccent : WingColor);
            }
        }

        private void BuildFrozenHexagon(
            Vector3 center,
            Vector3 right,
            Vector3 forward,
            float effectRadius)
        {
            center += Vector3.up * 0.08f;
            float outerRadius = Mathf.Max(3.6f, effectRadius);
            float polygonRadius = outerRadius * 0.88f;
            AddPolygon(center, right, forward, polygonRadius, 6, 0f, 1.8f,
                _baseWidth * 1.45f, true, 25f, TimeslotAccent);
            Vector3[] vertices = new Vector3[6];
            for (int i = 0; i < vertices.Length; i++)
            {
                float radians = i * Mathf.PI / 3f;
                vertices[i] = center + right * (Mathf.Cos(radians) * polygonRadius) +
                              forward * (Mathf.Sin(radians) * polygonRadius);
            }

            for (int i = 0; i < 6; i++)
            {
                AddBezier(vertices[i], center + Vector3.up * 0.75f,
                    vertices[(i + 3) % 6], 0.2f + i * 0.09f,
                    0.65f, _baseWidth, true,
                    i % 2 == 0 ? WingColor : TimeslotColor);
            }

            AddArc(center, right, forward, outerRadius * 0.08f, outerRadius, 0f, 360f,
                0f, 1.2f, _baseWidth * 0.75f, -180f, true, true,
                WingAccent);
        }

        private void BuildOpposedOrbit(
            Vector3 center,
            Vector3 right,
            Vector3 forward,
            float effectRadius)
        {
            float outerRadius = Mathf.Max(3.3f, effectRadius);
            for (int i = 0; i < 3; i++)
            {
                AddArc(center + Vector3.up * (0.15f + i * 0.22f), right, forward,
                    outerRadius * (0.14f + i * 0.08f),
                    outerRadius * (0.8f + i * 0.1f), i * 55f,
                    360f, i * 0.04f, 0.58f, _baseWidth * 1.15f,
                    720f + i * 180f, true, false, SequenceColor);
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * right;
                Vector3 start = center + radial * (outerRadius * 0.85f) +
                                Vector3.up * 0.5f;
                Vector3 end = center - radial * (outerRadius * 0.6f) +
                              Vector3.up * 0.5f;
                AddBezier(start, center + Vector3.up * 1.4f, end,
                    i * 0.05f, 0.55f, _baseWidth * 0.85f,
                    true, i % 2 == 0 ? WingColor : WingAccent);
            }
        }

        private static float ResolveMaximumHitRadius(WeaponActionDefinition action)
        {
            if (action == null || action.Hits == null)
            {
                return 0f;
            }

            float radius = 0f;
            WeaponHitDefinition[] hits = action.Hits;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] != null)
                {
                    radius = Mathf.Max(radius, hits[i].Radius);
                }
            }

            return radius;
        }

        private void AddBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float delay,
            float duration,
            float width,
            bool loop,
            Color color)
        {
            EffectLine line = CreateLine(Shape.Bezier, color, delay, duration,
                width, loop);
            line.A = start;
            line.B = control;
            line.C = end;
        }

        private void AddArc(
            Vector3 center,
            Vector3 axisA,
            Vector3 axisB,
            float radiusStart,
            float radiusEnd,
            float startAngle,
            float sweep,
            float delay,
            float duration,
            float width,
            float spinDegrees,
            bool loop,
            bool reverse,
            Color color)
        {
            EffectLine line = CreateLine(Shape.Arc, color, delay, duration,
                width, loop);
            line.A = center;
            line.AxisA = axisA.normalized;
            line.AxisB = axisB.normalized;
            line.RadiusStart = radiusStart;
            line.RadiusEnd = radiusEnd;
            line.StartAngle = startAngle;
            line.Sweep = sweep;
            line.SpinDegrees = spinDegrees;
            line.Reverse = reverse;
        }

        private void AddPolygon(
            Vector3 center,
            Vector3 axisA,
            Vector3 axisB,
            float radius,
            int sides,
            float delay,
            float duration,
            float width,
            bool loop,
            float spinDegrees,
            Color color)
        {
            EffectLine line = CreateLine(Shape.Polygon, color, delay, duration,
                width, loop);
            line.A = center;
            line.AxisA = axisA.normalized;
            line.AxisB = axisB.normalized;
            line.RadiusStart = radius;
            line.RadiusEnd = radius;
            line.Sides = Mathf.Max(3, sides);
            line.SpinDegrees = spinDegrees;
            line.Closing = true;
        }

        private EffectLine CreateLine(
            Shape shape,
            Color color,
            float delay,
            float duration,
            float width,
            bool loop)
        {
            GameObject lineObject = new GameObject("WeaponSkillVfx_Runtime");
            lineObject.layer = gameObject.layer;
            lineObject.transform.SetParent(transform, false);
            LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.loop = false;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 2;
            renderer.numCornerVertices = 2;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ResolveMaterial();

            EffectLine line = new EffectLine
            {
                GameObject = lineObject,
                Renderer = renderer,
                Shape = shape,
                Color = color,
                StartClock = _clock + Mathf.Max(0f, delay),
                Duration = Mathf.Max(0.05f, duration),
                Width = Mathf.Max(0.01f, width),
                PointCount = Mathf.Max(8, _curveSegments),
                Loop = loop
            };
            lineObject.SetActive(false);
            _lines.Add(line);
            return line;
        }

        private bool UpdateLine(EffectLine line)
        {
            if (line == null || line.Renderer == null)
            {
                return false;
            }

            if (_clock < line.StartClock)
            {
                return true;
            }

            float age = _clock - line.StartClock;
            if (line.Ending && _clock >= line.EndClock)
            {
                return false;
            }

            if (!line.Loop && age >= line.Duration && !line.Ending)
            {
                return false;
            }

            if (!line.GameObject.activeSelf)
            {
                line.GameObject.SetActive(true);
            }

            float cycle = line.Loop
                ? Mathf.Repeat(age / line.Duration, 1f)
                : Mathf.Clamp01(age / line.Duration);
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cycle * 2.2f));
            float alpha = line.Loop
                ? 0.45f + Mathf.Sin(cycle * Mathf.PI) * 0.55f
                : 1f - Mathf.SmoothStep(0f, 1f, cycle);
            if (line.Ending)
            {
                float fade = Mathf.InverseLerp(line.EndClock, line.EndClock - 0.16f, _clock);
                alpha *= Mathf.Clamp01(fade);
            }

            Color faded = new Color(line.Color.r, line.Color.g,
                line.Color.b, line.Color.a * alpha);
            line.Renderer.startColor = faded;
            line.Renderer.endColor = new Color(faded.r, faded.g, faded.b, 0f);
            line.Renderer.widthMultiplier = line.Width *
                                            Mathf.Lerp(1f, 0.25f, cycle);

            switch (line.Shape)
            {
                case Shape.Bezier:
                    UpdateBezier(line, reveal, cycle);
                    break;
                case Shape.Arc:
                    UpdateArc(line, reveal, cycle);
                    break;
                case Shape.Polygon:
                    UpdatePolygon(line, reveal, cycle);
                    break;
            }

            return true;
        }

        private static void UpdateBezier(EffectLine line, float reveal, float cycle)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(line.PointCount * reveal));
            line.Renderer.positionCount = count;
            float travel = line.Loop ? cycle : reveal;
            float head = Mathf.Clamp01(travel);
            float tail = Mathf.Max(0f, head - (line.Loop ? 0.34f : 1f));
            for (int i = 0; i < count; i++)
            {
                float local = count <= 1 ? 0f : i / (float)(count - 1);
                float t = Mathf.Lerp(tail, head, local);
                float inverse = 1f - t;
                Vector3 point = inverse * inverse * line.A +
                                2f * inverse * t * line.B +
                                t * t * line.C;
                line.Renderer.SetPosition(i, point);
            }
        }

        private static void UpdateArc(EffectLine line, float reveal, float cycle)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(line.PointCount * reveal));
            line.Renderer.positionCount = count;
            float radius = Mathf.Lerp(line.RadiusStart, line.RadiusEnd,
                line.Loop ? 0.5f + Mathf.Sin(cycle * Mathf.PI * 2f) * 0.5f : cycle);
            float spin = line.SpinDegrees * cycle * (line.Reverse ? -1f : 1f);
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : i / (float)(count - 1);
                float angle = (line.StartAngle + spin + line.Sweep * t * reveal) *
                              Mathf.Deg2Rad;
                Vector3 point = line.A + line.AxisA * (Mathf.Cos(angle) * radius) +
                                line.AxisB * (Mathf.Sin(angle) * radius);
                line.Renderer.SetPosition(i, point);
            }
        }

        private static void UpdatePolygon(EffectLine line, float reveal, float cycle)
        {
            int total = line.Sides + (line.Closing ? 1 : 0);
            int count = Mathf.Max(2, Mathf.CeilToInt(total * reveal));
            line.Renderer.positionCount = count;
            float spin = line.SpinDegrees * cycle * Mathf.Deg2Rad;
            for (int i = 0; i < count; i++)
            {
                int vertex = i % line.Sides;
                float angle = spin + vertex * Mathf.PI * 2f / line.Sides;
                Vector3 point = line.A +
                                line.AxisA * (Mathf.Cos(angle) * line.RadiusStart) +
                                line.AxisB * (Mathf.Sin(angle) * line.RadiusStart);
                line.Renderer.SetPosition(i, point);
            }
        }

        private void BeginEndingAll(float fadeSeconds)
        {
            float fade = Mathf.Max(0.05f, fadeSeconds);
            for (int i = 0; i < _lines.Count; i++)
            {
                _lines[i].Ending = true;
                _lines[i].EndClock = _clock + fade;
            }
        }

        private void ClearAll()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                DestroyLine(_lines[i]);
            }
            _lines.Clear();
        }

        private static void DestroyLine(EffectLine line)
        {
            if (line != null && line.GameObject != null)
            {
                Destroy(line.GameObject);
            }
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (_actions != null)
            {
                _actions.ActionStarted += HandleActionStarted;
                _actions.ActionEnded += HandleActionEnded;
            }
            if (_hitboxResolver != null)
            {
                _hitboxResolver.HitApplied += HandleHitApplied;
            }
        }

        private void UnbindEvents()
        {
            if (_actions != null)
            {
                _actions.ActionStarted -= HandleActionStarted;
                _actions.ActionEnded -= HandleActionEnded;
            }
            if (_hitboxResolver != null)
            {
                _hitboxResolver.HitApplied -= HandleHitApplied;
            }
        }

        private void CacheComponents()
        {
            if (_actions == null)
            {
                _actions = GetComponent<WeaponActionController>();
            }
            if (_hitboxResolver == null)
            {
                _hitboxResolver = GetComponent<CombatHitboxResolver>();
            }
            if (_visualRoot == null)
            {
                _visualRoot = transform.Find("VisualRoot");
            }
        }

        private Vector3 ResolveOrigin()
        {
            return _visualRoot != null
                ? _visualRoot.position + Vector3.up * 0.25f
                : transform.position + Vector3.up * 0.85f;
        }

        private float ResolveReach(WeaponActionDefinition action)
        {
            if (action != null && action.Motion != null &&
                action.Motion.Distance > 0.1f)
            {
                return action.Motion.Distance;
            }
            return _defaultReach;
        }

        private static Vector3 ResolveTargetPoint(EntityIdentity target)
        {
            Selectable selectable = target != null
                ? target.GetComponent<Selectable>()
                : null;
            return selectable != null
                ? selectable.GetSelectionWorldPoint()
                : target != null
                    ? target.transform.position + Vector3.up * 0.75f
                    : Vector3.zero;
        }

        private Material ResolveMaterial()
        {
            if (_effectMaterial != null)
            {
                return _effectMaterial;
            }
            if (_fallbackMaterial != null)
            {
                return _fallbackMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                return null;
            }

            _fallbackMaterial = new Material(shader)
            {
                name = "MAT_WeaponSkillVfx_RuntimeFallback",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            if (_fallbackMaterial.HasProperty("_Surface"))
            {
                _fallbackMaterial.SetFloat("_Surface", 1f);
                _fallbackMaterial.SetFloat("_ZWrite", 0f);
                _fallbackMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _fallbackMaterial.SetFloat("_DstBlend",
                    (float)BlendMode.OneMinusSrcAlpha);
                _fallbackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return _fallbackMaterial;
        }

        private static Color ResolveColor(string actionId, bool accent)
        {
            string id = ContentIdUtility.Normalize(actionId);
            if (id.StartsWith("action.y.timeslot.", StringComparison.Ordinal))
            {
                return accent ? TimeslotAccent : TimeslotColor;
            }
            if (id.StartsWith("action.y.sequence.", StringComparison.Ordinal))
            {
                return accent ? SequenceAccent : SequenceColor;
            }
            return accent ? WingAccent : WingColor;
        }

        private static Recipe ResolveRecipe(string actionId)
        {
            switch (actionId)
            {
                case "action.y.timeslot.ground-1":
                case "action.y.timeslot.air-1":
                    return Recipe.TimeslotSlash;
                case "action.y.timeslot.ground-2":
                case "action.y.timeslot.air-2":
                    return Recipe.TimeslotReverseSlash;
                case "action.y.timeslot.ground-3":
                    return Recipe.TimeslotCross;
                case "action.y.timeslot.ground-4":
                    return Recipe.TimeslotHeavySlash;
                case "action.y.timeslot.air-3":
                    return Recipe.TimeslotSpin;
                case "action.y.timeslot.dash":
                case "action.y.timeslot.lock-dash":
                case "action.y.timeslot.air-dash":
                case "action.y.timeslot.air-lock-dash":
                    return Recipe.TimeslotDash;
                case "action.y.timeslot.wave":
                    return Recipe.TimeslotWave;
                case "action.y.timeslot.rift":
                    return Recipe.TimeslotRift;
                case "action.y.timeslot.teleport-slash":
                    return Recipe.TimeslotTeleportSlash;
                case "action.y.timeslot.launch-short":
                case "action.y.timeslot.launch-long":
                    return Recipe.TimeslotLaunch;
                case "action.y.timeslot.air-slam":
                    return Recipe.TimeslotSlam;

                case "action.y.sequence.ground-1":
                case "action.y.sequence.air-1":
                    return Recipe.SequenceSlash;
                case "action.y.sequence.ground-2":
                case "action.y.sequence.air-2":
                    return Recipe.SequenceReverseSlash;
                case "action.y.sequence.ground-3":
                    return Recipe.SequenceDoubleSpin;
                case "action.y.sequence.ground-4":
                    return Recipe.SequencePhantoms;
                case "action.y.sequence.air-3":
                    return Recipe.SequenceVerticalSpin;
                case "action.y.sequence.air-4":
                case "action.y.sequence.ground-finisher":
                case "action.y.sequence.air-slam":
                    return Recipe.SequenceSlam;
                case "action.y.sequence.throw":
                case "action.y.sequence.lock-throw":
                    return Recipe.SequenceThrow;
                case "action.y.sequence.orbit":
                    return Recipe.SequenceOrbit;
                case "action.y.sequence.vertical-three":
                    return Recipe.SequenceTripleVertical;
                case "action.y.sequence.air-hook":
                    return Recipe.SequenceHook;
                case "action.y.sequence.air-cleave":
                    return Recipe.SequenceHeavyCleave;

                case "action.y.final-wing.volley":
                    return Recipe.FinalWingVolley;
                case "action.y.final-wing.focus":
                    return Recipe.FinalWingFocus;
                case "action.y.final-wing.rear-burst":
                    return Recipe.FinalWingRearBurst;
                case "action.y.final-wing.launch":
                    return Recipe.FinalWingLaunch;
                case "action.y.final-wing.timeslot-finisher":
                    return Recipe.FinalWingTimeslotFinisher;
                case "action.y.final-wing.sequence-finisher":
                    return Recipe.FinalWingSequenceFinisher;
                default:
                    return Recipe.None;
            }
        }
    }
}
