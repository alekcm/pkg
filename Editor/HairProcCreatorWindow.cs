// HairProcCreatorWindow.cs – Unity Editor
// Исправленная версия: использует ScalpProfile для распределения корней
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;
using CharacterEditor.Hair;
using System.Collections.Generic;

namespace CharacterEditor.Hair.EditorTool
{
    public class HairProcCreatorWindow : EditorWindow
    {
        [MenuItem("Tools/Character/Hair/7. Hair Proc Creator Window", false, 50)]
        public static void Open() => GetWindow<HairProcCreatorWindow>("Hair Proc");

        string newId = "hair_new_01";
        HairSlot slot = HairSlot.Bangs;
        int guideCount = 36;
        float length = 0.22f;
        int strandSides = 4;
        float strandWidth = 2.5f;
        float strandDepth = 0.4f;
        Vector2 scroll;
        bool autoAssignMask = true;

        void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural Hair Creator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Создаёт HairPieceDefinitionProc с guide-сплайнами, " +
                "распределёнными по зонам скальпа.\n" +
                "После создания запусти Fix ALL Hair Roots To Scalp Zones.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            newId = EditorGUILayout.TextField("ID", newId);
            slot = (HairSlot)EditorGUILayout.EnumPopup("Slot", slot);
            guideCount = EditorGUILayout.IntSlider("Guides", guideCount, 8, 160);
            length = EditorGUILayout.Slider("Length (m)", length, 0.05f, 0.6f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Strand Shape (Anime)", EditorStyles.boldLabel);
            strandSides = EditorGUILayout.IntSlider("Cross-section sides", strandSides, 3, 8);
            strandWidth = EditorGUILayout.Slider("Width scale", strandWidth, 0.5f, 5f);
            strandDepth = EditorGUILayout.Slider("Depth scale", strandDepth, 0.1f, 2f);

            EditorGUILayout.Space();
            autoAssignMask = EditorGUILayout.Toggle("Auto-assign head mask", autoAssignMask);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Procedural Hair", GUILayout.Height(32)))
                CreateHair();

            EditorGUILayout.EndScrollView();
        }

        void CreateHair()
        {
            if (string.IsNullOrEmpty(newId))
            {
                EditorUtility.DisplayDialog("Error", "ID is empty", "OK");
                return;
            }

            // Ищем ScalpProfile и HeadCollisionMask
            ScalpProfileDefinition profile = FindProfile();
            HeadCollisionMaskDefinition mask = FindMask();

            var asset = ScriptableObject.CreateInstance<HairPieceDefinitionProc>();
            asset.id = newId;
            asset.displayName = newId;
            asset.slot = slot;

            // Strand shape — аниме-стиль
            asset.strandSides = strandSides;
            asset.strandWidthScale = strandWidth;
            asset.strandDepthScale = strandDepth;
            asset.enableStrandSeparation = true;
            asset.strandSeparationRadius = 0.024f;
            asset.strandSeparationStrength = 0.55f;

            asset.scalpProfile = profile;
            asset.headCollisionMask = autoAssignMask ? mask : null;

            // Параметры
            asset.defaultLength = 1f;
            asset.defaultDensity = 1f;
            asset.defaultThickness = 1f;
            asset.defaultCurl = 0f;
            asset.defaultWave = 0f;
            asset.defaultFrizz = 0f;

            // Создаём гайды с распределением по ScalpZone
            var guides = new List<HairGuide>();
            var rnd = new System.Random(newId.GetHashCode());

            for (int i = 0; i < guideCount; i++)
            {
                HairGuide g;

                if (profile != null)
                {
                    // Используем ScalpProfile для распределения по зонам
                    ScalpZoneId zone = SlotToZone(slot, i, guideCount);
                    float u = (float)(rnd.NextDouble() * 2f - 1f);
                    float v = (float)(rnd.NextDouble() * 2f - 1f);
                    Vector3 root = profile.GetRoot(zone, u, v);
                    g = HairGuide.CreateDefault("c_head", root, length);
                    g.groupId = (int)zone;

                    // Добавляем небольшое отклонение контрольных точек
                    for (int p = 0; p < g.pointsLocal.Length; p++)
                    {
                        g.pointsLocal[p].x += ((float)rnd.NextDouble() - 0.5f) * 0.008f;
                        g.pointsLocal[p].z += ((float)rnd.NextDouble() - 0.5f) * 0.008f;
                    }
                }
                else
                {
                    // fallback — полусфера (старое поведение, но с лучшим распределением)
                    Vector3 rootLocal = SlotToRootFallback(slot, rnd);
                    Vector3 headLocal = rootLocal - new Vector3(0, 1.6f, 0);
                    g = HairGuide.CreateDefault("c_head", headLocal, length);
                    g.groupId = (int)slot;

                    for (int p = 0; p < g.pointsLocal.Length; p++)
                    {
                        g.pointsLocal[p].x += ((float)rnd.NextDouble() - 0.5f) * 0.008f;
                        g.pointsLocal[p].z += ((float)rnd.NextDouble() - 0.5f) * 0.008f;
                    }
                }

                g.thicknessRoot = 0.005f;
                g.thicknessTip = 0.001f;
                g.sideCount = strandSides;
                guides.Add(g);
            }

            asset.guides = guides.ToArray();

            // Сохраняем
            string folder = "Assets/Generated/HairLibraryProc";
            EnsureFolder("Assets/Generated");
            EnsureFolder(folder);

            string path = $"{folder}/{newId}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[HairProc] Created {path} with {guideCount} guides using scalp zones.", asset);

