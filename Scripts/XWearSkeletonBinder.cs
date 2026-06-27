// XWearSkeletonBinder.cs (v11 - Correct Hips-to-Hips Alignment)
// Proper alignment: matches clothing's own Hips bone to character's Hips bone
// This fixes both position and rotation issues caused by different parent rotations

using System.Collections.Generic;
using UnityEngine;

namespace XWearImporter
{
    [ExecuteAlways]
    [AddComponentMenu("XWear/Skeleton Binder (v11)")]
    public class XWearSkeletonBinder : MonoBehaviour
    {
        public enum BindMode
        {
            DirectSMRLink,
            SmartRetargeting,
            WorldPoseFollower
        }

        [Header("Core Settings")]
        public BindMode bindMode = BindMode.DirectSMRLink;
        public Transform mainCharacterRoot;
        public List<Transform> clothingRoots = new List<Transform>();

        [Header("Automatic Alignment (Hips-to-Hips)")]
        [Tooltip("Automatically align clothing's own Hips bone to character's Hips")]
        public bool autoAlignToHips = true;

        [Header("Safety")]
        public bool disableAutoScaleCompensation = true;

        [Header("Debug")]
        public bool enableDebugLogs = true;
        public bool showGizmos = true;

        private readonly Dictionary<Transform, Transform> _bodyToClothMap = new Dictionary<Transform, Transform>();
        private bool _isInitialized = false;

        #region Public API

        [ContextMenu("Rebind All Clothing")]
        public void RebindAll() => InitializeBinder();

        [ContextMenu("Restore Visibility")]
        public void RestoreAllVisibility()
        {
            foreach (var clothing in clothingRoots)
            {
                if (clothing == null) continue;
                clothing.gameObject.SetActive(true);

                foreach (var name in new[] { "RootGameObject", "SkeletonRoot", "Armature" })
                {
                    var t = clothing.Find(name);
                    if (t != null) t.gameObject.SetActive(true);
                }

                foreach (var smr in clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.enabled = true;
                    smr.gameObject.SetActive(true);
                }
            }
        }

        [ContextMenu("Force Rebind")]
        public void ForceRebind()
        {
            _isInitialized = false;
            InitializeBinder();
        }

        #endregion

        #region Initialization

        private void Awake()
        {
            if (Application.isPlaying) InitializeBinder();
        }

        private void OnValidate()
        {
            if (mainCharacterRoot == null) mainCharacterRoot = transform;
        }

        public void InitializeBinder()
        {
            _bodyToClothMap.Clear();

            if (mainCharacterRoot == null) mainCharacterRoot = transform;

            var bodyBones = new Dictionary<string, Transform>();
            CollectBonesCanonically(mainCharacterRoot, bodyBones);

            foreach (var clothing in clothingRoots)
            {
                if (clothing == null) continue;

                RestoreSingleVisibility(clothing);

                if (autoAlignToHips)
                    AlignClothingHipsToCharacterHips(clothing);

                if (!disableAutoScaleCompensation)
                    ApplyScaleCompensation(clothing);

                RepairClothingSMR(clothing);

                if (bindMode == BindMode.DirectSMRLink)
                    ApplyDirectSMRLink(clothing, bodyBones);
                else
                    ApplyDeltaOrWorldSync(clothing, bodyBones);
            }

            _isInitialized = true;

            if (enableDebugLogs)
                Debug.Log($"[SkeletonBinder] Bound {clothingRoots.Count} items with Hips alignment");
        }

