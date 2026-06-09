using System;
using UnityEngine;

namespace MapEditorPrototype
{
    public class WallSegment : MonoBehaviour, IPaintSurfaceOwner
    {
        [SerializeField] private string segmentId;
        [SerializeField] private int cellX;
        [SerializeField] private int cellY;
        [SerializeField] private WallOrientation orientation;
        [SerializeField] private WallDefinition wallDefinition;
        [SerializeField] private WallOpeningDefinition openingDefinition;

        public string SegmentId => segmentId;
        public string SurfaceOwnerId => segmentId;
        public WallEdge Edge => new WallEdge(cellX, cellY, orientation);
        public WallDefinition WallDefinition => wallDefinition;
        public WallOpeningDefinition OpeningDefinition => openingDefinition;

        public void Initialize(WallEdge edge, string newSegmentId = null)
        {
            segmentId = string.IsNullOrWhiteSpace(newSegmentId) ? Guid.NewGuid().ToString("N") : newSegmentId;
            cellX = edge.x;
            cellY = edge.y;
            orientation = edge.orientation;
        }

        public void SetDefinitions(WallDefinition newWallDefinition, WallOpeningDefinition newOpeningDefinition)
        {
            wallDefinition = newWallDefinition;
            openingDefinition = newOpeningDefinition;
        }
    }
}
