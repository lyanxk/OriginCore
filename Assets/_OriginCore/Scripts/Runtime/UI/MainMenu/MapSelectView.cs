using System;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MapSelectView : MonoBehaviour
    {
        [SerializeField] private Button _systemTestButton;

        private bool _buttonBound;

        public event Action SystemTestSelected;

        private void Awake()
        {
            BindButton();
        }

        private void OnDestroy()
        {
            if (_buttonBound && _systemTestButton != null)
            {
                _systemTestButton.onClick.RemoveListener(SelectSystemTest);
            }
        }

        public void Configure(Button systemTestButton)
        {
            _systemTestButton = systemTestButton;
            if (Application.isPlaying)
            {
                BindButton();
            }
        }

        public void SelectSystemTest()
        {
            SystemTestSelected?.Invoke();
        }

        private void BindButton()
        {
            if (_buttonBound || _systemTestButton == null)
            {
                return;
            }

            _systemTestButton.onClick.AddListener(SelectSystemTest);
            _buttonBound = true;
        }
    }
}
