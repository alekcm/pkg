#if UNITY_EDITOR
using CharacterEditor.Hair.Proc;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorTool
{
    public static class ScalpProfileTools
    {
        private const string OutputFolder = "Assets/Generated/HairMasks";

        [MenuItem("Tools/Character/Hair/Create Scalp Profile From Selected Head Mask")]
        public static void CreateScalpProfileFromSelectedMask()
        {
            HeadCollisionMaskDefinition mask = Selection.activeObject as HeadCollisionMaskDefinition;
            if (mask == null)
            {
                EditorUtility.DisplayDialog("Scalp Profile", "Select a HeadCollisionMaskDefinition asset first.", "OK");
                return;
            }

            EnsureFolder(OutputFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/ScalpProfile_{mask.name}.asset");
            ScalpProfileDefinition profile = ScriptableObject.CreateInstance<ScalpProfileDefinition>();
            profile.headMask = mask;
            profile.PresetFromMaskAnimeDefault();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(profile);

            if (EditorUtility.DisplayDialog("Scalp Profile created", $"Created:\n{path}\n\nAssign this profile to all hair pieces now?", "Assign", "Not now"))
                AssignProfileToAllHairPieces(profile);
        }

        [MenuItem("Tools/Character/Hair/Assign Selected Scalp Profile To All Hair Pieces")]
        public static void AssignSelectedScalpProfileToAllHairPieces()
        {
            ScalpProfileDefinition profile = Selection.activeObject as ScalpProfileDefinition;
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Scalp Profile", "Select a ScalpProfileDefinition asset first.", "OK");
                return;
            }
            AssignProfileToAllHairPieces(profile);
        }

        public static void AssignProfileToAllHairPieces(ScalpProfileDefinition profile)
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                HairPieceDefinitionProc piece = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (piece == null) continue;
                piece.scalpProfile = profile;
                if (piece.headCollisionMask == null && profile.headMask != null)
                    piece.headCollisionMask = profile.headMask;
                EditorUtility.SetDirty(piece);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ScalpProfileTools] Assigned {profile.name} to {count} hair pieces.", profile);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
