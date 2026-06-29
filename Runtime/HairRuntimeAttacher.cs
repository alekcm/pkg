using System.Collections.Generic;
using UnityEngine;

namespace CharacterEditor.Hair
{
    /// <summary>
    /// Unity 6 FIXED – VRM hair runtime binder
    /// - Remaps bones by canonical name
    /// - REBUILDS bindposes (fixes Mixamo scale 100)
    /// - Forces rootBone = Head
    /// - Forces stable localBounds + updateWhenOffscreen
    /// </summary>
    public class HairRuntimeAttacher : MonoBehaviour
    {
        public Transform avatarRoot;
        public Transform fallbackParent;

        [Header("Unity 6 Fix")]
        public bool rebuildBindposes = true;
        public bool forceUpdateWhenOffscreen = true;
        public bool useLargeStableBounds = true;
        public Vector3 stableBoundsCenter = new Vector3(0f, 0.15f, 0f);
        public Vector3 stableBoundsExtents = new Vector3(0.3f, 0.3f, 0.3f);

        private readonly Dictionary<string, Transform> _boneMap = new Dictionary<string, Transform>();

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
            if (parent == null) parent = this.transform;
            GameObject instance = UnityEngine.Object.Instantiate(piece.prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            RemapSkinnedMeshes(instance);
            return instance;
        }

        public void RemapSkinnedMeshes(GameObject hairInstance)
        {
            if (hairInstance == null) return;
            if (_boneMap.Count == 0) RebuildBoneMap();

            // Try to pre-find head / neck bones for forced root
            Transform headBone = null;
            _boneMap.TryGetValue("c_head", out headBone);
            if (headBone == null) _boneMap.TryGetValue("head", out headBone);
            Transform neckBone = null;
            _boneMap.TryGetValue("c_neck", out neckBone);
            if (neckBone == null) _boneMap.TryGetValue("neck", out neckBone);

            foreach (var smr in hairInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;

                // --- CLONE MESH: never modify a shared asset in Play Mode ---
                Mesh mesh = smr.sharedMesh;
                bool needClone = false;
#if UNITY_EDITOR
                if (Application.isPlaying) needClone = UnityEditor.AssetDatabase.Contains(mesh);
#endif
#if !UNITY_EDITOR
                needClone = true;
#endif
                if (needClone)
                {
                    var clone = UnityEngine.Object.Instantiate(mesh);
                    clone.name = mesh.name + "_RuntimeBound";
                    clone.hideFlags = HideFlags.DontSave;
                    smr.sharedMesh = clone;
                    mesh = clone;
                }

                Transform[] sourceBones = smr.bones;
                if (sourceBones == null || sourceBones.Length == 0) continue;

                Transform[] mappedBones = new Transform[sourceBones.Length];
                bool anyMapped = false;
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    Transform src = sourceBones[i];
                    Transform tgt = Resolve(src);
                    if (tgt != null) { mappedBones[i] = tgt; anyMapped = true; }
                    else { mappedBones[i] = src; }
                }
                if (!anyMapped) continue;

                // Bindpose rebuild – critical for Mixamo scale 100
                if (rebuildBindposes)
                {
                    try { RebuildBindposes(smr, sourceBones, mappedBones, mesh); }
                    catch (System.Exception ex) { Debug.LogWarning("[HairRuntimeAttacher] bindpose failed: " + ex.Message, smr); }
                }

                smr.bones = mappedBones;

                // --- ROOT BONE: force Head > Neck > first mapped ---
                Transform newRoot = null;
                // 1) try resolve original root
                newRoot = Resolve(smr.rootBone);
                // 2) force head if available
                if (headBone != null && System.Array.IndexOf(mappedBones, headBone) >= 0)
                    newRoot = headBone;
                else if (neckBone != null && System.Array.IndexOf(mappedBones, neckBone) >= 0)
                    newRoot = neckBone;
                // 3) validate is in bones[]
                if (newRoot == null || System.Array.IndexOf(mappedBones, newRoot) < 0)
                {
                    // find first head/neck/hips in mapped list
                    foreach (var b in mappedBones)
                    {
                        if (b == null) continue;
                        string n = b.name.ToLowerInvariant();
                        if (n.Contains("head") || n.Contains("neck")) { newRoot = b; break; }
                    }
                    if (newRoot == null)
                        foreach (var b in mappedBones) if (b != null) { newRoot = b; break; }
                }
                if (newRoot != null) smr.rootBone = newRoot;

                // --- BOUNDS FIX Unity 6 ---
                try { mesh.RecalculateBounds(); } catch {}
                if (useLargeStableBounds)
                    smr.localBounds = new Bounds(stableBoundsCenter, stableBoundsExtents * 2f);
                else
                    smr.localBounds = mesh.bounds;

                if (forceUpdateWhenOffscreen) smr.updateWhenOffscreen = true;
                smr.quality = SkinQuality.Bone4;
                smr.skinnedMotionVectors = true;
                smr.allowOcclusionWhenDynamic = false;
                smr.forceMatrixRecalculationPerRender = true;

                // Force SMR transform to identity relative to avatar head – prevents 100x offset
                // Do NOT move the SMR transform itself if it would break skinning – only ensure scale 1
                smr.transform.localScale = Vector3.one;
            }
        }

