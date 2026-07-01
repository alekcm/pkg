// HairPresetsFinalizer.cs - Доводит пресеты до ума после генерации
// Запускать ПОСЛЕ Generate Eden Garden Presets и Generate Story Presets
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;
using CharacterEditor.Hair;

namespace CharacterEditor.Hair.EditorTool
{
    public static class HairPresetsFinalizer
    {
        [MenuItem("Tools/Character/Hair/FINALIZE - Tune Every Preset Strand Shape & Colors", false, 33)]
        public static void FinalizeAllPresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int count = 0;

            ScalpProfileDefinition profile = FindFirst<ScalpProfileDefinition>();
            HeadCollisionMaskDefinition mask = FindFirst<HeadCollisionMaskDefinition>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var piece = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (piece == null) continue;

                bool changed = false;
                string name = (piece.id ?? piece.name ?? "").ToLowerInvariant();

                // ===== Аниме-умолчания для ВСЕХ =====
                if (piece.strandSides == 0)
                {
                    if (name.Contains("desmond")) piece.strandSides = 6;
                    else if (name.Contains("wenona") || name.Contains("eva")) piece.strandSides = 5;
                    else piece.strandSides = 4;
                    changed = true;
                }
                if (piece.strandWidthScale < 0.01f)
                {
                    // Широкие пряди для определённых персонажей
                    if (name.Contains("grace")) piece.strandWidthScale = 3.5f;
                    else if (name.Contains("eloise")) piece.strandWidthScale = 3.2f;
                    else if (name.Contains("desmond")) piece.strandWidthScale = 1.2f;
                    else piece.strandWidthScale = 2.5f;
                    changed = true;
                }
                if (piece.strandDepthScale < 0.01f)
                {
                    if (name.Contains("desmond")) piece.strandDepthScale = 0.9f;
                    else if (name.Contains("eloise")) piece.strandDepthScale = 0.35f;
                    else piece.strandDepthScale = 0.4f;
                    changed = true;
                }
                if (piece.strandSides > 0 && piece.strandSides <= 3)
                {
                    piece.strandSides = 4;
                    changed = true;
                }

                // ===== ScalpProfile + HeadMask =====
                if (piece.headCollisionMask == null && mask != null)
                {
                    piece.headCollisionMask = mask;
                    // Настраиваем softness для разных типов
                    if (name.Contains("desmond")) piece.headCollisionMask.softness = 0.15f;
                    else piece.headCollisionMask.softness = 0.08f;
                    changed = true;
                }
                if (piece.scalpProfile == null && profile != null)
                {
                    piece.scalpProfile = profile;
                    changed = true;
                }

                // ===== Separation =====
                if (!piece.enableStrandSeparation)
                {
                    piece.enableStrandSeparation = true;
                    if (name.Contains("desmond"))
                    {
                        piece.strandSeparationRadius = 0.030f;
                        piece.strandSeparationStrength = 0.70f;
                    }
                    else if (name.Contains("wenona"))
                    {
                        piece.strandSeparationRadius = 0.028f;
                        piece.strandSeparationStrength = 0.65f;
                    }
                    else
                    {
                        piece.strandSeparationRadius = 0.024f;
                        piece.strandSeparationStrength = 0.55f;
                    }
                    changed = true;
                }

                // ===== Braid настройки =====
                if (name.Contains("wenona") || name.Contains("eva"))
                {
                    bool needBraid = false;
                    if (piece.defaultBraid.type == BraidType.None && name.Contains("wenona"))
                    {
                        piece.defaultBraid = new BraidProfile
                        {
                            type = BraidType.ThreeStrand,
                            crossings = 9,
                            strandRadius = 0.011f,
                            tightness = 0.9f,
                            taperEnd = true
                        };
                        needBraid = true;
                    }
                    // Для Eva делаем 2-strand twist на одной стороне
                    if (name.Contains("eva") && piece.defaultBraid.type == BraidType.None)
                    {
                        piece.defaultBraid = new BraidProfile
                        {
                            type = BraidType.TwoStrandTwist,
                            crossings = 6,
                            strandRadius = 0.008f,
                            tightness = 0.6f,
                            taperEnd = true
                        };
                        needBraid = true;
                    }
                    if (needBraid) changed = true;
                }

                if (name.Contains("desmond") && piece.defaultBraid.type != BraidType.Dreadlock)
                {
                    piece.defaultBraid = new BraidProfile
                    {
                        type = BraidType.Dreadlock,
                        crossings = 0,
                        strandRadius = 0.009f,
                        tightness = 0.4f,
                        taperEnd = false
                    };
                    changed = true;
                }

                // ===== LOD настройки =====
                if (piece.segmentsLOD0 < 12) { piece.segmentsLOD0 = 16; changed = true; }

                if (changed)
                {
                    EditorUtility.SetDirty(piece);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Обновляем каталог
            string catalogPath = "Assets/Resources/HairCatalogProc.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<HairCatalogProc>(catalogPath);
            if (catalog != null)
            {
                catalog.pieces.Clear();
                foreach (string guid in AssetDatabase.FindAssets("t:HairPieceDefinitionProc"))
                {
                    var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(AssetDatabase.GUIDToAssetPath(guid));
                    if (p != null && !catalog.pieces.Contains(p))
                        catalog.pieces.Add(p);
                }
                catalog.Rebuild();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog("Finalizer Complete",
                $"Tuned {count} hair preset(s).\n\n" +
                $"Next:\n" +
                "1. Run 'Fix ALL Hair Roots To Scalp Zones'\n" +
                "2. Check results in Play mode\n" +
                "3. For Desmond dreads: manually gather into ponytail via BundleAnchor\n" +
                "   (see Desmond section in guide below)",
                "OK");
        }

        private static T FindFirst<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
#endif