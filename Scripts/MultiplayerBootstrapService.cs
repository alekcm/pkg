using System.Threading.Tasks;
using UnityEngine;

namespace MapEditorPrototype
{
    public class MultiplayerBootstrapService : MonoBehaviour
    {
        [SerializeField] private NetworkSessionManager networkSessionManager;
        [SerializeField] private RelayConnectionService relayConnectionService;
        [SerializeField] private NgoTransportAdapter ngoTransportAdapter;
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private WorldReplicationMessageRouter worldReplicationMessageRouter;
        [SerializeField] private int defaultMaxPlayers = 8;
        [SerializeField] private string localPlayerId = "local_player";

        public async Task<bool> HostRelaySessionAsync()
        {
            if (relayConnectionService == null || ngoTransportAdapter == null || mapSaveSystem == null || networkSessionManager == null)
            {
                return false;
            }

            RelayAllocationData relayData = await relayConnectionService.CreateRelayHostAsync(defaultMaxPlayers - 1);
            if (relayData == null)
            {
                return false;
            }

            bool started = ngoTransportAdapter.StartHostWithRelay(relayData);
            if (!started)
            {
                return false;
            }

            networkSessionManager.StartHostSession(
                mapSaveSystem.CurrentWorldId,
                relayData.JoinCode,
                localPlayerId,
                mapSaveSystem.CurrentBuildVersion,
                mapSaveSystem.CurrentRuntimeVersion,
                defaultMaxPlayers);

            return true;
        }

        public async Task<bool> JoinRelaySessionAsync(string joinCode)
        {
            if (relayConnectionService == null || ngoTransportAdapter == null || networkSessionManager == null)
            {
                return false;
            }

            RelayAllocationData relayData = await relayConnectionService.JoinRelayAsync(joinCode);
            if (relayData == null)
            {
                return false;
            }

            bool started = ngoTransportAdapter.StartClientWithRelay(relayData);
            if (!started)
            {
                return false;
            }

            networkSessionManager.JoinClientSession(string.Empty, joinCode, string.Empty, 0, 0, defaultMaxPlayers);
            worldReplicationMessageRouter?.RequestWorldSnapshotFromHost();
            return true;
        }

        public bool HostLanSession(ushort port, string listenAddress = "0.0.0.0")
        {
            if (ngoTransportAdapter == null || mapSaveSystem == null || networkSessionManager == null)
            {
                return false;
            }

            bool started = ngoTransportAdapter.StartHostLan(port, listenAddress);
            if (!started)
            {
                return false;
            }

            networkSessionManager.StartHostSession(
                mapSaveSystem.CurrentWorldId,
                string.Empty,
                localPlayerId,
                mapSaveSystem.CurrentBuildVersion,
                mapSaveSystem.CurrentRuntimeVersion,
                defaultMaxPlayers);
            return true;
        }

        public bool JoinLanSession(string address, ushort port)
        {
            if (ngoTransportAdapter == null || networkSessionManager == null)
            {
                return false;
            }

            bool started = ngoTransportAdapter.StartClientLan(address, port);
            if (!started)
            {
                return false;
            }

            networkSessionManager.JoinClientSession(string.Empty, string.Empty, string.Empty, 0, 0, defaultMaxPlayers);
            worldReplicationMessageRouter?.RequestWorldSnapshotFromHost();
            return true;
        }

        public void ShutdownSession()
        {
            ngoTransportAdapter?.Shutdown();
            networkSessionManager?.EndSession();
        }

        public void SetLocalPlayerId(string playerId)
        {
            localPlayerId = string.IsNullOrWhiteSpace(playerId) ? localPlayerId : playerId;
        }
    }
}