        static void RebuildBindposes(SkinnedMeshRenderer smr, Transform[] oldBones, Transform[] newBones, Mesh mesh)
        {
            if (mesh == null || newBones == null) return;
            var oldBindposes = mesh.bindposes;
            if (oldBindposes == null || oldBindposes.Length != newBones.Length)
            {
                oldBindposes = new Matrix4x4[newBones.Length];
                for (int i = 0; i < oldBindposes.Length; i++) oldBindposes[i] = Matrix4x4.identity;
            }
            var newBindposes = new Matrix4x4[newBones.Length];
            Matrix4x4 rendererWorld = smr.transform.localToWorldMatrix;
            for (int i = 0; i < newBones.Length; i++)
            {
                Transform nb = newBones[i];
                Transform ob = i < oldBones.Length ? oldBones[i] : null;
                Matrix4x4 obp = i < oldBindposes.Length ? oldBindposes[i] : Matrix4x4.identity;
                if (nb == null) { newBindposes[i] = obp; continue; }
                if (ob != null)
                    newBindposes[i] = nb.worldToLocalMatrix * ob.localToWorldMatrix * obp;
                else
                    newBindposes[i] = nb.worldToLocalMatrix * rendererWorld;
            }
            mesh.bindposes = newBindposes;
            mesh.RecalculateBounds();
        }

        private Transform Resolve(Transform source)
        {
            if (source == null) return null;
            if (_boneMap.TryGetValue(source.name, out var exact)) return exact;
            string canon = GetCanonicalBoneName(source.name);
            if (!string.IsNullOrEmpty(canon) && _boneMap.TryGetValue(canon, out var c)) return c;
            return null;
        }
        private void AddBoneKey(string key, Transform t)
        {
            if (!string.IsNullOrEmpty(key) && !_boneMap.ContainsKey(key)) _boneMap.Add(key, t);
        }
        public static string GetCanonicalBoneName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";
            string n = rawName.ToLowerInvariant();
            n = n.Replace("mixamorig:", "").Replace("j_bip_c_", "").Replace("j_bip_l_", "l_").Replace("j_bip_r_", "r_").Replace("j_bip_", "").Replace("character_", "").Replace("vrm_", "").Replace("_end", "");
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
            else if (n.Contains("upleg") || n.Contains("upperleg") || n.Contains("thigh")) role = "upperleg";
            else if (n.Contains("lowerleg") || n.Contains("calf") || (n.Contains("leg") && !n.Contains("upleg"))) role = "lowerleg";
            else if (n.Contains("foot")) role = "foot";
            else if (n.Contains("toebase") || n.Contains("toes") || n.Contains("toe")) role = "toes";
            if (string.IsNullOrEmpty(role)) return n;
            string side = isLeft ? "l_" : (isRight ? "r_" : "c_");
            return side + role;
        }
    }
}
