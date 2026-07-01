// HairSetup_EdenDefaults.cs - Исправляет Base() для EdenGardenHairPresetsGenerator
// Этот файл добавляет partial extension к EdenGardenHairPresetsGenerator
// с правильными настройками для аниме-волос
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair;
using CharacterEditor.Hair.Proc;

namespace CharacterEditor.Hair.EditorTool
{
    /// <summary>
    /// Дополнение к EdenGardenHairPresetsGenerator.
    /// Исправляет функцию Base() чтобы она правильно настраивала
    /// аниме-параметры (strandSides=4, strandWidthScale=2.5, и т.д.)
    /// и назначала scalpProfile / headCollisionMask.
    /// </summary>
    public static class HairSetup_EdenDefaults
    {
        // Вызывать ПОСЛЕ генерации пресетов
        [MenuItem("Tools/Character/Hair/Apply Anime Strand Defaults To All Presets", false, 32)]
        public static void ApplyAnimeDefaultsToAll()
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

                // Аниме-умолчания
                if (piece.strandSides == 0 || piece.strandSides > 6)
                {
                    // Dreads (Desmond) — оставляем 6-7 сторон
                    if (piece.id != null && piece.id.ToLowerInvariant().Contains("desmond"))
                    {
                        if (piece.strandSides == 0) { piece.strandSides = 6; changed = true; }
                    }
                    else
                    {
                        piece.strandSides = 4; changed = true;
                    }
                }
                if (piece.strandWidthScale < 0.01f) { piece.strandWidthScale = 2.5f; changed = true; }
                if (piece.strandDepthScale < 0.01f) { piece.strandDepthScale = 0.4f; changed = true; }

                // Separation
                if (!piece.enableStrandSeparation)
                {
                    piece.enableStrandSeparation = true;
                    piece.strandSeparationRadius = 0.024f;
                    piece.strandSeparationStrength = 0.55f;
                    changed = true;
                }

                // Маска и профиль
                if (piece.headCollisionMask == null && mask != null)
                {
                    piece.headCollisionMask = mask;
                    changed = true;
                }
                if (piece.scalpProfile == null && profile != null)
                {
                    piece.scalpProfile = profile;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(piece);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimeDefaults] Applied to {count} presets.");
            EditorUtility.DisplayDialog("Anime Defaults",
                $"Applied anime strand defaults to {count} presets.\n" +
                "Run 'Fix ALL Hair Roots' next.", "OK");
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