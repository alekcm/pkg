using System;
using UnityEngine;

namespace MapEditorPrototype
{
    public class WallSegment : MonoBehaviour
    {
        [SerializeField] private string segmentId;
        [SerializeField] private int cellX;
        [SerializeField] private int cellY;
        [SerializeField] private WallOrientation orientation;
        [SerializeField] private WallDefinition wallDefinition;
        [SerializeField] private WallOpeningDefinition openingDefinition;

        private WallSegmentState cachedState;
        private bool isStateDirty = true;

        public string SegmentId => segmentId;
        public WallEdge Edge => new WallEdge(cellX, cellY, orientation);
        public WallDefinition WallDefinition => wallDefinition;
        public WallOpeningDefinition OpeningDefinition => openingDefinition;

        public WallSegmentState GetState()
        {
            if (isStateDirty || cachedState == null)
            {
                cachedState = new WallSegmentState
                {
                    SegmentId = segmentId,
                    Edge = new WallEdge(cellX, cellY, orientation),
                    WallDefinitionId = wallDefinition?.id,
                    OpeningDefinitionId = openingDefinition?.id
                };
                isStateDirty = false;
            }
            return cachedState;
        }

        public void Initialize(WallEdge edge, string newSegmentId = null)
        {
            segmentId = string.IsNullOrWhiteSpace(newSegmentId) ? Guid.NewGuid().ToString("N") : newSegmentId;
            cellX = edge.x;
            cellY = edge.y;
            orientation = edge.orientation;
            isStateDirty = true;
        }

        public void SetDefinitions(WallDefinition newWallDefinition, WallOpeningDefinition newOpeningDefinition)
        {
            wallDefinition = newWallDefinition;
            openingDefinition = newOpeningDefinition;
            isStateDirty = true;
        }
    }
}
