// AllHairCrownRootsFixer.cs - Исправленная версия
// Перераспределяет корни guide-ов по зонам скальпа
// Использует ScalpProfile если он есть, иначе - интеллектуальную аппроксимацию
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;

namespace CharacterEditor.Hair.EditorTool
{
    public static class AllHairCrownRootsFixer
    {
        private enum ScalpZone
        {
            Top, BangsFront, SideLeftTop, SideRightTop, BackCrown,
            BraidLeftTop, BraidRightTop, NapeBack
        }

        [MenuItem("Tools/Character/Hair/4. Fix ALL Hair Roots To Scalp Zones", false, 30)]
        public static void FixAllHairRootsToScalpZonesMenu()
        {
            int count = FixAllHairRoots(log: true);
            int missingMask = 0, missingProfile = 0;
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            foreach (string guid in guids)
            {
                var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(AssetDatabase.GUIDToAssetPath(guid));
                if (p == null) continue;
                if (p.headCollisionMask == null) missingMask++;
                if (p.scalpProfile == null) missingProfile++;
            }

            string msg = $"Updated {count} HairPieceDefinitionProc asset(s).\n\n" +
                "Roots are now distributed over scalp zones.\n" +
                "Switch presets in the UI or press Clear and select hair again to rebuild.\n\n";
            if (missingMask > 0)
                msg += $"⚠ {missingMask} piece(s) have no HeadCollisionMask.\n";
            if (missingProfile > 0)
                msg += $"⚠ {missingProfile} piece(s) have no ScalpProfile.\n";
            if (missingMask > 0 || missingProfile > 0)
                msg += "\nRun Setup Wizard first to create base assets.";

            EditorUtility.DisplayDialog("Hair roots fixed", msg, "OK");
        }

        public static int FixAllHairRoots(bool log)
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var piece = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (piece == null || piece.guides == null || piece.guides.Length == 0)
                    continue;

                Undo.RecordObject(piece, "Fix Hair Roots To Scalp Zones");
                bool changed = FixPiece(piece);

                // Включаем strand separation
                if (!piece.enableStrandSeparation)
                {
                    piece.enableStrandSeparation = true;
                    piece.strandSeparationRadius = Mathf.Max(piece.strandSeparationRadius, 0.024f);
                    piece.strandSeparationStrength = Mathf.Max(piece.strandSeparationStrength, 0.55f);
                    changed = true;
                }

                // Аниме-настройки strand shape если не заданы
                if (piece.strandSides == 0 || piece.strandSides > 6)
                {
                    piece.strandSides = 4;
                    changed = true;
                }
                if (piece.strandWidthScale < 0.01f)
                {
                    piece.strandWidthScale = 2.5f;
                    changed = true;
                }
                if (piece.strandDepthScale < 0.01f)
                {
                    piece.strandDepthScale = 0.4f;
                    changed = true;
                }

                if (!changed) continue;

                EditorUtility.SetDirty(piece);
                count++;
                if (log)
                    Debug.Log($"[HairScalpFixer] Fixed: {path}", piece);
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        private static bool FixPiece(HairPieceDefinitionProc piece)
        {
            string id = (piece.id ?? piece.name ?? string.Empty).ToLowerInvariant();
            var guides = piece.guides;
            bool changed = false;

            for (int i = 0; i < guides.Length; i++)
            {
                var g = guides[i];
                if (g.pointsLocal == null || g.pointsLocal.Length < 2)
                    continue;

                ScalpZone zone = ResolveZone(id, g.groupId, g.rootLocalPos, i, guides.Length);
                float length = EstimateStyleLength(id, g);

                float u = Halton(i + 1, 2) * 2f - 1f;
                float v = Halton(i + 1, 3) * 2f - 1f;

                Vector3 newRoot;
                Vector3 scalpNormal;

                if (piece.scalpProfile != null)
                {
                    ScalpZoneId profileZone = ToProfileZone(zone);
                    newRoot = piece.scalpProfile.GetRoot(profileZone, u, v);
                    scalpNormal = piece.scalpProfile.GetNormal(newRoot);
                }
                else if (piece.headCollisionMask != null)
                {
                    newRoot = RootForZoneFallback(zone, i, guides.Length, g.rootLocalPos);
                    newRoot = ProjectRootToHeadMaskSurface(newRoot, piece.headCollisionMask, zone);
                    scalpNormal = HeadMaskSurfaceNormal(newRoot, piece.headCollisionMask);
                }
                else
                {
                    newRoot = RootForZoneFallback(zone, i, guides.Length, g.rootLocalPos);
                    scalpNormal = ExitDirection(zone, g.rootLocalPos, i, guides.Length);
                }

                var newPoints = BuildGuidePointsForZone(zone, length, i, guides.Length,
                    g.rootLocalPos, newRoot, scalpNormal);

                g.rootLocalPos = newRoot;
                g.pointsLocal = newPoints;
                guides[i] = g;
                changed = true;
            }

            piece.guides = guides;
            return changed;
        }

        private static ScalpZone ResolveZone(string pieceId, int groupId, Vector3 oldRoot,
            int index, int total)
        {
            // Специфичные для персонажей переопределения
            if (pieceId.Contains("wenona"))
            {
                if (groupId == 1) return ScalpZone.BraidLeftTop;
                if (groupId == 2) return ScalpZone.BraidRightTop;
                return ScalpZone.Top;
            }
            if (pieceId.Contains("grace"))
            {
                if (groupId == 1 || oldRoot.x < 0f) return ScalpZone.SideLeftTop;
                return ScalpZone.SideRightTop;
            }
            if (pieceId.Contains("desmond"))
                return ScalpZone.BackCrown;
            if (pieceId.Contains("fuyuhiko") || pieceId.Contains("ulysses"))
                return ScalpZone.Top;
            if (pieceId.Contains("diana"))
            {
                if (groupId == 0) return ScalpZone.BangsFront;
                if (groupId == 1) return ScalpZone.SideLeftTop;
                if (groupId == 2) return ScalpZone.SideRightTop;
                return ScalpZone.BackCrown;
            }

            // По groupId
            switch (groupId)
            {
                case 0: return ScalpZone.BangsFront;
                case 1: return ScalpZone.SideLeftTop;
                case 2: return ScalpZone.SideRightTop;
                case 3: return ScalpZone.BackCrown;
                case 4: return ScalpZone.Top;
                case 5: return ScalpZone.BraidLeftTop;
                case 6: return ScalpZone.BraidRightTop;
                case 7: return ScalpZone.NapeBack;
                default: return ScalpZone.Top;
            }
        }

        private static Vector3 RootForZoneFallback(ScalpZone zone, int index, int total,
            Vector3 oldRoot)
        {
            // Улучшенный fallback — распределение по голове без scalpProfile
            float t = total > 1 ? index / (float)(total - 1) : 0.5f;
            float angle = t * Mathf.PI * 2f;
            float radius = 0.055f;

            switch (zone)
            {
                case ScalpZone.BangsFront:
                    return new Vector3(Mathf.Lerp(-0.040f, 0.040f, (float)index / total),
                        0.100f, 0.060f);
                case ScalpZone.SideLeftTop:
                    return new Vector3(-0.072f, 0.085f, Mathf.Lerp(0.030f, -0.010f, t));
                case ScalpZone.SideRightTop:
                    return new Vector3(0.072f, 0.085f, Mathf.Lerp(0.030f, -0.010f, t));
                case ScalpZone.BackCrown:
                    return new Vector3(Mathf.Cos(angle) * radius * 0.8f,
                        0.110f - Mathf.Abs(Mathf.Sin(angle)) * 0.020f,
                        -0.070f + Mathf.Sin(angle) * radius * 0.8f);
                case ScalpZone.BraidLeftTop:
                    return new Vector3(-0.060f, 0.080f, -0.040f);
                case ScalpZone.BraidRightTop:
                    return new Vector3(0.060f, 0.080f, -0.040f);
                case ScalpZone.NapeBack:
                    return new Vector3(Mathf.Lerp(-0.035f, 0.035f, t), 0.020f, -0.075f);
                default: // Top
                    return new Vector3(
                        Mathf.Cos(angle) * radius * t,
                        0.145f - Mathf.Abs(Mathf.Sin(angle)) * 0.015f * t,
                        Mathf.Sin(angle) * radius * t);
            }
        }

        private static Vector3 ProjectRootToHeadMaskSurface(Vector3 point,
            HeadCollisionMaskDefinition mask, ScalpZone zone)
        {
            if (mask == null) return point;
            Vector3 center = mask.center;
            Vector3 r = mask.SafeRadii;

            Vector3 offset = point - center;
            // Нормализуем к поверхности эллипсоида
            Vector3 q = new Vector3(
                offset.x / Mathf.Max(0.001f, r.x),
                offset.y / Mathf.Max(0.001f, r.y),
                offset.z / Mathf.Max(0.001f, r.z));

            float len = q.magnitude;
            if (len < 0.0001f)
            {
                // Если точка в центре — сдвигаем наружу в направлении зоны
                Vector3 dir = ExitDirection(zone, point, 0, 1);
                q = new Vector3(dir.x / r.x, dir.y / r.y, dir.z / r.z);
                len = q.magnitude;
                if (len < 0.0001f) q = new Vector3(0, 1, 0);
                len = 1f;
            }

            Vector3 surface = center + new Vector3(
                q.x / len * r.x,
                q.y / len * r.y,
                q.z / len * r.z);

            return surface + HeadMaskSurfaceNormal(surface, mask) * 0.003f;
        }

        private static Vector3 HeadMaskSurfaceNormal(Vector3 point,
            HeadCollisionMaskDefinition mask)
        {
            if (mask == null) return Vector3.up;
            Vector3 center = mask.center;
            Vector3 r = mask.SafeRadii;
            Vector3 n = new Vector3(
                (point.x - center.x) / Mathf.Max(0.001f, r.x * r.x),
                (point.y - center.y) / Mathf.Max(0.001f, r.y * r.y),
                (point.z - center.z) / Mathf.Max(0.001f, r.z * r.z));
            if (n.sqrMagnitude < 0.0001f) return Vector3.up;
            return n.normalized;
        }

        private static Vector3 ExitDirection(ScalpZone zone, Vector3 pos,
            int index, int total)
        {
            switch (zone)
            {
                case ScalpZone.BangsFront: return new Vector3(0, -0.3f, 1).normalized;
                case ScalpZone.SideLeftTop: return new Vector3(-1, -0.3f, 0).normalized;
                case ScalpZone.SideRightTop: return new Vector3(1, -0.3f, 0).normalized;
                case ScalpZone.BackCrown: return new Vector3(0, -0.5f, -1).normalized;
                case ScalpZone.BraidLeftTop: return new Vector3(-0.7f, -0.5f, -0.5f).normalized;
                case ScalpZone.BraidRightTop: return new Vector3(0.7f, -0.5f, -0.5f).normalized;
                case ScalpZone.NapeBack: return new Vector3(0, -0.2f, -1).normalized;
                default: return Vector3.down;
            }
        }

        private static Vector3[] BuildGuidePointsForZone(ScalpZone zone, float length,
            int index, int total, Vector3 oldRoot, Vector3 newRoot, Vector3 scalpNormal)
        {
            Vector3 exitDir = scalpNormal;

            // Для разных зон — разные направления роста волос
            switch (zone)
            {
                case ScalpZone.BangsFront:
                    exitDir = Vector3.Lerp(scalpNormal, new Vector3(0, -0.3f, 0.7f), 0.6f).normalized;
                    break;
                case ScalpZone.SideLeftTop:
                    exitDir = Vector3.Lerp(scalpNormal, new Vector3(-0.6f, -0.3f, 0.3f), 0.5f).normalized;
                    break;
                case ScalpZone.SideRightTop:
                    exitDir = Vector3.Lerp(scalpNormal, new Vector3(0.6f, -0.3f, 0.3f), 0.5f).normalized;
                    break;
                case ScalpZone.BackCrown:
                case ScalpZone.NapeBack:
                    exitDir = Vector3.Lerp(scalpNormal, new Vector3(0, -0.3f, -0.7f), 0.7f).normalized;
                    break;
                case ScalpZone.BraidLeftTop:
                    exitDir = Vector3.Lerp(scalpNormal, new Vector3(-0.5f, -0.4f, -0.3f), 0.5f).normalized;
                    break;
                case ScalpZone.BraidRightTop:
                    exitDir = Vector3.Lerp(scalpNormal, new Vector3(0.5f, -0.4f, -0.3f), 0.5f).normalized;
                    break;
                default:
                    exitDir = Vector3.Lerp(scalpNormal, Vector3.down, 0.5f).normalized;
                    break;
            }

            // Строим контрольные точки
            float segmentLength = Mathf.Max(length * 0.01f, 0.005f);
            var points = new Vector3[4];
            points[0] = Vector3.zero; // root

            for (int p = 1; p < 4; p++)
            {
                float t = p / 3f;
                float spread = Mathf.Sin(t * Mathf.PI * 0.5f);
                Vector3 dir = exitDir + new Vector3(
                    (float)(index % 5 - 2) * 0.003f * spread,
                    0,
                    (float)((index * 7) % 5 - 2) * 0.003f * spread
                );
                dir.Normalize();
                points[p] = dir * segmentLength * t * 50f;
            }

            return points;
        }

