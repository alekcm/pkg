// HairSetupWizard.cs - ОДНИМ НАЖАТИЕМ создаёт ScalpProfile, HeadCollisionMask и назначает на всё
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;
using System.IO;

namespace CharacterEditor.Hair.EditorTool
{
    public static class HairSetupWizard
    {
        private const string MASK_FOLDER = "Assets/Generated/HairMasks";
        private const string PROFILE_FOLDER = "Assets/Generated/HairProfiles";

        [MenuItem("Tools/Character/Hair/Setup Wizard - Create All Base Assets", true)]
        static bool ValidateSetup() => true;

        public static void Run()
        {
            // Шаг 1: Создаём HeadCollisionMask (если нет)
            EnsureFolder(MASK_FOLDER);
            string maskPath = MASK_FOLDER + "/HeadCollisionMask_Default.asset";
            HeadCollisionMaskDefinition mask = AssetDatabase.LoadAssetAtPath<HeadCollisionMaskDefinition>(maskPath);
            if (mask == null)
            {
                mask = ScriptableObject.CreateInstance<HeadCollisionMaskDefinition>();
                mask.center = new Vector3(0f, 0.06f, 0f);
                mask.radii = new Vector3(0.080f, 0.115f, 0.080f);
                mask.surfacePadding = 0.012f;
                mask.softness = 0.08f;
                mask.affectUntilT = 1f;
                AssetDatabase.CreateAsset(mask, maskPath);
                Debug.Log($"[HairSetup] Created default head mask: {maskPath}");
            }

            // Шаг 2: Создаём ScalpProfile (если нет)
            EnsureFolder(PROFILE_FOLDER);
            string profilePath = PROFILE_FOLDER + "/ScalpProfile_Default.asset";
            ScalpProfileDefinition profile = AssetDatabase.LoadAssetAtPath<ScalpProfileDefinition>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ScalpProfileDefinition>();
                profile.headMask = mask;
                // Landmarks настроены для аниме-головы (head local space)
                profile.crown = new Vector3(0f, 0.145f, -0.015f);
                profile.frontCenter = new Vector3(0f, 0.100f, 0.070f);
                profile.leftTemple = new Vector3(-0.070f, 0.078f, 0.045f);
                profile.rightTemple = new Vector3(0.070f, 0.078f, 0.045f);
                profile.leftSideTop = new Vector3(-0.078f, 0.092f, -0.006f);
                profile.rightSideTop = new Vector3(0.078f, 0.092f, -0.006f);
                profile.backCrown = new Vector3(0f, 0.108f, -0.076f);
                profile.nape = new Vector3(0f, 0.018f, -0.078f);
                profile.rootSurfaceOffset = 0.004f;
                AssetDatabase.CreateAsset(profile, profilePath);
                Debug.Log($"[HairSetup] Created default scalp profile: {profilePath}");
            }

            // Шаг 3: Создаём HairCatalog (если нет)
            EnsureFolder("Assets/Resources");
            string catalogPath = "Assets/Resources/HairCatalogProc.asset";
            HairCatalogProc catalog = AssetDatabase.LoadAssetAtPath<HairCatalogProc>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HairCatalogProc>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                Debug.Log($"[HairSetup] Created empty catalog: {catalogPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Шаг 4: Назначаем всем существующим HairPieceDefinitionProc
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int assigned = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("_EdenGarden") || path.Contains("_Story"))
                {
                    // Для EdenGarden и Story пресетов - назначаем маску и профиль
                    var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                    if (p != null)
                    {
                        bool changed = false;
                        if (p.headCollisionMask == null) { p.headCollisionMask = mask; changed = true; }
                        if (p.scalpProfile == null) { p.scalpProfile = profile; changed = true; }
                        // Настраиваем strandSides для аниме-стиля
                        if (p.strandSides == 0 || p.strandSides > 6) { p.strandSides = 4; changed = true; }
                        if (p.strandWidthScale < 0.01f) { p.strandWidthScale = 2.5f; changed = true; }
                        if (p.strandDepthScale < 0.01f) { p.strandDepthScale = 0.4f; changed = true; }
                        if (changed)
                        {
                            EditorUtility.SetDirty(p);
                            assigned++;
                        }
                    }
                }
            }

            // Обновляем каталог
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
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Hair Setup Complete",
                $"Created/Found:\n" +
                $"- Head Collision Mask: {Path.GetFileName(maskPath)}\n" +
                $"- Scalp Profile: {Path.GetFileName(profilePath)}\n" +
                $"- Hair Catalog: HairCatalogProc.asset\n\n" +
                $"Assigned to {assigned} hair presets.\n\n" +
                $"NEXT: Run 'Generate Eden Garden Presets' and 'Generate Story Presets'\n" +
                $"then 'Fix ALL Hair Roots To Scalp Zones'",
                "OK");

            EditorGUIUtility.PingObject(mask);
        }

        private static void EnsureFolder(string f)
        {
            if (string.IsNullOrEmpty(f) || f == "Assets") return;
            if (AssetDatabase.IsValidFolder(f)) return;
            string parent = Path.GetDirectoryName(f).Replace('\\', '/');
            string name = Path.GetFileName(f);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
