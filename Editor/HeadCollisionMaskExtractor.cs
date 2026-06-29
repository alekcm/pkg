#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using CharacterEditor.Hair.Proc;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorTool
{
    public static class HeadCollisionMaskExtractor
    {
        private const string OutputFolder = "Assets/Generated/HairMasks";

        [MenuItem("Tools/Character/Hair/Create Head Collision Mask From Selected FBX or Character")]
        public static void CreateMaskFromSelection()
        {
            Object selected = Selection.activeObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Head Mask", "Select a character FBX/prefab asset or a character object in the scene first.", "OK");
                return;
            }

            GameObject sourceGo = selected as GameObject;
            string sourcePath = AssetDatabase.GetAssetPath(selected);
            GameObject instance = null;
            bool temporary = false;

            try
            {
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    GameObject assetGo = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                    if (assetGo == null)
                    {
                        EditorUtility.DisplayDialog("Head Mask", "Selected asset is not a GameObject/FBX prefab.", "OK");
                        return;
                    }

                    instance = Object.Instantiate(assetGo);
                    instance.name = assetGo.name + "_HeadMaskTemp";
                    temporary = true;
                }
                else
                {
                    instance = sourceGo;
                }

                if (instance == null)
                    return;

                Transform head = FindHeadBone(instance.transform);
                if (head == null)
                {
                    EditorUtility.DisplayDialog("Head Mask", "Could not find a head bone. Expected a bone name containing 'head'.", "OK");
                    return;
                }

                List<Vector3> headLocalPoints = CollectHeadPoints(instance, head);
                if (headLocalPoints.Count < 16)
                {
                    EditorUtility.DisplayDialog("Head Mask", $"Found too few head points: {headLocalPoints.Count}. Try selecting the instantiated scene character instead of the raw FBX.", "OK");
                    return;
                }

                Bounds b = BuildBounds(headLocalPoints);

                EnsureFolder(OutputFolder);
                string safeName = MakeSafeFileName(instance.name.Replace("_HeadMaskTemp", string.Empty));
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/HeadCollisionMask_{safeName}.asset");

                HeadCollisionMaskDefinition mask = ScriptableObject.CreateInstance<HeadCollisionMaskDefinition>();
                // Some imported Mixamo/VRoid FBX files have Armature scale 100 or split face bones.
                // In those cases vertex-weight extraction can produce a tiny face-only bounds like
                // 0.02/0.03m, which is useless for hair collision. Clamp to a practical anime-head
                // ellipsoid in the same authoring space used by the procedural hair guides.
                Vector3 practicalCenter = b.center;
                if (b.extents.y < 0.055f)
                    practicalCenter = new Vector3(0f, 0.06f, 0f);

                mask.center = practicalCenter;
                mask.radii = new Vector3(
                    Mathf.Max(0.080f, b.extents.x),
                    Mathf.Max(0.115f, b.extents.y),
                    Mathf.Max(0.080f, b.extents.z)
                );
                mask.surfacePadding = 0.012f;
                mask.softness = 0.08f;
                mask.affectUntilT = 1f;
                mask.sourceHeadLocalBounds = b;
                mask.sourceModelPath = sourcePath;
                mask.sourceHeadBoneName = head.name;

                AssetDatabase.CreateAsset(mask, assetPath);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(mask);

                Debug.Log($"[HeadMask] Created {assetPath}\nHead bone: {head.name}\nPoints: {headLocalPoints.Count}\nCenter: {mask.center}\nRadii: {mask.radii}", mask);

                if (EditorUtility.DisplayDialog("Head Mask created", $"Created:\n{assetPath}\n\nAssign this mask to generated HairPieceDefinitionProc assets now?", "Assign", "Not now"))
                    AssignMaskToAllHairPieces(mask);
            }
            finally
            {
                if (temporary && instance != null)
                    Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("Tools/Character/Hair/Assign Selected Head Mask To All Hair Pieces")]
        public static void AssignSelectedMaskToAllHairPieces()
        {
            HeadCollisionMaskDefinition mask = Selection.activeObject as HeadCollisionMaskDefinition;
            if (mask == null)
            {
                EditorUtility.DisplayDialog("Head Mask", "Select a HeadCollisionMaskDefinition asset first.", "OK");
                return;
            }
            AssignMaskToAllHairPieces(mask);
        }

        private static void AssignMaskToAllHairPieces(HeadCollisionMaskDefinition mask)
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                HairPieceDefinitionProc piece = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (piece == null)
                    continue;

                piece.headCollisionMask = mask;
                EditorUtility.SetDirty(piece);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HeadMask] Assigned {mask.name} to {count} HairPieceDefinitionProc assets.", mask);
        }

        private static List<Vector3> CollectHeadPoints(GameObject instance, Transform head)
        {
            var result = new List<Vector3>(2048);
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (SkinnedMeshRenderer smr in renderers)
            {
                if (smr.sharedMesh == null)
                    continue;

                Mesh baked = new Mesh();
                try
                {
                    smr.BakeMesh(baked);
                    Vector3[] vertices = baked.vertices;
                    BoneWeight[] weights = smr.sharedMesh.boneWeights;
                    Transform[] bones = smr.bones;
                    bool canUseWeights = weights != null && weights.Length == vertices.Length && bones != null && bones.Length > 0;

                    HashSet<int> headBoneIndices = BuildHeadBoneIndexSet(bones);

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 world = smr.transform.TransformPoint(vertices[i]);
                        Vector3 local = head.InverseTransformPoint(world);

                        bool selectedByWeight = false;
                        if (canUseWeights && headBoneIndices.Count > 0)
                            selectedByWeight = HasHeadWeight(weights[i], headBoneIndices, 0.08f);

                        bool selectedByFallbackBox = IsLikelyHeadLocalPoint(local);
                        if (selectedByWeight || selectedByFallbackBox)
                            result.Add(local);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }

            return result;
        }

        private static HashSet<int> BuildHeadBoneIndexSet(Transform[] bones)
        {
            var set = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                Transform b = bones[i];
                if (b == null)
                    continue;
                string n = b.name.ToLowerInvariant();
                if (n.Contains("head") || n.Contains("face") || n.Contains("eye") || n.Contains("jaw"))
                    set.Add(i);
            }
            return set;
        }

        private static bool HasHeadWeight(BoneWeight w, HashSet<int> headBoneIndices, float minWeight)
        {
            return (headBoneIndices.Contains(w.boneIndex0) && w.weight0 >= minWeight)
                || (headBoneIndices.Contains(w.boneIndex1) && w.weight1 >= minWeight)
                || (headBoneIndices.Contains(w.boneIndex2) && w.weight2 >= minWeight)
                || (headBoneIndices.Contains(w.boneIndex3) && w.weight3 >= minWeight);
        }

        private static bool IsLikelyHeadLocalPoint(Vector3 p)
        {
            // Generic fallback for Mixamo/VRoid-like rigs where the head bone origin is near the neck/base of head.
            return p.x > -0.16f && p.x < 0.16f
                && p.y > -0.08f && p.y < 0.26f
                && p.z > -0.18f && p.z < 0.16f;
        }

        private static Bounds BuildBounds(List<Vector3> points)
        {
            Bounds b = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++)
                b.Encapsulate(points[i]);

            // Hair should collide with the skull, not eyelashes or tiny facial extremes.
            // Slightly inflate to keep locks outside the skin.
            b.Expand(new Vector3(0.012f, 0.016f, 0.012f));
            return b;
        }

        private static Transform FindHeadBone(Transform root)
        {
            Transform best = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "head" || n.EndsWith("_head") || n.Contains("c_head"))
                    return t;
                if (best == null && n.Contains("head"))
                    best = t;
            }
            return best;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Character" : name;
        }
    }
}
#endif