            EditorUtility.DisplayDialog("Hair Created",
                $"Created: {path}\n\n" +
                $"Next steps:\n" +
                "1. Run 'Fix ALL Hair Roots To Scalp Zones'\n" +
                "2. Run 'Rebuild Hair Catalog'",
                "OK");
        }

        ScalpProfileDefinition FindProfile()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScalpProfileDefinition");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<ScalpProfileDefinition>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        HeadCollisionMaskDefinition FindMask()
        {
            string[] guids = AssetDatabase.FindAssets("t:HeadCollisionMaskDefinition");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<HeadCollisionMaskDefinition>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static ScalpZoneId SlotToZone(HairSlot slot, int index, int total)
        {
            switch (slot)
            {
                case HairSlot.Bangs: return ScalpZoneId.BangsFront;
                case HairSlot.SideLeft: return ScalpZoneId.SideLeftTop;
                case HairSlot.SideRight: return ScalpZoneId.SideRightTop;
                case HairSlot.Back: return ScalpZoneId.BackCrown;
                case HairSlot.Ponytail: return ScalpZoneId.BackCrown;
                default: return ScalpZoneId.Top;
            }
        }

        static Vector3 SlotToRootFallback(HairSlot slot, System.Random rnd)
        {
            switch (slot)
            {
                case HairSlot.Bangs:
                    return new Vector3(
                        Mathf.Lerp(-0.07f, 0.07f, (float)rnd.NextDouble()),
                        1.72f + (float)rnd.NextDouble() * 0.015f,
                        0.085f + (float)rnd.NextDouble() * 0.015f);
                case HairSlot.SideLeft:
                    return new Vector3(-0.085f, 1.68f + (float)rnd.NextDouble() * 0.04f, 0.01f);
                case HairSlot.SideRight:
                    return new Vector3(0.085f, 1.68f + (float)rnd.NextDouble() * 0.04f, 0.01f);
                default: // Back
                    return new Vector3(
                        Mathf.Lerp(-0.08f, 0.08f, (float)rnd.NextDouble()),
                        1.65f + (float)rnd.NextDouble() * 0.06f,
                        -0.06f - (float)rnd.NextDouble() * 0.03f);
            }
        }

        static void EnsureFolder(string f)
        {
            if (AssetDatabase.IsValidFolder(f)) return;
            string parent = System.IO.Path.GetDirectoryName(f).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(f);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif