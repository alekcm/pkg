// HairProcCreatorWindow.cs – Unity Editor
// Convert legacy VRM hair OR create blank procedural hair piece
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;

namespace CharacterEditor.Hair.EditorTool
{
    public class HairProcCreatorWindow : EditorWindow
    {
        [MenuItem("Tools/Character/Hair Proc Creator")]
        static void Open() => GetWindow<HairProcCreatorWindow>("Hair Proc");

        string newId = "hair_bangs_01";
        HairSlot slot = HairSlot.Bangs;
        int guideCount = 36;
        float length = 0.22f;
        HairPieceDefinitionProc targetToFill;

        Vector2 scroll;

        void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural Hair – VRoid style", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Создаёт HairPieceDefinitionProc с guide-сплайнами. Потом в Play Mode игрок крутит Length/Density/Curl и таскает пряди мышкой (HairEditorTool).", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            newId = EditorGUILayout.TextField("ID", newId);
            slot = (HairSlot)EditorGUILayout.EnumPopup("Slot", slot);
            guideCount = EditorGUILayout.IntSlider("Guides", guideCount, 8, 160);
            length = EditorGUILayout.Slider("Length (m)", length, 0.05f, 0.6f);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Blank Procedural Hair", GUILayout.Height(32)))
                CreateBlank();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Convert legacy", EditorStyles.boldLabel);
            targetToFill = (HairPieceDefinitionProc)EditorGUILayout.ObjectField("Target Proc Asset", targetToFill, typeof(HairPieceDefinitionProc), false);
            if (GUILayout.Button("Fill Guides From Legacy VRM Piece"))
                FillFromLegacy();

            EditorGUILayout.EndScrollView();
        }

        void CreateBlank()
        {
            if (string.IsNullOrEmpty(newId)) { EditorUtility.DisplayDialog("Error", "ID empty", "OK"); return; }
            var asset = ScriptableObject.CreateInstance<HairPieceDefinitionProc>();
            asset.id = newId;
            asset.displayName = newId;
            asset.slot = slot;
            asset.defaultLength = 1f;
            asset.defaultDensity = 1f;
            asset.defaultThickness = 1f;

            // generate scalp points – simple hemisphere
            var guides = new HairGuide[guideCount];
            var rnd = new System.Random(newId.GetHashCode());
            for (int i = 0; i < guideCount; i++)
            {
                // distribute by slot
                Vector3 rootLocal = Vector3.zero;
                switch (slot)
                {
                    case HairSlot.Bangs:
                        rootLocal = new Vector3(
                            Mathf.Lerp(-0.07f, 0.07f, (float)rnd.NextDouble()),
                            1.72f + (float)rnd.NextDouble()*0.015f,
                            0.085f + (float)rnd.NextDouble()*0.015f);
                        break;
                    case HairSlot.SideLeft:
                        rootLocal = new Vector3(-0.085f, 1.68f + (float)rnd.NextDouble()*0.04f, 0.01f);
                        break;
                    case HairSlot.SideRight:
                        rootLocal = new Vector3(0.085f, 1.68f + (float)rnd.NextDouble()*0.04f, 0.01f);
                        break;
                    default: // Back
                        rootLocal = new Vector3(
                            Mathf.Lerp(-0.08f, 0.08f, (float)rnd.NextDouble()),
                            1.65f + (float)rnd.NextDouble()*0.06f,
                            -0.06f - (float)rnd.NextDouble()*0.03f);
                        break;
                }
                // convert world scalp pos to head-local (head ~ Y 1.6)
                Vector3 headLocal = rootLocal - new Vector3(0,1.6f,0);
                var g = HairGuide.CreateDefault("c_head", headLocal, length);
                g.groupId = (int)slot;
                // slight random
                for (int p = 0; p < g.pointsLocal.Length; p++)
                {
                    g.pointsLocal[p].x += ((float)rnd.NextDouble()-0.5f)*0.008f;
                    g.pointsLocal[p].z += ((float)rnd.NextDouble()-0.5f)*0.008f;
                }
                guides[i] = g;
            }
            asset.guides = guides;

            string folder = "Assets/Generated/HairLibraryProc";
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Generated", "HairLibraryProc");
            string path = $"{folder}/{newId}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[HairProc] Created {path} with {guideCount} guides", asset);
        }

        void FillFromLegacy()
        {
            if (targetToFill == null) { EditorUtility.DisplayDialog("Error", "Select target Proc asset", "OK"); return; }
            // call the ContextMenu extractor already in HairPieceDefinitionProc
            var mi = typeof(HairPieceDefinitionProc).GetMethod("ExtractGuidesFromLegacy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (mi != null) mi.Invoke(targetToFill, null);
            else Debug.LogWarning("ExtractGuidesFromLegacy not found – use asset context menu directly.");
        }
    }
}
#endif
