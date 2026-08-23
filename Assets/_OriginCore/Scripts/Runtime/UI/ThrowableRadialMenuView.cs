using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Weapons;
using TMPro;
using UnityEngine;

namespace OriginCore.UI
{
    [DisallowMultipleComponent]
    public sealed class ThrowableRadialMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _root;
        [SerializeField] private RectTransform _itemsRoot;
        [SerializeField] private TMP_Text _statusLabel;
        [Min(40f), SerializeField] private float _radius = 145f;
        [Min(8f), SerializeField] private float _fontSize = 18f;

        private readonly List<TMP_Text> _labels = new List<TMP_Text>(8);
        private ThrowableController _controller;
        private int _builtCount = -1;

        private void OnEnable()
        {
            ResolveController();
            Refresh();
        }

        private void OnDisable()
        {
            BindController(null);
            SetVisible(false);
            ClearEntries();
        }

        private void Update()
        {
            if (_controller == null)
            {
                ResolveController();
            }
            Refresh();
        }

        public void Configure(
            CanvasGroup root,
            RectTransform itemsRoot,
            TMP_Text statusLabel,
            float radius = 145f)
        {
            _root = root;
            _itemsRoot = itemsRoot;
            _statusLabel = statusLabel;
            _radius = Mathf.Max(40f, radius);
            Refresh();
        }

        private void ResolveController()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.MatchSession.ActiveHero == null)
            {
                BindController(null);
                return;
            }

            BindController(root.Services.MatchSession.ActiveHero
                .GetComponent<ThrowableController>());
        }

        private void BindController(ThrowableController controller)
        {
            if (_controller == controller)
            {
                return;
            }
            if (_controller != null)
            {
                _controller.Changed -= HandleChanged;
            }
            _controller = controller;
            if (_controller != null)
            {
                _controller.Changed += HandleChanged;
            }
            _builtCount = -1;
        }

        private void HandleChanged(ThrowableController controller)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool visible = _controller != null && _controller.IsWheelOpen;
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            ThrowableDefinition[] available = _controller.Available;
            if (_builtCount != available.Length)
            {
                BuildEntries(available);
            }

            for (int i = 0; i < _labels.Count; i++)
            {
                _labels[i].color = i == _controller.SelectedIndex
                    ? new Color(1f, 0.78f, 0.35f, 1f)
                    : Color.white;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = _controller.Selected != null
                    ? BuildCostLabel(_controller.Selected)
                    : "NO THROWABLES CONFIGURED";
            }
        }

        private void BuildEntries(ThrowableDefinition[] available)
        {
            ClearEntries();
            _builtCount = available.Length;
            if (_itemsRoot == null)
            {
                return;
            }

            for (int i = 0; i < available.Length; i++)
            {
                ThrowableDefinition definition = available[i];
                GameObject labelObject = new GameObject(
                    "Throwable_" + i,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.SetParent(_itemsRoot, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(150f, 58f);
                float angle = Mathf.PI * 2f * i / Mathf.Max(1, available.Length) +
                              Mathf.PI * 0.5f;
                rect.anchoredPosition = new Vector2(
                    Mathf.Cos(angle) * _radius,
                    Mathf.Sin(angle) * _radius);

                TMP_Text label = labelObject.GetComponent<TMP_Text>();
                label.fontSize = _fontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = definition != null
                    ? definition.DisplayName
                    : "EMPTY";
                _labels.Add(label);
            }
        }

        private static string BuildCostLabel(ThrowableDefinition definition)
        {
            ResourceCost cost = definition.UseCost;
            return definition.DisplayName.ToUpperInvariant() + "  " +
                   cost.Crystal + "C / " +
                   cost.CommanderResource + "R / " +
                   cost.Influence + "I";
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                if (_labels[i] != null)
                {
                    Destroy(_labels[i].gameObject);
                }
            }
            _labels.Clear();
        }

        private void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }
            _root.alpha = visible ? 1f : 0f;
            _root.interactable = false;
            _root.blocksRaycasts = false;
        }
    }
}
