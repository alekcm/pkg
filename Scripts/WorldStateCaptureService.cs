using System;
using UnityEngine;

namespace MapEditorPrototype
{
    public class WorldStateCaptureService
    {
        private readonly GridBuildingSystem gridBuildingSystem;
        private readonly WallSystem wallSystem;
        private readonly PathSystem pathSystem;
        private readonly ExplorerController explorerController;

        public WorldStateCaptureService(
            GridBuildingSystem gridBuildingSystem,
            WallSystem wallSystem,
            PathSystem pathSystem,
            ExplorerController explorerController)
        {
            this.gridBuildingSystem = gridBuildingSystem;
            this.wallSystem = wallSystem;
            this.pathSystem = pathSystem;
            this.explorerController = explorerController;
        }

        public WorldState Capture(string worldId, int buildVersion, int runtimeVersion)
        {
            WorldState worldState = new WorldState
            {
                WorldId = string.IsNullOrWhiteSpace(worldId) ? Guid.NewGuid().ToString("N") : worldId
            };

            worldState.Versions.BuildVersion = buildVersion;
            worldState.Versions.RuntimeVersion = runtimeVersion;

            CapturePlacedObjects(worldState);
            CaptureWalls(worldState);
            CapturePaths(worldState);
            CaptureDetailMasks(worldState);
            CaptureRuntime(worldState);

            return worldState;
        }

        private void CapturePlacedObjects(WorldState worldState)
        {
            if (gridBuildingSystem == null)
            {
                return;
            }

            for (int i = 0; i < gridBuildingSystem.PlacedObjects.Count; i++)
            {
                PlacedObject placedObject = gridBuildingSystem.PlacedObjects[i];
                if (placedObject == null || placedObject.Definition == null)
                {
                    continue;
                }

                worldState.Build.PlacedObjects.Add(new PlacedObjectState
                {
                    ObjectId = placedObject.ObjectId,
                    DefinitionId = placedObject.Definition.id,
                    OriginCell = placedObject.OriginCell,
                    RotationSteps = placedObject.RotationSteps,
                    RotationY = placedObject.RotationY,
                    UsesGridPlacement = placedObject.UsesGridPlacement,
                    BaseY = placedObject.BaseY,
                    WorldPosition = placedObject.transform.position
                });
            }
        }

        private void CaptureWalls(WorldState worldState)
        {
            if (wallSystem == null)
            {
                return;
            }

            foreach (WallSegment segment in wallSystem.Segments)
            {
                if (segment == null || segment.WallDefinition == null)
                {
                    continue;
                }

                worldState.Build.Walls.Add(new WallSegmentState
                {
                    SegmentId = segment.SegmentId,
                    Edge = segment.Edge,
                    WallDefinitionId = segment.WallDefinition != null ? segment.WallDefinition.id : string.Empty,
                    OpeningDefinitionId = segment.OpeningDefinition != null ? segment.OpeningDefinition.id : string.Empty
                });
            }
        }

        private void CapturePaths(WorldState worldState)
        {
            if (pathSystem == null)
            {
                return;
            }

            for (int i = 0; i < pathSystem.Strokes.Count; i++)
            {
                PathStroke stroke = pathSystem.Strokes[i];
                if (stroke == null || stroke.Definition == null)
                {
                    continue;
                }

                PathStrokeState strokeState = new PathStrokeState
                {
                    StrokeId = stroke.StrokeId,
                    DefinitionId = stroke.Definition.id,
                    Width = stroke.Width
                };

                for (int pointIndex = 0; pointIndex < stroke.ControlPoints.Count; pointIndex++)
                {
                    strokeState.ControlPoints.Add(stroke.ControlPoints[pointIndex]);
                }

                worldState.Build.PathStrokes.Add(strokeState);
            }
        }

        private void CaptureDetailMasks(WorldState worldState)
        {
            DetailPaintableSurface[] surfaces = UnityEngine.Object.FindObjectsOfType<DetailPaintableSurface>(true);
            for (int i = 0; i < surfaces.Length; i++)
            {
                DetailPaintableSurface surface = surfaces[i];
                if (surface == null || !surface.HasSavedMask)
                {
                    continue;
                }

                string maskBase64 = surface.ExportMaskToBase64();
                if (string.IsNullOrWhiteSpace(maskBase64))
                {
                    continue;
                }

                worldState.Build.DetailSurfaceMasks.Add(new DetailSurfaceMaskState
                {
                    SurfaceId = surface.SurfaceId,
                    MaskPngBase64 = maskBase64
                });
            }
        }

        private void CaptureRuntime(WorldState worldState)
        {
            if (explorerController != null)
            {
                worldState.Runtime.ExplorerPosition = explorerController.transform.position;
                worldState.Runtime.ExplorerYaw = explorerController.transform.eulerAngles.y;
            }
        }
    }
}
