using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CharacterEditor.Hair.Proc
{
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class HairRuntimeAttacherProc : MonoBehaviour
    {
        [Header("Avatar")]
        public Transform avatarRoot;
        public Animator animator;
        [Tooltip("Usually Head bone – where hair root lives")]
        public Transform headBone;

        [Header("Active Hair")]
        public HairPieceDefinitionProc currentPiece;
        public HairDna currentDna;

        [Header("Rendering – HDRP")]
        public Material overrideMaterial; // if null → piece.hairMaterial → HDRP/Hair
        public bool castShadows = true;

        private readonly Dictionary<string, SkinnedMeshRenderer> _activeSmr = new Dictionary<string, SkinnedMeshRenderer>();
        private readonly Dictionary<string, GameObject> _activeGO = new Dictionary<string, GameObject>();

        void Reset()
        {
            animator = GetComponentInChildren<Animator>(true);
            if (animator != null) avatarRoot = animator.transform;
        }

        void Awake()
        {
            if (avatarRoot == null) avatarRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (headBone == null) headBone = FindBone("Head") ?? FindBone("c_head") ?? avatarRoot;
        }

        Transform FindBone(string nameContains)
        {
            if (avatarRoot == null) return transform;
            var all = avatarRoot.GetComponentsInChildren<Transform>(true);
            string n = nameContains.ToLowerInvariant();
            foreach (var t in all) if (t.name.ToLowerInvariant().Contains(n)) return t;
            return avatarRoot;
        }

        /// <summary>Attach or update procedural hair – main API for character creator + netcode</summary>
        public SkinnedMeshRenderer ApplyHair(HairPieceDefinitionProc piece, HairDna dna, int lod = 0)
        {
            if (piece == null) { ClearSlot(""); return null; }
            currentPiece = piece;
            currentDna = dna;

            string slotKey = piece.slot.ToString();
            // remove old
            if (_activeGO.TryGetValue(slotKey, out var oldGo) && oldGo != null)
                Destroy(oldGo);

            // bake mesh
            var result = HairBaker.Bake(piece, dna, lod);
            if (result.mesh == null) return null;

            // create holder GO parented to head
            var holder = new GameObject($"HairProc_{piece.id}_{piece.slot}");
            Transform parent = headBone != null ? headBone : (avatarRoot != null ? avatarRoot : transform);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            holder.layer = gameObject.layer;

            var smr = holder.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = result.mesh;
            smr.updateWhenOffscreen = true;
            smr.skinnedMotionVectors = true;
            smr.allowOcclusionWhenDynamic = false;
            smr.forceMatrixRecalculationPerRender = true;
            smr.quality = SkinQuality.Bone4;
            smr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
#if UNITY_6000_0_OR_NEWER
            smr.forceMatrixRecalculationPerRender = true;
#endif
            // bind to head bone only – cheapest, stable
            Transform bindBone = headBone != null ? headBone : parent;
            smr.rootBone = bindBone;
            smr.bones = new Transform[] { bindBone };
            // rebuild bindpose to head
            var bindposes = new Matrix4x4[1];
            bindposes[0] = bindBone.worldToLocalMatrix * smr.transform.localToWorldMatrix;
            result.mesh.bindposes = bindposes;

            // bounds – stable large box to avoid Unity6 culling flicker
            smr.localBounds = new Bounds(new Vector3(0, 0.08f, 0), new Vector3(0.5f, 0.5f, 0.5f));

            // material – HDRP Hair
            Material mat = overrideMaterial != null ? overrideMaterial : piece.hairMaterial;
            if (mat == null)
            {
                mat = new Material(Shader.Find("HDRP/Hair") ?? Shader.Find("HDRP/Lit"));
                mat.name = "HairProc_Auto_HDRP";
                mat.EnableKeyword("_DOUBLESIDED_ON");
            }
            // apply DNA colors via MaterialPropertyBlock – allows 20 unique colors with 1 shared material (SRP batcher friendly)
            var mpb = new MaterialPropertyBlock();
            smr.GetPropertyBlock(mpb);
            Color rc = new Color(currentDna.rootColor.r / 255f, currentDna.rootColor.g / 255f, currentDna.rootColor.b / 255f);
            Color tc = new Color(currentDna.tipColor.r / 255f, currentDna.tipColor.g / 255f, currentDna.tipColor.b / 255f);
            if (mat.HasProperty("_BaseColor")) mpb.SetColor("_BaseColor", rc);
            if (mat.HasProperty("_EmissiveColor")) mpb.SetColor("_EmissiveColor", Color.black);
            // HDRP hair specific
            if (mat.HasProperty("_HairStrandColor")) mpb.SetColor("_HairStrandColor", rc);
            if (mat.HasProperty("_HairTipColor")) mpb.SetColor("_HairTipColor", tc);
            smr.SetPropertyBlock(mpb);
            smr.sharedMaterial = mat;

            _activeSmr[slotKey] = smr;
            _activeGO[slotKey] = holder;

            return smr;
        }

        public void ClearSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot))
            {
                foreach (var kv in _activeGO) if (kv.Value != null) Destroy(kv.Value);
                _activeGO.Clear(); _activeSmr.Clear();
                return;
            }
            if (_activeGO.TryGetValue(slot, out var go) && go != null) Destroy(go);
            _activeGO.Remove(slot); _activeSmr.Remove(slot);
        }

        public void UpdateDna(HairDna newDna, int lod = 0)
        {
            if (currentPiece == null) return;
            currentDna = newDna;
            ApplyHair(currentPiece, newDna, lod);
        }

        void OnDestroy()
        {
            ClearSlot("");
        }

        // simple LOD hook – call from your crowd system
        public void SetLOD(int lod)
        {
            if (currentPiece == null) return;
            ApplyHair(currentPiece, currentDna, lod);
        }
    }
}
