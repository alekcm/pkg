using System.Collections.Generic;
using CharacterEditor.Hair.Net;
using CharacterEditor.Hair.Proc;
using CharacterEditor.Hair.Proc.Integration;
using CharacterEditor.Hair.Profile;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace CharacterEditor.Hair.UI
{
    /// <summary>
    /// Simple runtime UI for working with generated HairPieceDefinitionProc presets.
    /// Add this component to any scene object, assign a HairCatalogProc if needed,
    /// then press Play. If no Canvas/Panel is assigned, the UI is built automatically.
    /// </summary>
    public class CharacterHairPresetUi : MonoBehaviour
    {
        [Header("Hair refs")]
        public HairCatalogProc catalog;
        public HairRuntimeAttacherProc procAttacher;
        public HairProcIntegration procIntegration;
        public HairNetworkBridge networkBridge;

        [Header("UI refs - optional")]
        public Canvas canvas;
        public RectTransform panelRoot;
        public RectTransform presetButtonContainer;
        public Button presetButtonPrefab;
        public Text selectedPresetLabel;

        [Header("Behaviour")]
        public bool buildUiAutomatically = true;
        public bool applyFirstPresetOnStart = false;
        public KeyCode toggleKey = KeyCode.H;

        [Header("Style")]
        public Font font;
        public Color panelColor = new Color(0.06f, 0.07f, 0.09f, 0.92f);
        public Color buttonColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        public Color selectedButtonColor = new Color(0.20f, 0.48f, 0.95f, 1f);
        public Color textColor = Color.white;

        private readonly List<Button> _spawnedButtons = new List<Button>();
        private readonly Dictionary<Button, HairPieceDefinitionProc> _buttonToPiece = new Dictionary<Button, HairPieceDefinitionProc>();

        private HairPieceDefinitionProc _currentPiece;
        private HairDna _currentDna;
        private bool _suppressSliderEvents;

        private Slider _lengthSlider;
        private Slider _densitySlider;
        private Slider _thicknessSlider;
        private Slider _curlSlider;
        private Slider _waveSlider;
        private Slider _frizzSlider;
        private Slider _rootRSlider;
        private Slider _rootGSlider;
        private Slider _rootBSlider;
        private Slider _tipRSlider;
        private Slider _tipGSlider;
        private Slider _tipBSlider;

        private Text _lengthValue;
        private Text _densityValue;
        private Text _thicknessValue;
        private Text _curlValue;
        private Text _waveValue;
        private Text _frizzValue;
        private Text _rootValue;
        private Text _tipValue;

        private void Awake()
        {
            ResolveRefs();
        }

        private void Start()
        {
            if (buildUiAutomatically)
                BuildUiIfNeeded();

            RebuildPresetButtons();

            if (applyFirstPresetOnStart && catalog != null && catalog.pieces != null && catalog.pieces.Count > 0)
            {
                for (int i = 0; i < catalog.pieces.Count; i++)
                {
                    if (catalog.pieces[i] != null)
                    {
                        SelectPreset(catalog.pieces[i]);
                        break;
                    }
                }
            }
            else
            {
                RefreshUiState();
            }
        }

        private void Update()
        {
            if (ToggleWasPressed() && panelRoot != null)
                panelRoot.gameObject.SetActive(!panelRoot.gameObject.activeSelf);
        }

        private bool ToggleWasPressed()
        {
            if (toggleKey == KeyCode.None)
                return false;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            switch (toggleKey)
            {
                case KeyCode.H: return keyboard.hKey.wasPressedThisFrame;
                case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.F1: return keyboard.f1Key.wasPressedThisFrame;
                case KeyCode.F2: return keyboard.f2Key.wasPressedThisFrame;
                case KeyCode.F3: return keyboard.f3Key.wasPressedThisFrame;
                case KeyCode.F4: return keyboard.f4Key.wasPressedThisFrame;
                case KeyCode.F5: return keyboard.f5Key.wasPressedThisFrame;
                case KeyCode.F6: return keyboard.f6Key.wasPressedThisFrame;
                case KeyCode.F7: return keyboard.f7Key.wasPressedThisFrame;
                case KeyCode.F8: return keyboard.f8Key.wasPressedThisFrame;
                case KeyCode.F9: return keyboard.f9Key.wasPressedThisFrame;
                case KeyCode.F10: return keyboard.f10Key.wasPressedThisFrame;
                case KeyCode.F11: return keyboard.f11Key.wasPressedThisFrame;
                case KeyCode.F12: return keyboard.f12Key.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(toggleKey);
#endif
        }

        private void ResolveRefs()
        {
            if (catalog == null)
                catalog = Resources.Load<HairCatalogProc>("HairCatalogProc");

            if (procIntegration == null)
                procIntegration = FindObjectOfType<HairProcIntegration>();

            if (procAttacher == null)
            {
                if (procIntegration != null && procIntegration.procAttacher != null)
                    procAttacher = procIntegration.procAttacher;
                else
                    procAttacher = FindObjectOfType<HairRuntimeAttacherProc>();
            }

            if (networkBridge == null)
            {
                if (procAttacher != null)
                    networkBridge = procAttacher.GetComponentInParent<HairNetworkBridge>();
                if (networkBridge == null)
                    networkBridge = FindObjectOfType<HairNetworkBridge>();
            }
        }

        public void RebuildPresetButtons()
        {
            ResolveRefs();
            if (catalog != null)
            {
                AutoFillCatalogInEditorIfNeeded();
                catalog.Rebuild();
            }

            foreach (Button b in _spawnedButtons)
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            _spawnedButtons.Clear();
            _buttonToPiece.Clear();

            if (presetButtonContainer == null || presetButtonPrefab == null)
            {
                RefreshUiState();
                return;
            }

            if (catalog == null || catalog.pieces == null || catalog.pieces.Count == 0)
            {
                SetLabel(selectedPresetLabel, "HairCatalogProc не найден или пуст. Проверь Assets/Resources/HairCatalogProc.asset");
                return;
            }

            foreach (HairPieceDefinitionProc piece in catalog.pieces)
            {
                if (piece == null)
                    continue;

                Button button = Instantiate(presetButtonPrefab, presetButtonContainer);
                button.gameObject.SetActive(true);
                SetButtonText(button, string.IsNullOrEmpty(piece.displayName) ? piece.id : piece.displayName);
                HairPieceDefinitionProc captured = piece;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPreset(captured));
                _spawnedButtons.Add(button);
                _buttonToPiece[button] = captured;
            }

            RefreshUiState();
        }

        private void AutoFillCatalogInEditorIfNeeded()
        {
#if UNITY_EDITOR
            if (catalog == null || catalog.pieces == null)
                return;

            // In the character-creator scene it is common to forget to run
            // HairCatalogProc/Auto-Fill after generating presets. If the catalog has
            // 0-1 entries, fill it automatically in Editor/Play Mode so the UI shows
            // all available hair types immediately.
            if (catalog.pieces.Count > 1)
                return;

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            bool changed = false;
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                HairPieceDefinitionProc piece = UnityEditor.AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (piece != null && !catalog.pieces.Contains(piece))
                {
                    catalog.pieces.Add(piece);
                    changed = true;
                }
            }

            if (changed)
            {
                catalog.Rebuild();
                UnityEditor.EditorUtility.SetDirty(catalog);
                Debug.Log($"[HairPresetUi] Auto-filled HairCatalogProc with {catalog.pieces.Count} pieces.", catalog);
            }
#endif
        }

        public void SelectPreset(HairPieceDefinitionProc piece)
        {
            if (piece == null)
                return;

            _currentPiece = piece;
            _currentDna = HairDna.Default(piece.id);

            _currentDna.lengthScale = Mathf.Clamp(piece.defaultLength <= 0f ? 1f : piece.defaultLength, 0.5f, 1.8f);
            _currentDna.density = Mathf.Clamp(piece.defaultDensity <= 0f ? 1f : piece.defaultDensity, 0.3f, 2f);
            _currentDna.thickness = Mathf.Clamp(piece.defaultThickness <= 0f ? 1f : piece.defaultThickness, 0.5f, 1.5f);
            _currentDna.curl = Mathf.Clamp01(piece.defaultCurl);
            _currentDna.wave = Mathf.Clamp01(piece.defaultWave);
            _currentDna.frizz = Mathf.Clamp01(piece.defaultFrizz);
            _currentDna.rootColor = ToColor32(piece.defaultColor.rootColor, new Color32(38, 26, 20, 255));
            _currentDna.tipColor = ToColor32(piece.defaultColor.tipColor, new Color32(64, 46, 30, 255));
            _currentDna.rootFade255 = (byte)Mathf.Clamp(Mathf.RoundToInt(piece.defaultColor.rootFade * 255f), 0, 255);
            _currentDna.highlightColor = ToColor32(piece.defaultColor.highlightColor, new Color32(128, 90, 64, 255));
            _currentDna.highlightStrength255 = (byte)Mathf.Clamp(Mathf.RoundToInt(piece.defaultColor.highlightStrength * 255f), 0, 255);

            ApplyCurrentDna();
            RefreshUiState();
        }

        public void SaveCurrentHairProfile()
        {
            if (_currentPiece == null || _currentDna.pieceHash == 0)
            {
                Debug.LogWarning("[HairPresetUi] Select a hair preset before saving.", this);
                return;
            }

            HairCharacterProfileStore.Save(_currentDna);
            SetLabel(selectedPresetLabel, $"Saved: {SafeName(_currentPiece)}");
        }

        public void LoadSavedHairProfile()
        {
            if (!HairCharacterProfileStore.TryLoad(out HairDna dna))
            {
                Debug.LogWarning("[HairPresetUi] No saved hair profile found.", this);
                return;
            }

            ResolveRefs();
            HairPieceDefinitionProc piece = catalog != null ? catalog.ResolveByHash(dna.pieceHash) : null;
            if (piece == null)
            {
                Debug.LogWarning($"[HairPresetUi] Saved hair hash {dna.pieceHash:X8} was not found in HairCatalogProc.", this);
                return;
            }

            _currentPiece = piece;
            _currentDna = dna;
            ApplyCurrentDna();
            RefreshUiState();
        }

        public void ClearHair()
        {
            if (procAttacher != null)
                procAttacher.ClearSlot("");

            _currentPiece = null;
            _currentDna = default;
            RefreshUiState();
        }

        private void ApplyCurrentDna()
        {
            if (_currentPiece == null)
                return;

            ResolveRefs();

            if (procAttacher != null)
                procAttacher.ApplyHair(_currentPiece, _currentDna, 0);
            else if (procIntegration != null)
                procIntegration.ApplyDna(_currentDna);

            if (networkBridge != null && (networkBridge.IsOwner || networkBridge.IsServer))
                networkBridge.PushLocalDna(_currentDna);
        }

        private void RefreshUiState()
        {
            if (_currentPiece != null)
                SetLabel(selectedPresetLabel, $"Preset: {SafeName(_currentPiece)}");
            else if (catalog == null)
                SetLabel(selectedPresetLabel, "Preset: catalog not found");
            else
                SetLabel(selectedPresetLabel, "Preset: none");

            _suppressSliderEvents = true;
            SetSlider(_lengthSlider, _currentPiece != null ? _currentDna.lengthScale : 1f);
            SetSlider(_densitySlider, _currentPiece != null ? _currentDna.density : 1f);
            SetSlider(_thicknessSlider, _currentPiece != null ? _currentDna.thickness : 1f);
            SetSlider(_curlSlider, _currentPiece != null ? _currentDna.curl : 0f);
            SetSlider(_waveSlider, _currentPiece != null ? _currentDna.wave : 0f);
            SetSlider(_frizzSlider, _currentPiece != null ? _currentDna.frizz : 0f);

            Color root = _currentPiece != null ? (Color)_currentDna.rootColor : new Color(0.15f, 0.1f, 0.08f, 1f);
            Color tip = _currentPiece != null ? (Color)_currentDna.tipColor : new Color(0.25f, 0.18f, 0.12f, 1f);
            SetSlider(_rootRSlider, root.r);
            SetSlider(_rootGSlider, root.g);
            SetSlider(_rootBSlider, root.b);
            SetSlider(_tipRSlider, tip.r);
            SetSlider(_tipGSlider, tip.g);
            SetSlider(_tipBSlider, tip.b);
            _suppressSliderEvents = false;

            UpdateValueLabels();
            UpdateButtonHighlights();
        }

        private void UpdateDnaFromSlidersAndApply()
        {
            if (_suppressSliderEvents || _currentPiece == null)
                return;

            _currentDna.lengthScale = _lengthSlider != null ? _lengthSlider.value : _currentDna.lengthScale;
            _currentDna.density = _densitySlider != null ? _densitySlider.value : _currentDna.density;
            _currentDna.thickness = _thicknessSlider != null ? _thicknessSlider.value : _currentDna.thickness;
            _currentDna.curl = _curlSlider != null ? _curlSlider.value : _currentDna.curl;
            _currentDna.wave = _waveSlider != null ? _waveSlider.value : _currentDna.wave;
            _currentDna.frizz = _frizzSlider != null ? _frizzSlider.value : _currentDna.frizz;

            _currentDna.rootColor = new Color32(
                ToByte(_rootRSlider != null ? _rootRSlider.value : 0.15f),
                ToByte(_rootGSlider != null ? _rootGSlider.value : 0.10f),
                ToByte(_rootBSlider != null ? _rootBSlider.value : 0.08f),
                255);

            _currentDna.tipColor = new Color32(
                ToByte(_tipRSlider != null ? _tipRSlider.value : 0.25f),
                ToByte(_tipGSlider != null ? _tipGSlider.value : 0.18f),
                ToByte(_tipBSlider != null ? _tipBSlider.value : 0.12f),
                255);

            UpdateValueLabels();
            ApplyCurrentDna();
        }

        private void BuildUiIfNeeded()
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (canvas == null)
                canvas = FindObjectOfType<Canvas>();

            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("HairPresetCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            EnsureEventSystemForCurrentInputBackend();

            if (panelRoot == null)
                panelRoot = CreatePanel(canvas.transform as RectTransform);

            if (presetButtonContainer == null || presetButtonPrefab == null)
                BuildDefaultPanelContent(panelRoot);
        }

        private void EnsureEventSystemForCurrentInputBackend()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemGo.GetComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            // When Player Settings -> Active Input Handling is set to "Input System Package",
            // StandaloneInputModule uses the old UnityEngine.Input API and spams exceptions.
            StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
                Destroy(legacyModule);

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
#else
            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private RectTransform CreatePanel(RectTransform parent)
        {
            GameObject panel = new GameObject("HairPresetPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvas.transform, false);
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.08f);
            rt.anchorMax = new Vector2(0f, 0.92f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(18f, 0f);
            rt.sizeDelta = new Vector2(320f, 0f);

            Image image = panel.GetComponent<Image>();
            image.color = panelColor;

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        private void BuildDefaultPanelContent(RectTransform root)
        {
            Text title = CreateText(root, "HAIR PRESETS", 22, FontStyle.Bold);
            SetPreferredHeight(title.gameObject, 24f);

            selectedPresetLabel = CreateText(root, "Preset: none", 16, FontStyle.Normal);
            SetPreferredHeight(selectedPresetLabel.gameObject, 20f);

            GameObject scrollGo = new GameObject("PresetScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(root, false);
            SetPreferredHeight(scrollGo, 120f);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.22f);

            RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRt = viewport.GetComponent<RectTransform>();
            Stretch(viewportRt);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            presetButtonContainer = contentRt;

            ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            presetButtonPrefab = CreateButton(root, "Preset Button Prefab");
            presetButtonPrefab.gameObject.SetActive(false);
            SetPreferredHeight(presetButtonPrefab.gameObject, 28f);

            CreateButtonRow(root,
                ("Refresh", RebuildPresetButtons),
                ("Load", LoadSavedHairProfile));

            CreateButtonRow(root,
                ("Save", SaveCurrentHairProfile),
                ("Clear", ClearHair));

            // Sliders are placed into their own scroll area so labels/values never run
            // below the screen on 768p/900p windows.
            GameObject controlsScroll = new GameObject("ControlsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            controlsScroll.transform.SetParent(root, false);
            SetPreferredHeight(controlsScroll, 260f);
            controlsScroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.16f);

            GameObject controlsViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            controlsViewport.transform.SetParent(controlsScroll.transform, false);
            RectTransform controlsViewportRt = controlsViewport.GetComponent<RectTransform>();
            Stretch(controlsViewportRt);
            controlsViewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.04f);
            controlsViewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject controlsContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            controlsContent.transform.SetParent(controlsViewport.transform, false);
            RectTransform controlsContentRt = controlsContent.GetComponent<RectTransform>();
            controlsContentRt.anchorMin = new Vector2(0f, 1f);
            controlsContentRt.anchorMax = new Vector2(1f, 1f);
            controlsContentRt.pivot = new Vector2(0.5f, 1f);
            controlsContentRt.anchoredPosition = Vector2.zero;
            controlsContentRt.sizeDelta = Vector2.zero;
            VerticalLayoutGroup controlsLayout = controlsContent.GetComponent<VerticalLayoutGroup>();
            controlsLayout.spacing = 4f;
            controlsLayout.childControlWidth = true;
            controlsLayout.childControlHeight = false;
            controlsLayout.childForceExpandWidth = true;
            controlsLayout.childForceExpandHeight = false;
            controlsContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect controlsScrollRect = controlsScroll.GetComponent<ScrollRect>();
            controlsScrollRect.viewport = controlsViewportRt;
            controlsScrollRect.content = controlsContentRt;
            controlsScrollRect.horizontal = false;
            controlsScrollRect.vertical = true;

            CreateSliderBlock(controlsContent.transform, "Length", 0.5f, 1.8f, out _lengthSlider, out _lengthValue);
            CreateSliderBlock(controlsContent.transform, "Density", 0.3f, 2f, out _densitySlider, out _densityValue);
            CreateSliderBlock(controlsContent.transform, "Thickness", 0.5f, 1.5f, out _thicknessSlider, out _thicknessValue);
            CreateSliderBlock(controlsContent.transform, "Curl", 0f, 1f, out _curlSlider, out _curlValue);
            CreateSliderBlock(controlsContent.transform, "Wave", 0f, 1f, out _waveSlider, out _waveValue);
            CreateSliderBlock(controlsContent.transform, "Frizz", 0f, 1f, out _frizzSlider, out _frizzValue);

            Text colorTitle = CreateText(controlsContent.transform, "Colors", 16, FontStyle.Bold);
            SetPreferredHeight(colorTitle.gameObject, 18f);
            CreateSliderBlock(controlsContent.transform, "Root R", 0f, 1f, out _rootRSlider, out _rootValue);
            CreateSliderBlock(controlsContent.transform, "Root G", 0f, 1f, out _rootGSlider, out _rootValue);
            CreateSliderBlock(controlsContent.transform, "Root B", 0f, 1f, out _rootBSlider, out _rootValue);
            CreateSliderBlock(controlsContent.transform, "Tip R", 0f, 1f, out _tipRSlider, out _tipValue);
            CreateSliderBlock(controlsContent.transform, "Tip G", 0f, 1f, out _tipGSlider, out _tipValue);
            CreateSliderBlock(controlsContent.transform, "Tip B", 0f, 1f, out _tipBSlider, out _tipValue);

            Text hint = CreateText(root, $"Toggle: {toggleKey} | scroll controls", 13, FontStyle.Italic);
            hint.color = new Color(1f, 1f, 1f, 0.65f);
            SetPreferredHeight(hint.gameObject, 16f);
        }

        private void CreateSliderBlock(Transform parent, string label, float min, float max, out Slider slider, out Text valueText)
        {
            GameObject row = new GameObject(label + " Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            SetPreferredHeight(row, 22f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            Text labelText = CreateText(row.transform, label, 13, FontStyle.Normal);
            LayoutElement labelLe = labelText.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 58f;

            slider = CreateSlider(row.transform, min, max);
            LayoutElement sliderLe = slider.gameObject.AddComponent<LayoutElement>();
            sliderLe.flexibleWidth = 1f;

            valueText = CreateText(row.transform, "0.00", 12, FontStyle.Normal);
            valueText.alignment = TextAnchor.MiddleRight;
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.preferredWidth = 36f;

            slider.onValueChanged.AddListener(_ => UpdateDnaFromSlidersAndApply());
        }

        private Slider CreateSlider(Transform parent, float min, float max)
        {
            GameObject go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(150f, 22f);

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(go.transform, false);
            RectTransform bgRt = background.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(4f, 0f);
            fillAreaRt.offsetMax = new Vector2(-4f, 0f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            Stretch(fillRt);
            fill.GetComponent<Image>().color = selectedButtonColor;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>());

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(10f, 18f);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private void CreateButtonRow(Transform parent, params (string label, UnityEngine.Events.UnityAction action)[] buttons)
        {
            GameObject row = new GameObject("Button Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            SetPreferredHeight(row, 22f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            foreach (var def in buttons)
            {
                Button b = CreateButton(row.transform, def.label);
                b.onClick.AddListener(def.action);
            }
        }

        private Button CreateButton(Transform parent, string label)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = buttonColor;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            Text t = CreateText(go.transform, label, 14, FontStyle.Normal);
            t.alignment = TextAnchor.MiddleCenter;
            Stretch(t.rectTransform);
            return button;
        }

        private Text CreateText(Transform parent, string text, int size, FontStyle style)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = textColor;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            return t;
        }

        private void UpdateValueLabels()
        {
            SetLabel(_lengthValue, _lengthSlider != null ? _lengthSlider.value.ToString("0.00") : "-");
            SetLabel(_densityValue, _densitySlider != null ? _densitySlider.value.ToString("0.00") : "-");
            SetLabel(_thicknessValue, _thicknessSlider != null ? _thicknessSlider.value.ToString("0.00") : "-");
            SetLabel(_curlValue, _curlSlider != null ? _curlSlider.value.ToString("0.00") : "-");
            SetLabel(_waveValue, _waveSlider != null ? _waveSlider.value.ToString("0.00") : "-");
            SetLabel(_frizzValue, _frizzSlider != null ? _frizzSlider.value.ToString("0.00") : "-");

            Color32 root = _currentPiece != null ? _currentDna.rootColor : new Color32(0, 0, 0, 255);
            Color32 tip = _currentPiece != null ? _currentDna.tipColor : new Color32(0, 0, 0, 255);
            SetLabel(_rootValue, $"#{root.r:X2}{root.g:X2}{root.b:X2}");
            SetLabel(_tipValue, $"#{tip.r:X2}{tip.g:X2}{tip.b:X2}");
        }

        private void UpdateButtonHighlights()
        {
            foreach (Button b in _spawnedButtons)
            {
                if (b == null)
                    continue;

                Image image = b.GetComponent<Image>();
                if (image != null)
                {
                    bool selected = _buttonToPiece.TryGetValue(b, out HairPieceDefinitionProc p) && p == _currentPiece;
                    image.color = selected ? selectedButtonColor : buttonColor;
                }
            }
        }

        private static void SetPreferredHeight(GameObject go, float height)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null)
                le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void SetButtonText(Button button, string text)
        {
            Text t = button.GetComponentInChildren<Text>(true);
            if (t != null)
                t.text = text;
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
                slider.value = value;
        }

        private static void SetLabel(Text label, string text)
        {
            if (label != null)
                label.text = text;
        }

        private static string SafeName(HairPieceDefinitionProc piece)
        {
            if (piece == null)
                return "None";
            if (!string.IsNullOrEmpty(piece.displayName))
                return piece.displayName;
            if (!string.IsNullOrEmpty(piece.id))
                return piece.id;
            return piece.name;
        }

        private static Color32 ToColor32(Color c, Color32 fallback)
        {
            if (c.a <= 0f && c.r <= 0f && c.g <= 0f && c.b <= 0f)
                return fallback;
            return c;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }
    }
}
