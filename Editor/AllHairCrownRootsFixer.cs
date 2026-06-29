#if UNITY_EDITOR
using CharacterEditor.Hair.Proc;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorTool
{
    /// <summary>
    /// Zone-aware scalp root fixer.
    /// Moves guide roots to believable scalp zones (front/top/sides/back) instead of a single crown point
    /// and rebuilds guide control points so strands exit the scalp before falling down.
    /// </summary>
    public static class AllHairCrownRootsFixer
    {
        private enum ScalpZone
        {
            Top,
            BangsFront,
            SideLeftTop,
            SideRightTop,
            BackCrown,
            BraidLeftTop,
            BraidRightTop
        }

        [MenuItem("Tools/Character/Hair/Fix ALL Hair Roots To Scalp Zones")]
        public static void FixAllHairRootsToScalpZonesMenu()
        {
            int count = FixAllHairRootsToCrown(log: true);
            EditorUtility.DisplayDialog(
                "Hair roots fixed",
                $"Updated {count} HairPieceDefinitionProc asset(s).\n\nRoots are now distributed over scalp zones, not one crown point.\nSwitch presets in the UI or press Clear and select hair again to rebuild meshes.",
                "OK");
        }

        // Backward-compatible menu name used in earlier instructions.
        [MenuItem("Tools/Character/Hair/Fix ALL Hair Roots To Crown Scalp")]
        public static void FixAllHairRootsToCrownMenu()
        {
            FixAllHairRootsToScalpZonesMenu();
        }

        public static int FixAllHairRootsToCrown(bool log)
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                HairPieceDefinitionProc piece = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (piece == null || piece.guides == null || piece.guides.Length == 0)
                    continue;

                Undo.RecordObject(piece, "Fix Hair Roots To Scalp Zones");
                bool changed = FixPiece(piece);

                // Enable cheap centerline separation by default on generated hair pieces.
                // This prevents tube locks from occupying the same space.
                piece.enableStrandSeparation = true;
                piece.strandSeparationRadius = Mathf.Max(piece.strandSeparationRadius, 0.024f);
                piece.strandSeparationStrength = Mathf.Max(piece.strandSeparationStrength, 0.55f);
                changed = true;

                if (!changed)
                    continue;

                EditorUtility.SetDirty(piece);
                count++;
                if (log)
                    Debug.Log($"[HairScalpZoneFixer] Fixed scalp-zone roots: {path}", piece);
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        private static bool FixPiece(HairPieceDefinitionProc piece)
        {
            string id = (piece.id ?? piece.name ?? string.Empty).ToLowerInvariant();
            HairGuide[] guides = piece.guides;
            bool changed = false;

            for (int i = 0; i < guides.Length; i++)
            {
                HairGuide g = guides[i];
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
                else
                {
                    newRoot = RootForZone(zone, i, guides.Length, g.rootLocalPos);
                    if (piece.headCollisionMask != null)
                        newRoot = ProjectRootToHeadMaskSurface(newRoot, piece.headCollisionMask, zone);

                    scalpNormal = piece.headCollisionMask != null
                        ? HeadMaskSurfaceNormal(newRoot, piece.headCollisionMask)
                        : ExitDirection(zone, g.rootLocalPos, i, guides.Length);
                }

                Vector3[] newPoints = BuildGuidePointsForZone(zone, length, i, guides.Length, g.rootLocalPos, newRoot, scalpNormal);

                g.rootLocalPos = newRoot;
                g.pointsLocal = newPoints;
                guides[i] = g;
                changed = true;
            }

            piece.guides = guides;
            return changed;
        }

        private static ScalpZone ResolveZone(string pieceId, int groupId, Vector3 oldRoot, int index, int total)
        {
            // Preset-specific overrides first.
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

            // Generic group mapping.
            switch (groupId)
            {
                case 0: return ScalpZone.BangsFront;
                case 1: return ScalpZone.SideLeftTop;
                case 2: return ScalpZone.SideRightTop;
                case 3: return ScalpZone.BackCrown;
                default:
                    if (oldRoot.z > 0.025f) return ScalpZone.BangsFront;
                    if (oldRoot.x < -0.035f) return ScalpZone.SideLeftTop;
                    if (oldRoot.x > 0.035f) return ScalpZone.SideRightTop;
                    if (oldRoot.z < -0.025f) return ScalpZone.BackCrown;
                    return ScalpZone.Top;
            }
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
                default: return ScalpZoneId.Top;
            }
        }

        private static Vector3 RootForZone(ScalpZone zone, int index, int total, Vector3 oldRoot)
        {
            float u = Halton(index + 1, 2) * 2f - 1f;
            float v = Halton(index + 1, 3) * 2f - 1f;
            Vector2 disk = Vector2.ClampMagnitude(new Vector2(u, v), 1f);

            switch (zone)
            {
                case ScalpZone.BangsFront:
                    return new Vector3(disk.x * 0.050f, 0.128f - Mathf.Abs(disk.x) * 0.010f, 0.048f + Mathf.Abs(disk.y) * 0.010f);

                case ScalpZone.SideLeftTop:
                    return new Vector3(-0.058f + disk.x * 0.014f, 0.112f + disk.y * 0.012f, 0.006f + disk.y * 0.018f);

                case ScalpZone.SideRightTop:
                    return new Vector3(0.058f + disk.x * 0.014f, 0.112f + disk.y * 0.012f, 0.006f + disk.y * 0.018f);

                case ScalpZone.BackCrown:
                    // Broad back-scalp distribution: top-back curtain roots.
                    return new Vector3(disk.x * 0.070f, 0.132f - Mathf.Abs(disk.x) * 0.018f, -0.035f + disk.y * 0.030f);

                case ScalpZone.BraidLeftTop:
                    return new Vector3(-0.052f + disk.x * 0.012f, 0.113f + disk.y * 0.010f, -0.030f + disk.y * 0.012f);

                case ScalpZone.BraidRightTop:
                    return new Vector3(0.052f + disk.x * 0.012f, 0.113f + disk.y * 0.010f, -0.030f + disk.y * 0.012f);

                default:
                    return new Vector3(disk.x * 0.045f, 0.145f - disk.sqrMagnitude * 0.020f, -0.005f + disk.y * 0.040f);
            }
        }

        private static Vector3[] BuildGuidePointsForZone(ScalpZone zone, float length, int index, int total, Vector3 oldRoot, Vector3 newRoot, Vector3 scalpNormal)
        {
            // Always 4 points for generated presets. Runtime baker supports more, but 4 keeps editing simple.
            Vector3[] p = new Vector3[4];
            p[0] = Vector3.zero;

            Vector3 zoneExit = ExitDirection(zone, oldRoot, index, total);
            Vector3 exit = Vector3.Slerp(zoneExit, scalpNormal.normalized, 0.75f).normalized;
            Vector3 side = SideDirection(zone, oldRoot, index);
            Vector3 fall = FallDirection(zone);
            float sway = (Halton(index + 1, 5) - 0.5f) * 0.018f;

            // The first point exits the scalp outward; later points fall down according to zone.
            p[1] = exit * 0.035f + Vector3.down * Mathf.Min(0.060f, length * 0.25f) + side * sway * 0.25f;
            p[2] = exit * 0.055f + fall * 0.035f + Vector3.down * (length * 0.62f) + side * sway * 0.65f;
            p[3] = exit * 0.065f + fall * 0.070f + Vector3.down * length + side * sway;

            // Zone-specific silhouette shaping.
            switch (zone)
            {
                case ScalpZone.BangsFront:
                    p[2] += Vector3.forward * 0.040f;
                    p[3] += Vector3.forward * 0.070f;
                    break;

                case ScalpZone.SideLeftTop:
                    p[2] += Vector3.left * 0.035f;
                    p[3] += Vector3.left * 0.065f + Vector3.back * 0.018f;
                    break;

                case ScalpZone.SideRightTop:
                    p[2] += Vector3.right * 0.035f;
                    p[3] += Vector3.right * 0.065f + Vector3.back * 0.018f;
                    break;

                case ScalpZone.BackCrown:
                    // Back hair should fall as a curtain from many back-scalp roots, not
                    // merge into one ponytail slab. Keep it close to the skull and fan it
                    // horizontally according to the root's X position.
                    float rootSide = Mathf.Clamp(newRoot.x / 0.060f, -1f, 1f);
                    Vector3 fanSide = rootSide < 0f ? Vector3.left : Vector3.right;
                    float fan = Mathf.Abs(rootSide);

                    // Override the generic back route with a closer curtain-like route.
                    p[1] = Vector3.back * 0.018f + Vector3.down * Mathf.Min(0.055f, length * 0.20f) + fanSide * fan * 0.010f;
                    p[2] = Vector3.back * 0.040f + Vector3.down * (length * 0.56f) + fanSide * fan * 0.035f + side * sway * 0.35f;
                    p[3] = Vector3.back * 0.055f + Vector3.down * length + fanSide * fan * 0.070f + side * sway * 0.60f;
                    break;

                case ScalpZone.BraidLeftTop:
                    p[2] += Vector3.left * 0.050f + Vector3.back * 0.050f;
                    p[3] += Vector3.left * 0.080f + Vector3.back * 0.090f;
                    break;

                case ScalpZone.BraidRightTop:
                    p[2] += Vector3.right * 0.050f + Vector3.back * 0.050f;
                    p[3] += Vector3.right * 0.080f + Vector3.back * 0.090f;
                    break;
            }

            return p;
        }

        private static Vector3 ExitDirection(ScalpZone zone, Vector3 oldRoot, int index, int total)
        {
            switch (zone)
            {
                case ScalpZone.BangsFront: return (Vector3.up * 0.15f + Vector3.forward).normalized;
                case ScalpZone.SideLeftTop: return (Vector3.left + Vector3.up * 0.1f).normalized;
                case ScalpZone.SideRightTop: return (Vector3.right + Vector3.up * 0.1f).normalized;
                case ScalpZone.BackCrown: return (Vector3.back + Vector3.up * 0.1f).normalized;
                case ScalpZone.BraidLeftTop: return (Vector3.left + Vector3.back * 0.7f).normalized;
                case ScalpZone.BraidRightTop: return (Vector3.right + Vector3.back * 0.7f).normalized;
                default:
                    float a = (index + 0.5f) / Mathf.Max(1, total) * Mathf.PI * 2f;
                    return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
            }
        }

        private static Vector3 FallDirection(ScalpZone zone)
        {
            switch (zone)
            {
                case ScalpZone.BangsFront: return Vector3.forward;
                case ScalpZone.SideLeftTop: return (Vector3.left + Vector3.back * 0.25f).normalized;
                case ScalpZone.SideRightTop: return (Vector3.right + Vector3.back * 0.25f).normalized;
                case ScalpZone.BackCrown: return Vector3.back;
                case ScalpZone.BraidLeftTop: return (Vector3.left + Vector3.back).normalized;
                case ScalpZone.BraidRightTop: return (Vector3.right + Vector3.back).normalized;
                default: return Vector3.back * 0.25f;
            }
        }

        private static Vector3 SideDirection(ScalpZone zone, Vector3 oldRoot, int index)
        {
            switch (zone)
            {
                case ScalpZone.SideLeftTop:
                case ScalpZone.BraidLeftTop:
                    return Vector3.left;
                case ScalpZone.SideRightTop:
                case ScalpZone.BraidRightTop:
                    return Vector3.right;
                default:
                    if (Mathf.Abs(oldRoot.x) > 0.005f)
                        return oldRoot.x < 0f ? Vector3.left : Vector3.right;
                    return index % 2 == 0 ? Vector3.left : Vector3.right;
            }
        }

        private static Vector3 ProjectRootToHeadMaskSurface(Vector3 candidate, HeadCollisionMaskDefinition mask, ScalpZone zone)
        {
            Vector3 center = mask.center;
            Vector3 r = mask.SafeRadii;

            // Keep roots in plausible hair-bearing parts of the ellipsoid.
            // The mask itself is collision-only; these clamps approximate scalp zones.
            candidate.y = Mathf.Max(candidate.y, center.y + r.y * 0.28f);
            if (zone == ScalpZone.BangsFront)
                candidate.z = Mathf.Max(candidate.z, center.z + r.z * 0.45f);
            else if (zone == ScalpZone.BackCrown || zone == ScalpZone.BraidLeftTop || zone == ScalpZone.BraidRightTop)
                candidate.z = Mathf.Min(candidate.z, center.z - r.z * 0.18f);

            Vector3 q = new Vector3(
                (candidate.x - center.x) / Mathf.Max(0.001f, r.x),
                (candidate.y - center.y) / Mathf.Max(0.001f, r.y),
                (candidate.z - center.z) / Mathf.Max(0.001f, r.z)
            );

            if (q.sqrMagnitude < 0.0001f)
                q = new Vector3(0f, 1f, 0f);

            q.Normalize();
            Vector3 surface = center + new Vector3(q.x * r.x, q.y * r.y, q.z * r.z);

            // Tiny offset outside the ellipsoid so tube roots are not embedded in the skin.
            Vector3 n = HeadMaskSurfaceNormal(surface, mask);
            return surface + n * 0.003f;
        }

        private static Vector3 HeadMaskSurfaceNormal(Vector3 point, HeadCollisionMaskDefinition mask)
        {
            Vector3 center = mask.center;
            Vector3 r = mask.SafeRadii;

            // Approximate ellipsoid normal: gradient of x^2/rx^2 + y^2/ry^2 + z^2/rz^2.
            Vector3 n = new Vector3(
                (point.x - center.x) / Mathf.Max(0.001f, r.x * r.x),
                (point.y - center.y) / Mathf.Max(0.001f, r.y * r.y),
                (point.z - center.z) / Mathf.Max(0.001f, r.z * r.z)
            );
            if (n.sqrMagnitude < 0.0001f)
                return Vector3.up;
            return n.normalized;
        }

        private static float EstimateStyleLength(string pieceId, HairGuide g)
        {
            float max = 0.08f;
            if (g.pointsLocal != null)
            {
                for (int i = 1; i < g.pointsLocal.Length; i++)
                    max = Mathf.Max(max, g.pointsLocal[i].magnitude);
            }

            if (pieceId.Contains("fuyuhiko") || pieceId.Contains("ulysses")) return Mathf.Clamp(max, 0.055f, 0.120f);
            if (pieceId.Contains("diana")) return Mathf.Clamp(max, 0.120f, 0.230f);
            if (pieceId.Contains("grace")) return Mathf.Clamp(max, 0.280f, 0.560f);
            if (pieceId.Contains("desmond")) return Mathf.Clamp(max, 0.300f, 0.500f);
            if (pieceId.Contains("wenona")) return Mathf.Clamp(max, 0.250f, 0.480f);

            if (g.groupId == 0) return Mathf.Clamp(max, 0.090f, 0.240f);
            if (g.groupId == 1 || g.groupId == 2) return Mathf.Clamp(max, 0.140f, 0.450f);
            if (g.groupId == 3) return Mathf.Clamp(max, 0.180f, 0.550f);
            return Mathf.Clamp(max, 0.100f, 0.400f);
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
    }
}
#endif
