using System.Collections.Generic;

namespace MapEditorPrototype
{
    public class WorldStateDiffBuilder
    {
        public WorldPatch BuildPatch(WorldState baseState, WorldState editedState)
        {
            WorldPatch patch = new WorldPatch
            {
                WorldId = editedState != null ? editedState.WorldId : baseState != null ? baseState.WorldId : string.Empty,
                BaseBuildVersion = baseState != null && baseState.Versions != null ? baseState.Versions.BuildVersion : 0,
                NewBuildVersion = editedState != null && editedState.Versions != null ? editedState.Versions.BuildVersion : 0
            };

            if (baseState == null || editedState == null)
            {
                return patch;
            }

            BuildObjectDiff(baseState, editedState, patch);
            BuildWallDiff(baseState, editedState, patch);
            BuildPathDiff(baseState, editedState, patch);
            BuildMaskDiff(baseState, editedState, patch);
            return patch;
        }

        private void BuildObjectDiff(WorldState baseState, WorldState editedState, WorldPatch patch)
        {
            Dictionary<string, PlacedObjectState> baseMap = new Dictionary<string, PlacedObjectState>();
            for (int i = 0; i < baseState.Build.PlacedObjects.Count; i++)
            {
                PlacedObjectState item = baseState.Build.PlacedObjects[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.ObjectId))
                {
                    baseMap[item.ObjectId] = item;
                }
            }

            Dictionary<string, PlacedObjectState> editedMap = new Dictionary<string, PlacedObjectState>();
            for (int i = 0; i < editedState.Build.PlacedObjects.Count; i++)
            {
                PlacedObjectState item = editedState.Build.PlacedObjects[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.ObjectId))
                {
                    editedMap[item.ObjectId] = item;
                }
            }

            foreach (KeyValuePair<string, PlacedObjectState> pair in editedMap)
            {
                if (!baseMap.TryGetValue(pair.Key, out PlacedObjectState baseItem) || !PlacedObjectsEqual(baseItem, pair.Value))
                {
                    patch.UpsertPlacedObjects.Add(pair.Value);
                }
            }

