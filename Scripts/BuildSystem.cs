using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MapEditorPrototype
{
    public class BuildSystem : MonoBehaviour
    {
        [Serializable]
        private class PendingMoveState
        {
            public BuildingDefinition definition;
            public Vector2Int originCell;
            public int rotationSteps;
            public float rotationY;
            public bool useGridPlacement;
            public float baseY;
            public Vector3 worldPosition;
            public string undoSnapshotJson;
        }

        private struct CursorContext
        {
            public Ray ray;
            public Vector3 planePoint;
            public bool hasPhysicsHit;
            public RaycastHit physicsHit;
            public PlacedObject hoveredPlacedObject;
            public PathStroke hoveredPathStroke;
            public PathHandleMarker hoveredPathHandleMarker;
        }

        private struct ObjectPlacementPreview
        {
            public bool useGridPlacement;
            public Vector2Int originCell;
            public Vector3 worldPosition;
            public float baseY;
            public float yRotation;
            public bool canPlace;
        }

        [Header("References")]
        [SerializeField] private GameModeController gameModeController;
        [SerializeField] private GridBuildingSystem gridBuildingSystem;
        [SerializeField] private WallSystem wallSystem;
        [SerializeField] private PathSystem pathSystem;
        [SerializeField] private BuildCatalog buildCatalog;
        [SerializeField] private WallCatalog wallCatalog;
        [SerializeField] private PathCatalog pathCatalog;
        [SerializeField] private DetailPaintBrushCatalog detailPaintBrushCatalog;
        [SerializeField] private BuildPreview buildPreview;
        [SerializeField] private PathPreviewRenderer pathPreviewRenderer;
        [SerializeField] private PathEditingHandlesRenderer pathEditingHandlesRenderer;
        [SerializeField] private EditDraftSessionController editDraftSessionController;
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private EditorUndoRedoSystem undoRedoSystem;

        [Header("Input")]
        [SerializeField] private KeyCode rotateKey = KeyCode.R;
        [SerializeField] private KeyCode deleteKey = KeyCode.X;
        [SerializeField] private KeyCode moveSelectedKey = KeyCode.M;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private KeyCode objectToolKey = KeyCode.F1;
        [SerializeField] private KeyCode wallToolKey = KeyCode.F2;
        [SerializeField] private KeyCode doorToolKey = KeyCode.F3;
        [SerializeField] private KeyCode windowToolKey = KeyCode.F4;
        [SerializeField] private KeyCode pathToolKey = KeyCode.F6;
        [SerializeField] private KeyCode detailPaintToolKey = KeyCode.F7;
        [SerializeField] private KeyCode saveKey = KeyCode.F5;
        [SerializeField] private KeyCode loadKey = KeyCode.F9;
        [SerializeField] private LayerMask placementRaycastMask = ~0;

        [Header("Placement")]
        [SerializeField] private float stackedSurfacePadding = 0.01f;
        [SerializeField] private float freeRotationSnapAngle = 15f;
        [SerializeField] private float freeRotationMinMovement = 0.025f;

        private int rotationSteps;
        private PlacedObject selectedObject;
        private PendingMoveState pendingMove;
        private BuildToolMode activeTool = BuildToolMode.Object;
        private PlacementPaintMode paintMode = PlacementPaintMode.Single;

        private bool isObjectBrushPainting;
        private bool isObjectRectanglePainting;
        private bool isWallBrushPainting;
        private bool isWallRectanglePainting;
        private bool dragSessionRecordedChange;

        private Vector2Int objectPaintStartCell;
        private Vector2Int objectPaintLastCell;
        private float objectPaintBaseY;

        private WallEdge wallPaintLastEdge;
        private Vector2Int wallRectangleStartCell;

        private bool hasFreeRotationReference;
        private Vector3 lastFreePlacementPoint;
        private float freeRotationY;

        private readonly List<Vector3> activePathPoints = new List<Vector3>();
        private bool isPathDrawing;
        private bool detailPaintStrokeRecordedUndo;
        private bool pathDragDirty;

        private PathStroke selectedPathStroke;
        private int selectedPathPointIndex = -1;
        private int selectedPathWidthSegmentIndex = -1;
        private bool isDraggingPathPoint;
        private bool isDraggingPathWidthHandle;
        [SerializeField] private float pathPointSelectRadius = 0.6f;
        [SerializeField] private float pathWidthStep = 0.25f;
        [SerializeField] private KeyCode increasePathWidthKey = KeyCode.RightBracket;
        [SerializeField] private KeyCode decreasePathWidthKey = KeyCode.LeftBracket;
        [SerializeField] private KeyCode deleteSelectedPathKey = KeyCode.Delete;

        private BuildWorldCommandService buildCommandService;

        public event Action StateChanged;

        public BuildToolMode CurrentTool => pendingMove != null ? BuildToolMode.Move : activeTool;
        public PlacementPaintMode CurrentPaintMode => paintMode;
        public PlacedObject SelectedObject => selectedObject;
        public bool HasPendingMove => pendingMove != null;
        public bool HasSelectedPath => selectedPathStroke != null;
        public PathStroke SelectedPathStroke => selectedPathStroke;
        public bool HasActiveEditDraft => editDraftSessionController != null && editDraftSessionController.HasActiveDraft;

        private void Awake()
        {
            RebuildCommandService();
            UpdateCommandServiceDraftMode();
        }

        private void Start()
        {
            RefreshPreviewDefinition();
            NotifyStateChanged();
        }

        private void Update()
        {
            if (gameModeController == null || gridBuildingSystem == null || buildCatalog == null || buildPreview == null)
            {
                return;
            }

            if (!ReferenceEquals(selectedObject, null) && selectedObject == null)
            {
                selectedObject = null;
                NotifyStateChanged();
            }

            if (!ReferenceEquals(selectedPathStroke, null) && selectedPathStroke == null)
            {
                selectedPathStroke = null;
                selectedPathPointIndex = -1;
                selectedPathWidthSegmentIndex = -1;
                isDraggingPathPoint = false;
                isDraggingPathWidthHandle = false;
                pathEditingHandlesRenderer?.Hide();
                NotifyStateChanged();
            }

            if (gameModeController.CurrentMode != GameMode.Edit)
            {
                buildPreview.SetVisible(false);
                ResetInteractionStates();
                return;
            }

            HandleGlobalInput();

            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                case BuildToolMode.Door:
                case BuildToolMode.Window:
                    HandleWallTool();
                    break;
                case BuildToolMode.Path:
                    HandlePathTool();
                    break;
                case BuildToolMode.DetailPaint:
                    HandleDetailPaintTool();
                    break;
                default:
                    HandleObjectTool();
                    break;
            }
        }

        public void SetTool(BuildToolMode tool)
        {
            if (tool == BuildToolMode.Move)
            {
                return;
            }

            if (pendingMove != null && tool != BuildToolMode.Object)
            {
                CancelPendingMoveAndRestore();
            }

            activeTool = tool;
            ResetInteractionStates();

            if (tool != BuildToolMode.Object)
            {
                DeselectCurrentObject();
            }

            if (tool != BuildToolMode.Path && tool != BuildToolMode.DetailPaint)
            {
                DeselectPathStroke();
            }

            pathDragDirty = false;

            RefreshPreviewDefinition();
            NotifyStateChanged();
        }

        public void SetLayer(BuildLayer layer)
        {
            if (pendingMove != null)
            {
                CancelPendingMoveAndRestore();
            }

            buildCatalog.SetLayer(layer);
            activeTool = BuildToolMode.Object;
            ResetInteractionStates();
            RefreshPreviewDefinition();
            NotifyStateChanged();
        }

        public void SetPaintMode(PlacementPaintMode mode)
        {
            if (paintMode == mode)
            {
                return;
            }

            paintMode = mode;
            ResetDragStates();
            NotifyStateChanged();
        }

        public void ResetTransientEditorState()
        {
            DeselectCurrentObject();
            DeselectPathStroke();
            pendingMove = null;
            rotationSteps = 0;
            activeTool = BuildToolMode.Object;
            paintMode = PlacementPaintMode.Single;
            ResetInteractionStates();
            RefreshPreviewDefinition();
            if (buildPreview != null)
            {
                buildPreview.SetVisible(false);
            }
            NotifyStateChanged();
        }

        private void RebuildCommandService()
        {
            buildCommandService = new BuildWorldCommandService(
                gridBuildingSystem,
                wallSystem,
                pathSystem,
                mapSaveSystem,
                undoRedoSystem);
        }

        private void UpdateCommandServiceDraftMode()
        {
            if (buildCommandService != null)
            {
                buildCommandService.SuppressBuildVersionTracking = HasActiveEditDraft;
            }
        }

        private void EnsureDraftSessionStarted()
        {
            if (editDraftSessionController != null && !editDraftSessionController.HasActiveDraft)
            {
                editDraftSessionController.BeginDraftSession();
            }

            UpdateCommandServiceDraftMode();
        }

        private void TouchBuildVersionIfNotDraft()
        {
            if (!HasActiveEditDraft)
            {
                mapSaveSystem?.IncrementBuildVersion();
            }
        }

        public bool BeginEditDraftSession()
        {
            if (editDraftSessionController == null)
            {
                return false;
            }

            if (!editDraftSessionController.HasActiveDraft)
            {
                editDraftSessionController.BeginDraftSession();
            }

            UpdateCommandServiceDraftMode();
            NotifyStateChanged();
            return editDraftSessionController.HasActiveDraft;
        }

        public WorldPatch ApplyEditDraftSession()
        {
            if (editDraftSessionController == null || !editDraftSessionController.HasActiveDraft)
            {
                return null;
            }

            WorldPatch patch = editDraftSessionController.ApplyDraftSession();
            UpdateCommandServiceDraftMode();
            NotifyStateChanged();
            return patch;
        }

        public void CancelEditDraftSession()
        {
            if (editDraftSessionController == null || !editDraftSessionController.HasActiveDraft)
            {
                return;
            }

            editDraftSessionController.CancelDraftSession();
            UpdateCommandServiceDraftMode();
            ResetInteractionStates();
            NotifyStateChanged();
        }

        public void ClearEditDraftSession()
        {
            editDraftSessionController?.ClearDraftSession();
            UpdateCommandServiceDraftMode();
            NotifyStateChanged();
        }

        public string GetCurrentSelectionLabel()
        {
            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                    return wallCatalog != null && wallCatalog.CurrentWall != null ? wallCatalog.CurrentWall.SafeDisplayName : "No wall selected";
                case BuildToolMode.Door:
                    return wallCatalog != null && wallCatalog.CurrentDoor != null ? wallCatalog.CurrentDoor.SafeDisplayName : "No door selected";
                case BuildToolMode.Window:
                    return wallCatalog != null && wallCatalog.CurrentWindow != null ? wallCatalog.CurrentWindow.SafeDisplayName : "No window selected";
                case BuildToolMode.Path:
                    if (selectedPathStroke != null)
                    {
                        string pointText = selectedPathPointIndex >= 0 ? $", point {selectedPathPointIndex + 1}" : string.Empty;
                        return $"Path: {selectedPathStroke.Definition.SafeDisplayName} | width {selectedPathStroke.Width:0.##} | {selectedPathStroke.ControlPoints.Count} pts{pointText}";
                    }

                    return pathCatalog != null && pathCatalog.Current != null ? pathCatalog.Current.SafeDisplayName : "No path selected";
                case BuildToolMode.DetailPaint:
                    if (detailPaintBrushCatalog != null && detailPaintBrushCatalog.Current != null)
                    {
                        string targetLabel = selectedPathStroke != null ? $" | Target: {selectedPathStroke.Definition.SafeDisplayName}" : string.Empty;
                        return detailPaintBrushCatalog.Current.SafeDisplayName + targetLabel;
                    }

                    return "No detail brush selected";
                case BuildToolMode.Move:
                    return pendingMove != null && pendingMove.definition != null ? $"Moving: {pendingMove.definition.SafeDisplayName}" : "Move";
                default:
                    return GetCurrentObjectDefinition() != null ? GetCurrentObjectDefinition().SafeDisplayName : "No object selected";
            }
        }

        public string GetCurrentLayerLabel()
        {
            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                case BuildToolMode.Door:
                case BuildToolMode.Window:
                    return "Walls";
                case BuildToolMode.Path:
                    return "Paths";
                case BuildToolMode.DetailPaint:
                    return "Surface Details";
                case BuildToolMode.Move:
                    return pendingMove != null && pendingMove.definition != null ? pendingMove.definition.layer.ToString() : buildCatalog.CurrentLayer.ToString();
                default:
                    return buildCatalog.CurrentLayer.ToString();
            }
        }

        public string GetCurrentPaintModeLabel()
        {
            if (CurrentTool == BuildToolMode.Path)
            {
                return "Polyline";
            }

            if (CurrentTool == BuildToolMode.DetailPaint)
            {
                return "Brush";
            }

            switch (paintMode)
            {
                case PlacementPaintMode.Brush: return "Brush";
                case PlacementPaintMode.Rectangle: return "Rectangle";
                default: return "Single";
            }
        }

        private void HandleGlobalInput()
        {
            if (InputHelper.GetKeyDown(objectToolKey)) SetTool(BuildToolMode.Object);
            if (InputHelper.GetKeyDown(wallToolKey)) SetTool(BuildToolMode.Wall);
            if (InputHelper.GetKeyDown(doorToolKey)) SetTool(BuildToolMode.Door);
            if (InputHelper.GetKeyDown(windowToolKey)) SetTool(BuildToolMode.Window);
            if (InputHelper.GetKeyDown(pathToolKey)) SetTool(BuildToolMode.Path);
            if (InputHelper.GetKeyDown(detailPaintToolKey)) SetTool(BuildToolMode.DetailPaint);

            if (InputHelper.GetKeyDown(saveKey) && buildCommandService != null)
            {
                buildCommandService.SaveWorld();
            }

            if (InputHelper.GetKeyDown(loadKey) && buildCommandService != null)
            {
                ClearEditDraftSession();
                buildCommandService.LoadWorld();
                return;
            }

            HandleSelectionInput();

            if (InputHelper.GetKeyDown(cancelKey))
            {
                if (pendingMove != null)
                {
                    CancelPendingMoveAndRestore();
                }
                else if (isPathDrawing)
                {
                    ResetPathDrawing();
                }
            }
        }

        private void HandleSelectionInput()
        {
            if (pendingMove != null)
            {
                return;
            }

            bool changed = false;
            float scroll = InputHelper.MouseScrollY;
            if (scroll > 0.01f)
            {
                SelectNextForCurrentTool();
                changed = true;
            }
            else if (scroll < -0.01f)
            {
                SelectPreviousForCurrentTool();
                changed = true;
            }

            if (CurrentTool == BuildToolMode.Object)
            {
                if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { buildCatalog.Select(0); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { buildCatalog.Select(1); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { buildCatalog.Select(2); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha4)) { buildCatalog.Select(3); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha5)) { buildCatalog.Select(4); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha6)) { buildCatalog.Select(5); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha7)) { buildCatalog.Select(6); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha8)) { buildCatalog.Select(7); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha9)) { buildCatalog.Select(8); changed = true; }
            }
            else if (CurrentTool == BuildToolMode.Path && pathCatalog != null)
            {
                if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { pathCatalog.Select(0); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { pathCatalog.Select(1); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { pathCatalog.Select(2); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha4)) { pathCatalog.Select(3); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha5)) { pathCatalog.Select(4); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha6)) { pathCatalog.Select(5); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha7)) { pathCatalog.Select(6); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha8)) { pathCatalog.Select(7); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha9)) { pathCatalog.Select(8); changed = true; }
            }
            else if (CurrentTool == BuildToolMode.DetailPaint && detailPaintBrushCatalog != null)
            {
                if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { detailPaintBrushCatalog.Select(0); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { detailPaintBrushCatalog.Select(1); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { detailPaintBrushCatalog.Select(2); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha4)) { detailPaintBrushCatalog.Select(3); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha5)) { detailPaintBrushCatalog.Select(4); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha6)) { detailPaintBrushCatalog.Select(5); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha7)) { detailPaintBrushCatalog.Select(6); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha8)) { detailPaintBrushCatalog.Select(7); changed = true; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha9)) { detailPaintBrushCatalog.Select(8); changed = true; }
            }
            else if (wallCatalog != null)
            {
                if (CurrentTool == BuildToolMode.Wall)
                {
                    if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { wallCatalog.SelectWall(0); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { wallCatalog.SelectWall(1); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { wallCatalog.SelectWall(2); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha4)) { wallCatalog.SelectWall(3); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha5)) { wallCatalog.SelectWall(4); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha6)) { wallCatalog.SelectWall(5); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha7)) { wallCatalog.SelectWall(6); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha8)) { wallCatalog.SelectWall(7); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha9)) { wallCatalog.SelectWall(8); changed = true; }
                }
                else if (CurrentTool == BuildToolMode.Door)
                {
                    if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { wallCatalog.SelectDoor(0); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { wallCatalog.SelectDoor(1); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { wallCatalog.SelectDoor(2); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha4)) { wallCatalog.SelectDoor(3); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha5)) { wallCatalog.SelectDoor(4); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha6)) { wallCatalog.SelectDoor(5); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha7)) { wallCatalog.SelectDoor(6); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha8)) { wallCatalog.SelectDoor(7); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha9)) { wallCatalog.SelectDoor(8); changed = true; }
                }
                else if (CurrentTool == BuildToolMode.Window)
                {
                    if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { wallCatalog.SelectWindow(0); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { wallCatalog.SelectWindow(1); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { wallCatalog.SelectWindow(2); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha4)) { wallCatalog.SelectWindow(3); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha5)) { wallCatalog.SelectWindow(4); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha6)) { wallCatalog.SelectWindow(5); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha7)) { wallCatalog.SelectWindow(6); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha8)) { wallCatalog.SelectWindow(7); changed = true; }
                    if (InputHelper.GetKeyDown(KeyCode.Alpha9)) { wallCatalog.SelectWindow(8); changed = true; }
                }
            }

            if (changed)
            {
                RefreshPreviewDefinition();
                NotifyStateChanged();
            }
        }

        private void HandleObjectTool()
        {
            BuildingDefinition currentDefinition = GetCurrentObjectDefinition();
            buildPreview.SetDefinition(currentDefinition);

            if (!TryGetCursorContext(out CursorContext cursorContext))
            {
                buildPreview.SetVisible(false);
                ResetDragStatesIfMouseReleased();
                ResetFreeRotationTracking();
                return;
            }

            bool hasPreview = TryBuildObjectPlacement(currentDefinition, cursorContext, out ObjectPlacementPreview preview);
            if (hasPreview)
            {
                buildPreview.SetVisible(true);
                buildPreview.UpdatePose(preview.worldPosition, Quaternion.Euler(0f, preview.yRotation, 0f), preview.canPlace);
            }
            else
            {
                buildPreview.SetVisible(false);
            }

            if (HandleObjectPaintInput(currentDefinition, hasPreview, preview))
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (InputHelper.GetMouseButtonDown(0))
            {
                if (ShouldSelectHoveredObject(currentDefinition, cursorContext.hoveredPlacedObject))
                {
                    SelectObject(cursorContext.hoveredPlacedObject);
                }
                else if (hasPreview && preview.canPlace)
                {
                    PlaceObjectFromPreview(currentDefinition, preview);
                }
                else if (cursorContext.hoveredPlacedObject == null)
                {
                    DeselectCurrentObject();
                }

                NotifyStateChanged();
            }

            if (InputHelper.GetKeyDown(deleteKey))
            {
                if (selectedObject != null)
                {
                    EnsureDraftSessionStarted();
                    buildCommandService?.DeletePlacedObject(selectedObject, true, true);
                    selectedObject = null;
                }
                else if (cursorContext.hoveredPlacedObject != null)
                {
                    EnsureDraftSessionStarted();
                    buildCommandService?.DeletePlacedObject(cursorContext.hoveredPlacedObject, true, true);
                }

                NotifyStateChanged();
            }

            if (InputHelper.GetKeyDown(moveSelectedKey) && selectedObject != null)
            {
                StartMovingSelectedObject();
            }

            if (InputHelper.GetKeyDown(rotateKey))
            {
                if (selectedObject != null && pendingMove == null)
                {
                    RotateSelectedObject();
                }
                else if (currentDefinition != null && !IsFreePlacementModifierHeld())
                {
                    rotationSteps = (rotationSteps + 1) % 4;
                    NotifyStateChanged();
                }
            }
        }

        private void HandleWallTool()
        {
            if (wallSystem == null || wallCatalog == null)
            {
                buildPreview.SetVisible(false);
                ResetDragStatesIfMouseReleased();
                return;
            }

            GameObject previewPrefab = null;
            Vector3 worldOffset = Vector3.zero;
            bool canPlace = false;

            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                    if (wallCatalog.CurrentWall != null)
                    {
                        previewPrefab = wallCatalog.CurrentWall.prefab;
                        worldOffset = wallCatalog.CurrentWall.worldOffset;
                    }
                    break;
                case BuildToolMode.Door:
                    if (wallCatalog.CurrentDoor != null)
                    {
                        previewPrefab = wallCatalog.CurrentDoor.prefab;
                        worldOffset = wallCatalog.CurrentDoor.worldOffset;
                    }
                    break;
                case BuildToolMode.Window:
                    if (wallCatalog.CurrentWindow != null)
                    {
                        previewPrefab = wallCatalog.CurrentWindow.prefab;
                        worldOffset = wallCatalog.CurrentWindow.worldOffset;
                    }
                    break;
            }

            buildPreview.SetPrefab(previewPrefab);

            if (!TryGetMouseWorldPosition(out Vector3 mouseWorldPosition) || !wallSystem.TryGetNearestEdge(mouseWorldPosition, out WallEdge edge, out Vector3 edgePosition, out Quaternion edgeRotation))
            {
                buildPreview.SetVisible(false);
                ResetDragStatesIfMouseReleased();
                return;
            }

            if (previewPrefab != null)
            {
                switch (CurrentTool)
                {
                    case BuildToolMode.Wall:
                        canPlace = wallSystem.CanPlaceWall(wallCatalog.CurrentWall, edge);
                        break;
                    case BuildToolMode.Door:
                        canPlace = wallSystem.CanPlaceOpening(wallCatalog.CurrentDoor, edge);
                        break;
                    case BuildToolMode.Window:
                        canPlace = wallSystem.CanPlaceOpening(wallCatalog.CurrentWindow, edge);
                        break;
                }

                buildPreview.SetVisible(true);
                buildPreview.UpdatePose(edgePosition + edgeRotation * worldOffset, edgeRotation, canPlace);
            }
            else
            {
                buildPreview.SetVisible(false);
            }

            if (HandleWallPaintInput(mouseWorldPosition, edge, canPlace))
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (InputHelper.GetMouseButtonDown(0) && canPlace)
            {
                EnsureDraftSessionStarted();
                PlaceWallToolAtEdge(edge);
            }

            if (InputHelper.GetKeyDown(deleteKey))
            {
                EnsureDraftSessionStarted();
                buildCommandService?.RemoveWallAtEdge(edge, true);
            }
        }

        private bool TryBuildObjectPlacement(BuildingDefinition definition, CursorContext cursorContext, out ObjectPlacementPreview preview)
        {
            preview = default;
            if (definition == null)
            {
                ResetFreeRotationTracking();
                return false;
            }

            if (definition.IsWallMounted)
            {
                ResetFreeRotationTracking();
                return TryBuildWallMountedPlacement(definition, cursorContext, out preview);
            }

            Vector3 anchorPoint = cursorContext.hasPhysicsHit ? cursorContext.physicsHit.point : cursorContext.planePoint;
            float baseY = gridBuildingSystem.GridOrigin.y;
            if (ShouldUseSurfacePlacement(definition, cursorContext.hoveredPlacedObject))
            {
                baseY = cursorContext.hoveredPlacedObject.GetTopY() + stackedSurfacePadding;
            }

            bool preserveFreePlacement = pendingMove != null && !pendingMove.useGridPlacement;
            bool useFreePlacement = definition.allowAltFreePlacement && (IsFreePlacementModifierHeld() || preserveFreePlacement);
            preview.useGridPlacement = !useFreePlacement;
            preview.baseY = baseY;

            if (useFreePlacement)
            {
                preview.originCell = gridBuildingSystem.WorldToCell(anchorPoint, definition.layer);
                preview.worldPosition = new Vector3(anchorPoint.x, baseY, anchorPoint.z) + definition.worldOffset;
                preview.yRotation = UpdateAndGetFreeRotation(anchorPoint, pendingMove != null ? pendingMove.rotationY : GetSnappedRotationY(rotationSteps));
                preview.canPlace = true;
                return true;
            }

            ResetFreeRotationTracking();
            Vector2Int rotatedFootprint = GridBuildingSystem.RotateFootprint(definition.Footprint, rotationSteps);
            preview.originCell = gridBuildingSystem.WorldToCell(anchorPoint, definition.layer);
            preview.worldPosition = gridBuildingSystem.GetPlacementPosition(preview.originCell, rotatedFootprint, definition.worldOffset, definition.layer, baseY);
            preview.yRotation = GetSnappedRotationY(rotationSteps);
            preview.canPlace = gridBuildingSystem.CanPlace(definition, preview.originCell, rotationSteps, baseY);
            return true;
        }

        private bool TryBuildWallMountedPlacement(BuildingDefinition definition, CursorContext cursorContext, out ObjectPlacementPreview preview)
        {
            preview = default;
            if (wallSystem == null)
            {
                return false;
            }

            Vector3 sourcePoint = cursorContext.hasPhysicsHit ? cursorContext.physicsHit.point : cursorContext.planePoint;
            if (!wallSystem.TryGetNearestEdge(sourcePoint, out WallEdge edge, out Vector3 edgePosition, out Quaternion edgeRotation))
            {
                return false;
            }

            WallSegment segment = wallSystem.GetSegment(edge);
            preview.useGridPlacement = false;
            preview.originCell = gridBuildingSystem.WorldToCell(edgePosition, definition.layer);
            preview.worldPosition = edgePosition + edgeRotation * definition.worldOffset;
            preview.baseY = preview.worldPosition.y - definition.worldOffset.y;
            preview.yRotation = edgeRotation.eulerAngles.y;
            preview.canPlace = segment != null && segment.WallDefinition != null && segment.OpeningDefinition == null;
            return true;
        }

        private bool HandleObjectPaintInput(BuildingDefinition definition, bool hasPreview, ObjectPlacementPreview preview)
        {
            if (!CanUseObjectPaintMode(definition, hasPreview))
            {
                ResetObjectPaintIfMouseReleased();
                return false;
            }

            if (IsPointerOverUi())
            {
                ResetObjectPaintIfMouseReleased();
                return false;
            }

            switch (paintMode)
            {
                case PlacementPaintMode.Brush:
                    return HandleObjectBrushPaint(definition, preview);
                case PlacementPaintMode.Rectangle:
                    return HandleObjectRectanglePaint(definition, preview);
                default:
                    ResetObjectPaintIfMouseReleased();
                    return false;
            }
        }

        private bool HandleWallPaintInput(Vector3 mouseWorldPosition, WallEdge currentEdge, bool canPlaceCurrentEdge)
        {
            if (CurrentTool != BuildToolMode.Wall || wallCatalog == null || wallCatalog.CurrentWall == null)
            {
                ResetWallPaintIfMouseReleased();
                return false;
            }

            if (IsPointerOverUi())
            {
                ResetWallPaintIfMouseReleased();
                return false;
            }

            switch (paintMode)
            {
                case PlacementPaintMode.Brush:
                    return HandleWallBrushPaint(currentEdge, canPlaceCurrentEdge);
                case PlacementPaintMode.Rectangle:
                    return HandleWallRectanglePaint(gridBuildingSystem.WorldToCell(mouseWorldPosition, BuildLayer.Furniture));
                default:
                    ResetWallPaintIfMouseReleased();
                    return false;
            }
        }

        private bool HandleObjectBrushPaint(BuildingDefinition definition, ObjectPlacementPreview preview)
        {
            if (InputHelper.GetMouseButtonDown(0))
            {
                isObjectBrushPainting = true;
                dragSessionRecordedChange = false;
                objectPaintStartCell = preview.originCell;
                objectPaintLastCell = preview.originCell;
                objectPaintBaseY = preview.baseY;
                TryPaintGridCell(definition, preview.originCell, objectPaintBaseY);
                return true;
            }

            if (isObjectBrushPainting && InputHelper.GetMouseButton(0))
            {
                PaintObjectLine(definition, objectPaintLastCell, preview.originCell, objectPaintBaseY);
                objectPaintLastCell = preview.originCell;
                return true;
            }

            if (InputHelper.GetMouseButtonUp(0))
            {
                isObjectBrushPainting = false;
                dragSessionRecordedChange = false;
                return true;
            }

            return isObjectBrushPainting;
        }

        private bool HandleObjectRectanglePaint(BuildingDefinition definition, ObjectPlacementPreview preview)
        {
            if (InputHelper.GetMouseButtonDown(0))
            {
                isObjectRectanglePainting = true;
                dragSessionRecordedChange = false;
                objectPaintStartCell = preview.originCell;
                objectPaintBaseY = preview.baseY;
                return true;
            }

            if (isObjectRectanglePainting && InputHelper.GetMouseButtonUp(0))
            {
                PaintObjectRectangle(definition, objectPaintStartCell, preview.originCell, objectPaintBaseY);
                isObjectRectanglePainting = false;
                dragSessionRecordedChange = false;
                return true;
            }

            return isObjectRectanglePainting;
        }

        private bool HandleWallBrushPaint(WallEdge currentEdge, bool canPlaceCurrentEdge)
        {
            if (InputHelper.GetMouseButtonDown(0))
            {
                isWallBrushPainting = true;
                dragSessionRecordedChange = false;
                wallPaintLastEdge = currentEdge;
                if (canPlaceCurrentEdge)
                {
                    TryPaintWallEdge(currentEdge);
                }
                return true;
            }

            if (isWallBrushPainting && InputHelper.GetMouseButton(0))
            {
                PaintWallLine(wallPaintLastEdge, currentEdge);
                wallPaintLastEdge = currentEdge;
                return true;
            }

            if (InputHelper.GetMouseButtonUp(0))
            {
                isWallBrushPainting = false;
                dragSessionRecordedChange = false;
                return true;
            }

            return isWallBrushPainting;
        }

        private bool HandleWallRectanglePaint(Vector2Int currentCell)
        {
            if (InputHelper.GetMouseButtonDown(0))
            {
                isWallRectanglePainting = true;
                dragSessionRecordedChange = false;
                wallRectangleStartCell = currentCell;
                return true;
            }

            if (isWallRectanglePainting && InputHelper.GetMouseButtonUp(0))
            {
                PaintWallRectangle(wallRectangleStartCell, currentCell);
                isWallRectanglePainting = false;
                dragSessionRecordedChange = false;
                return true;
            }

            return isWallRectanglePainting;
        }

        private void PaintObjectLine(BuildingDefinition definition, Vector2Int from, Vector2Int to, float baseY)
        {
            List<Vector2Int> cells = GetGridLine(from, to);
            for (int i = 0; i < cells.Count; i++)
            {
                TryPaintGridCell(definition, cells[i], baseY);
            }
        }

        private void PaintObjectRectangle(BuildingDefinition definition, Vector2Int from, Vector2Int to, float baseY)
        {
            int minX = Mathf.Min(from.x, to.x);
            int maxX = Mathf.Max(from.x, to.x);
            int minY = Mathf.Min(from.y, to.y);
            int maxY = Mathf.Max(from.y, to.y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    TryPaintGridCell(definition, new Vector2Int(x, y), baseY);
                }
            }
        }

        private void PaintWallLine(WallEdge from, WallEdge to)
        {
            if (from.orientation != to.orientation)
            {
                TryPaintWallEdge(to);
                return;
            }

            if (from.orientation == WallOrientation.Horizontal)
            {
                int fixedY = from.y;
                int startX = Mathf.Min(from.x, to.x);
                int endX = Mathf.Max(from.x, to.x);
                for (int x = startX; x <= endX; x++)
                {
                    TryPaintWallEdge(new WallEdge(x, fixedY, WallOrientation.Horizontal));
                }
            }
            else
            {
                int fixedX = from.x;
                int startY = Mathf.Min(from.y, to.y);
                int endY = Mathf.Max(from.y, to.y);
                for (int y = startY; y <= endY; y++)
                {
                    TryPaintWallEdge(new WallEdge(fixedX, y, WallOrientation.Vertical));
                }
            }
        }

        private void PaintWallRectangle(Vector2Int from, Vector2Int to)
        {
            int minX = Mathf.Min(from.x, to.x);
            int maxX = Mathf.Max(from.x, to.x);
            int minY = Mathf.Min(from.y, to.y);
            int maxY = Mathf.Max(from.y, to.y);

            for (int x = minX; x <= maxX; x++)
            {
                TryPaintWallEdge(new WallEdge(x, minY, WallOrientation.Horizontal));
                TryPaintWallEdge(new WallEdge(x, maxY + 1, WallOrientation.Horizontal));
            }

            for (int y = minY; y <= maxY; y++)
            {
                TryPaintWallEdge(new WallEdge(minX, y, WallOrientation.Vertical));
                TryPaintWallEdge(new WallEdge(maxX + 1, y, WallOrientation.Vertical));
            }
        }

        private void TryPaintGridCell(BuildingDefinition definition, Vector2Int cell, float baseY)
        {
            if (buildCommandService == null)
            {
                return;
            }

            if (!gridBuildingSystem.CanPlace(definition, cell, rotationSteps, baseY))
            {
                return;
            }

            RecordDragChangeIfNeeded();
            buildCommandService.TryPaintGridCell(definition, cell, rotationSteps, baseY, GetSnappedRotationY(rotationSteps));
        }

        private void TryPaintWallEdge(WallEdge edge)
        {
            if (wallCatalog == null || wallCatalog.CurrentWall == null || buildCommandService == null)
            {
                return;
            }

            if (!wallSystem.CanPlaceWall(wallCatalog.CurrentWall, edge))
            {
                return;
            }

            RecordDragChangeIfNeeded();
            buildCommandService.TryPaintWallEdge(wallCatalog.CurrentWall, edge);
        }

        private void RecordDragChangeIfNeeded()
        {
            if (dragSessionRecordedChange)
            {
                return;
            }

            EnsureDraftSessionStarted();
            buildCommandService?.RecordUndoState();
            dragSessionRecordedChange = true;
        }

        private void PlaceObjectFromPreview(BuildingDefinition definition, ObjectPlacementPreview preview)
        {
            bool isMovePlacement = pendingMove != null;
            if (isMovePlacement)
            {
                if (pendingMove != null && !string.IsNullOrWhiteSpace(pendingMove.undoSnapshotJson))
                {
                    buildCommandService?.RecordUndoSnapshot(pendingMove.undoSnapshotJson);
                }
            }

            EnsureDraftSessionStarted();

            PlacedObject placedObject = preview.useGridPlacement
                ? buildCommandService != null ? buildCommandService.PlaceGridObject(definition, preview.originCell, rotationSteps, preview.baseY, preview.yRotation, !isMovePlacement) : null
                : buildCommandService != null ? buildCommandService.PlaceFreeObject(definition, preview.worldPosition, rotationSteps, preview.yRotation, !isMovePlacement) : null;

            if (placedObject == null)
            {
                return;
            }

            pendingMove = null;
            activeTool = BuildToolMode.Object;
            SelectObject(placedObject);
            rotationSteps = placedObject.RotationSteps;
            ResetFreeRotationTracking();
            RefreshPreviewDefinition();
        }

        private void PlaceWallToolAtEdge(WallEdge edge)
        {
            if (buildCommandService == null)
            {
                return;
            }

            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                    buildCommandService.PlaceWall(wallCatalog.CurrentWall, edge, true);
                    break;
                case BuildToolMode.Door:
                    buildCommandService.PlaceOpening(wallCatalog.CurrentDoor, edge, true);
                    break;
                case BuildToolMode.Window:
                    buildCommandService.PlaceOpening(wallCatalog.CurrentWindow, edge, true);
                    break;
            }
        }

        private void StartMovingSelectedObject()
        {
            if (selectedObject == null || selectedObject.Definition == null)
            {
                return;
            }

            EnsureDraftSessionStarted();
            string undoSnapshotJson = mapSaveSystem != null ? mapSaveSystem.CaptureCurrentStateJson() : null;

            pendingMove = new PendingMoveState
            {
                definition = selectedObject.Definition,
                originCell = selectedObject.OriginCell,
                rotationSteps = selectedObject.RotationSteps,
                rotationY = selectedObject.RotationY,
                useGridPlacement = selectedObject.UsesGridPlacement,
                baseY = selectedObject.BaseY,
                worldPosition = selectedObject.transform.position,
                undoSnapshotJson = undoSnapshotJson
            };

            rotationSteps = pendingMove.rotationSteps;
            buildCatalog.Select(selectedObject.Definition);
            gridBuildingSystem.Remove(selectedObject);
            selectedObject = null;
            activeTool = BuildToolMode.Object;
            ResetInteractionStates();
            RefreshPreviewDefinition();
            NotifyStateChanged();
        }

        private void CancelPendingMoveAndRestore()
        {
            if (pendingMove == null)
            {
                return;
            }

            PlacedObject restoredObject = pendingMove.useGridPlacement
                ? gridBuildingSystem.Place(pendingMove.definition, pendingMove.originCell, pendingMove.rotationSteps, pendingMove.baseY, pendingMove.rotationY)
                : gridBuildingSystem.PlaceFree(pendingMove.definition, pendingMove.worldPosition, pendingMove.rotationSteps, pendingMove.rotationY);

            pendingMove = null;
            rotationSteps = 0;
            activeTool = BuildToolMode.Object;
            ResetInteractionStates();
            RefreshPreviewDefinition();

            if (restoredObject != null)
            {
                SelectObject(restoredObject);
            }

            NotifyStateChanged();
        }

        private void RotateSelectedObject()
        {
            if (selectedObject == null)
            {
                return;
            }

            EnsureDraftSessionStarted();
            int newRotation = (selectedObject.RotationSteps + 1) % 4;
            float newRotationY = GetSnappedRotationY(newRotation);
            undoRedoSystem?.RecordStateBeforeChange();

            bool rotated = selectedObject.UsesGridPlacement
                ? gridBuildingSystem.TryReposition(selectedObject, selectedObject.OriginCell, newRotation, selectedObject.BaseY, newRotationY)
                : gridBuildingSystem.TryRepositionFree(selectedObject, selectedObject.transform.position, newRotation, newRotationY);

            if (rotated)
            {
                rotationSteps = newRotation;
                TouchBuildVersionIfNotDraft();
                NotifyStateChanged();
            }
        }

        private void HandlePathTool()
        {
            if (pathSystem == null)
            {
                buildPreview.SetVisible(false);
                if (pathPreviewRenderer != null)
                {
                    pathPreviewRenderer.SetVisible(false);
                }
                pathEditingHandlesRenderer?.Hide();
                return;
            }

            buildPreview.SetVisible(false);

            bool hasCursor = TryGetCursorContext(out CursorContext cursorContext);
            PathDefinition currentPath = pathCatalog != null ? pathCatalog.Current : null;
            float previewWidth = selectedPathStroke != null ? selectedPathStroke.Width : currentPath != null ? currentPath.defaultWidth : 1f;
            float previewYOffset = currentPath != null ? currentPath.yOffset : 0.02f;
            Vector3 currentPoint = hasCursor ? cursorContext.planePoint + Vector3.up * previewYOffset : Vector3.zero;

            if (pathPreviewRenderer != null && isPathDrawing && activePathPoints.Count > 0 && hasCursor)
            {
                pathPreviewRenderer.UpdatePreview(activePathPoints, currentPoint, previewWidth);
            }
            else if (pathPreviewRenderer != null)
            {
                pathPreviewRenderer.SetVisible(false);
            }

            PathHandleMarker hoveredHandleMarker = hasCursor && cursorContext.hoveredPathHandleMarker != null && cursorContext.hoveredPathHandleMarker.TargetStroke == selectedPathStroke
                ? cursorContext.hoveredPathHandleMarker
                : null;

            if (selectedPathStroke != null)
            {
                pathEditingHandlesRenderer?.Show(selectedPathStroke, selectedPathPointIndex, hoveredHandleMarker, selectedPathWidthSegmentIndex);
            }
            else
            {
                pathEditingHandlesRenderer?.Hide();
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (selectedPathStroke != null)
            {
                HandleSelectedPathShortcuts();
            }

            if (hasCursor && selectedPathStroke != null)
            {
                if (isDraggingPathPoint && selectedPathPointIndex >= 0)
                {
                    Vector3 draggedPoint = new Vector3(cursorContext.planePoint.x, selectedPathStroke.ControlPoints[selectedPathPointIndex].y, cursorContext.planePoint.z);
                    selectedPathStroke.SetControlPoint(selectedPathPointIndex, draggedPoint);
                    pathDragDirty = true;
                    pathSystem.NotifyStrokeChanged();
                    pathEditingHandlesRenderer?.Show(selectedPathStroke, selectedPathPointIndex, hoveredHandleMarker, selectedPathWidthSegmentIndex);
                }
                else if (isDraggingPathWidthHandle)
                {
                    float newWidth = selectedPathStroke.CalculateWidthFromSegmentHandlePosition(selectedPathWidthSegmentIndex, cursorContext.planePoint);
                    if (InputHelper.GetKey(KeyCode.LeftShift) || InputHelper.GetKey(KeyCode.RightShift))
                    {
                        newWidth = Mathf.Round(newWidth / pathWidthStep) * pathWidthStep;
                    }

                    selectedPathStroke.SetWidth(Mathf.Max(0.1f, newWidth));
                    pathDragDirty = true;
                    pathSystem.NotifyStrokeChanged();
                    pathEditingHandlesRenderer?.Show(selectedPathStroke, selectedPathPointIndex, hoveredHandleMarker, selectedPathWidthSegmentIndex);
                }
            }

            if (InputHelper.GetMouseButtonUp(0))
            {
                if ((isDraggingPathPoint || isDraggingPathWidthHandle) && pathDragDirty)
                {
                    mapSaveSystem?.IncrementBuildVersion();
                }

                isDraggingPathPoint = false;
                isDraggingPathWidthHandle = false;
                selectedPathWidthSegmentIndex = -1;
                pathDragDirty = false;
            }

            if (InputHelper.GetMouseButtonDown(0) && hasCursor)
            {
                if (isPathDrawing)
                {
                    if (activePathPoints.Count == 0 || Vector3.Distance(activePathPoints[activePathPoints.Count - 1], currentPoint) > 0.05f)
                    {
                        activePathPoints.Add(currentPoint);
                    }

                    if (pathPreviewRenderer != null)
                    {
                        pathPreviewRenderer.UpdatePreview(activePathPoints, currentPoint, previewWidth);
                    }
                }
                else
                {
                    PathHandleMarker hoveredHandle = cursorContext.hoveredPathHandleMarker;
                    if (hoveredHandle != null && hoveredHandle.TargetStroke != null)
                    {
                        SelectPathStroke(hoveredHandle.TargetStroke);

                        if (hoveredHandle.HandleType == PathHandleType.ControlPoint)
                        {
                            EnsureDraftSessionStarted();
                            selectedPathPointIndex = hoveredHandle.HandleIndex;
                            selectedPathWidthSegmentIndex = -1;
                            isDraggingPathPoint = true;
                            isDraggingPathWidthHandle = false;
                            undoRedoSystem?.RecordStateBeforeChange();
                        }
                        else if (hoveredHandle.HandleType == PathHandleType.InsertPoint)
                        {
                            if (selectedPathStroke != null && selectedPathStroke.TryGetSegmentMidpoint(hoveredHandle.HandleIndex, out Vector3 midpoint))
                            {
                                EnsureDraftSessionStarted();
                                if (buildCommandService != null && buildCommandService.InsertPathPoint(selectedPathStroke, hoveredHandle.HandleIndex, midpoint, out int insertedIndex, true))
                                {
                                    selectedPathPointIndex = insertedIndex;
                                    selectedPathWidthSegmentIndex = -1;
                                    isDraggingPathPoint = true;
                                    isDraggingPathWidthHandle = false;
                                }
                            }
                        }
                        else if (hoveredHandle.HandleType == PathHandleType.Width)
                        {
                            EnsureDraftSessionStarted();
                            selectedPathPointIndex = -1;
                            selectedPathWidthSegmentIndex = hoveredHandle.HandleIndex;
                            isDraggingPathPoint = false;
                            isDraggingPathWidthHandle = true;
                            undoRedoSystem?.RecordStateBeforeChange();
                        }
                    }
                    else if (cursorContext.hoveredPathStroke != null)
                    {
                        SelectPathStroke(cursorContext.hoveredPathStroke);
                        selectedPathPointIndex = -1;
                        selectedPathWidthSegmentIndex = -1;
                    }
                    else if (currentPath != null)
                    {
                        DeselectPathStroke();
                        activePathPoints.Clear();
                        activePathPoints.Add(currentPoint);
                        isPathDrawing = true;
                    }
                    else
                    {
                        DeselectPathStroke();
                    }
                }

                NotifyStateChanged();
            }

            if (InputHelper.GetKeyDown(KeyCode.Return))
            {
                FinalizeOrCancelPath();
            }

            if (InputHelper.GetKeyDown(KeyCode.Backspace))
            {
                if (isPathDrawing && activePathPoints.Count > 0)
                {
                    activePathPoints.RemoveAt(activePathPoints.Count - 1);
                    if (activePathPoints.Count == 0)
                    {
                        ResetPathDrawing();
                    }
                }
                else if (selectedPathStroke != null && selectedPathPointIndex >= 0)
                {
                    RemoveSelectedPathPoint();
                }
            }
        }

        private void HandleSelectedPathShortcuts()
        {
            if (selectedPathStroke == null)
            {
                return;
            }

            if (InputHelper.GetKeyDown(increasePathWidthKey))
            {
                EnsureDraftSessionStarted();
                buildCommandService?.UpdatePathWidth(selectedPathStroke, selectedPathStroke.Width + pathWidthStep, true);
                NotifyStateChanged();
            }

            if (InputHelper.GetKeyDown(decreasePathWidthKey))
            {
                EnsureDraftSessionStarted();
                buildCommandService?.UpdatePathWidth(selectedPathStroke, Mathf.Max(0.1f, selectedPathStroke.Width - pathWidthStep), true);
                NotifyStateChanged();
            }

            if (InputHelper.GetKeyDown(deleteSelectedPathKey) || InputHelper.GetKeyDown(deleteKey))
            {
                if (selectedPathPointIndex >= 0)
                {
                    RemoveSelectedPathPoint();
                }
                else
                {
                    DeleteSelectedPath();
                }
            }
        }

        private void HandleDetailPaintTool()
        {
            buildPreview.SetVisible(false);
            pathEditingHandlesRenderer?.Hide();
            if (pathPreviewRenderer != null)
            {
                pathPreviewRenderer.SetVisible(false);
            }

            if (detailPaintBrushCatalog == null || detailPaintBrushCatalog.Current == null)
            {
                detailPaintStrokeRecordedUndo = false;
                return;
            }

            if (!TryGetCursorContext(out CursorContext cursorContext))
            {
                detailPaintStrokeRecordedUndo = false;
                return;
            }

            if (IsPointerOverUi())
            {
                if (InputHelper.GetMouseButtonUp(0) || InputHelper.GetMouseButtonUp(1) || (!InputHelper.GetMouseButton(0) && !InputHelper.GetMouseButton(1)))
                {
                    detailPaintStrokeRecordedUndo = false;
                }
                return;
            }

            if (cursorContext.hasPhysicsHit)
            {
                DetailPaintableSurface surface = cursorContext.physicsHit.collider != null
                    ? cursorContext.physicsHit.collider.GetComponentInParent<DetailPaintableSurface>()
                    : null;

                if (surface != null)
                {
                    if (selectedPathStroke != null)
                    {
                        IPaintSurfaceOwner owner = surface.GetComponentInParent<IPaintSurfaceOwner>();
                        if (!ReferenceEquals(owner, selectedPathStroke))
                        {
                            if (InputHelper.GetMouseButtonUp(0) || InputHelper.GetMouseButtonUp(1) || (!InputHelper.GetMouseButton(0) && !InputHelper.GetMouseButton(1)))
                            {
                                detailPaintStrokeRecordedUndo = false;
                            }
                            return;
                        }
                    }

                    bool paint = InputHelper.GetMouseButton(0);
                    bool erase = InputHelper.GetMouseButton(1);
                    if ((paint || erase) && !detailPaintStrokeRecordedUndo)
                    {
                        EnsureDraftSessionStarted();
                        buildCommandService?.RecordUndoState();
                        detailPaintStrokeRecordedUndo = true;
                    }

                    if (paint)
                    {
                        buildCommandService?.PaintDetail(surface, cursorContext.physicsHit, detailPaintBrushCatalog.Current, false, false);
                    }
                    else if (erase)
                    {
                        buildCommandService?.PaintDetail(surface, cursorContext.physicsHit, detailPaintBrushCatalog.Current, true, false);
                    }
                }
            }

            if (InputHelper.GetMouseButtonUp(0) || InputHelper.GetMouseButtonUp(1) || (!InputHelper.GetMouseButton(0) && !InputHelper.GetMouseButton(1)))
            {
                detailPaintStrokeRecordedUndo = false;
            }
        }

        private void FinalizeOrCancelPath()
        {
            if (!isPathDrawing)
            {
                return;
            }

            if (pathPreviewRenderer != null)
            {
                pathPreviewRenderer.SetVisible(false);
            }

            if (activePathPoints.Count >= 2 && pathCatalog != null && pathCatalog.Current != null)
            {
                EnsureDraftSessionStarted();
                PathStroke createdStroke = buildCommandService != null
                    ? buildCommandService.CreatePath(pathCatalog.Current, activePathPoints, pathCatalog.Current.defaultWidth, true)
                    : null;
                if (createdStroke != null)
                {
                    SelectPathStroke(createdStroke);
                }
            }

            ResetPathDrawing();
            NotifyStateChanged();
        }

        private void ResetPathDrawing()
        {
            isPathDrawing = false;
            activePathPoints.Clear();
            if (pathPreviewRenderer != null)
            {
                pathPreviewRenderer.SetVisible(false);
            }
        }

        public void DeleteSelectedPath()
        {
            if (selectedPathStroke == null)
            {
                return;
            }

            EnsureDraftSessionStarted();
            PathStroke pathToRemove = selectedPathStroke;
            DeselectPathStroke();
            buildCommandService?.DeletePath(pathToRemove, true);
            NotifyStateChanged();
        }

        private void SelectPathStroke(PathStroke stroke)
        {
            if (selectedPathStroke == stroke)
            {
                return;
            }

            DeselectPathStroke();
            selectedPathStroke = stroke;
            selectedPathPointIndex = -1;
            selectedPathWidthSegmentIndex = -1;

            if (selectedPathStroke != null)
            {
                selectedPathStroke.SetSelected(true);
                pathEditingHandlesRenderer?.Show(selectedPathStroke, selectedPathPointIndex, null, selectedPathWidthSegmentIndex);
            }
        }

        private void DeselectPathStroke()
        {
            if (selectedPathStroke != null)
            {
                selectedPathStroke.SetSelected(false);
            }

            selectedPathStroke = null;
            selectedPathPointIndex = -1;
            selectedPathWidthSegmentIndex = -1;
            isDraggingPathPoint = false;
            isDraggingPathWidthHandle = false;
            pathEditingHandlesRenderer?.Hide();
        }

        private void RemoveSelectedPathPoint()
        {
            if (selectedPathStroke == null || selectedPathPointIndex < 0 || buildCommandService == null)
            {
                return;
            }

            EnsureDraftSessionStarted();
            selectedPathWidthSegmentIndex = -1;
            PathStroke stroke = selectedPathStroke;
            int pointIndex = selectedPathPointIndex;
            bool removed = buildCommandService.RemovePathPoint(stroke, pointIndex, true);
            if (!removed)
            {
                DeselectPathStroke();
            }
            else
            {
                selectedPathPointIndex = Mathf.Clamp(pointIndex, 0, stroke.ControlPoints.Count - 1);
            }

            NotifyStateChanged();
        }

        private bool ShouldSelectHoveredObject(BuildingDefinition currentDefinition, PlacedObject hoveredObject)
        {
            if (hoveredObject == null || pendingMove != null || CurrentTool != BuildToolMode.Object)
            {
                return false;
            }

            BuildLayer activeLayer = currentDefinition != null ? currentDefinition.layer : buildCatalog.CurrentLayer;
            return hoveredObject.Layer == activeLayer;
        }

        private bool ShouldUseSurfacePlacement(BuildingDefinition definition, PlacedObject hoveredObject)
        {
            return definition != null && hoveredObject != null && definition.SupportsSurfacePlacement && hoveredObject != selectedObject;
        }

        private bool TryGetCursorContext(out CursorContext context)
        {
            context = default;
            Camera activeCamera = gameModeController.ActiveCamera;
            if (activeCamera == null)
            {
                return false;
            }

            context.ray = activeCamera.ScreenPointToRay(InputHelper.MousePosition);
            Plane buildPlane = new Plane(Vector3.up, new Vector3(0f, gridBuildingSystem.GridOrigin.y, 0f));
            if (!buildPlane.Raycast(context.ray, out float enterDistance))
            {
                return false;
            }

            context.planePoint = context.ray.GetPoint(enterDistance);
            if (Physics.Raycast(context.ray, out RaycastHit hit, 1000f, placementRaycastMask, QueryTriggerInteraction.Ignore))
            {
                context.hasPhysicsHit = true;
                context.physicsHit = hit;
                context.hoveredPlacedObject = hit.collider != null ? hit.collider.GetComponentInParent<PlacedObject>() : null;
                context.hoveredPathStroke = hit.collider != null ? hit.collider.GetComponentInParent<PathStroke>() : null;
                context.hoveredPathHandleMarker = hit.collider != null ? hit.collider.GetComponent<PathHandleMarker>() : null;
            }

            return true;
        }

        private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
        {
            if (TryGetCursorContext(out CursorContext context))
            {
                worldPosition = context.planePoint;
                return true;
            }

            worldPosition = default;
            return false;
        }

        private BuildingDefinition GetCurrentObjectDefinition()
        {
            return pendingMove != null ? pendingMove.definition : buildCatalog.Current;
        }

        private bool CanUseObjectPaintMode(BuildingDefinition definition, bool hasPreview)
        {
            return paintMode != PlacementPaintMode.Single
                && pendingMove == null
                && definition != null
                && hasPreview
                && definition.placementMode == BuildingPlacementMode.Grid
                && definition.Footprint == Vector2Int.one
                && !definition.IsWallMounted
                && !IsFreePlacementModifierHeld();
        }

        private float GetSnappedRotationY(int steps)
        {
            return ((steps % 4) + 4) % 4 * 90f;
        }

        private float UpdateAndGetFreeRotation(Vector3 currentWorldPoint, float fallbackRotation)
        {
            if (!hasFreeRotationReference)
            {
                hasFreeRotationReference = true;
                lastFreePlacementPoint = currentWorldPoint;
                freeRotationY = fallbackRotation;
            }
            else
            {
                Vector3 delta = currentWorldPoint - lastFreePlacementPoint;
                delta.y = 0f;
                if (delta.sqrMagnitude >= freeRotationMinMovement * freeRotationMinMovement)
                {
                    freeRotationY = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    lastFreePlacementPoint = currentWorldPoint;
                }
            }

            float result = freeRotationY;
            if (InputHelper.GetKey(KeyCode.LeftShift) || InputHelper.GetKey(KeyCode.RightShift))
            {
                float snap = Mathf.Max(1f, freeRotationSnapAngle);
                result = Mathf.Round(result / snap) * snap;
            }

            return result;
        }

        private bool IsFreePlacementModifierHeld()
        {
            return InputHelper.GetKey(KeyCode.LeftAlt) || InputHelper.GetKey(KeyCode.RightAlt);
        }

        private void SelectNextForCurrentTool()
        {
            if (CurrentTool == BuildToolMode.Object || CurrentTool == BuildToolMode.Move)
            {
                buildCatalog.SelectNext();
                return;
            }

            if (CurrentTool == BuildToolMode.Path && pathCatalog != null)
            {
                pathCatalog.SelectNext();
                return;
            }

            if (CurrentTool == BuildToolMode.DetailPaint && detailPaintBrushCatalog != null)
            {
                detailPaintBrushCatalog.SelectNext();
                return;
            }

            if (wallCatalog == null)
            {
                return;
            }

            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                    wallCatalog.SelectNextWall();
                    break;
                case BuildToolMode.Door:
                    wallCatalog.SelectNextDoor();
                    break;
                case BuildToolMode.Window:
                    wallCatalog.SelectNextWindow();
                    break;
            }
        }

        private void SelectPreviousForCurrentTool()
        {
            if (CurrentTool == BuildToolMode.Object || CurrentTool == BuildToolMode.Move)
            {
                buildCatalog.SelectPrevious();
                return;
            }

            if (CurrentTool == BuildToolMode.Path && pathCatalog != null)
            {
                pathCatalog.SelectPrevious();
                return;
            }

            if (CurrentTool == BuildToolMode.DetailPaint && detailPaintBrushCatalog != null)
            {
                detailPaintBrushCatalog.SelectPrevious();
                return;
            }

            if (wallCatalog == null)
            {
                return;
            }

            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                    wallCatalog.SelectPreviousWall();
                    break;
                case BuildToolMode.Door:
                    wallCatalog.SelectPreviousDoor();
                    break;
                case BuildToolMode.Window:
                    wallCatalog.SelectPreviousWindow();
                    break;
            }
        }

        private void SelectObject(PlacedObject placedObject)
        {
            if (selectedObject == placedObject)
            {
                return;
            }

            DeselectCurrentObject();
            selectedObject = placedObject;

            if (selectedObject != null)
            {
                selectedObject.SetSelected(true);
                buildCatalog.Select(selectedObject.Definition);
                rotationSteps = selectedObject.RotationSteps;
            }

            NotifyStateChanged();
        }

        private void DeselectCurrentObject()
        {
            bool hadSelection = !ReferenceEquals(selectedObject, null);
            if (selectedObject != null)
            {
                selectedObject.SetSelected(false);
            }

            selectedObject = null;

            if (hadSelection)
            {
                NotifyStateChanged();
            }
        }

        private void RefreshPreviewDefinition()
        {
            if (buildPreview == null)
            {
                return;
            }

            switch (CurrentTool)
            {
                case BuildToolMode.Wall:
                    buildPreview.SetPrefab(wallCatalog != null && wallCatalog.CurrentWall != null ? wallCatalog.CurrentWall.prefab : null);
                    break;
                case BuildToolMode.Door:
                    buildPreview.SetPrefab(wallCatalog != null && wallCatalog.CurrentDoor != null ? wallCatalog.CurrentDoor.prefab : null);
                    break;
                case BuildToolMode.Window:
                    buildPreview.SetPrefab(wallCatalog != null && wallCatalog.CurrentWindow != null ? wallCatalog.CurrentWindow.prefab : null);
                    break;
                case BuildToolMode.Path:
                case BuildToolMode.DetailPaint:
                    buildPreview.SetPrefab(null);
                    break;
                default:
                    buildPreview.SetDefinition(GetCurrentObjectDefinition());
                    break;
            }
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void ResetInteractionStates()
        {
            ResetDragStates();
            ResetFreeRotationTracking();
            ResetPathDrawing();
            pathEditingHandlesRenderer?.Hide();
            isDraggingPathPoint = false;
            isDraggingPathWidthHandle = false;
            pathDragDirty = false;
            detailPaintStrokeRecordedUndo = false;
        }

        private void ResetDragStates()
        {
            isObjectBrushPainting = false;
            isObjectRectanglePainting = false;
            isWallBrushPainting = false;
            isWallRectanglePainting = false;
            dragSessionRecordedChange = false;
        }

        private void ResetObjectPaintIfMouseReleased()
        {
            if (InputHelper.GetMouseButtonUp(0) || !InputHelper.GetMouseButton(0))
            {
                isObjectBrushPainting = false;
                isObjectRectanglePainting = false;
                dragSessionRecordedChange = false;
            }
        }

        private void ResetWallPaintIfMouseReleased()
        {
            if (InputHelper.GetMouseButtonUp(0) || !InputHelper.GetMouseButton(0))
            {
                isWallBrushPainting = false;
                isWallRectanglePainting = false;
                dragSessionRecordedChange = false;
            }
        }

        private void ResetDragStatesIfMouseReleased()
        {
            if (!InputHelper.GetMouseButton(0))
            {
                ResetDragStates();
            }
        }

        private void ResetFreeRotationTracking()
        {
            hasFreeRotationReference = false;
        }

        private List<Vector2Int> GetGridLine(Vector2Int from, Vector2Int to)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                result.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return result;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