        private void RestoreSingleVisibility(Transform clothingRoot)
        {
            if (clothingRoot == null) return;
            clothingRoot.gameObject.SetActive(true);

            foreach (var name in new[] { "RootGameObject", "SkeletonRoot", "Armature" })
            {
                var t = clothingRoot.Find(name);
                if (t != null) t.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Correctly aligns clothing by matching its own Hips bone to character's Hips bone.
        /// This respects different parent rotations.
        /// </summary>
        private void AlignClothingHipsToCharacterHips(Transform clothingRoot)
        {
            if (clothingRoot == null || mainCharacterRoot == null) return;

            Transform characterHips = FindHipsBone(mainCharacterRoot);
            Transform clothingHips = FindHipsBone(clothingRoot);

            if (characterHips == null)
            {
                if (enableDebugLogs) Debug.LogWarning("[SkeletonBinder] Character Hips not found");
                return;
            }

            if (clothingHips == null)
            {
                // Fallback: just move root to character root
                clothingRoot.position = mainCharacterRoot.position;
                clothingRoot.rotation = mainCharacterRoot.rotation;
                if (enableDebugLogs) Debug.LogWarning($"[SkeletonBinder] No Hips found in '{clothingRoot.name}', using fallback");
                return;
            }

            // Calculate offset between clothing root and its own Hips
            Vector3 offset = clothingHips.position - clothingRoot.position;

            // Place clothing root so that its Hips lands exactly on character's Hips
            clothingRoot.position = characterHips.position - offset;
            clothingRoot.rotation = characterHips.rotation;

            if (enableDebugLogs)
                Debug.Log($"[SkeletonBinder] Aligned '{clothingRoot.name}' Hips-to-Hips");
        }

        private Transform FindHipsBone(Transform root)
        {
            string[] names = { "mixamorig:Hips", "Hips", "hips", "pelvis", "c_hips", "j_bip_c_hips" };

            foreach (string name in names)
            {
                Transform t = root.Find(name);
                if (t != null) return t;

                Transform deep = FindChildDeep(root, name);
                if (deep != null) return deep;
            }

            return FindChildContaining(root, "hip") ?? FindChildContaining(root, "pelvis");
        }

        private Transform FindChildDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildDeep(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private Transform FindChildContaining(Transform parent, string contains)
        {
            if (parent.name.ToLower().Contains(contains)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildContaining(parent.GetChild(i), contains);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region Scale & Binding

        private void ApplyScaleCompensation(Transform clothingRoot)
        {
            if (disableAutoScaleCompensation) return;
            if (mainCharacterRoot == null || clothingRoot == null) return;

            Transform armature = FindArmatureRecursive(mainCharacterRoot);
            float scale = armature != null ? armature.lossyScale.x : mainCharacterRoot.lossyScale.x;

            if (scale < 5f) return;
            clothingRoot.localScale = Vector3.one * (1f / scale);
        }

        private Transform FindArmatureRecursive(Transform current)
        {
            if (current.name.ToLower().Contains("armature")) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform found = FindArmatureRecursive(current.GetChild(i));
                if (found != null) return found;
            }
            return null;
        }

        private void ApplyDirectSMRLink(Transform clothingRoot, Dictionary<string, Transform> bodyBones)
        {
            var smrs = clothingRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;

                smr.enabled = true;
                smr.gameObject.SetActive(true);

                var original = smr.bones;
                var newBones = new Transform[original.Length];
                bool mapped = false;

                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;

                    string canon = GetCanonicalBoneName(original[i].name);

                    if (bodyBones.TryGetValue(canon, out var bodyBone))
                    {
                        newBones[i] = bodyBone;
                        mapped = true;
                        if (!_bodyToClothMap.ContainsKey(bodyBone))
                            _bodyToClothMap[bodyBone] = original[i];
                    }
                    else
                    {
                        newBones[i] = original[i];
                    }
                }

                if (mapped)
                {
                    smr.bones = newBones;
                    UpdateBindposes(smr, newBones);

                    if (smr.rootBone != null)
                    {
                        string rootCanon = GetCanonicalBoneName(smr.rootBone.name);
                        if (bodyBones.TryGetValue(rootCanon, out var newRoot))
                            smr.rootBone = newRoot;
                    }
                }
            }
        }

        private void UpdateBindposes(SkinnedMeshRenderer smr, Transform[] newBones)
        {
            if (smr.sharedMesh == null) return;

            var bindposes = new Matrix4x4[newBones.Length];
            for (int i = 0; i < newBones.Length; i++)
            {
                bindposes[i] = newBones[i] != null
                    ? newBones[i].worldToLocalMatrix * smr.transform.localToWorldMatrix
                    : Matrix4x4.identity;
            }
            smr.sharedMesh.bindposes = bindposes;
        }

        private void ApplyDeltaOrWorldSync(Transform clothingRoot, Dictionary<string, Transform> bodyBones)
        {
            var container = clothingRoot.Find("RootGameObject") ?? clothingRoot.Find("SkeletonRoot");
            if (container != null) container.gameObject.SetActive(true);

            var clothBones = new Dictionary<string, Transform>();
            CollectBonesCanonically(clothingRoot, clothBones);

            foreach (var kvp in clothBones)
            {
                if (bodyBones.TryGetValue(kvp.Key, out var bodyBone) &&
                    bodyBone != mainCharacterRoot && kvp.Value != clothingRoot)
                {
                    _bodyToClothMap[bodyBone] = kvp.Value;
                }
            }
        }

        #endregion

        #region Bone Utilities

        private static void CollectBonesCanonically(Transform current, Dictionary<string, Transform> dict)
        {
            if (current == null) return;
            string canon = GetCanonicalBoneName(current.name);
            if (!string.IsNullOrEmpty(canon) && !dict.ContainsKey(canon))
                dict[canon] = current;

            for (int i = 0; i < current.childCount; i++)
                CollectBonesCanonically(current.GetChild(i), dict);
        }

        private static string GetCanonicalBoneName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string n = rawName.ToLowerInvariant()
                .Replace("mixamorig:", "").Replace("j_bip_c_", "").Replace("j_bip_l_", "l_")
                .Replace("j_bip_r_", "r_").Replace("j_bip_", "").Replace("character_", "").Replace("_", "");

            bool left = n.StartsWith("l") || n.Contains("left");
            bool right = n.StartsWith("r") || n.Contains("right");
            string side = left ? "l_" : right ? "r_" : "c_";

            string role = "";
            if (n.Contains("hips") || n.Contains("pelvis")) role = "hips";
            else if (n.Contains("upperchest") || n.Contains("spine2")) role = "upperchest";
            else if (n.Contains("chest") || n.Contains("spine1")) role = "chest";
            else if (n.Contains("spine")) role = "spine";
            else if (n.Contains("neck")) role = "neck";
            else if (n.Contains("head")) role = "head";
            else if (n.Contains("shoulder")) role = "shoulder";
            else if (n.Contains("upperarm") || n.Contains("arm")) role = "upperarm";
            else if (n.Contains("lowerarm") || n.Contains("forearm")) role = "lowerarm";
            else if (n.Contains("hand")) role = "hand";
            else if (n.Contains("upperleg") || n.Contains("upleg")) role = "upperleg";
            else if (n.Contains("lowerleg") || n.Contains("calf")) role = "lowerleg";
            else if (n.Contains("foot")) role = "foot";
            else if (n.Contains("toe")) role = "toes";

            return string.IsNullOrEmpty(role) ? n : side + role;
        }

        private void RepairClothingSMR(Transform clothing)
        {
            if (clothing == null) return;

            var container = clothing.Find("RootGameObject") ?? clothing.Find("SkeletonRoot");
            if (container == null) return;

            container.gameObject.SetActive(true);

            var clothBones = new Dictionary<string, Transform>();
            CollectBonesCanonically(container, clothBones);

            foreach (var smr in clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;

                var current = smr.bones;
                var fixedBones = new Transform[current.Length];

                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i] == null) continue;
                    string canon = GetCanonicalBoneName(current[i].name);
                    fixedBones[i] = clothBones.TryGetValue(canon, out var correct) ? correct : current[i];
                }

                smr.bones = fixedBones;
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos || mainCharacterRoot == null) return;

            foreach (var kvp in _bodyToClothMap)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(kvp.Key.position, kvp.Value.position);
                }
            }
        }

        #endregion
    }
}