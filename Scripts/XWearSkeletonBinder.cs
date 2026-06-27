// XWearSkeletonBinder.cs
// Synchronizes VRoid .xwear clothing skeletons to any animated character
// via relative animation angle changes (Delta Poses). Fully non-destructive and stable.

using System.Collections.Generic;
using UnityEngine;

namespace XWearImporter
{
    [ExecuteAlways]
    [AddComponentMenu("XWear/Skeleton Binder")]
    public class XWearSkeletonBinder : MonoBehaviour
    {
        public enum BindMode
        {
            [Tooltip("Synchronizes animation joint angles (Delta Poses) and matches Main Hips position. Safe and non-destructive for any rig.")]
            SmartRetargeting,

            [Tooltip("Destructively overwrites SkinnedMeshRenderer bone references. Only compatible if body and clothes match completely in raw hierarchies.")]
            DirectSMRLink,

            [Tooltip("Directly copies raw world positions and rotations.")]
            WorldPoseFollower
        }

        [Header("Architecture")]
        public BindMode bindMode = BindMode.SmartRetargeting;

        [Tooltip("The root Transform of the animated character (e.g. the model containing mixamorig:Hips).")]
        public Transform mainCharacterRoot;

        [Tooltip("List of imported XWear clothing root instances attached to this character.")]
        public List<Transform> clothingRoots = new List<Transform>();

        [Header("Direct SMR Binding")]
        [Tooltip("Before DirectSMRLink rebinds the SMR to the body bones, move the clothing root to the character root position. Rotation is controlled separately below so a manually fixed 180-degree clothing offset is not destroyed on Play.")]
        public bool alignClothingRootToCharacterInDirectSMR = true;

        [Tooltip("Also copy the character root rotation to clothing roots before binding. Leave OFF if your XWear item needs a manual 180-degree rotation or a different prefab rotation. Position alignment still works when this is OFF.")]
        public bool alignClothingRootRotationToCharacterInDirectSMR = false;

        [Tooltip("When DirectSMRLink replaces clothing bones with body bones, clone the mesh and rebuild bindposes preserving the original clothing skinning matrices. This fixes root rotation/scale differences such as Mixamo scale=100 and XWear/Mixamo axis offsets. No per-frame cost; it runs once during InitializeBinder().")]
        public bool rebuildBindposesForDirectSMR = true;

        [Header("Crowd Optimization")]
        public bool optimizeFarPhysics = true;
        public float farDistanceThreshold = 15f;

        private struct BoneLink
        {
            public Transform bodyBone;
            public Transform clothBone;
            
            public Quaternion bodyStartLocalRot;
            public Quaternion clothStartLocalRot;
            public bool isRootHips;
        }

        private readonly List<BoneLink> _links = new List<BoneLink>();
        private readonly List<XWearSpringBone> _springBones = new List<XWearSpringBone>();
        private Camera _mainCam;
        private bool _isInitialized = false;

        void Awake()
        {
            if (Application.isPlaying)
            {
                InitializeBinder();
            }
        }

