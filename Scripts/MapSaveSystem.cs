using System.IO;
using UnityEngine;

namespace MapEditorPrototype
{
    public class MapSaveSystem : MonoBehaviour
    {
        [Header("World References")]
        [SerializeField] private GridBuildingSystem gridBuildingSystem;
        [SerializeField] private BuildCatalog buildCatalog;
        [SerializeField] private WallSystem wallSystem;
        [SerializeField] private WallCatalog wallCatalog;
        [SerializeField] private PathSystem pathSystem;
        [SerializeField] private PathCatalog pathCatalog;
        [SerializeField] private ExplorerController explorerController;
        [SerializeField] private BuildSystem buildSystem;

        [Header("Persistence")]
        [SerializeField] private string defaultFileName = "map_save.json";
        [SerializeField] private bool prettyPrintJson = true;
        [SerializeField] private string currentWorldId = "default_world";
        [SerializeField] private int currentBuildVersion;
        [SerializeField] private int currentRuntimeVersion;

        private WorldStateCaptureService captureService;
        private WorldStateApplyService applyService;
        private WorldStateSerializationService serializationService;

        public string DefaultSavePath => GetPath(defaultFileName);
        public string CurrentWorldId => currentWorldId;
        public int CurrentBuildVersion => currentBuildVersion;
        public int CurrentRuntimeVersion => currentRuntimeVersion;

        private void Awake()
        {
            RebuildServices();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                RebuildServices();
            }
        }

        public void SaveDefault()
        {
            Save(defaultFileName);
        }

        public void LoadDefault()
        {
            Load(defaultFileName);
        }

        public void Save(string fileName)
        {
            string json = CaptureCurrentStateJson();
            string path = GetPath(fileName);
            File.WriteAllText(path, json);
            Debug.Log($"Map saved to: {path}");
        }

        public void Load(string fileName)
        {
            string path = GetPath(fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"MapSaveSystem: file not found: {path}");
                return;
            }

            string json = File.ReadAllText(path);
            LoadFromJson(json);
            Debug.Log($"Map loaded from: {path}");
        }

        public string CaptureCurrentStateJson()
        {
            EnsureServices();
            WorldState worldState = CaptureCurrentWorldState();
            return serializationService.Serialize(worldState);
        }

        public MapSaveData CaptureCurrentStateData()
        {
            return WorldStateDtoMapper.ToSaveData(CaptureCurrentWorldState());
        }

        public WorldState CaptureCurrentWorldState()
        {
            EnsureServices();
            return captureService.Capture(currentWorldId, currentBuildVersion, currentRuntimeVersion);
        }

        public void LoadFromJson(string json)
        {
            EnsureServices();
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("MapSaveSystem: empty json.");
                return;
            }

            WorldState worldState = serializationService.Deserialize(json);
            if (worldState == null)
            {
                Debug.LogWarning("MapSaveSystem: failed to parse save data.");
                return;
            }

            ApplyWorldState(worldState);
        }

        public void ApplyStateData(MapSaveData data)
        {
            ApplyWorldState(WorldStateDtoMapper.FromSaveData(data));
        }

        public void ApplyWorldState(WorldState worldState)
        {
            EnsureServices();
            if (worldState == null)
            {
                return;
            }

            currentWorldId = string.IsNullOrWhiteSpace(worldState.WorldId) ? currentWorldId : worldState.WorldId;
            currentBuildVersion = worldState.Versions != null ? worldState.Versions.BuildVersion : 0;
            currentRuntimeVersion = worldState.Versions != null ? worldState.Versions.RuntimeVersion : 0;
            applyService.Apply(worldState);
        }

        public void SetCurrentWorldContext(string worldId, int buildVersion = 0, int runtimeVersion = 0)
        {
            currentWorldId = string.IsNullOrWhiteSpace(worldId) ? currentWorldId : worldId;
            currentBuildVersion = buildVersion;
            currentRuntimeVersion = runtimeVersion;
        }

        public void IncrementBuildVersion()
        {
            currentBuildVersion++;
        }

        public void IncrementRuntimeVersion()
        {
            currentRuntimeVersion++;
        }

        private void EnsureServices()
        {
            if (captureService == null || applyService == null || serializationService == null)
            {
                RebuildServices();
            }
        }

        private void RebuildServices()
        {
            captureService = new WorldStateCaptureService(
                gridBuildingSystem,
                wallSystem,
                pathSystem,
                explorerController);

            applyService = new WorldStateApplyService(
                gridBuildingSystem,
                buildCatalog,
                wallSystem,
                wallCatalog,
                pathSystem,
                pathCatalog,
                explorerController,
                buildSystem);

            serializationService = new WorldStateSerializationService(prettyPrintJson);
        }

        private string GetPath(string fileName)
        {
            string resolvedFileName = string.IsNullOrWhiteSpace(fileName) ? defaultFileName : fileName;
            return Path.Combine(Application.persistentDataPath, resolvedFileName);
        }
    }
}
