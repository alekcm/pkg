using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MapEditorPrototype
{
    public class BuildUiController : MonoBehaviour
    {
        private class SpawnedItemButtonInfo
        {
            public Button button;
            public string itemId;
        }

        [Header("References")]
        [SerializeField] private BuildSystem buildSystem;
        [SerializeField] private BuildCatalog buildCatalog;
        [SerializeField] private WallCatalog wallCatalog;
        [SerializeField] private PathCatalog pathCatalog;
        [SerializeField] private DetailPaintBrushCatalog detailPaintBrushCatalog;
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private EditorUndoRedoSystem undoRedoSystem;

        [Header("Tool Buttons")]
        [SerializeField] private Button objectToolButton;
        [SerializeField] private Button wallToolButton;
        [SerializeField] private Button doorToolButton;
        [SerializeField] private Button windowToolButton;
        [SerializeField] private Button pathToolButton;
        [SerializeField] private Button detailPaintToolButton;

        [Header("Layer Buttons")]
        [SerializeField] private Button floorLayerButton;
        [SerializeField] private Button furnitureLayerButton;
        [SerializeField] private Button decorLayerButton;

        [Header("Paint Mode Buttons")]
        [SerializeField] private Button singlePaintModeButton;
        [SerializeField] private Button brushPaintModeButton;
        [SerializeField] private Button rectanglePaintModeButton;

        [Header("History Buttons")]
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;

        [Header("Path Buttons")]
        [SerializeField] private Button deleteSelectedPathButton;

        [Header("Draft Buttons")]
        [SerializeField] private Button beginDraftButton;
        [SerializeField] private Button applyDraftButton;
        [SerializeField] private Button cancelDraftButton;

        [Header("Save Buttons")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;

        [Header("Dynamic List")]
        [SerializeField] private RectTransform itemButtonContainer;
        [SerializeField] private Button itemButtonPrefab;

        [Header("Labels")]
        [SerializeField] private TMP_Text currentToolLabel;
        [SerializeField] private TMP_Text currentSelectionLabel;
        [SerializeField] private TMP_Text currentLayerLabel;
        [SerializeField] private TMP_Text currentPaintModeLabel;
        [SerializeField] private TMP_Text draftStatusLabel;
        [SerializeField] private TMP_Text savePathLabel;

        [Header("Colors")]
        [SerializeField] private Color activeButtonColor = new Color(0.18f, 0.62f, 1f, 1f);
        [SerializeField] private Color inactiveButtonColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        [SerializeField] private Color activeTextColor = Color.white;
        [SerializeField] private Color inactiveTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        private readonly List<SpawnedItemButtonInfo> spawnedButtons = new List<SpawnedItemButtonInfo>();

        private void OnEnable()
        {
            if (buildSystem != null)
            {
                buildSystem.StateChanged += HandleBuildStateChanged;
            }

            if (buildCatalog != null)
            {
                buildCatalog.CatalogChanged += RebuildList;
                buildCatalog.SelectionChanged += RefreshUi;
            }

            if (wallCatalog != null)
            {
                wallCatalog.SelectionChanged += RefreshUi;
            }

            if (pathCatalog != null)
            {
                pathCatalog.SelectionChanged += RefreshUi;
            }

            if (detailPaintBrushCatalog != null)
            {
                detailPaintBrushCatalog.SelectionChanged += RefreshUi;
            }

            if (undoRedoSystem != null)
            {
                undoRedoSystem.StateChanged += RefreshUi;
            }
        }

        private void Start()
        {
            BindStaticButtons();
            RebuildList();
            RefreshUi();
        }

        private void OnDisable()
        {
            if (buildSystem != null)
            {
                buildSystem.StateChanged -= HandleBuildStateChanged;
            }

            if (buildCatalog != null)
            {
                buildCatalog.CatalogChanged -= RebuildList;
                buildCatalog.SelectionChanged -= RefreshUi;
            }

            if (wallCatalog != null)
            {
                wallCatalog.SelectionChanged -= RefreshUi;
            }

            if (pathCatalog != null)
            {
                pathCatalog.SelectionChanged -= RefreshUi;
            }

            if (detailPaintBrushCatalog != null)
            {
                detailPaintBrushCatalog.SelectionChanged -= RefreshUi;
            }

            if (undoRedoSystem != null)
            {
                undoRedoSystem.StateChanged -= RefreshUi;
            }
        }

        private void BindStaticButtons()
        {
            BindButton(objectToolButton, () => { if (buildSystem != null) buildSystem.SetTool(BuildToolMode.Object); RebuildList(); });
            BindButton(wallToolButton, () => { if (buildSystem != null) buildSystem.SetTool(BuildToolMode.Wall); RebuildList(); });
            BindButton(doorToolButton, () => { if (buildSystem != null) buildSystem.SetTool(BuildToolMode.Door); RebuildList(); });
            BindButton(windowToolButton, () => { if (buildSystem != null) buildSystem.SetTool(BuildToolMode.Window); RebuildList(); });
            BindButton(pathToolButton, () => { if (buildSystem != null) buildSystem.SetTool(BuildToolMode.Path); RebuildList(); });
            BindButton(detailPaintToolButton, () => { if (buildSystem != null) buildSystem.SetTool(BuildToolMode.DetailPaint); RebuildList(); });

            BindButton(floorLayerButton, () => { if (buildSystem != null) buildSystem.SetLayer(BuildLayer.Floor); RebuildList(); });
            BindButton(furnitureLayerButton, () => { if (buildSystem != null) buildSystem.SetLayer(BuildLayer.Furniture); RebuildList(); });
            BindButton(decorLayerButton, () => { if (buildSystem != null) buildSystem.SetLayer(BuildLayer.Decor); RebuildList(); });

            BindButton(singlePaintModeButton, () => { if (buildSystem != null) buildSystem.SetPaintMode(PlacementPaintMode.Single); RefreshUi(); });
            BindButton(brushPaintModeButton, () => { if (buildSystem != null) buildSystem.SetPaintMode(PlacementPaintMode.Brush); RefreshUi(); });
            BindButton(rectanglePaintModeButton, () => { if (buildSystem != null) buildSystem.SetPaintMode(PlacementPaintMode.Rectangle); RefreshUi(); });

            BindButton(undoButton, () => undoRedoSystem?.Undo());
            BindButton(redoButton, () => undoRedoSystem?.Redo());
            BindButton(deleteSelectedPathButton, () => buildSystem?.DeleteSelectedPath());
            BindButton(beginDraftButton, () => buildSystem?.BeginEditDraftSession());
            BindButton(applyDraftButton, () => buildSystem?.ApplyEditDraftSession());
            BindButton(cancelDraftButton, () => buildSystem?.CancelEditDraftSession());
            BindButton(saveButton, () => mapSaveSystem?.SaveDefault());
            BindButton(loadButton, () =>
            {
                if (undoRedoSystem != null)
                {
                    undoRedoSystem.RecordStateBeforeChange();
                }

                mapSaveSystem?.LoadDefault();
            });
        }

        private void HandleBuildStateChanged()
        {
            RebuildList();
        }

        public void RebuildList()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i].button != null)
                {
                    Destroy(spawnedButtons[i].button.gameObject);
                }
            }
            spawnedButtons.Clear();

            if (itemButtonContainer == null || itemButtonPrefab == null || buildSystem == null)
            {
                RefreshUi();
                return;
            }

            switch (buildSystem.CurrentTool)
            {
                case BuildToolMode.Wall:
                    BuildWallButtons();
                    break;
                case BuildToolMode.Door:
                    BuildDoorButtons();
                    break;
                case BuildToolMode.Window:
                    BuildWindowButtons();
                    break;
                case BuildToolMode.Path:
                    BuildPathButtons();
                    break;
                case BuildToolMode.DetailPaint:
                    BuildDetailBrushButtons();
                    break;
                default:
                    BuildObjectButtons();
                    break;
            }

            RefreshUi();
        }

        public void RefreshUi()
        {
            if (buildSystem != null)
            {
                if (currentToolLabel != null)
                {
                    currentToolLabel.text = $"Tool: {GetToolTitle(buildSystem.CurrentTool)}";
                }

                if (currentSelectionLabel != null)
                {
                    string selection = buildSystem.GetCurrentSelectionLabel();
                    if (buildSystem.SelectedObject != null && buildSystem.SelectedObject.Definition != null)
                    {
                        selection += $" | Selected: {buildSystem.SelectedObject.Definition.SafeDisplayName}";
                    }

                    currentSelectionLabel.text = selection;
                }

                if (currentLayerLabel != null)
                {
                    currentLayerLabel.text = $"Layer: {buildSystem.GetCurrentLayerLabel()}";
                }

                if (currentPaintModeLabel != null)
                {
                    currentPaintModeLabel.text = $"Paint: {buildSystem.GetCurrentPaintModeLabel()}";
                }
            }

            if (draftStatusLabel != null && buildSystem != null)
            {
                draftStatusLabel.text = buildSystem.HasActiveEditDraft ? "Draft: Active" : "Draft: Inactive";
            }

            if (savePathLabel != null && mapSaveSystem != null)
            {
                savePathLabel.text = mapSaveSystem.DefaultSavePath;
            }

            UpdateStaticButtonHighlights();
            UpdateSpawnedButtonHighlights();

            if (undoButton != null && undoRedoSystem != null)
            {
                undoButton.interactable = undoRedoSystem.CanUndo;
            }

            if (redoButton != null && undoRedoSystem != null)
            {
                redoButton.interactable = undoRedoSystem.CanRedo;
            }

            if (deleteSelectedPathButton != null)
            {
                deleteSelectedPathButton.interactable = buildSystem != null && buildSystem.HasSelectedPath;
            }

            if (beginDraftButton != null)
            {
                beginDraftButton.interactable = buildSystem != null && !buildSystem.HasActiveEditDraft;
            }

            if (applyDraftButton != null)
            {
                applyDraftButton.interactable = buildSystem != null && buildSystem.HasActiveEditDraft;
            }

            if (cancelDraftButton != null)
            {
                cancelDraftButton.interactable = buildSystem != null && buildSystem.HasActiveEditDraft;
            }
        }

        private void BuildObjectButtons()
        {
            if (buildCatalog == null)
            {
                return;
            }

            IReadOnlyList<BuildingDefinition> items = buildCatalog.FilteredItems;
            for (int i = 0; i < items.Count; i++)
            {
                BuildingDefinition definition = items[i];
                if (definition == null)
                {
                    continue;
                }

                Button button = SpawnButton(definition.SafeDisplayName, definition.id);
                int cachedIndex = i;
                button.onClick.AddListener(() =>
                {
                    buildCatalog.Select(cachedIndex);
                    if (buildSystem != null)
                    {
                        buildSystem.SetTool(BuildToolMode.Object);
                    }
                    RefreshUi();
                });
            }
        }

        private void BuildWallButtons()
        {
            if (wallCatalog == null)
            {
                return;
            }

            IReadOnlyList<WallDefinition> walls = wallCatalog.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                WallDefinition definition = walls[i];
                if (definition == null)
                {
                    continue;
                }

                Button button = SpawnButton(definition.SafeDisplayName, definition.id);
                int cachedIndex = i;
                button.onClick.AddListener(() =>
                {
                    wallCatalog.SelectWall(cachedIndex);
                    RefreshUi();
                });
            }
        }

        private void BuildDoorButtons()
        {
            if (wallCatalog == null)
            {
                return;
            }

            IReadOnlyList<WallOpeningDefinition> doors = wallCatalog.Doors;
            for (int i = 0; i < doors.Count; i++)
            {
                WallOpeningDefinition definition = doors[i];
                if (definition == null)
                {
                    continue;
                }

                Button button = SpawnButton(definition.SafeDisplayName, definition.id);
                int cachedIndex = i;
                button.onClick.AddListener(() =>
                {
                    wallCatalog.SelectDoor(cachedIndex);
                    RefreshUi();
                });
            }
        }

        private void BuildWindowButtons()
        {
            if (wallCatalog == null)
            {
                return;
            }

            IReadOnlyList<WallOpeningDefinition> windows = wallCatalog.Windows;
            for (int i = 0; i < windows.Count; i++)
            {
                WallOpeningDefinition definition = windows[i];
                if (definition == null)
                {
                    continue;
                }

                Button button = SpawnButton(definition.SafeDisplayName, definition.id);
                int cachedIndex = i;
                button.onClick.AddListener(() =>
                {
                    wallCatalog.SelectWindow(cachedIndex);
                    RefreshUi();
                });
            }
        }

        private void BuildPathButtons()
        {
            if (pathCatalog == null)
            {
                return;
            }

            IReadOnlyList<PathDefinition> paths = pathCatalog.Paths;
            for (int i = 0; i < paths.Count; i++)
            {
                PathDefinition definition = paths[i];
                if (definition == null)
                {
                    continue;
                }

                Button button = SpawnButton(definition.SafeDisplayName, definition.id);
                int cachedIndex = i;
                button.onClick.AddListener(() =>
                {
                    pathCatalog.Select(cachedIndex);
                    RefreshUi();
                });
            }
        }

        private void BuildDetailBrushButtons()
        {
            if (detailPaintBrushCatalog == null)
            {
                return;
            }

            IReadOnlyList<DetailPaintBrushDefinition> brushes = detailPaintBrushCatalog.Brushes;
            for (int i = 0; i < brushes.Count; i++)
            {
                DetailPaintBrushDefinition definition = brushes[i];
                if (definition == null)
                {
                    continue;
                }

                Button button = SpawnButton(definition.SafeDisplayName, definition.id);
                int cachedIndex = i;
                button.onClick.AddListener(() =>
                {
                    detailPaintBrushCatalog.Select(cachedIndex);
                    RefreshUi();
                });
            }
        }

        private Button SpawnButton(string label, string itemId)
        {
            Button button = Instantiate(itemButtonPrefab, itemButtonContainer);
            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
            }

            spawnedButtons.Add(new SpawnedItemButtonInfo
            {
                button = button,
                itemId = itemId
            });

            return button;
        }

        private void UpdateStaticButtonHighlights()
        {
            if (buildSystem == null)
            {
                return;
            }

            SetButtonHighlighted(objectToolButton, buildSystem.CurrentTool == BuildToolMode.Object || buildSystem.CurrentTool == BuildToolMode.Move);
            SetButtonHighlighted(wallToolButton, buildSystem.CurrentTool == BuildToolMode.Wall);
            SetButtonHighlighted(doorToolButton, buildSystem.CurrentTool == BuildToolMode.Door);
            SetButtonHighlighted(windowToolButton, buildSystem.CurrentTool == BuildToolMode.Window);
            SetButtonHighlighted(pathToolButton, buildSystem.CurrentTool == BuildToolMode.Path);
            SetButtonHighlighted(detailPaintToolButton, buildSystem.CurrentTool == BuildToolMode.DetailPaint);

            bool isWallFamily = buildSystem.CurrentTool == BuildToolMode.Wall || buildSystem.CurrentTool == BuildToolMode.Door || buildSystem.CurrentTool == BuildToolMode.Window;
            SetButtonHighlighted(floorLayerButton, !isWallFamily && buildCatalog != null && buildCatalog.CurrentLayer == BuildLayer.Floor);
            SetButtonHighlighted(furnitureLayerButton, !isWallFamily && buildCatalog != null && buildCatalog.CurrentLayer == BuildLayer.Furniture);
            SetButtonHighlighted(decorLayerButton, !isWallFamily && buildCatalog != null && buildCatalog.CurrentLayer == BuildLayer.Decor);

            bool pathOrDetailTool = buildSystem.CurrentTool == BuildToolMode.Path || buildSystem.CurrentTool == BuildToolMode.DetailPaint;
            SetButtonHighlighted(singlePaintModeButton, !pathOrDetailTool && buildSystem.CurrentPaintMode == PlacementPaintMode.Single);
            SetButtonHighlighted(brushPaintModeButton, buildSystem.CurrentTool == BuildToolMode.DetailPaint || (!pathOrDetailTool && buildSystem.CurrentPaintMode == PlacementPaintMode.Brush));
            SetButtonHighlighted(rectanglePaintModeButton, !pathOrDetailTool && buildSystem.CurrentPaintMode == PlacementPaintMode.Rectangle);
        }

        private void UpdateSpawnedButtonHighlights()
        {
            string selectedId = null;
            switch (buildSystem != null ? buildSystem.CurrentTool : BuildToolMode.Object)
            {
                case BuildToolMode.Wall:
                    selectedId = wallCatalog != null && wallCatalog.CurrentWall != null ? wallCatalog.CurrentWall.id : null;
                    break;
                case BuildToolMode.Door:
                    selectedId = wallCatalog != null && wallCatalog.CurrentDoor != null ? wallCatalog.CurrentDoor.id : null;
                    break;
                case BuildToolMode.Window:
                    selectedId = wallCatalog != null && wallCatalog.CurrentWindow != null ? wallCatalog.CurrentWindow.id : null;
                    break;
                case BuildToolMode.Path:
                    selectedId = pathCatalog != null && pathCatalog.Current != null ? pathCatalog.Current.id : null;
                    break;
                case BuildToolMode.DetailPaint:
                    selectedId = detailPaintBrushCatalog != null && detailPaintBrushCatalog.Current != null ? detailPaintBrushCatalog.Current.id : null;
                    break;
                default:
                    selectedId = buildCatalog != null && buildCatalog.Current != null ? buildCatalog.Current.id : null;
                    break;
            }

            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                SpawnedItemButtonInfo info = spawnedButtons[i];
                if (info != null && info.button != null)
                {
                    SetButtonHighlighted(info.button, !string.IsNullOrWhiteSpace(selectedId) && info.itemId == selectedId);
                }
            }
        }

        private void SetButtonHighlighted(Button button, bool highlighted)
        {
            if (button == null)
            {
                return;
            }

            Color background = highlighted ? activeButtonColor : inactiveButtonColor;
            Color textColor = highlighted ? activeTextColor : inactiveTextColor;

            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.selectedColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.15f);
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.35f);
            button.colors = colors;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = background;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.color = textColor;
            }
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private string GetToolTitle(BuildToolMode tool)
        {
            switch (tool)
            {
                case BuildToolMode.Wall: return "Wall";
                case BuildToolMode.Door: return "Door";
                case BuildToolMode.Window: return "Window";
                case BuildToolMode.Path: return "Path";
                case BuildToolMode.DetailPaint: return "Detail Paint";
                case BuildToolMode.Move: return "Move";
                default: return "Object";
            }
        }
    }
}
