using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.FPS
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonVisibility : MonoBehaviour
    {
        [SerializeField] private Renderer[] _hiddenRenderers = Array.Empty<Renderer>();

        private bool[] _originalEnabledStates = Array.Empty<bool>();

        public IReadOnlyList<Renderer> HiddenRenderers => _hiddenRenderers;
        public bool IsFirstPersonHidden { get; private set; }

        private void OnDisable()
        {
            SetFirstPersonActive(false);
        }

        public void Configure(Renderer[] hiddenRenderers)
        {
            SetFirstPersonActive(false);
            if (hiddenRenderers == null || hiddenRenderers.Length == 0)
            {
                _hiddenRenderers = Array.Empty<Renderer>();
                _originalEnabledStates = Array.Empty<bool>();
                return;
            }

            List<Renderer> unique = new List<Renderer>(hiddenRenderers.Length);
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                Renderer candidate = hiddenRenderers[i];
                if (candidate != null && !unique.Contains(candidate))
                {
                    unique.Add(candidate);
                }
            }

            _hiddenRenderers = unique.ToArray();
            _originalEnabledStates = new bool[_hiddenRenderers.Length];
        }

        public void SetFirstPersonActive(bool active)
        {
            if (active == IsFirstPersonHidden)
            {
                return;
            }

            if (active)
            {
                if (_originalEnabledStates.Length != _hiddenRenderers.Length)
                {
                    _originalEnabledStates = new bool[_hiddenRenderers.Length];
                }

                for (int i = 0; i < _hiddenRenderers.Length; i++)
                {
                    Renderer renderer = _hiddenRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    _originalEnabledStates[i] = renderer.enabled;
                    renderer.enabled = false;
                }

                IsFirstPersonHidden = true;
                return;
            }

            for (int i = 0; i < _hiddenRenderers.Length; i++)
            {
                Renderer renderer = _hiddenRenderers[i];
                if (renderer != null && i < _originalEnabledStates.Length)
                {
                    renderer.enabled = _originalEnabledStates[i];
                }
            }

            IsFirstPersonHidden = false;
        }
    }
}
