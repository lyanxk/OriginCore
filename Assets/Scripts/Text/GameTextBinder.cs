using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Content
{
    [DisallowMultipleComponent]
    public sealed class GameTextBinder : MonoBehaviour
    {
        [SerializeField] string textId;
        [SerializeField] bool applyOnEnable = true;

        void Awake()
        {
            Apply();
        }

        void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        public void Apply()
        {
            string text = GameText.GetText(textId, textId);

            TMP_Text tmpText = GetComponent<TMP_Text>();
            if (tmpText != null)
                tmpText.text = text;

            Text legacyText = GetComponent<Text>();
            if (legacyText != null)
                legacyText.text = text;
        }
    }
}
