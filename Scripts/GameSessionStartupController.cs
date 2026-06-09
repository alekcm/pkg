using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapEditorPrototype
{
    public class GameSessionStartupController : MonoBehaviour
    {
        [SerializeField] private string fallbackMainMenuSceneName = "MainMenu";
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private MultiplayerBootstrapService multiplayerBootstrapService;
        [SerializeField] private NetworkSessionManager networkSessionManager;
        [SerializeField] private TMP_Text statusLabel;

        private async void Start()
        {
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            GameLaunchConfig config = GameLaunchContext.Current;
            if (config == null || config.Mode == SessionLaunchMode.None)
            {
                SetStatus("No launch config. Returning to main menu.");
                await Task.Delay(1000);
                SceneManager.LoadScene(fallbackMainMenuSceneName);
                return;
            }

            switch (config.Mode)
            {
                case SessionLaunchMode.SoloEdit:
                    LoadWorldFromConfig(config);
                    networkSessionManager?.StartSoloSession(config.WorldId);
                    SetStatus($"Loaded solo world: {config.WorldId}");
                    break;
                case SessionLaunchMode.HostRelay:
                    LoadWorldFromConfig(config);
                    SetStatus("Starting relay host...");
                    if (multiplayerBootstrapService != null)
                    {
                        bool hostStarted = await multiplayerBootstrapService.HostRelaySessionAsync();
                        SetStatus(hostStarted ? "Relay host started." : "Failed to start relay host.");
                    }
                    break;
                case SessionLaunchMode.JoinRelay:
                    SetStatus("Joining relay...");
                    if (multiplayerBootstrapService != null)
                    {
                        bool joined = await multiplayerBootstrapService.JoinRelaySessionAsync(config.JoinCode);
                        SetStatus(joined ? "Joined relay session." : "Failed to join relay session.");
                    }
                    break;
                case SessionLaunchMode.HostLan:
                    LoadWorldFromConfig(config);
                    SetStatus("Starting LAN host...");
                    if (multiplayerBootstrapService != null)
                    {
                        bool lanHostStarted = multiplayerBootstrapService.HostLanSession(config.Port);
                        SetStatus(lanHostStarted ? "LAN host started." : "Failed to start LAN host.");
                    }
                    break;
                case SessionLaunchMode.JoinLan:
                    SetStatus("Joining LAN session...");
                    if (multiplayerBootstrapService != null)
                    {
                        bool lanJoined = multiplayerBootstrapService.JoinLanSession(config.Address, config.Port);
                        SetStatus(lanJoined ? "Joined LAN session." : "Failed to join LAN session.");
                    }
                    break;
            }
        }

        public void ReturnToMainMenu()
        {
            multiplayerBootstrapService?.ShutdownSession();
            GameLaunchContext.Clear();
            SceneManager.LoadScene(fallbackMainMenuSceneName);
        }

        private void LoadWorldFromConfig(GameLaunchConfig config)
        {
            if (mapSaveSystem == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(config.WorldId))
            {
                mapSaveSystem.SetCurrentWorldContext(config.WorldId);
            }

            if (!string.IsNullOrWhiteSpace(config.WorldFileName) && WorldLibraryService.Exists(config.WorldFileName))
            {
                mapSaveSystem.Load(config.WorldFileName);
            }
            else
            {
                mapSaveSystem.ApplyWorldState(new WorldState { WorldId = string.IsNullOrWhiteSpace(config.WorldId) ? System.Guid.NewGuid().ToString("N") : config.WorldId });
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
