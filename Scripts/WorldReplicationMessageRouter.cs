using UnityEngine;

namespace MapEditorPrototype
{
    public class WorldReplicationMessageRouter : MonoBehaviour
    {
        [SerializeField] private NgoNamedMessageBridge messageBridge;
        [SerializeField] private NgoTransportAdapter transportAdapter;
        [SerializeField] private HostWorldReplicationService hostWorldReplicationService;
        [SerializeField] private ClientWorldReplicationService clientWorldReplicationService;
        [SerializeField] private MultiplayerEditApplyService multiplayerEditApplyService;
        [SerializeField] private NetworkSessionManager networkSessionManager;

        private readonly WorldNetworkSerializationService serializationService = new WorldNetworkSerializationService(false);

        private void OnEnable()
        {
            if (messageBridge != null)
            {
                messageBridge.SnapshotRequestReceived += HandleSnapshotRequestReceived;
                messageBridge.SnapshotReceived += HandleSnapshotReceived;
                messageBridge.PatchReceived += HandlePatchReceived;
                messageBridge.EditApplyRequestReceived += HandleEditApplyRequestReceived;
                messageBridge.EditApplyResultReceived += HandleEditApplyResultReceived;
            }

            if (multiplayerEditApplyService != null)
            {
                multiplayerEditApplyService.PatchSubmissionRequested += HandleLocalPatchSubmissionRequested;
                multiplayerEditApplyService.PatchAppliedLocally += HandleLocalPatchApplied;
            }
        }

        private void OnDisable()
        {
            if (messageBridge != null)
            {
                messageBridge.SnapshotRequestReceived -= HandleSnapshotRequestReceived;
                messageBridge.SnapshotReceived -= HandleSnapshotReceived;
                messageBridge.PatchReceived -= HandlePatchReceived;
                messageBridge.EditApplyRequestReceived -= HandleEditApplyRequestReceived;
                messageBridge.EditApplyResultReceived -= HandleEditApplyResultReceived;
            }

            if (multiplayerEditApplyService != null)
            {
                multiplayerEditApplyService.PatchSubmissionRequested -= HandleLocalPatchSubmissionRequested;
                multiplayerEditApplyService.PatchAppliedLocally -= HandleLocalPatchApplied;
            }
        }

        public void RequestWorldSnapshotFromHost()
        {
            messageBridge?.RequestSnapshotFromServer();
        }

        private void HandleSnapshotRequestReceived(ulong senderClientId)
        {
            if (networkSessionManager == null || !networkSessionManager.IsHost || hostWorldReplicationService == null || messageBridge == null)
            {
                return;
            }

            WorldSnapshotDto snapshotDto = hostWorldReplicationService.BuildSnapshotDto();
            if (snapshotDto == null)
            {
                return;
            }

            string json = serializationService.SerializeSnapshot(snapshotDto);
            messageBridge.SendSnapshotToClient(senderClientId, json);
        }

        private void HandleSnapshotReceived(ulong senderClientId, string payload)
        {
            if (networkSessionManager == null || !networkSessionManager.IsClient || clientWorldReplicationService == null)
            {
                return;
            }

            WorldSnapshotDto snapshotDto = serializationService.DeserializeSnapshot(payload);
            if (snapshotDto == null)
            {
                return;
            }

            clientWorldReplicationService.ApplySnapshotDto(snapshotDto);
            networkSessionManager.UpdateVersions(snapshotDto.buildVersion, snapshotDto.runtimeVersion);
        }

        private void HandlePatchReceived(ulong senderClientId, string payload)
        {
            if (networkSessionManager == null || !networkSessionManager.IsClient || clientWorldReplicationService == null)
            {
                return;
            }

            WorldPatchDto patchDto = serializationService.DeserializePatch(payload);
            if (patchDto == null)
            {
                return;
            }

            clientWorldReplicationService.ApplyPatchDto(patchDto);
            networkSessionManager.UpdateVersions(patchDto.newBuildVersion, networkSessionManager.CurrentSession != null ? networkSessionManager.CurrentSession.RuntimeVersion : 0);
        }

        private void HandleLocalPatchSubmissionRequested(EditApplyRequestDto request)
        {
            if (request == null || messageBridge == null)
            {
                return;
            }

            string json = serializationService.SerializeApplyRequest(request);
            messageBridge.SendEditApplyRequestToServer(json);
        }

        private void HandleEditApplyRequestReceived(ulong senderClientId, string payload)
        {
            if (networkSessionManager == null || !networkSessionManager.IsHost || hostWorldReplicationService == null || messageBridge == null)
            {
                return;
            }

            EditApplyRequestDto requestDto = serializationService.DeserializeApplyRequest(payload);
            if (requestDto == null || requestDto.patch == null)
            {
                return;
            }

            WorldPatch patch = WorldNetworkDtoMapper.FromPatchDto(requestDto.patch);
            WorldPatchDto appliedPatchDto = hostWorldReplicationService.CommitPatchAndBuildDto(patch);
            if (appliedPatchDto == null)
            {
                EditApplyResultDto rejected = new EditApplyResultDto
                {
                    accepted = false,
                    reason = "Patch was empty or invalid.",
                    appliedPatch = null
                };
                messageBridge.SendEditApplyResultToClient(senderClientId, serializationService.SerializeApplyResult(rejected));
                return;
            }

            EditApplyResultDto accepted = new EditApplyResultDto
            {
                accepted = true,
                reason = string.Empty,
                appliedPatch = appliedPatchDto
            };

            messageBridge.SendEditApplyResultToClient(senderClientId, serializationService.SerializeApplyResult(accepted));
            messageBridge.BroadcastPatch(serializationService.SerializePatch(appliedPatchDto), transportAdapter);
        }

        private void HandleEditApplyResultReceived(ulong senderClientId, string payload)
        {
            if (networkSessionManager == null || !networkSessionManager.IsClient)
            {
                return;
            }

            EditApplyResultDto resultDto = serializationService.DeserializeApplyResult(payload);
            if (resultDto == null)
            {
                return;
            }

            if (resultDto.accepted && resultDto.appliedPatch != null)
            {
                clientWorldReplicationService?.ApplyPatchDto(resultDto.appliedPatch);
            }
            else if (!string.IsNullOrWhiteSpace(resultDto.reason))
            {
                Debug.LogWarning($"WorldReplicationMessageRouter: patch rejected by host: {resultDto.reason}");
            }
        }

        private void HandleLocalPatchApplied(EditApplyResultDto result)
        {
            if (networkSessionManager == null || !networkSessionManager.IsHost || result == null || !result.accepted || result.appliedPatch == null || messageBridge == null)
            {
                return;
            }

            string patchJson = serializationService.SerializePatch(result.appliedPatch);
            messageBridge.BroadcastPatch(patchJson, transportAdapter);
        }
    }
}
