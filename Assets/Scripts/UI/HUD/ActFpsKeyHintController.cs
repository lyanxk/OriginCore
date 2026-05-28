using System;
using System.Text;
using Content;
using Core;
using TMPro;
using Unit.Combat.Hero;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace UI.HUD
{
    public sealed class ActFpsKeyHintController : MonoBehaviour
    {
        const float PaddingX = 14f;
        const float PaddingY = 10f;
        const float MaxWidth = 420f;

        [Header("Layout")]
        [SerializeField] Vector2 anchoredPosition = new Vector2(16f, -16f);
        [SerializeField] float fontSize = 20f;
        [SerializeField] float lineSpacing = -8f;
        [SerializeField] Color textColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.48f);
        [SerializeField] TMP_FontAsset chineseFontAsset;

        RectTransform _panel;
        Image _background;
        TMP_Text _text;
        CanvasGroup _canvasGroup;
        string _lastHint;
        bool _visible;

        readonly StringBuilder _builder = new StringBuilder(256);
        static TMP_FontAsset s_runtimeChineseFontAsset;

        const string BundledChineseFontResourcePath = "Fonts/NotoSansSC-VF";

        void Awake()
        {
            EnsureView();
            SetVisible(false);
        }

        void OnEnable()
        {
            EnsureView();
            Refresh();
        }

        void Update()
        {
            Refresh();
        }

        void Refresh()
        {
            EnsureView();

            string hint = BuildHintText();
            bool hasHint = !string.IsNullOrWhiteSpace(hint);
            SetVisible(hasHint);

            if (!hasHint || string.Equals(_lastHint, hint, StringComparison.Ordinal))
                return;

            _lastHint = hint;
            _text.text = hint;
            ResizePanel();
        }

        string BuildHintText()
        {
            ControlModeManager manager = ControlModeManager.Instance;
            if (manager == null)
                return string.Empty;

            string modeName = manager.CurrentModeName;
            if (!IsSupportedMode(modeName))
                return string.Empty;

            _builder.Clear();
            _builder.AppendLine(modeName.ToUpperInvariant());
            AppendModeSwitchHints(modeName);

            if (string.Equals(modeName, "ACT", StringComparison.OrdinalIgnoreCase))
            {
                HeroBase hero = ResolveControlledHero(manager);
                _builder.AppendLine();
                AppendActHints(hero);
            }
            else if (string.Equals(modeName, "FPS", StringComparison.OrdinalIgnoreCase))
            {
                _builder.AppendLine();
                AppendFpsHints();
            }

            return _builder.ToString();
        }

        void AppendModeSwitchHints(string currentModeName)
        {
            if (!string.Equals(currentModeName, "RTS", StringComparison.OrdinalIgnoreCase))
                _builder.AppendLine(GameText.GetText("hud.modeSwitch.rts", "hud.modeSwitch.rts"));
            if (!string.Equals(currentModeName, "ACT", StringComparison.OrdinalIgnoreCase))
                _builder.AppendLine(GameText.GetText("hud.modeSwitch.act", "hud.modeSwitch.act"));
            if (!string.Equals(currentModeName, "FPS", StringComparison.OrdinalIgnoreCase))
                _builder.AppendLine(GameText.GetText("hud.modeSwitch.fps", "hud.modeSwitch.fps"));
        }

        void AppendActHints(HeroBase hero)
        {
            _builder.AppendLine(GameText.GetText("hud.weapon.switch", "hud.weapon.switch"));
            _builder.AppendLine(GameText.GetText("hud.ability.dash", "hud.ability.dash"));
            _builder.AppendLine(GameText.GetText("hud.ability.flightToggle", "hud.ability.flightToggle"));

            if (hero == null)
                return;

            HeroWeapon weapon = hero.CurrentWeapon;
            if (weapon is RevolverWeapon)
            {
                _builder.AppendLine();
                _builder.AppendLine(GameText.GetText("hud.weapon.revolver", "hud.weapon.revolver"));
                _builder.AppendLine(GameText.GetText("hud.weapon.revolver.charge", "hud.weapon.revolver.charge"));
            }
            else if (weapon is SwordWeapon)
            {
                _builder.AppendLine();
                _builder.AppendLine(GameText.GetText("hud.weapon.sword", "hud.weapon.sword"));
                _builder.AppendLine(GameText.GetText("hud.weapon.sword.thrust", "hud.weapon.sword.thrust"));
                _builder.AppendLine(GameText.GetText("hud.weapon.sword.uppercut", "hud.weapon.sword.uppercut"));
                _builder.AppendLine(GameText.GetText("hud.weapon.sword.teleportBehind", "hud.weapon.sword.teleportBehind"));
            }
        }

        void AppendFpsHints()
        {
            _builder.AppendLine(GameText.GetText("hud.weapon.switch", "hud.weapon.switch"));
            _builder.AppendLine(GameText.GetText("hud.fps.aim", "hud.fps.aim"));
            _builder.AppendLine(GameText.GetText("hud.ability.dash", "hud.ability.dash"));
        }

        static bool IsSupportedMode(string modeName)
        {
            return string.Equals(modeName, "RTS", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(modeName, "ACT", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(modeName, "FPS", StringComparison.OrdinalIgnoreCase);
        }

        static HeroBase ResolveControlledHero(ControlModeManager manager)
        {
            if (manager == null || manager.unit == null)
                return null;

            HeroBase hero = manager.unit.GetComponent<HeroBase>();
            if (hero != null)
                return hero;

            hero = manager.unit.GetComponentInChildren<HeroBase>(true);
            if (hero != null)
                return hero;

            return manager.unit.GetComponentInParent<HeroBase>();
        }

        void EnsureView()
        {
            if (_panel != null && _text != null && _background != null && _canvasGroup != null)
                return;

            Transform existing = transform.Find("ActFpsKeyHintPanel");
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject("ActFpsKeyHintPanel", typeof(RectTransform));
            if (existing == null)
                panelObject.transform.SetParent(transform, false);

            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = anchoredPosition;

            _background = panelObject.GetComponent<Image>();
            if (_background == null)
                _background = panelObject.AddComponent<Image>();
            _background.color = backgroundColor;
            _background.raycastTarget = false;

            _canvasGroup = panelObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = panelObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            Transform textTransform = panelObject.transform.Find("Text");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("Text", typeof(RectTransform));
            if (textTransform == null)
                textObject.transform.SetParent(panelObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(PaddingX, PaddingY);
            textRect.offsetMax = new Vector2(-PaddingX, -PaddingY);

            _text = textObject.GetComponent<TMP_Text>();
            if (_text == null)
                _text = textObject.AddComponent<TextMeshProUGUI>();
            _text.raycastTarget = false;
            _text.color = textColor;
            _text.fontSize = fontSize;
            _text.lineSpacing = lineSpacing;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Overflow;
            ApplyChineseCapableFont();
        }

        void ApplyChineseCapableFont()
        {
            TMP_FontAsset fontAsset = chineseFontAsset != null
                ? chineseFontAsset
                : GetRuntimeChineseFontAsset();

            if (fontAsset == null)
                return;

            _text.font = fontAsset;
            _text.fontMaterial = fontAsset.material;
        }

        static TMP_FontAsset GetRuntimeChineseFontAsset()
        {
            if (s_runtimeChineseFontAsset != null)
                return s_runtimeChineseFontAsset;

            Font bundledFont = Resources.Load<Font>(BundledChineseFontResourcePath);
            TMP_FontAsset bundledFontAsset = TryCreateRuntimeFontAsset(bundledFont);
            if (bundledFontAsset != null)
            {
                s_runtimeChineseFontAsset = bundledFontAsset;
                return s_runtimeChineseFontAsset;
            }

            string[] preferredFonts =
            {
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "DengXian",
                "SimHei",
                "SimSun",
                "Noto Sans SC"
            };

            string[] installedFonts = Font.GetOSInstalledFontNames();
            for (int i = 0; i < preferredFonts.Length; i++)
            {
                string preferred = preferredFonts[i];
                for (int j = 0; j < installedFonts.Length; j++)
                {
                    if (!string.Equals(installedFonts[j], preferred, StringComparison.OrdinalIgnoreCase))
                        continue;

                    TMP_FontAsset fontAsset = TryCreateRuntimeFontAssetFromOSFont(installedFonts[j]);
                    if (fontAsset != null)
                    {
                        s_runtimeChineseFontAsset = fontAsset;
                        return s_runtimeChineseFontAsset;
                    }
                }
            }

            for (int i = 0; i < preferredFonts.Length; i++)
            {
                TMP_FontAsset fontAsset = TryCreateRuntimeFontAssetFromOSFont(preferredFonts[i]);
                if (fontAsset != null)
                {
                    s_runtimeChineseFontAsset = fontAsset;
                    return s_runtimeChineseFontAsset;
                }
            }

            return null;
        }

        static TMP_FontAsset TryCreateRuntimeFontAssetFromOSFont(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName))
                return null;

            Font font = Font.CreateDynamicFontFromOSFont(fontName, 48);
            return TryCreateRuntimeFontAsset(font);
        }

        static TMP_FontAsset TryCreateRuntimeFontAsset(Font font)
        {
            if (font == null)
                return null;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                48,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
                return null;

            fontAsset.name = "Runtime Chinese TMP Font";
            string requiredCharacters = GameText.GetCjkCharacters();
            if (!string.IsNullOrEmpty(requiredCharacters))
                fontAsset.TryAddCharacters(requiredCharacters, out _);

            return fontAsset;
        }

        void ResizePanel()
        {
            _text.ForceMeshUpdate();
            Vector2 preferred = _text.GetPreferredValues(_text.text, MaxWidth - PaddingX * 2f, 0f);
            _panel.sizeDelta = new Vector2(
                Mathf.Min(MaxWidth, preferred.x + PaddingX * 2f),
                preferred.y + PaddingY * 2f);
        }

        void SetVisible(bool visible)
        {
            if (_visible == visible && _canvasGroup != null)
                return;

            _visible = visible;
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
