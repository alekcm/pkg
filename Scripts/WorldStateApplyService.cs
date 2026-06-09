using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    public class WorldStateApplyService
    {
        private readonly GridBuildingSystem gridBuildingSystem;
        private readonly BuildCatalog buildCatalog;
        private readonly WallSystem wallSystem;
        private readonly WallCatalog wallCatalog;
        private readonly PathSystem pathSystem;
        private readonly PathCatalog pathCatalog;
        private readonly ExplorerController explorerController;
        private readonly BuildSystem buildSystem;

        public WorldStateApplyService(
            GridBuildingSystem gridBuildingSystem,
            BuildCatalog buildCatalog,
            WallSystem wallSystem,
            WallCatalog wallCatalog,
            PathSystem pathSystem,
            PathCatalog pathCatalog,
            ExplorerController explorerController,
            BuildSystem buildSystem)
        {
            this.gridBuildingSystem = gridBuildingSystem;
            this.buildCatalog = buildCatalog;
            this.wallSystem = wallSystem;
            this.wallCatalog = wallCatalog;
            this.pathSystem = pathSystem;
            this.pathCatalog = pathCatalog;
            this.explorerController = explorerController;
            this.buildSystem = buildSystem;
        }

        public void Apply(WorldState worldState)
        {
            if (worldState == null)
            {
                return;
            }

            if (buildSystem != null)
            {
                buildSystem.ResetTransientEditorState();
            }

            if (gridBuildingSystem != null)
            {
                gridBuildingSystem.ClearAll();
            }

            if (wallSystem != null)
            {
                wallSystem.ClearAll();
            }

            if (pathSystem != null)
            {
                pathSystem.ClearAll();
            }

            ApplyPlacedObjects(worldState);
            ApplyWalls(worldState);
            ApplyPaths(worldState);
            ApplyDetailMasks(worldState);
            ApplyRuntime(worldState);
        }

        private void ApplyPlacedObjects(WorldState worldState)
        {
            if (gridBuildingSystem == null || buildCatalog == null || worldState.Build == null)
            {
                return;
            }

            for (int i = 0; i < worldState.Build.PlacedObjects.Count; i++)
            {
                PlacedObjectState item = worldState.Build.PlacedObjects[i];
                if (item == null)
                {
                    continue;
                }

                BuildingDefinition definition = buildCatalog.FindById(item.DefinitionId);
                if (definition == null)
                {
                    Debug.LogWarning($"WorldStateApplyService: building definition not found: {item.DefinitionId}");
                    continue;
                }

                if (item.UsesGridPlacement)
                {
                    gridBuildingSystem.Place(definition, item.OriginCell, item.RotationSteps, item.BaseY, item.RotationY, item.ObjectId);
                }
                else
                {
                    gridBuildingSystem.PlaceFree(definition, item.WorldPosition, item.RotationSteps, item.RotationY, item.ObjectId);
                }
            }
        }

        private void ApplyWalls(WorldState worldState)
        {
            if (wallSystem == null || wallCatalog == null || worldState.Build == null)
            {
                return;
            }

            for (int i = 0; i < worldState.Build.Walls.Count; i++)
            {
                WallSegmentState item = worldState.Build.Walls[i];
                if (item == null)
                {
                    continue;
                }

                WallDefinition wallDefinition = wallCatalog.FindWallById(item.WallDefinitionId);
                if (wallDefinition != null)
                {
                    wallSystem.PlaceWall(wallDefinition, item.Edge, item.SegmentId);
                }

                if (!string.IsNullOrWhiteSpace(item.OpeningDefinitionId))
                {
                    WallOpeningDefinition openingDefinition = wallCatalog.FindOpeningById(item.OpeningDefinitionId);
                    if (openingDefinition != null)
                    {
                        wallSystem.PlaceOpening(openingDefinition, item.Edge);
                    }
                }
            }
        }

        private void ApplyPaths(WorldState worldState)
        {
            if (pathSystem == null || pathCatalog == null || worldState.Build == null)
            {
                return;
            }

            for (int i = 0; i < worldState.Build.PathStrokes.Count; i++)
            {
                PathStrokeState item = worldState.Build.PathStrokes[i];
                if (item == null)
                {
                    continue;
                }

                PathDefinition definition = pathCatalog.FindById(item.DefinitionId);
                if (definition == null)
                {
                    Debug.LogWarning($"WorldStateApplyService: path definition not found: {item.DefinitionId}");
                    continue;
                }

                pathSystem.CreateStroke(definition, item.ControlPoints, item.Width, item.StrokeId);
            }
        }

        private void ApplyDetailMasks(WorldState worldState)
        {
            if (worldState.Build == null || worldState.Build.DetailSurfaceMasks.Count == 0)
            {
                return;
            }

            Dictionary<string, string> masksBySurfaceId = new Dictionary<string, string>();
            for (int i = 0; i < worldState.Build.DetailSurfaceMasks.Count; i++)
            {
                DetailSurfaceMaskState item = worldState.Build.DetailSurfaceMasks[i];
                if (item == null || string.IsNullOrWhiteSpace(item.SurfaceId))
                {
                    continue;
                }

                masksBySurfaceId[item.SurfaceId] = item.MaskPngBase64;
            }

            DetailPaintableSurface[] surfaces = UnityEngine.Object.FindObjectsOfType<DetailPaintableSurface>(true);
            for (int i = 0; i < surfaces.Length; i++)
            {
                DetailPaintableSurface surface = surfaces[i];
                if (surface != null && masksBySurfaceId.TryGetValue(surface.SurfaceId, out string base64))
                {
                    surface.ImportMaskFromBase64(base64);
                }
            }
        }

        private void ApplyRuntime(WorldState worldState)
        {
            if (explorerController != null && worldState.Runtime != null)
            {
                explorerController.TeleportTo(worldState.Runtime.ExplorerPosition, Quaternion.Euler(0f, worldState.Runtime.ExplorerYaw, 0f));
            }
        }
    }
}
