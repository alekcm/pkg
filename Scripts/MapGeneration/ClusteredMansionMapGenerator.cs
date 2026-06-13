using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    /// <summary>
    /// MVP-2 генератор тестового особняка поверх существующего WorldState.
    ///
    /// Это не финальный UI-конструктор шаблонов, а рабочий генератор/адаптер,
    /// который показывает правильную архитектуру:
    /// - карта строится как дерево кластеров;
    /// - кластеры могут содержать кластеры любой глубины;
    /// - комнаты привязаны к кластеру;
    /// - обязательность проверяется универсальным MapGenerationValidator;
    /// - результат экспортируется в обычный WorldState и применяется через MapSaveSystem.
    ///
    /// Чтобы использовать: повесь компонент на объект сцены, назначь MapSaveSystem,
    /// затем вызови GenerateAndApply() из кнопки UI или контекстного меню.
    /// </summary>
    public class ClusteredMansionMapGenerator : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private MapSaveSystem mapSaveSystem;
        [SerializeField] private MapGenerationTemplateLibraryService templateLibrary;

        [Header("Generation")]
        [SerializeField] private int seed = 12345;
        [SerializeField, Min(1)] private int playerCount = 16;
        [SerializeField] private bool includeCourtroom = true;
        [SerializeField] private bool includeOutdoorYard = true;
        [SerializeField] private bool randomizeSeedOnGenerate = true;
        [SerializeField] private bool randomizeGeometryFromSeed = true;
        [SerializeField, Min(0)] private int maxRandomExtraWidth = 8;
        [SerializeField, Min(0)] private int maxRandomExtraLength = 12;
        [SerializeField, Min(1)] private int maxGenerationAttempts = 5;
        [SerializeField] private int retrySeedStep = 9973;
        [SerializeField] private bool logFailedAttempts;
        [SerializeField] private bool useTemplateLibraryForValidation;
        [SerializeField] private string templateId = "core.template.mvp2_mansion_clustered";

        [Header("Catalog Definition Ids")]
        [SerializeField] private string floorDefinitionId = "2";
        [SerializeField] private string wallDefinitionId = "1";
        [SerializeField] private string doorOpeningDefinitionId = "1";
        [SerializeField] private string windowOpeningDefinitionId = "2";
        [SerializeField] private string bedPlaceholderDefinitionId = "3";
        [SerializeField] private string storagePlaceholderDefinitionId = "5";
        [SerializeField] private string smallPlaceholderDefinitionId = "1";
        [SerializeField] private string decorPlaceholderDefinitionId = "4";
        [SerializeField] private string pathDefinitionId = "";

        [Header("Geometry")]
        [SerializeField] private ClusterLayoutPreset clusterLayoutPreset = ClusterLayoutPreset.MansionSpine;
        [SerializeField, Min(8)] private int buildingWidth = 24;
        [SerializeField, Min(12)] private int buildingLength = 34;
        [SerializeField, Min(1)] private int corridorWidth = 2;
        [SerializeField, Min(3)] private int roomDepth = 10;
        [SerializeField, Min(4)] private int roomLength = 6;
        [SerializeField, Min(0.5f)] private float cellSize = 1f;

        private GeneratedMapLayout lastLayout;
        private MapGenerationValidationResult lastValidation;
        private string lastValidationReport;

        public int Seed { get => seed; set => seed = value; }
        public int PlayerCount { get => playerCount; set => playerCount = Mathf.Max(1, value); }
        public bool IncludeCourtroom { get => includeCourtroom; set => includeCourtroom = value; }
        public bool IncludeOutdoorYard { get => includeOutdoorYard; set => includeOutdoorYard = value; }
        public bool RandomizeSeedOnGenerate { get => randomizeSeedOnGenerate; set => randomizeSeedOnGenerate = value; }
        public bool RandomizeGeometryFromSeed { get => randomizeGeometryFromSeed; set => randomizeGeometryFromSeed = value; }
        public int MaxGenerationAttempts { get => maxGenerationAttempts; set => maxGenerationAttempts = Mathf.Max(1, value); }
        public string TemplateId { get => templateId; set => templateId = value; }

        public GeneratedMapLayout LastLayout => lastLayout;
        public MapGenerationValidationResult LastValidation => lastValidation;
        public string LastValidationReport => lastValidationReport;

        [ContextMenu("Generate And Apply MVP-2 Mansion")]
        public void GenerateAndApply()
        {
            if (randomizeSeedOnGenerate)
            {
                seed = Environment.TickCount;
            }

            WorldState state = GenerateWorldState(out lastLayout, out lastValidation);
            lastValidationReport = MapGenerationValidationReportFormatter.Format(lastValidation, lastLayout);
            if (lastValidation != null && !lastValidation.IsValid)
            {
                Debug.LogError(lastValidationReport, this);
                return;
            }

            Debug.Log(lastValidationReport, this);

            if (mapSaveSystem == null)
            {
                mapSaveSystem = FindObjectOfType<MapSaveSystem>();
            }

            if (mapSaveSystem == null)
            {
                Debug.LogError("ClusteredMansionMapGenerator: MapSaveSystem is not assigned and was not found in scene.", this);
                return;
            }

            mapSaveSystem.ApplyWorldState(state);
        }

        public WorldState GenerateWorldState(out GeneratedMapLayout layout, out MapGenerationValidationResult validation)
        {
            int attempts = Mathf.Max(1, maxGenerationAttempts);
            int baseSeed = seed;

            WorldState lastState = null;
            GeneratedMapLayout lastLayout = null;
            MapGenerationValidationResult lastValidation = null;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int attemptSeed = GetAttemptSeed(baseSeed, attempt);
                WorldState state = GenerateWorldStateOnce(attemptSeed, out GeneratedMapLayout attemptLayout, out MapGenerationValidationResult attemptValidation);

                lastState = state;
                lastLayout = attemptLayout;
                lastValidation = attemptValidation;

                if (attemptValidation == null || attemptValidation.IsValid)
                {
                    seed = attemptSeed;
                    layout = attemptLayout;
                    validation = attemptValidation;
                    lastValidationReport = MapGenerationValidationReportFormatter.FormatGenerationSuccess(attempt + 1, attemptLayout, attemptValidation);

                    if (attempt > 0)
                    {
                        Debug.Log($"ClusteredMansionMapGenerator: generation succeeded on attempt {attempt + 1}/{attempts}, seed {attemptSeed}.", this);
                    }

                    return state;
                }

                if (logFailedAttempts)
                {
                    Debug.LogWarning(MapGenerationValidationReportFormatter.FormatGenerationFailure(attempt + 1, attemptLayout, attemptValidation), this);
                }
            }

            layout = lastLayout;
            validation = lastValidation;
            lastValidationReport = MapGenerationValidationReportFormatter.FormatGenerationFailure(attempts, lastLayout, lastValidation);
            return lastState;
        }

        private WorldState GenerateWorldStateOnce(int actualSeed, out GeneratedMapLayout layout, out MapGenerationValidationResult validation)
        {
            System.Random rng = new System.Random(actualSeed);
            int originalBuildingWidth = buildingWidth;
            int originalBuildingLength = buildingLength;

            ApplySeededGeometryVariation(rng);

            try
            {
                string worldId = "generated_mansion_" + Mathf.Abs(actualSeed);
                MapGenerationWorldStateBuilder builder = new MapGenerationWorldStateBuilder(worldId, 1, cellSize, Vector3.zero);

                layout = new GeneratedMapLayout
                {
                    TemplateId = "core.template.mvp2_mansion_clustered",
                    WorldId = worldId,
                    Seed = actualSeed,
                    PlayerCount = playerCount
                };

                BuildClusterTree(layout);
                BuildIndoorFloors(layout, builder, rng);

                if (includeCourtroom)
                {
                    BuildCourtroom(layout, builder);
                }

                if (includeOutdoorYard)
                {
                    BuildOutdoorYard(layout, builder);
                }

                AddWalls(builder);
                AddRuntimeSpawn(builder, layout);

                MapGenerationValidationOptions options = BuildValidationOptions();
                validation = new MapGenerationValidator().Validate(layout, options);
                MapGenerationValidationResult gridValidation = new MapGenerationGridConnectivityValidator().Validate(layout);
                AppendValidation(validation, gridValidation);
                return builder.State;
            }
            finally
            {
                buildingWidth = originalBuildingWidth;
                buildingLength = originalBuildingLength;
            }
        }

        private void ApplySeededGeometryVariation(System.Random rng)
        {
            if (!randomizeGeometryFromSeed || rng == null)
            {
                return;
            }

            int widthStep = 2;
            int lengthStep = Mathf.Max(1, roomLength);

            int widthSteps = Mathf.Max(0, maxRandomExtraWidth / widthStep);
            int lengthSteps = Mathf.Max(0, maxRandomExtraLength / lengthStep);

            int extraWidth = widthSteps <= 0 ? 0 : rng.Next(0, widthSteps + 1) * widthStep;
            int extraLength = lengthSteps <= 0 ? 0 : rng.Next(0, lengthSteps + 1) * lengthStep;

            buildingWidth += extraWidth;
            buildingLength += extraLength;
        }

        private int GetAttemptSeed(int baseSeed, int attempt)
        {
            if (attempt <= 0)
            {
                return baseSeed;
            }

            int step = retrySeedStep == 0 ? 9973 : retrySeedStep;
            unchecked
            {
                return baseSeed + attempt * step;
            }
        }

        private static void AppendValidation(MapGenerationValidationResult target, MapGenerationValidationResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Issues.Count; i++)
            {
                if (source.Issues[i] != null)
                {
                    target.Issues.Add(source.Issues[i]);
                }
            }
        }

        private readonly Dictionary<WallEdge, string> pendingWalls = new Dictionary<WallEdge, string>();

        private void BuildClusterTree(GeneratedMapLayout layout)
        {
            RectInt buildingBounds = new RectInt(0, 0, buildingWidth, buildingLength);
            RectInt rootBounds = new RectInt(-8, -8, buildingWidth + 16, buildingLength + 16);
            MapGenerationTemplateData template = ResolveTemplateForGeneration();
            new TemplateClusterTreeBuilder().BuildInto(layout, template, rootBounds);
            new ClusterLayoutPlanner().Apply(layout, new ClusterLayoutPlannerOptions
            {
                preset = clusterLayoutPreset,
                mapBounds = rootBounds,
                buildingBounds = buildingBounds,
                buildingWidth = buildingWidth,
                buildingLength = buildingLength,
                roomDepth = roomDepth,
                roomLength = roomLength,
                outdoorPadding = 8,
                includeCourtroom = includeCourtroom,
                includeOutdoorYard = includeOutdoorYard
            });
        }

        private MapGenerationTemplateData ResolveTemplateForGeneration()
        {
            MapGenerationTemplateData template = null;

            if (useTemplateLibraryForValidation)
            {
                if (templateLibrary == null)
                {
                    templateLibrary = FindObjectOfType<MapGenerationTemplateLibraryService>();
                }

                if (templateLibrary != null)
                {
                    template = templateLibrary.FindById(templateId);
                }
            }

            return template ?? DefaultMansionTemplateFactory.Create(includeCourtroom, includeOutdoorYard);
        }

        private void BuildIndoorFloors(GeneratedMapLayout layout, MapGenerationWorldStateBuilder builder, System.Random rng)
        {
            pendingWalls.Clear();

            List<PlannedFloorLayout> plannedFloors = new ClusterRoomSlotPlanner().PlanMansionSpineRooms(
                layout,
                playerCount,
                buildingWidth,
                buildingLength,
                corridorWidth,
                roomDepth,
                roomLength);

            int corridorX = Mathf.Clamp((buildingWidth - corridorWidth) / 2, roomDepth + 1, buildingWidth - roomDepth - corridorWidth - 1);

            for (int i = 0; i < plannedFloors.Count; i++)
            {
                PlannedFloorLayout plannedFloor = plannedFloors[i];
                if (plannedFloor == null)
                {
                    continue;
                }

                GeneratedFloorPlan floorPlan = layout.GetOrCreateFloor(plannedFloor.floor);
                floorPlan.BuildingKey = "mansion";
                floorPlan.Bounds = plannedFloor.bounds;

                FillFloorRect(builder, plannedFloor.floor, floorPlan.Bounds);
                AddRectWalls(floorPlan.Bounds, plannedFloor.floor, null);

                floorPlan.CorridorCells.AddRange(plannedFloor.corridorCells);

                for (int slotIndex = 0; slotIndex < plannedFloor.roomSlots.Count; slotIndex++)
                {
                    PlannedRoomSlot slot = plannedFloor.roomSlots[slotIndex];
                    if (slot == null)
                    {
                        continue;
                    }

                    GeneratedRoomPlan room = AddRoom(
                        layout,
                        floorPlan,
                        builder,
                        slot.rect,
                        slot.floor,
                        slot.roomId,
                        slot.locationId,
                        slot.displayName,
                        slot.clusterId,
                        slot.required,
                        slot.tags.ToArray());

                    if (SlotHasTag(slot, "sys:personal_room"))
                    {
                        int personalIndex = ExtractTrailingNumber(slot.roomId) - 1;
                        if (personalIndex < 0) personalIndex = slotIndex;
                        room.OwnerPlayerId = "player_" + (personalIndex + 1);
                        PlaceBedroomPlaceholders(builder, slot.rect, slot.floor, personalIndex);
                    }
                }

                AddStairsAndElevator(layout, builder, plannedFloor.floor, corridorX, plannedFloor.bounds.yMax);
            }
        }

        private static bool SlotHasTag(PlannedRoomSlot slot, string tag)
        {
            if (slot == null || slot.tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (int i = 0; i < slot.tags.Count; i++)
            {
                if (string.Equals(slot.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ExtractTrailingNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            int end = value.Length - 1;
            while (end >= 0 && char.IsDigit(value[end]))
            {
                end--;
            }

            if (end == value.Length - 1)
            {
                return -1;
            }

            string number = value.Substring(end + 1);
            int result;
            return int.TryParse(number, out result) ? result : -1;
        }

        private void AddPersonalRoom(GeneratedMapLayout layout, GeneratedFloorPlan floorPlan, MapGenerationWorldStateBuilder builder, RectInt rect, int floor, string clusterId, int index)
        {
            GeneratedRoomPlan room = AddRoom(layout, floorPlan, builder, rect, floor,
                "personal_room_" + (index + 1),
                "core.location.personal_room",
                "Комната игрока " + (index + 1),
                clusterId,
                true,
                "sys:personal_room",
                "cat:bedroom");

            room.OwnerPlayerId = "player_" + (index + 1);
            PlaceBedroomPlaceholders(builder, rect, floor, index);
        }

        private GeneratedRoomPlan AddRoom(GeneratedMapLayout layout, GeneratedFloorPlan floorPlan, MapGenerationWorldStateBuilder builder, RectInt rect, int floor, string roomId, string locationId, string displayName, string clusterId, bool required, params string[] tags)
        {
            GeneratedRoomPlan room = new GeneratedRoomPlan
            {
                RoomId = roomId,
                LocationId = locationId,
                ClusterId = clusterId,
                DisplayName = displayName,
                Placement = GeneratedLocationPlacement.Indoor,
                Floor = floor,
                Rect = rect,
                Required = required
            };

            if (tags != null)
            {
                room.Tags.AddRange(tags);
            }

            WallEdge door = GetDoorEdge(rect, floor);
            room.DoorEdges.Add(door);
            AddRectWalls(rect, floor, null);
            SetOpening(door, doorOpeningDefinitionId);

            layout.Connectors.Add(new GeneratedConnectorPlan
            {
                ConnectorId = "door_" + roomId,
                Kind = GeneratedConnectorKind.Door,
                FromId = roomId,
                ToId = "corridor_f" + floor,
                FromFloor = floor,
                ToFloor = floor,
                FromCell = room.CenterCell,
                ToCell = new Vector2Int(door.x, door.y),
                Edge = door,
                DefinitionId = doorOpeningDefinitionId
            });

            AddWindows(rect, floor);
            floorPlan.Rooms.Add(room);

            GeneratedClusterPlan cluster = layout.FindCluster(clusterId);
            if (cluster != null && !cluster.RoomIds.Contains(roomId))
            {
                cluster.RoomIds.Add(roomId);
            }

            return room;
        }

        private WallEdge GetDoorEdge(RectInt rect, int floor)
        {
            int midZ = rect.yMin + Mathf.Clamp(rect.height / 2, 1, rect.height - 2);
            int corridorStart = Mathf.Clamp((buildingWidth - corridorWidth) / 2, roomDepth + 1, buildingWidth - roomDepth - corridorWidth - 1);
            if (rect.xMax <= corridorStart)
            {
                return new WallEdge(rect.xMax, midZ, WallOrientation.Vertical, floor);
            }

            return new WallEdge(rect.xMin, midZ, WallOrientation.Vertical, floor);
        }

        private void FillFloorRect(MapGenerationWorldStateBuilder builder, int floor, RectInt rect)
        {
            if (string.IsNullOrWhiteSpace(floorDefinitionId))
            {
                return;
            }

            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int z = rect.yMin; z < rect.yMax; z++)
                {
                    builder.AddGridObject(floorDefinitionId, new Vector2Int(x, z), floor);
                }
            }
        }

        private void AddRectWalls(RectInt rect, int floor, string openingDefinitionId)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                AddWall(new WallEdge(x, rect.yMin, WallOrientation.Horizontal, floor), openingDefinitionId);
                AddWall(new WallEdge(x, rect.yMax, WallOrientation.Horizontal, floor), openingDefinitionId);
            }

            for (int z = rect.yMin; z < rect.yMax; z++)
            {
                AddWall(new WallEdge(rect.xMin, z, WallOrientation.Vertical, floor), openingDefinitionId);
                AddWall(new WallEdge(rect.xMax, z, WallOrientation.Vertical, floor), openingDefinitionId);
            }
        }

        private void AddWall(WallEdge edge, string openingDefinitionId)
        {
            if (pendingWalls.ContainsKey(edge))
            {
                if (!string.IsNullOrWhiteSpace(openingDefinitionId))
                {
                    pendingWalls[edge] = openingDefinitionId;
                }
                return;
            }

            pendingWalls.Add(edge, openingDefinitionId);
        }

        private void SetOpening(WallEdge edge, string openingDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(openingDefinitionId))
            {
                return;
            }

            pendingWalls[edge] = openingDefinitionId;
        }

        private void AddWindows(RectInt rect, int floor)
        {
            if (string.IsNullOrWhiteSpace(windowOpeningDefinitionId))
            {
                return;
            }

            if (rect.xMin <= 1)
            {
                int z = rect.yMin + rect.height / 2;
                SetOpening(new WallEdge(rect.xMin, z, WallOrientation.Vertical, floor), windowOpeningDefinitionId);
            }
            else if (rect.xMax >= buildingWidth - 1)
            {
                int z = rect.yMin + rect.height / 2;
                SetOpening(new WallEdge(rect.xMax, z, WallOrientation.Vertical, floor), windowOpeningDefinitionId);
            }
        }

        private void AddWalls(MapGenerationWorldStateBuilder builder)
        {
            foreach (KeyValuePair<WallEdge, string> pair in pendingWalls)
            {
                builder.AddWall(wallDefinitionId, pair.Key, pair.Value);
            }
        }

        private void PlaceBedroomPlaceholders(MapGenerationWorldStateBuilder builder, RectInt rect, int floor, int index)
        {
            builder.AddGridObject(bedPlaceholderDefinitionId, new Vector2Int(rect.xMin + 1, rect.yMin + 1), floor, 0, 0f);
            builder.AddGridObject(storagePlaceholderDefinitionId, new Vector2Int(rect.xMax - 3, rect.yMin + 1), floor, 0, 0f);
            if (index % 3 == 0)
            {
                builder.AddGridObject(decorPlaceholderDefinitionId, new Vector2Int(rect.xMin + 1, rect.yMax - 2), floor, 0, 0f);
            }
        }

        private void AddStairsAndElevator(GeneratedMapLayout layout, MapGenerationWorldStateBuilder builder, int floor, int corridorX, int length)
        {
            int stairZ = length - 4;
            string stairRoomId = "stairs_f" + floor;
            string elevatorRoomId = "elevator_f" + floor;

            GeneratedClusterPlan mansion = layout.FindCluster("mansion");
            if (mansion != null)
            {
                // Логические системные точки пока не комнаты, поэтому только connector endpoints.
            }

            builder.AddGridObject(smallPlaceholderDefinitionId, new Vector2Int(corridorX, stairZ), floor);
            builder.AddGridObject(smallPlaceholderDefinitionId, new Vector2Int(corridorX + 1, stairZ), floor);

            if (floor == 0)
            {
                layout.Connectors.Add(new GeneratedConnectorPlan
                {
                    ConnectorId = "stairs_0_1",
                    Kind = GeneratedConnectorKind.Stairs,
                    FromId = "corridor_f0",
                    ToId = "corridor_f1",
                    FromFloor = 0,
                    ToFloor = 1,
                    FromCell = new Vector2Int(corridorX, stairZ),
                    ToCell = new Vector2Int(corridorX, stairZ),
                    DefinitionId = smallPlaceholderDefinitionId
                });

                if (includeCourtroom)
                {
                    builder.AddGridObject(decorPlaceholderDefinitionId, new Vector2Int(corridorX, 2), 0);
                    layout.Connectors.Add(new GeneratedConnectorPlan
                    {
                        ConnectorId = "elevator_to_courtroom",
                        Kind = GeneratedConnectorKind.Elevator,
                        FromId = "corridor_f0",
                        ToId = "courtroom",
                        FromFloor = 0,
                        ToFloor = -1,
                        FromCell = new Vector2Int(corridorX, 2),
                        ToCell = new Vector2Int(buildingWidth / 2, buildingLength / 2),
                        DefinitionId = decorPlaceholderDefinitionId
                    });
                }
            }
        }

        private void BuildCourtroom(GeneratedMapLayout layout, MapGenerationWorldStateBuilder builder)
        {
            GeneratedClusterPlan courtCluster = layout.FindCluster("court_block");
            RectInt courtBounds = courtCluster != null && courtCluster.Bounds.width > 0 && courtCluster.Bounds.height > 0
                ? courtCluster.Bounds
                : new RectInt(2, 2, buildingWidth - 4, buildingLength - 4);
            int courtFloor = courtCluster != null ? courtCluster.Floor : -1;

            GeneratedFloorPlan floorPlan = layout.GetOrCreateFloor(courtFloor);
            floorPlan.BuildingKey = "mansion_basement";
            floorPlan.Bounds = courtBounds;

            RectInt courtRect = InsetRect(courtBounds, 2);
            FillFloorRect(builder, courtFloor, floorPlan.Bounds);
            AddRectWalls(floorPlan.Bounds, courtFloor, null);

            GeneratedRoomPlan court = new GeneratedRoomPlan
            {
                RoomId = "courtroom",
                LocationId = "core.location.courtroom",
                ClusterId = "court_block",
                DisplayName = "Зал суда",
                Placement = GeneratedLocationPlacement.Indoor,
                Floor = courtFloor,
                Rect = courtRect,
                Required = true,
                Windowless = true
            };
            court.Tags.Add("sys:courtroom");
            court.Tags.Add("cat:courtroom");
            WallEdge door = new WallEdge(courtRect.xMin + courtRect.width / 2, courtRect.yMin, WallOrientation.Horizontal, courtFloor);
            court.DoorEdges.Add(door);
            SetOpening(door, doorOpeningDefinitionId);
            floorPlan.Rooms.Add(court);

            GeneratedClusterPlan cluster = layout.FindCluster("court_block");
            if (cluster != null)
            {
                cluster.RoomIds.Add(court.RoomId);
            }

            Vector2Int center = new Vector2Int(courtRect.xMin + courtRect.width / 2, courtRect.yMin + courtRect.height / 2);
            builder.AddGridObject(decorPlaceholderDefinitionId, center, courtFloor);

            int stands = Mathf.Max(1, playerCount);
            float radius = Mathf.Min(courtRect.width, courtRect.height) * 0.35f;
            for (int i = 0; i < stands; i++)
            {
                float angle = (Mathf.PI * 2f * i) / stands;
                int x = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * radius);
                int z = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * radius);
                builder.AddGridObject(smallPlaceholderDefinitionId, new Vector2Int(x, z), courtFloor, 0, -angle * Mathf.Rad2Deg + 180f);
            }
        }

        private static RectInt InsetRect(RectInt rect, int inset)
        {
            int safeInset = Mathf.Max(0, inset);
            int width = Mathf.Max(1, rect.width - safeInset * 2);
            int height = Mathf.Max(1, rect.height - safeInset * 2);
            return new RectInt(rect.xMin + safeInset, rect.yMin + safeInset, width, height);
        }

        private void BuildOutdoorYard(GeneratedMapLayout layout, MapGenerationWorldStateBuilder builder)
        {
            GeneratedClusterPlan outdoorCluster = layout.FindCluster("outdoor_grounds");
            RectInt yardBounds = outdoorCluster != null && outdoorCluster.Bounds.width > 0 && outdoorCluster.Bounds.height > 0
                ? outdoorCluster.Bounds
                : new RectInt(-8, -8, buildingWidth + 16, buildingLength + 16);

            GeneratedOutdoorZonePlan yard = new GeneratedOutdoorZonePlan
            {
                ZoneId = "yard",
                LocationId = "core.location.yard",
                ClusterId = "outdoor_grounds",
                DisplayName = "Двор",
                Bounds = yardBounds,
                TerraceLevel = 0,
                Required = true
            };
            yard.Tags.Add("cat:outdoor");
            yard.Tags.Add("yard");
            int entranceX = buildingWidth / 2;
            int gateZ = yardBounds.yMin + 2;
            yard.InterestPoints.Add(new Vector2Int(entranceX, gateZ));
            yard.InterestPoints.Add(new Vector2Int(entranceX, 1));
            layout.OutdoorZones.Add(yard);

            GeneratedClusterPlan cluster = layout.FindCluster("outdoor_grounds");
            if (cluster != null)
            {
                cluster.OutdoorZoneIds.Add(yard.ZoneId);
            }

            layout.Connectors.Add(new GeneratedConnectorPlan
            {
                ConnectorId = "entrance_to_yard",
                Kind = GeneratedConnectorKind.OutdoorPath,
                FromId = "corridor_f0",
                ToId = "yard",
                FromFloor = 0,
                ToFloor = 0,
                FromCell = new Vector2Int(entranceX, 1),
                ToCell = new Vector2Int(entranceX, gateZ)
            });

            if (!string.IsNullOrWhiteSpace(pathDefinitionId))
            {
                builder.AddPath(pathDefinitionId, new List<Vector3>
                {
                    new Vector3(entranceX + 0.5f, 0f, gateZ + 0.5f),
                    new Vector3(entranceX + 0.5f, 0f, -2.0f),
                    new Vector3(entranceX + 0.5f, 0f, -0.5f)
                }, 1.5f);
            }
        }

        private MapGenerationValidationOptions BuildValidationOptions()
        {
            MapGenerationTemplateData template = ResolveTemplateForGeneration();
            return MapGenerationTemplateRuleConverter.ToValidationOptions(template, playerCount, "personal_room_1");
        }

        private void AddRuntimeSpawn(MapGenerationWorldStateBuilder builder, GeneratedMapLayout layout)
        {
            GeneratedRoomPlan firstRoom = layout.FindRoom("personal_room_1");
            if (firstRoom != null)
            {
                Vector2Int cell = firstRoom.CenterCell;
                builder.SetExplorerSpawn(new Vector3(cell.x + 0.5f, FloorContext.FloorY(firstRoom.Floor) + 1f, cell.y + 0.5f), 0f);
            }
        }
    }
}
