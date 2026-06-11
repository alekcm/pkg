using System;
using UnityEngine;

namespace MapEditorPrototype
{
    public class WorldStateCaptureService
    {
        private readonly GridBuildingSystem gridBuildingSystem;
        private readonly WallSystem wallSystem;

        public WorldStateCaptureService(GridBuildingSystem gb, WallSystem ws, PathSystem ps, ExplorerController ec)
        {
            this.gridBuildingSystem = gb;
            this.wallSystem = ws;
        }

        public WorldState Capture(string worldId, int buildVersion, int runtimeVersion)
        {
            WorldState worldState = new WorldState { WorldId = worldId };
            worldState.Versions.BuildVersion = buildVersion;
            worldState.Versions.RuntimeVersion = runtimeVersion;

            if (gridBuildingSystem != null)
            {
                var objs = gridBuildingSystem.PlacedObjects;
                for (int i = 0; i < objs.Count; i++)
                {
                    var p = objs[i];
                    if (p != null) worldState.Build.PlacedObjects.Add(p.GetState());
                }
            }

            if (wallSystem != null)
            {
                foreach (var s in wallSystem.Segments)
                {
                    if (s != null) worldState.Build.Walls.Add(s.GetState());
                }
            }
            return worldState;
        }
    }
}