        void Start()
        {
            if (Application.isPlaying && !_isInitialized)
            {
                InitializeBinder();
            }
        }

        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                foreach (var clothing in clothingRoots)
                {
                    RepairClothingSMR(clothing);
                }
            }
        }

        public void InitializeBinder()
        {
            _links.Clear();
            _springBones.Clear();

            if (mainCharacterRoot == null)
                mainCharacterRoot = this.transform;

            var excludedClothingRoots = new HashSet<Transform>();
            foreach (var c in clothingRoots)
            {
                if (c != null)
                    excludedClothingRoots.Add(c);
            }

            var mainBonesByName = new Dictionary<string, Transform>();
            CollectBodyBonesCanonically(mainCharacterRoot, mainBonesByName, excludedClothingRoots);

            _springBones.AddRange(this.GetComponentsInChildren<XWearSpringBone>(true));

            foreach (var clothing in clothingRoots)
            {
                if (clothing == null) continue;

                // Perfectly non-destructive auto-repair
                RepairClothingSMR(clothing);

                if (bindMode == BindMode.SmartRetargeting || bindMode == BindMode.WorldPoseFollower)
                {
                    var rootGO = clothing.Find("RootGameObject");
                    if (rootGO == null) rootGO = clothing.Find("SkeletonRoot");
                    if (rootGO != null) rootGO.gameObject.SetActive(true);

                    var clothBones = new Dictionary<string, Transform>();
                    CollectBonesCanonically(clothing, clothBones);

                    foreach (var kvp in clothBones)
                    {
                        var clothBone = kvp.Value;
                        if (mainBonesByName.TryGetValue(kvp.Key, out var bodyBone))
                        {
                            if (bodyBone != mainCharacterRoot && clothBone != clothing)
                            {
                                bool rootHips = kvp.Key.Contains("hips");

                                _links.Add(new BoneLink
                                {
                                    bodyBone           = bodyBone,
                                    clothBone          = clothBone,
                                    bodyStartLocalRot  = bodyBone.localRotation,
                                    clothStartLocalRot = clothBone.localRotation,
                                    isRootHips         = rootHips
                                });
                            }
                        }
                    }

                    _springBones.AddRange(clothing.GetComponentsInChildren<XWearSpringBone>(true));
                }
                else if (bindMode == BindMode.DirectSMRLink)
                {
                    if (alignClothingRootToCharacterInDirectSMR && mainCharacterRoot != null)
                    {
                        clothing.position = mainCharacterRoot.position;
                        if (alignClothingRootRotationToCharacterInDirectSMR)
                        {
                            clothing.rotation = mainCharacterRoot.rotation;
                        }
                    }

                    var smrs = clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var smr in smrs)
                    {
                        var curBones = smr.bones;
                        var newBones = new Transform[curBones.Length];

                        for (int b = 0; b < curBones.Length; b++)
                        {
                            var oldBone = curBones[b];
                            if (oldBone != null)
                            {
                                string canon = GetCanonicalBoneName(oldBone.name);
                                if (mainBonesByName.TryGetValue(canon, out var mainBone))
                                {
                                    newBones[b] = mainBone;
                                    continue;
                                }
                            }
                            newBones[b] = oldBone;
                        }

                        Transform newRootBone = smr.rootBone;
                        if (smr.rootBone != null)
                        {
                            string rootCanon = GetCanonicalBoneName(smr.rootBone.name);
                            if (mainBonesByName.TryGetValue(rootCanon, out var newRoot))
                            {
                                newRootBone = newRoot;
                            }
                        }

                        // IMPORTANT:
                        // DirectSMRLink changes the bones that drive the mesh. The imported
                        // .xwear bindposes were authored against the clothing's own skeleton
                        // (usually facing Unity +Z / Y=0). A Mixamo-returned body can have a
                        // different root transform, commonly Y=180 (and sometimes scale 100).
                        // If we only replace smr.bones and keep the old bindposes, Unity skins
                        // vertices with: bodyBone * oldClothingBindpose, so the whole garment
                        // gets the body's root delta baked in and appears rotated/misaligned.
                        // Rebuilding bindposes once against the target body makes the current
                        // rest pose the shared bind pose for body and clothing. This has no
                        // per-frame CPU cost; it only creates a per-instance mesh copy.
                        if (rebuildBindposesForDirectSMR && smr.sharedMesh != null)
                        {
                            RebuildDirectSMRBindposes(smr, curBones, newBones);
                        }

                        smr.bones = newBones;
                        smr.rootBone = newRootBone;
                    }

                    var clothingSprings = clothing.GetComponentsInChildren<XWearSpringBone>(true);
                    foreach (var cs in clothingSprings)
                    {
                        if (cs.transform.parent != null)
                        {
                            string parentCanon = GetCanonicalBoneName(cs.transform.parent.name);
                            if (mainBonesByName.TryGetValue(parentCanon, out var newParent))
                            {
                                cs.transform.SetParent(newParent, true);
                            }
                        }
                        _springBones.Add(cs);
                    }

                    var rootGO = clothing.Find("RootGameObject");
                    if (rootGO == null) rootGO = clothing.Find("SkeletonRoot");
                    if (rootGO != null) rootGO.gameObject.SetActive(false);
                }
            }

            _mainCam = Camera.main;
            _isInitialized = true;
        }

        static void RebuildDirectSMRBindposes(SkinnedMeshRenderer smr, Transform[] oldBones, Transform[] targetBones)
        {
            Mesh sourceMesh = smr.sharedMesh;
            if (sourceMesh == null || targetBones == null || targetBones.Length == 0)
                return;

            Mesh reboundMesh = UnityEngine.Object.Instantiate(sourceMesh);
            reboundMesh.name = sourceMesh.name + "_DirectSMRBound";

            var newBindposes = new Matrix4x4[targetBones.Length];
            var oldBindposes = sourceMesh.bindposes;
            Matrix4x4 rendererLocalToWorld = smr.transform.localToWorldMatrix;

            for (int i = 0; i < targetBones.Length; i++)
            {
                Transform targetBone = targetBones[i];
                Transform oldBone = oldBones != null && i < oldBones.Length ? oldBones[i] : null;
                Matrix4x4 oldBindpose = oldBindposes != null && i < oldBindposes.Length
                    ? oldBindposes[i]
                    : Matrix4x4.identity;

                if (targetBone == null)
                {
                    newBindposes[i] = oldBindpose;
                    continue;
                }

                if (oldBone != null)
                {
                    // Preserve the current clothing skinning transform while swapping the
                    // driver bone. Unity skins with: bone.localToWorldMatrix * bindpose.
                    // We need:
                    //     targetBone * newBindpose == oldBone * oldBindpose
                    // Therefore:
                    //     newBindpose = inverse(targetBone) * oldBone * oldBindpose
                    // This is more correct than the generic bindpose formula when the
                    // imported XWear skeleton has its own root rotation, scale, or axis
                    // conversion, and it is exactly the case that caused visual 0/180
                    // mismatches while the Inspector Transform looked correct.
                    newBindposes[i] = targetBone.worldToLocalMatrix * oldBone.localToWorldMatrix * oldBindpose;
                }
                else
                {
                    // Fallback for a missing old bone: sample a standard bindpose from the
                    // current renderer transform.
                    newBindposes[i] = targetBone.worldToLocalMatrix * rendererLocalToWorld;
                }
            }

            reboundMesh.bindposes = newBindposes;
            smr.sharedMesh = reboundMesh;
        }

        void RepairClothingSMR(Transform clothing)
        {
            if (clothing == null) return;

            var rootGO = clothing.Find("RootGameObject");
            if (rootGO == null) rootGO = clothing.Find("SkeletonRoot");
            if (rootGO == null) return;

            rootGO.gameObject.SetActive(true);

            var clothBonesByName = new Dictionary<string, Transform>();
            CollectBonesCanonically(rootGO, clothBonesByName);

            var smrs = clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                var curBones = smr.bones;
                var restoredBones = new Transform[curBones.Length];

                for (int b = 0; b < curBones.Length; b++)
                {
                    var cb = curBones[b];
                    if (cb != null)
                    {
                        string canon = GetCanonicalBoneName(cb.name);
                        if (clothBonesByName.TryGetValue(canon, out var nativeBone))
                        {
                            restoredBones[b] = nativeBone;
                            continue;
                        }
                    }
                    restoredBones[b] = cb;
                }

                smr.bones = restoredBones;

                if (smr.rootBone != null)
                {
                    string rootCanon = GetCanonicalBoneName(smr.rootBone.name);
                    if (clothBonesByName.TryGetValue(rootCanon, out var nativeRoot))
                    {
                        smr.rootBone = nativeRoot;
                    }
                }
            }
        }

        static void CollectBonesCanonically(Transform current, Dictionary<string, Transform> dict)
        {
            if (current == null) return;
            
            string canon = GetCanonicalBoneName(current.name);
            if (!string.IsNullOrEmpty(canon) && !dict.ContainsKey(canon))
            {
                dict[canon] = current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectBonesCanonically(current.GetChild(i), dict);
            }
        }

        static void CollectBodyBonesCanonically(Transform current, Dictionary<string, Transform> dict, HashSet<Transform> excludedRoots)
        {
            if (current == null) return;
            if (excludedRoots != null && excludedRoots.Contains(current)) return;
            
            string canon = GetCanonicalBoneName(current.name);
            if (!string.IsNullOrEmpty(canon) && !dict.ContainsKey(canon))
            {
                dict[canon] = current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectBodyBonesCanonically(current.GetChild(i), dict, excludedRoots);
            }
        }

        static string GetCanonicalBoneName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";

            string n = rawName.ToLowerInvariant();

            n = n.Replace("mixamorig:", "")
                 .Replace("j_bip_c_", "")
                 .Replace("j_bip_l_", "l_")
                 .Replace("j_bip_r_", "r_")
                 .Replace("j_bip_", "")
                 .Replace("character_", "");

            bool isLeft  = n.StartsWith("l_") || n.EndsWith("_l") || n.Contains("left");
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

        void LateUpdate()
        {
            if (!Application.isPlaying || !_isInitialized) return;

            if (bindMode == BindMode.SmartRetargeting)
            {
                // Simple bulletproof non-destructive delta pose synchronization
                for (int i = 0; i < _links.Count; i++)
                {
                    var link = _links[i];
                    if (link.bodyBone == null || link.clothBone == null) continue;

                    if (link.isRootHips)
                    {
                        link.clothBone.position = link.bodyBone.position;
                    }

                    Quaternion deltaRot = link.bodyBone.localRotation * Quaternion.Inverse(link.bodyStartLocalRot);
                    link.clothBone.localRotation = deltaRot * link.clothStartLocalRot;
                }
            }
            else if (bindMode == BindMode.WorldPoseFollower)
            {
                for (int i = 0; i < _links.Count; i++)
                {
                    var link = _links[i];
                    if (link.bodyBone != null && link.clothBone != null)
                    {
                        link.clothBone.position = link.bodyBone.position;
                        link.clothBone.rotation = link.bodyBone.rotation;
                    }
                }
            }

            // Crowd LOD
            if (optimizeFarPhysics && Time.frameCount % 30 == 0 && _mainCam != null)
            {
                float distSqr = (this.transform.position - _mainCam.transform.position).sqrMagnitude;
                bool far = distSqr > (farDistanceThreshold * farDistanceThreshold);

                for (int s = 0; s < _springBones.Count; s++)
                {
                    var sb = _springBones[s];
                    if (sb != null)
                    {
                        sb.useCollisions = !far;
                    }
                }
            }
        }
    }
}
