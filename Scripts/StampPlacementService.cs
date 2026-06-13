using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    /// <summary>
    /// «Вставить штамп»: проверка возможности и вставка содержимого StampData
    /// в мир с поворотом 0/90/180/270. Формирует WorldPatch-пару для
    /// undo/redo и мультиплеер-синхронизации (та же схема, что у
    /// TryPaintRoomBatch в BuildWorldCommandService).
    /// </summary>
    public class StampPlacementService
    {
        private readonly GridBuildingSystem grid;
        private readonly WallSystem walls;
        private readonly PathSystem paths;
        private readonly BuildCatalog buildCatalog;
        private readonly WallCatalog wallCatalog;
        private readonly PathCatalog pathCatalog;

        public StampPlacementService(
            GridBuildingSystem grid, WallSystem walls, PathSystem paths,
            BuildCatalog buildCatalog, WallCatalog wallCatalog, PathCatalog pathCatalog)
        {
            this.grid = grid;
            this.walls = walls;
            this.paths = paths;
            this.buildCatalog = buildCatalog;
            this.wallCatalog = wallCatalog;
            this.pathCatalog = pathCatalog;
        }

        /// <summary>Footprint штампа с учётом поворота.</summary>
        public static Vector2Int RotatedFootprint(StampData stamp, int rotationSteps)
        {
            return GridBuildingSystem.RotateFootprint(new Vector2Int(stamp.footprintW, stamp.footprintL), rotationSteps);
        }

        /// <summary>
        /// Поворот локального прямоугольника (например, clearance-зоны)
        /// внутри области areaW×areaL на rotationSteps CCW. Прямоугольник
        /// может выходить за пределы области — формула аффинная и работает.
        /// </summary>
        public static RectInt RotateLocalRect(RectInt rect, int areaW, int areaL, int rotationSteps)
        {
            int steps = ((rotationSteps % 4) + 4) % 4;
            int x = rect.xMin, y = rect.yMin, w = rect.width, l = rect.height;
            int aw = areaW, al = areaL;

            for (int i = 0; i < steps; i++)
            {
                int nx = y;
                int ny = aw - x - w;
                x = nx; y = ny;

                int t = w; w = l; l = t;
                t = aw; aw = al; al = t;
            }

            return new RectInt(x, y, w, l);
        }

        /// <summary>
        /// Можно ли вставить штамп так, чтобы ВСЕ его grid-объекты и стены
        /// встали без коллизий. originCell — юго-западная клетка штампа
        /// (основная сетка) после поворота.
        /// </summary>
        public bool CanPlace(StampData stamp, Vector2Int originCell, int rotationSteps, float baseYOffset = 0f)
        {
            foreach (StampPlacedObject o in stamp.content.placedObjects)
            {
                if (!o.useGridPlacement) continue; // free-объекты не конфликтуют по сетке
                BuildingDefinition def = buildCatalog.FindById(o.definitionId);
                if (def == null) return false;

                Vector2Int cell = TransformObjectCell(stamp, o, def, originCell, rotationSteps);
                int rot = o.rotationSteps + rotationSteps;
                if (!grid.CanPlace(def, cell, rot, grid.GridOrigin.y + o.baseY + baseYOffset)) return false;
            }

            foreach (StampWall w in stamp.content.walls)
            {
                WallDefinition def = wallCatalog.FindWallById(w.wallDefinitionId);
                if (def == null) return false;
                WallEdge edge = TransformWallEdge(stamp, w, originCell, rotationSteps, baseYOffset);
                // Существующая стена на том же ребре — не конфликт (пропустим её при вставке).
                if (walls.GetSegment(edge) == null && !walls.CanPlaceWall(def, edge)) return false;
            }

            return true;
        }

        /// <summary>
        /// Вставка. Возвращает forward/backward-патчи (без WorldId — его
        /// проставляет вызывающий). null = не удалось.
        /// </summary>
        public bool TryPlace(StampData stamp, Vector2Int originCell, int rotationSteps,
            out WorldPatch forward, out WorldPatch backward, float baseYOffset = 0f)
        {
            forward = new WorldPatch();
            backward = new WorldPatch();

            if (!CanPlace(stamp, originCell, rotationSteps, baseYOffset)) return false;

            float cellSize = grid.CellSize;
            Vector3 cornerWorld = grid.GridOrigin + new Vector3(originCell.x * cellSize, 0f, originCell.y * cellSize);

            // --- Объекты ---
            foreach (StampPlacedObject o in stamp.content.placedObjects)
            {
                BuildingDefinition def = buildCatalog.FindById(o.definitionId);
                if (def == null) continue;

                PlacedObject placed;
                if (o.useGridPlacement)
                {
                    Vector2Int cell = TransformObjectCell(stamp, o, def, originCell, rotationSteps);
                    int rot = o.rotationSteps + rotationSteps;
                    placed = grid.Place(def, cell, rot, grid.GridOrigin.y + o.baseY + baseYOffset, NormalizedYaw(rot));
                }
                else
                {
                    Vector3 local = o.localPosition.ToVector3();
                    Vector3 world = cornerWorld + RotateLocal(local, stamp, rotationSteps, cellSize) + Vector3.up * baseYOffset;
                    placed = grid.PlaceFree(def, world, o.rotationSteps + rotationSteps,
                        o.rotationY + rotationSteps * 90f);
                }

                if (placed != null)
                {
                    forward.UpsertPlacedObjects.Add(placed.GetState());
                    backward.DeletePlacedObjectIds.Add(placed.ObjectId);
                }
            }

            // --- Стены ---
            foreach (StampWall w in stamp.content.walls)
            {
                WallDefinition def = wallCatalog.FindWallById(w.wallDefinitionId);
                if (def == null) continue;
                WallEdge edge = TransformWallEdge(stamp, w, originCell, rotationSteps, baseYOffset);
                if (walls.GetSegment(edge) != null) continue; // уже есть — оставляем существующую

                WallSegment seg = walls.PlaceWall(def, edge);
                if (seg == null) continue;

                if (!string.IsNullOrEmpty(w.openingDefinitionId))
                {
                    WallOpeningDefinition opening = wallCatalog.FindOpeningById(w.openingDefinitionId);
                    if (opening != null) walls.PlaceOpening(opening, edge);
                }

                forward.UpsertWalls.Add(seg.GetState());
                backward.DeleteWallIds.Add(seg.SegmentId);
            }

            // --- Дорожки ---
            foreach (StampPath p in stamp.content.pathStrokes)
            {
                PathDefinition def = pathCatalog.FindById(p.definitionId);
                if (def == null) continue;

                List<Vector3> points = new List<Vector3>(p.localPoints.Count);
                foreach (SerializableVector3 sp in p.localPoints)
                {
                    points.Add(cornerWorld + RotateLocal(sp.ToVector3(), stamp, rotationSteps, cellSize));
                }

                PathStroke stroke = paths.CreateStroke(def, points, p.width);
                if (stroke != null)
                {
                    forward.UpsertPaths.Add(new PathStrokeState
                    {
                        StrokeId = stroke.StrokeId,
                        DefinitionId = def.id,
                        Width = stroke.Width,
                        ControlPoints = new List<Vector3>(stroke.ControlPoints),
                    });
                    backward.DeletePathIds.Add(stroke.StrokeId);
                }
            }

            return forward.HasAnyChanges;
        }

        // ------------------------------------------------------------------
        // Трансформации локальных координат при повороте штампа.
        // Поворот вокруг вертикальной оси, против часовой в шагах по 90°,
        // результат снова приводится к юго-западному углу.
        // ------------------------------------------------------------------

        /// <summary>Локальная клетка (в клетках слоя def) → мировая клетка слоя.</summary>
        private Vector2Int TransformObjectCell(StampData stamp, StampPlacedObject o, BuildingDefinition def,
            Vector2Int originCell, int rotationSteps)
        {
            float layerCell = grid.GetCellSize(def.layer);
            float ratio = grid.CellSize / layerCell; // клеток слоя на одну основную клетку
            int w = Mathf.RoundToInt(stamp.footprintW * ratio);
            int l = Mathf.RoundToInt(stamp.footprintL * ratio);

            Vector2Int fp = GridBuildingSystem.RotateFootprint(def.Footprint, o.rotationSteps);
            Vector2Int local = RotateCellRect(new Vector2Int(o.cellX, o.cellY), fp, w, l, rotationSteps);

            int baseX = Mathf.RoundToInt(originCell.x * ratio);
            int baseY = Mathf.RoundToInt(originCell.y * ratio);
            return new Vector2Int(baseX + local.x, baseY + local.y);
        }

        private WallEdge TransformWallEdge(StampData stamp, StampWall w, Vector2Int originCell, int rotationSteps, float baseYOffset = 0f)
        {
            int level = UnityEngine.Mathf.RoundToInt(baseYOffset / UnityEngine.Mathf.Max(0.01f, FloorContext.FloorHeight));
            int steps = ((rotationSteps % 4) + 4) % 4;
            int x = w.x, y = w.y;
            WallOrientation orient = (WallOrientation)w.orientation;
            int fw = stamp.footprintW, fl = stamp.footprintL;

            for (int i = 0; i < steps; i++)
            {
                // Поворот на 90° CCW: (x, y) → (y, fw - x), ориентация меняется.
                // Для рёбер: вертикальное ребро (x,y) становится горизонтальным (y, fw - x);
                // горизонтальное (x,y) — вертикальным (y, fw - 1 - x)… выведено через
                // соответствие ребра паре клеток, см. ниже.
                if (orient == WallOrientation.Vertical)
                {
                    int nx = y;
                    int ny = fw - x;
                    x = nx; y = ny;
                    orient = WallOrientation.Horizontal;
                }
                else
                {
                    int nx = y;
                    int ny = fw - 1 - x;
                    x = nx; y = ny;
                    orient = WallOrientation.Vertical;
                }
                int t = fw; fw = fl; fl = t;
            }

            return new WallEdge(originCell.x + x, originCell.y + y, orient, level);
        }

        /// <summary>
        /// Поворот origin-клетки прямоугольника fp внутри области w×l (в клетках слоя).
        /// </summary>
        private static Vector2Int RotateCellRect(Vector2Int cell, Vector2Int fp, int w, int l, int rotationSteps)
        {
            int steps = ((rotationSteps % 4) + 4) % 4;
            int x = cell.x, y = cell.y;
            int fpx = fp.x, fpy = fp.y;

            for (int i = 0; i < steps; i++)
            {
                // CCW 90°: новая origin-клетка прямоугольника.
                int nx = y;
                int ny = w - x - fpx;
                x = nx; y = ny;

                int t = w; w = l; l = t;
                t = fpx; fpx = fpy; fpy = t;
            }

            return new Vector2Int(x, y);
        }

        private static Vector3 RotateLocal(Vector3 local, StampData stamp, int rotationSteps, float cellSize)
        {
            int steps = ((rotationSteps % 4) + 4) % 4;
            float w = stamp.footprintW * cellSize;
            float l = stamp.footprintL * cellSize;
            float x = local.x, z = local.z;

            for (int i = 0; i < steps; i++)
            {
                float nx = z;
                float nz = w - x;
                x = nx; z = nz;
                float t = w; w = l; l = t;
            }

            return new Vector3(x, local.y, z);
        }

        private static float NormalizedYaw(int rotationSteps)
        {
            return (((rotationSteps % 4) + 4) % 4) * 90f;
        }
    }
}
