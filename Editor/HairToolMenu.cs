// HairToolMenu.cs - ЕДИНСТВЕННОЕ меню Tools/Character/Hair
// ВСЕ hair-инструменты собраны здесь. Никаких лишних кнопок.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;
using CharacterEditor.Hair;

namespace CharacterEditor.Hair.EditorTool
{
    public static class HairToolMenu
    {
        // ========== СЕТАП ==========
        [MenuItem("Tools/Character/Hair/1. Setup Wizard - Create All Base Assets", false, 10)]
        static void SetupWizard()
        {
            HairSetupWizard.Run();
        }

        // ========== ГЕНЕРАЦИЯ ПРЕСЕТОВ ==========
        [MenuItem("Tools/Character/Hair/2. Generate Eden Garden Presets", false, 20)]
        static void GenEden() => EdenGardenHairPresetsGenerator.Generate();

        [MenuItem("Tools/Character/Hair/3. Generate Story Presets", false, 21)]
        static void GenStory() => HairStoryPresetsGenerator.GenerateAll();

        // ========== КОРРЕКЦИЯ ==========
        [MenuItem("Tools/Character/Hair/4. Fix ALL Hair Roots To Scalp Zones", false, 30)]
        static void FixRoots() => AllHairCrownRootsFixer.FixAllHairRootsToScalpZonesMenu();

        [MenuItem("Tools/Character/Hair/5. Rebuild Hair Catalog", false, 31)]
        static void RebuildCatalog()
        {
            string path = "Assets/Resources/HairCatalogProc.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<HairCatalogProc>(path);
            if (catalog == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                catalog = ScriptableObject.CreateInstance<HairCatalogProc>();
                AssetDatabase.CreateAsset(catalog, path);
            }
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
            Debug.Log($"[HairCatalog] Rebuilt with {catalog.pieces.Count} pieces.");
            EditorGUIUtility.PingObject(catalog);
        }

        // ========== МАСКА ГОЛОВЫ ==========
        [MenuItem("Tools/Character/Hair/6. Create Head Collision Mask From FBX", false, 40)]
        static void CreateMask() => HeadCollisionMaskExtractor.CreateMaskFromSelection();

        // ========== ПРОЦЕДУРНЫЙ СОЗДАТЕЛЬ ==========
        [MenuItem("Tools/Character/Hair/7. Hair Proc Creator Window", false, 50)]
        static void OpenCreator() => HairProcCreatorWindow.Open();

        // ========== ДИАГНОСТИКА ==========
        [MenuItem("Tools/Character/Hair/8. Validate Head Masks On All Pieces", false, 60)]
        static void ValidateMasks()
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int missing = 0, total = 0;
            foreach (string guid in guids)
            {
                var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(AssetDatabase.GUIDToAssetPath(guid));
                if (p == null) continue;
                total++;
                if (p.headCollisionMask == null)
                {
                    Debug.LogWarning($"[HairValidate] {p.name} has NO headCollisionMask!", p);
                    missing++;
                }
                if (p.scalpProfile == null)
                {
                    Debug.LogWarning($"[HairValidate] {p.name} has NO scalpProfile!", p);
                    missing++;
                }
            }
            EditorUtility.DisplayDialog("Hair Validation",
                $"Checked {total} pieces.\n{missing} missing head mask or scalp profile.\nSee Console for details.", "OK");
        }

        // ========== ОТЛАДКА (только для разработки) ==========
        [MenuItem("Tools/Character/Hair/Debug - Print All Hair Assets", false, 100)]
        static void DebugPrintAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            Debug.Log($"--- Found {guids.Length} HairPieceDefinitionProc assets ---");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (p == null) continue;
                Debug.Log($"  {p.name} | id={p.id} | slot={p.slot} | " +
                    $"guides={p.GuideCount} | mask={(p.headCollisionMask != null ? "YES" : "NO")} | " +
                    $"scalp={(p.scalpProfile != null ? "YES" : "NO")} | path={path}", p);
            }
        }
    }
}
#endif
