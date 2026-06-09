using System;
using UnityEngine;

namespace MapEditorPrototype
{
    public class MultiplayerEditApplyService : MonoBehaviour
    {
        [SerializeField] private EditDraftSessionController editDraftSessionController;
        [SerializeField] private NetworkSessionManager networkSessionManager;
        [SerializeField] private HostWorldReplicationService hostWorldReplicationService;
        [SerializeField] private ClientWorldReplicationService clientWorldReplicationService;

        public event Action<EditApplyRequestDto> PatchSubmissionRequested;
        public event Action<EditApplyResultDto> PatchAppliedLocally;

        public EditApplyRequestDto BuildApplyRequest(string authorPlayerId)
        {
            if (editDraftSessionController == null)
            {
                return null;
            }

            WorldPatch patch = editDraftSessionController.ApplyDraftSession();
            if (patch == null || !patch.HasAnyChanges)
            {
                return null;
            }

            return new EditApplyRequestDto
            {
                worldId = patch.WorldId,
                authorPlayerId = authorPlayerId,
                patch = WorldNetworkDtoMapper.ToPatchDto(patch)
            };
        }

        public void SubmitCurrentDraft(string authorPlayerId)
        {
            EditApplyRequestDto request = BuildApplyRequest(authorPlayerId);
            if (request == null)
            {
                return;
            }

            if (networkSessionManager != null && networkSessionManager.IsHost && hostWorldReplicationService != null)
            {
                WorldPatch patch = WorldNetworkDtoMapper.FromPatchDto(request.patch);
                WorldPatchDto appliedPatchDto = hostWorldReplicationService.CommitPatchAndBuildDto(patch);
                if (appliedPatchDto != null)
                {
                    EditApplyResultDto result = new EditApplyResultDto
                    {
                        accepted = true,
                        reason = string.Empty,
                        appliedPatch = appliedPatchDto
                    };

                    clientWorldReplicationService?.ApplyPatchDto(appliedPatchDto);
                    PatchAppliedLocally?.Invoke(result);
                }
            }
            else
            {
                PatchSubmissionRequested?.Invoke(request);
            }
        }
    }
}
