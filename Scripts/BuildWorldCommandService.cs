using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    public class BuildWorldCommandService
    {
        private readonly GridBuildingSystem gridBuildingSystem;
        private readonly WallSystem wallSystem;
        private readonly PathSystem pathSystem;
        private readonly MapSaveSystem mapSaveSystem;
        private readonly EditorUndoRedoSystem undoRedoSystem;

        public bool SuppressBuildVersionTracking { get; set; }

        public BuildWorldCommandService(
            GridBuildingSystem gridBuildingSystem,
            WallSystem wallSystem,
            PathSystem pathSystem,
            MapSaveSystem mapSaveSystem,
            EditorUndoRedoSystem undoRedoSystem)
        {
            this.gridBuildingSystem = gridBuildingSystem;
            this.wallSystem = wallSystem;
            this.pathSystem = pathSystem;
            this.mapSaveSystem = mapSaveSystem;
            this.undoRedoSystem = undoRedoSystem;
        }

        public void SaveWorld()
        {
            mapSaveSystem?.SaveDefault();
        }

        public void TouchBuildVersion()
        {
            if (!SuppressBuildVersionTracking)
            {
                mapSaveSystem?.IncrementBuildVersion();
            }
        }

        public void LoadWorld()
        {
            undoRedoSystem?.RecordStateBeforeChange();
            mapSaveSystem?.LoadDefault();
        }

        public void RecordUndoState()
        {
            undoRedoSystem?.RecordStateBeforeChange();
        }

        public void RecordUndoSnapshot(string snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot))
            {
                undoRedoSystem?.RecordSpecificSnapshot(snapshot);
            }
        }

        public bool DeletePlacedObject(PlacedObject placedObject, bool recordUndo = true, bool touchBuildVersion = true)
        {
            if (placedObject == null || gridBuildingSystem == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            gridBuildingSystem.Remove(placedObject);
            if (touchBuildVersion)
            {
                TouchBuildVersion();
            }
            return true;
        }

        public PlacedObject PlaceGridObject(BuildingDefinition definition, Vector2Int originCell, int rotationSteps, float baseY, float rotationY, bool recordUndo = true)
        {
            if (gridBuildingSystem == null)
            {
                return null;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            PlacedObject result = gridBuildingSystem.Place(definition, originCell, rotationSteps, baseY, rotationY);
            if (result != null)
            {
                TouchBuildVersion();
            }
            return result;
        }

        public PlacedObject PlaceFreeObject(BuildingDefinition definition, Vector3 worldPosition, int rotationSteps, float rotationY, bool recordUndo = true)
        {
            if (gridBuildingSystem == null)
            {
                return null;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            PlacedObject result = gridBuildingSystem.PlaceFree(definition, worldPosition, rotationSteps, rotationY);
            if (result != null)
            {
                TouchBuildVersion();
            }
            return result;
        }

        public bool TryPaintGridCell(BuildingDefinition definition, Vector2Int cell, int rotationSteps, float baseY, float rotationY)
        {
            if (gridBuildingSystem == null || !gridBuildingSystem.CanPlace(definition, cell, rotationSteps, baseY))
            {
                return false;
            }

            PlacedObject result = gridBuildingSystem.Place(definition, cell, rotationSteps, baseY, rotationY);
            if (result != null)
            {
                TouchBuildVersion();
                return true;
            }

            return false;
        }

        public bool PlaceWall(WallDefinition definition, WallEdge edge, bool recordUndo = true)
        {
            if (wallSystem == null || definition == null || !wallSystem.CanPlaceWall(definition, edge))
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            WallSegment result = wallSystem.PlaceWall(definition, edge);
            if (result != null)
            {
                TouchBuildVersion();
                return true;
            }

            return false;
        }

        public bool PlaceOpening(WallOpeningDefinition definition, WallEdge edge, bool recordUndo = true)
        {
            if (wallSystem == null || definition == null || !wallSystem.CanPlaceOpening(definition, edge))
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            bool result = wallSystem.PlaceOpening(definition, edge);
            if (result)
            {
                TouchBuildVersion();
            }
            return result;
        }

        public bool RemoveWallAtEdge(WallEdge edge, bool recordUndo = true)
        {
            if (wallSystem == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            bool result = wallSystem.RemoveAtEdge(edge);
            if (result)
            {
                TouchBuildVersion();
            }
            return result;
        }

        public bool TryPaintWallEdge(WallDefinition definition, WallEdge edge)
        {
            if (wallSystem == null || definition == null || !wallSystem.CanPlaceWall(definition, edge))
            {
                return false;
            }

            WallSegment result = wallSystem.PlaceWall(definition, edge);
            if (result != null)
            {
                TouchBuildVersion();
                return true;
            }

            return false;
        }

        public PathStroke CreatePath(PathDefinition definition, IReadOnlyList<Vector3> points, float width, bool recordUndo = true)
        {
            if (pathSystem == null || definition == null)
            {
                return null;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            PathStroke stroke = pathSystem.CreateStroke(definition, points, width);
            if (stroke != null)
            {
                TouchBuildVersion();
            }
            return stroke;
        }

        public bool DeletePath(PathStroke pathStroke, bool recordUndo = true)
        {
            if (pathSystem == null || pathStroke == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            pathSystem.RemoveStroke(pathStroke);
            TouchBuildVersion();
            return true;
        }

        public bool UpdatePathPoint(PathStroke pathStroke, int pointIndex, Vector3 worldPosition, bool recordUndo = true)
        {
            if (pathStroke == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            bool result = pathStroke.SetControlPoint(pointIndex, worldPosition);
            if (result)
            {
                pathSystem?.NotifyStrokeChanged();
                TouchBuildVersion();
            }
            return result;
        }

        public bool InsertPathPoint(PathStroke pathStroke, int segmentIndex, Vector3 worldPosition, out int insertedIndex, bool recordUndo = true)
        {
            insertedIndex = -1;
            if (pathStroke == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            bool result = pathStroke.InsertControlPointAfterSegment(segmentIndex, worldPosition, out insertedIndex);
            if (result)
            {
                pathSystem?.NotifyStrokeChanged();
                TouchBuildVersion();
            }
            return result;
        }

        public bool RemovePathPoint(PathStroke pathStroke, int pointIndex, bool recordUndo = true)
        {
            if (pathStroke == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            bool result = pathStroke.RemoveControlPointAt(pointIndex);
            if (result)
            {
                pathSystem?.NotifyStrokeChanged();
                TouchBuildVersion();
            }
            else
            {
                pathSystem?.RemoveStroke(pathStroke);
                TouchBuildVersion();
                return false;
            }

            return true;
        }

        public bool UpdatePathWidth(PathStroke pathStroke, float width, bool recordUndo = true)
        {
            if (pathStroke == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            pathStroke.SetWidth(width);
            pathSystem?.NotifyStrokeChanged();
            TouchBuildVersion();
            return true;
        }

        public bool PaintDetail(DetailPaintableSurface surface, RaycastHit hit, DetailPaintBrushDefinition brush, bool erase, bool recordUndo = true)
        {
            if (surface == null || brush == null)
            {
                return false;
            }

            if (recordUndo)
            {
                undoRedoSystem?.RecordStateBeforeChange();
            }

            bool changed = surface.TryPaint(hit, brush, erase);
            if (changed)
            {
                TouchBuildVersion();
            }
            return changed;
        }
    }
}
