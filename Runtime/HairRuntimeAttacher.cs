using System.Collections.Generic;
using UnityEngine;

namespace CharacterEditor.Hair
{
    /// <summary>
    /// Optional runtime helper. It does NOT touch SkinnedMeshRenderer.localBounds.
    /// Use it only if you want to instantiate generated hair prefabs and remap their standard humanoid bones
    /// to the current avatar by name/canonical name. Custom VRoid hair bones stay inside the hair prefab.
    /// </summary>
    public class HairRuntimeAttacher : MonoBehaviour
    {
        public Transform avatarRoot;
        public Transform fallbackParent;

        private readonly Dictionary<string, Transform> _boneMap = new();

        public void RebuildBoneMap()
        {
            _boneMap.Clear();
            if (avatarRoot == null) avatarRoot = transform;

            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                AddBoneKey(t.name, t);
                AddBoneKey(GetCanonicalBoneName(t.name), t);
            }
        }

        public GameObject Attach(HairPieceDefinition piece)
        {
            if (piece == null || piece.prefab == null) return null;
            if (_boneMap.Count == 0) RebuildBoneMap();

            Transform parent = fallbackParent != null ? fallbackParent : avatarRoot;
            GameObject instance = Instantiate(piece.prefab, parent, false);
            RemapSkinnedMeshes(instance);
            return instance;
        }

        public void RemapSkinnedMeshes(GameObject hairInstance)
        {
            if (hairInstance == null) return;
            if (_boneMap.Count == 0) RebuildBoneMap();

            foreach (var smr in hairInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] sourceBones = smr.bones;
                Transform[] mappedBones = new Transform[sourceBones.Length];

                for (int i = 0; i < sourceBones.Length; i++)
                {
                    Transform source = sourceBones[i];
                    Transform target = Resolve(source);

                    // Only replace bones that actually exist on the target avatar.
                    // VRoid-specific hair bones remain in the generated hair prefab.
                    mappedBones[i] = target != null ? target : source;
                }

                smr.bones = mappedBones;

                Transform root = Resolve(smr.rootBone);
                if (root != null) smr.rootBone = root;
            }
        }

        private Transform Resolve(Transform source)
        {
            if (source == null) return null;
            if (_boneMap.TryGetValue(source.name, out var exact)) return exact;

            string canon = GetCanonicalBoneName(source.name);
            if (!string.IsNullOrEmpty(canon) && _boneMap.TryGetValue(canon, out var canonical))
                return canonical;

            return null;
        }

        private void AddBoneKey(string key, Transform t)
        {
            if (!string.IsNullOrEmpty(key) && !_boneMap.ContainsKey(key))
                _boneMap.Add(key, t);
        }

        public static string GetCanonicalBoneName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";

            string n = rawName.ToLowerInvariant();
            n = n.Replace("mixamorig:", "")
                 .Replace("j_bip_c_", "")
                 .Replace("j_bip_l_", "l_")
                 .Replace("j_bip_r_", "r_")
                 .Replace("j_bip_", "")
                 .Replace("character_", "");

            bool isLeft = n.StartsWith("l_") || n.EndsWith("_l") || n.Contains("left");
            bool isRight = n.StartsWith("r_") || n.EndsWith("_r") || n.Contains("right");

            string role = "";
            if (n.Contains("hips") || n.Contains("pelvis")) role = "hips";
            else if (n.Contains("upperchest") || n.Contains("spine2")) role = "upperchest";
            else if (n.Contains("chest") || n.Contains("spine1")) role = "chest";
            else if (n.Contains("spine")) role = "spine";
            else if (n.Contains("neck")) role = "neck";
            else if (n.Contains("head")) role = "head";
            else if (n.Contains("shoulder")) role = "shoulder";
            else if (n.Contains("forearm") || n.Contains("lowerarm")) role = "lowerarm";
            else if (n.Contains("upperarm") || n.Contains("arm")) role = "upperarm";
            else if (n.Contains("hand")) role = "hand";
            else if (n.Contains("upleg") || n.Contains("upperleg")) role = "upperleg";
            else if (n.Contains("lowerleg") || n.Contains("calf") || (n.Contains("leg") && !n.Contains("upleg"))) role = "lowerleg";
            else if (n.Contains("foot")) role = "foot";
            else if (n.Contains("toebase") || n.Contains("toes") || n.Contains("toe")) role = "toes";

            if (string.IsNullOrEmpty(role)) return n;
            string side = isLeft ? "l_" : (isRight ? "r_" : "c_");
            return side + role;
        }
    }
}