        private static float EstimateStyleLength(string pieceId, HairGuide g)
        {
            float max = 0.08f;
            if (g.pointsLocal != null)
            {
                for (int i = 1; i < g.pointsLocal.Length; i++)
                    max = Mathf.Max(max, g.pointsLocal[i].magnitude);
            }

            if (pieceId.Contains("fuyuhiko") || pieceId.Contains("ulysses"))
                return Mathf.Clamp(max, 0.055f, 0.120f);
            if (pieceId.Contains("diana"))
                return Mathf.Clamp(max, 0.120f, 0.230f);
            if (pieceId.Contains("grace"))
                return Mathf.Clamp(max, 0.280f, 0.560f);
            if (pieceId.Contains("desmond"))
                return Mathf.Clamp(max, 0.300f, 0.500f);
            if (pieceId.Contains("wenona"))
                return Mathf.Clamp(max, 0.250f, 0.480f);
            if (pieceId.Contains("eva"))
                return Mathf.Clamp(max, 0.400f, 0.750f);
            if (pieceId.Contains("cassidy"))
                return Mathf.Clamp(max, 0.250f, 0.450f);
            if (pieceId.Contains("eloise"))
                return Mathf.Clamp(max, 0.150f, 0.280f);
            if (pieceId.Contains("ingrid"))
                return Mathf.Clamp(max, 0.100f, 0.180f);
            if (pieceId.Contains("jean"))
                return Mathf.Clamp(max, 0.250f, 0.400f);
            if (pieceId.Contains("kai"))
                return Mathf.Clamp(max, 0.100f, 0.180f);
            if (pieceId.Contains("mark"))
                return Mathf.Clamp(max, 0.080f, 0.150f);
            if (pieceId.Contains("toshiko"))
                return Mathf.Clamp(max, 0.080f, 0.140f);
            if (pieceId.Contains("wolfgang"))
                return Mathf.Clamp(max, 0.100f, 0.200f);

            if (g.groupId == 0) return Mathf.Clamp(max, 0.090f, 0.240f);
            if (g.groupId == 1 || g.groupId == 2) return Mathf.Clamp(max, 0.140f, 0.450f);
            if (g.groupId == 3) return Mathf.Clamp(max, 0.180f, 0.550f);
            return Mathf.Clamp(max, 0.100f, 0.400f);
        }

        private static ScalpZoneId ToProfileZone(ScalpZone zone)
        {
            switch (zone)
            {
                case ScalpZone.BangsFront: return ScalpZoneId.BangsFront;
                case ScalpZone.SideLeftTop: return ScalpZoneId.SideLeftTop;
                case ScalpZone.SideRightTop: return ScalpZoneId.SideRightTop;
                case ScalpZone.BackCrown: return ScalpZoneId.BackCrown;
                case ScalpZone.BraidLeftTop: return ScalpZoneId.BraidLeftTop;
                case ScalpZone.BraidRightTop: return ScalpZoneId.BraidRightTop;
                case ScalpZone.NapeBack: return ScalpZoneId.NapeBack;
                default: return ScalpZoneId.Top;
            }
        }

        private static float Halton(int index, int b)
        {
            float f = 1f;
            float r = 0f;
            while (index > 0)
            {
                f /= b;
                r += f * (index % b);
                index = Mathf.FloorToInt(index / (float)b);
            }
            return r;
        }

        // ====== Совместимость со старым кодом ======
        // Старый HairStoryPresetsGenerator вызывает FixAllHairRootsToCrown()
        public static int FixAllHairRootsToCrown(bool log)
        {
            return FixAllHairRoots(log);
        }
    }
}
#endif
