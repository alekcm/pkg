using UnityEngine;

namespace MapEditorPrototype
{
    public class EditDraftSessionController : MonoBehaviour
    {
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private bool autoIncrementBuildVersionOnApply = true;

        private readonly WorldStateDiffBuilder diffBuilder = new WorldStateDiffBuilder();

        public bool HasActiveDraft { get; private set; }
        public WorldState BaseWorldState { get; private set; }
        public WorldState DraftWorldState { get; private set; }
        public WorldPatch LastComputedPatch { get; private set; }

        public bool BeginDraftSession()
        {
            if (mapSaveSystem == null)
            {
                return false;
            }

            BaseWorldState = mapSaveSystem.CaptureCurrentWorldState();
            DraftWorldState = WorldStateCloneUtility.Clone(BaseWorldState);
            LastComputedPatch = null;
            HasActiveDraft = BaseWorldState != null;
            return HasActiveDraft;
        }

        public WorldPatch BuildPatchFromCurrentScene()
        {
            if (!HasActiveDraft || mapSaveSystem == null || BaseWorldState == null)
            {
                return null;
            }

            DraftWorldState = mapSaveSystem.CaptureCurrentWorldState();
            LastComputedPatch = diffBuilder.BuildPatch(BaseWorldState, DraftWorldState);
            return LastComputedPatch;
        }

        public WorldPatch ApplyDraftSession()
        {
            if (!HasActiveDraft || mapSaveSystem == null || BaseWorldState == null)
            {
                return null;
            }

            DraftWorldState = mapSaveSystem.CaptureCurrentWorldState();
            if (autoIncrementBuildVersionOnApply && DraftWorldState != null && DraftWorldState.Versions != null)
            {
                DraftWorldState.Versions.BuildVersion += 1;
            }

            WorldPatch patch = diffBuilder.BuildPatch(BaseWorldState, DraftWorldState);
            LastComputedPatch = patch;
            BaseWorldState = WorldStateCloneUtility.Clone(DraftWorldState);
            DraftWorldState = WorldStateCloneUtility.Clone(BaseWorldState);

            if (DraftWorldState != null && DraftWorldState.Versions != null)
            {
                mapSaveSystem.SetCurrentWorldContext(DraftWorldState.WorldId, DraftWorldState.Versions.BuildVersion, DraftWorldState.Versions.RuntimeVersion);
            }

            HasActiveDraft = false;
            return patch;
        }

        public void CancelDraftSession()
        {
            if (!HasActiveDraft || mapSaveSystem == null || BaseWorldState == null)
            {
                HasActiveDraft = false;
                return;
            }

            mapSaveSystem.ApplyWorldState(WorldStateCloneUtility.Clone(BaseWorldState));
            DraftWorldState = WorldStateCloneUtility.Clone(BaseWorldState);
            LastComputedPatch = null;
            HasActiveDraft = false;
        }

        public void ClearDraftSession()
        {
            BaseWorldState = null;
            DraftWorldState = null;
            LastComputedPatch = null;
            HasActiveDraft = false;
        }
    }
}