            foreach (KeyValuePair<string, PlacedObjectState> pair in baseMap)
            {
                if (!editedMap.ContainsKey(pair.Key))
                {
                    patch.DeletePlacedObjectIds.Add(pair.Key);
                }
            }
        }

        private void BuildWallDiff(WorldState baseState, WorldState editedState, WorldPatch patch)
        {
            Dictionary<string, WallSegmentState> baseMap = new Dictionary<string, WallSegmentState>();
            for (int i = 0; i < baseState.Build.Walls.Count; i++)
            {
                WallSegmentState item = baseState.Build.Walls[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.SegmentId))
                {
                    baseMap[item.SegmentId] = item;
                }
            }

            Dictionary<string, WallSegmentState> editedMap = new Dictionary<string, WallSegmentState>();
            for (int i = 0; i < editedState.Build.Walls.Count; i++)
            {
                WallSegmentState item = editedState.Build.Walls[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.SegmentId))
                {
                    editedMap[item.SegmentId] = item;
                }
            }

            foreach (KeyValuePair<string, WallSegmentState> pair in editedMap)
            {
                if (!baseMap.TryGetValue(pair.Key, out WallSegmentState baseItem) || !WallsEqual(baseItem, pair.Value))
                {
                    patch.UpsertWalls.Add(pair.Value);
                }
            }

            foreach (KeyValuePair<string, WallSegmentState> pair in baseMap)
            {
                if (!editedMap.ContainsKey(pair.Key))
                {
                    patch.DeleteWallIds.Add(pair.Key);
                }
            }
        }

        private void BuildPathDiff(WorldState baseState, WorldState editedState, WorldPatch patch)
        {
            Dictionary<string, PathStrokeState> baseMap = new Dictionary<string, PathStrokeState>();
            for (int i = 0; i < baseState.Build.PathStrokes.Count; i++)
            {
                PathStrokeState item = baseState.Build.PathStrokes[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.StrokeId))
                {
                    baseMap[item.StrokeId] = item;
                }
            }

            Dictionary<string, PathStrokeState> editedMap = new Dictionary<string, PathStrokeState>();
            for (int i = 0; i < editedState.Build.PathStrokes.Count; i++)
            {
                PathStrokeState item = editedState.Build.PathStrokes[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.StrokeId))
                {
                    editedMap[item.StrokeId] = item;
                }
            }

            foreach (KeyValuePair<string, PathStrokeState> pair in editedMap)
            {
                if (!baseMap.TryGetValue(pair.Key, out PathStrokeState baseItem) || !PathsEqual(baseItem, pair.Value))
                {
                    patch.UpsertPaths.Add(pair.Value);
                }
            }

            foreach (KeyValuePair<string, PathStrokeState> pair in baseMap)
            {
                if (!editedMap.ContainsKey(pair.Key))
                {
                    patch.DeletePathIds.Add(pair.Key);
                }
            }
        }

        private void BuildMaskDiff(WorldState baseState, WorldState editedState, WorldPatch patch)
        {
            Dictionary<string, DetailSurfaceMaskState> baseMap = new Dictionary<string, DetailSurfaceMaskState>();
            for (int i = 0; i < baseState.Build.DetailSurfaceMasks.Count; i++)
            {
                DetailSurfaceMaskState item = baseState.Build.DetailSurfaceMasks[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.SurfaceId))
                {
                    baseMap[item.SurfaceId] = item;
                }
            }

            Dictionary<string, DetailSurfaceMaskState> editedMap = new Dictionary<string, DetailSurfaceMaskState>();
            for (int i = 0; i < editedState.Build.DetailSurfaceMasks.Count; i++)
            {
                DetailSurfaceMaskState item = editedState.Build.DetailSurfaceMasks[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.SurfaceId))
                {
                    editedMap[item.SurfaceId] = item;
                }
            }

            foreach (KeyValuePair<string, DetailSurfaceMaskState> pair in editedMap)
            {
                if (!baseMap.TryGetValue(pair.Key, out DetailSurfaceMaskState baseItem) || !MasksEqual(baseItem, pair.Value))
                {
                    patch.UpsertDetailMasks.Add(pair.Value);
                }
            }

            foreach (KeyValuePair<string, DetailSurfaceMaskState> pair in baseMap)
            {
                if (!editedMap.ContainsKey(pair.Key))
                {
                    patch.DeleteDetailMaskSurfaceIds.Add(pair.Key);
                }
            }
        }

        private bool PlacedObjectsEqual(PlacedObjectState a, PlacedObjectState b)
        {
            return a != null && b != null &&
                   a.DefinitionId == b.DefinitionId &&
                   a.OriginCell == b.OriginCell &&
                   a.RotationSteps == b.RotationSteps &&
                   a.RotationY == b.RotationY &&
                   a.UsesGridPlacement == b.UsesGridPlacement &&
                   a.BaseY == b.BaseY &&
                   a.WorldPosition == b.WorldPosition;
        }

        private bool WallsEqual(WallSegmentState a, WallSegmentState b)
        {
            return a != null && b != null &&
                   a.Edge.Equals(b.Edge) &&
                   a.WallDefinitionId == b.WallDefinitionId &&
                   a.OpeningDefinitionId == b.OpeningDefinitionId;
        }

        private bool PathsEqual(PathStrokeState a, PathStrokeState b)
        {
            if (a == null || b == null || a.DefinitionId != b.DefinitionId || a.Width != b.Width || a.ControlPoints.Count != b.ControlPoints.Count)
            {
                return false;
            }

            for (int i = 0; i < a.ControlPoints.Count; i++)
            {
                if (a.ControlPoints[i] != b.ControlPoints[i])
                {
                    return false;
                }
            }

            return true;
        }

        private bool MasksEqual(DetailSurfaceMaskState a, DetailSurfaceMaskState b)
        {
            return a != null && b != null && a.MaskPngBase64 == b.MaskPngBase64;
        }
    }
}
