using UnityEngine;

namespace MapEditorPrototype
{
    public class HostWorldReplicationService : MonoBehaviour
    {
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private NetworkSessionManager networkSessionManager;

        private readonly WorldPatchApplyService patchApplyService = new WorldPatchApplyService();

        public WorldState AuthoritativeWorldState { get; private set; }

        public WorldSnapshotDto BuildSnapshotDto()
        {
            CaptureAuthoritativeStateFromScene();
            return WorldNetworkDtoMapper.ToSnapshotDto(AuthoritativeWorldState);
        }

        public WorldPatchDto CommitPatchAndBuildDto(WorldPatch patch)
        {
            if (patch == null || !patch.HasAnyChanges)
            {
                return null;
            }

            CaptureAuthoritativeStateFromScene();
            if (AuthoritativeWorldState == null)
            {
                return null;
            }

            patchApplyService.Apply(AuthoritativeWorldState, patch);
            mapSaveSystem?.ApplyWorldState(WorldStateCloneUtility.Clone(AuthoritativeWorldState));
            if (networkSessionManager != null && AuthoritativeWorldState.Versions != null)
            {
                networkSessionManager.UpdateVersions(AuthoritativeWorldState.Versions.BuildVersion, AuthoritativeWorldState.Versions.RuntimeVersion);
            }

            return WorldNetworkDtoMapper.ToPatchDto(patch);
        }

        public void CaptureAuthoritativeStateFromScene()
        {
            if (mapSaveSystem == null)
            {
                return;
            }

            AuthoritativeWorldState = mapSaveSystem.CaptureCurrentWorldState();
            if (networkSessionManager != null && AuthoritativeWorldState != null && AuthoritativeWorldState.Versions != null)
            {
                networkSessionManager.UpdateVersions(AuthoritativeWorldState.Versions.BuildVersion, AuthoritativeWorldState.Versions.RuntimeVersion);
            }
        }
    }
}
