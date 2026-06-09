using UnityEngine;

namespace MapEditorPrototype
{
    public class ClientWorldReplicationService : MonoBehaviour
    {
        [SerializeField] private MapSaveSystem mapSaveSystem;

        private readonly WorldPatchApplyService patchApplyService = new WorldPatchApplyService();

        public WorldState LocalReplicatedWorldState { get; private set; }

        public void ApplySnapshotDto(WorldSnapshotDto snapshotDto)
        {
            if (snapshotDto == null || mapSaveSystem == null)
            {
                return;
            }

            LocalReplicatedWorldState = WorldNetworkDtoMapper.FromSnapshotDto(snapshotDto);
            mapSaveSystem.ApplyWorldState(WorldStateCloneUtility.Clone(LocalReplicatedWorldState));
        }

        public void ApplyPatchDto(WorldPatchDto patchDto)
        {
            if (patchDto == null || mapSaveSystem == null)
            {
                return;
            }

            if (LocalReplicatedWorldState == null)
            {
                LocalReplicatedWorldState = mapSaveSystem.CaptureCurrentWorldState();
            }

            WorldPatch patch = WorldNetworkDtoMapper.FromPatchDto(patchDto);
            patchApplyService.Apply(LocalReplicatedWorldState, patch);
            mapSaveSystem.ApplyWorldState(WorldStateCloneUtility.Clone(LocalReplicatedWorldState));
        }
    }
}
