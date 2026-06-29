#if UNITY_EDITOR
using CharacterEditor.Hair.DebugTools;
using CharacterEditor.Hair.Proc;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorTool
{
    public static class ScalpProfileGizmoSetup
    {
        [MenuItem("Tools/Character/Hair/Add Scalp Profile Gizmo Drawer To Selected Character")]
        public static void AddDrawerToSelectedCharacter()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Scalp Gizmo", "Select the character root in Hierarchy first.", "OK");
                return;
            }

            ScalpProfileGizmoDrawer drawer = go.GetComponent<ScalpProfileGizmoDrawer>();
            if (drawer == null)
                drawer = Undo.AddComponent<ScalpProfileGizmoDrawer>(go);

            drawer.headBone = FindHeadBone(go.transform);
            drawer.scalpProfile = FindSelectedOrProjectProfile();
            drawer.drawGizmos = true;
            drawer.drawHeadMask = true;
            drawer.drawHairline = true;
            drawer.drawZoneSamples = true;
            drawer.drawLandmarks = true;
            drawer.drawNormals = true;
            drawer.drawLabels = true;

            EditorUtility.SetDirty(drawer);
            Selection.activeObject = drawer;
            Debug.Log("[ScalpProfileGizmoSetup] Added/updated ScalpProfileGizmoDrawer.", drawer);
        }

        private static ScalpProfileDefinition FindSelectedOrProjectProfile()
        {
            ScalpProfileDefinition selected = Selection.activeObject as ScalpProfileDefinition;
            if (selected != null)
                return selected;

            string[] guids = AssetDatabase.FindAssets("t:ScalpProfileDefinition");
            if (guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ScalpProfileDefinition>(path);
        }

        private static Transform FindHeadBone(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "head" || n.EndsWith("_head") || n.Contains("c_head"))
                    return t;
            }
            return null;
        }
    }
}
#endif
