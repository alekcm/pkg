using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    public class WallSystem : MonoBehaviour
    {
        [SerializeField] private GridBuildingSystem gridBuildingSystem;
        [SerializeField] private Transform wallRoot;
        [SerializeField] private float wallYOffset;

        private readonly Dictionary<WallEdge, WallSegment> segments = new Dictionary<WallEdge, WallSegment>();

        public event Action Changed;

        public IReadOnlyCollection<WallSegment> Segments => segments.Values;

        public bool TryGetNearestEdge(Vector3 worldPosition, out WallEdge edge, out Vector3 edgePosition, out Quaternion edgeRotation)
        {
            edge = default;
            edgePosition = default;
            edgeRotation = Quaternion.identity;

            if (gridBuildingSystem == null)
            {
                return false;
            }

            float cellSize = gridBuildingSystem.CellSize;
            Vector3 local = worldPosition - gridBuildingSystem.GridOrigin;

            int cellX = Mathf.FloorToInt(local.x / cellSize);
            int cellY = Mathf.FloorToInt(local.z / cellSize);

            float localX = local.x - cellX * cellSize;
            float localY = local.z - cellY * cellSize;

            float distanceLeft = Mathf.Abs(localX);
            float distanceRight = Mathf.Abs(cellSize - localX);
            float distanceBottom = Mathf.Abs(localY);
            float distanceTop = Mathf.Abs(cellSize - localY);

            float minDistance = distanceLeft;
            edge = new WallEdge(cellX, cellY, WallOrientation.Vertical);

            if (distanceRight < minDistance)
            {
                minDistance = distanceRight;
                edge = new WallEdge(cellX + 1, cellY, WallOrientation.Vertical);
            }

            if (distanceBottom < minDistance)
            {
                minDistance = distanceBottom;
                edge = new WallEdge(cellX, cellY, WallOrientation.Horizontal);
            }

            if (distanceTop < minDistance)
            {
                edge = new WallEdge(cellX, cellY + 1, WallOrientation.Horizontal);
            }

            edgePosition = GetEdgeWorldPosition(edge);
            edgeRotation = GetEdgeRotation(edge);
            return true;
        }

        public Vector3 GetEdgeWorldPosition(WallEdge edge)
        {
            if (gridBuildingSystem == null)
            {
                return Vector3.zero;
            }

            float cellSize = gridBuildingSystem.CellSize;
            Vector3 origin = gridBuildingSystem.GridOrigin;

            if (edge.orientation == WallOrientation.Horizontal)
            {
                return origin + new Vector3((edge.x + 0.5f) * cellSize, wallYOffset, edge.y * cellSize);
            }

            return origin + new Vector3(edge.x * cellSize, wallYOffset, (edge.y + 0.5f) * cellSize);
        }

        public Quaternion GetEdgeRotation(WallEdge edge)
        {
            return edge.orientation == WallOrientation.Horizontal ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        }

        public bool CanPlaceWall(WallDefinition definition, WallEdge edge)
        {
            if (definition == null || definition.prefab == null)
            {
                return false;
            }

            if (!segments.TryGetValue(edge, out WallSegment segment))
            {
                return true;
            }

            return segment == null || segment.WallDefinition == null;
        }

        public WallSegment PlaceWall(WallDefinition definition, WallEdge edge, string segmentId = null)
        {
            if (!CanPlaceWall(definition, edge))
            {
                return null;
            }

            WallSegment segment = GetOrCreateSegment(edge, segmentId);
            segment.SetDefinitions(definition, null);
            RebuildVisual(segment);
            Changed?.Invoke();
            return segment;
        }

        public bool CanPlaceOpening(WallOpeningDefinition definition, WallEdge edge)
        {
            if (definition == null || definition.prefab == null)
            {
                return false;
            }

            if (!segments.TryGetValue(edge, out WallSegment segment) || segment == null)
            {
                return false;
            }

            return segment.WallDefinition != null && segment.OpeningDefinition == null;
        }

        public bool PlaceOpening(WallOpeningDefinition definition, WallEdge edge)
        {
            if (!CanPlaceOpening(definition, edge))
            {
                return false;
            }

            WallSegment segment = segments[edge];
            segment.SetDefinitions(segment.WallDefinition, definition);
            RebuildVisual(segment);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveAtEdge(WallEdge edge)
        {
            if (!segments.TryGetValue(edge, out WallSegment segment) || segment == null)
            {
                return false;
            }

            if (segment.OpeningDefinition != null)
            {
                segment.SetDefinitions(segment.WallDefinition, null);
                RebuildVisual(segment);
            }
            else
            {
                segments.Remove(edge);
                Destroy(segment.gameObject);
            }

            Changed?.Invoke();
            return true;
        }

        public WallSegment GetSegment(WallEdge edge)
        {
            segments.TryGetValue(edge, out WallSegment segment);
            return segment;
        }

        public void ClearAll()
        {
            foreach (WallSegment segment in segments.Values)
            {
                if (segment != null)
                {
                    Destroy(segment.gameObject);
                }
            }

            segments.Clear();
            Changed?.Invoke();
        }

        private WallSegment GetOrCreateSegment(WallEdge edge, string segmentId = null)
        {
            if (segments.TryGetValue(edge, out WallSegment segment) && segment != null)
            {
                return segment;
            }

            Transform parent = wallRoot != null ? wallRoot : transform;
            GameObject wallAnchor = new GameObject($"Wall_{edge.x}_{edge.y}_{edge.orientation}");
            wallAnchor.transform.SetParent(parent, false);
            wallAnchor.transform.SetPositionAndRotation(GetEdgeWorldPosition(edge), GetEdgeRotation(edge));

            segment = wallAnchor.AddComponent<WallSegment>();
            segment.Initialize(edge, segmentId);
            segments[edge] = segment;
            return segment;
        }

        private void RebuildVisual(WallSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            Transform segmentTransform = segment.transform;
            for (int i = segmentTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(segmentTransform.GetChild(i).gameObject);
            }

            GameObject prefabToUse = segment.OpeningDefinition != null ? segment.OpeningDefinition.prefab : segment.WallDefinition != null ? segment.WallDefinition.prefab : null;
            Vector3 worldOffset = segment.OpeningDefinition != null ? segment.OpeningDefinition.worldOffset : segment.WallDefinition != null ? segment.WallDefinition.worldOffset : Vector3.zero;

            segmentTransform.SetPositionAndRotation(GetEdgeWorldPosition(segment.Edge), GetEdgeRotation(segment.Edge));

            if (prefabToUse == null)
            {
                return;
            }

            GameObject visual = Instantiate(prefabToUse, segmentTransform);
            visual.transform.localPosition = worldOffset;
            visual.transform.localRotation = Quaternion.identity;
            visual.name = prefabToUse.name;

            DetailPaintableSurface.EnsureAutoAttached(visual, "Wall");
        }
    }
}
