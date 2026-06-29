using System.Collections.Generic;
using UnityEngine;
using CharacterEditor.Hair;

namespace CharacterEditor.Hair.Proc
{
    [CreateAssetMenu(menuName = "Character/Hair Piece Procedural", fileName = "New_HairProc")]
    public partial class HairPieceDefinitionProc : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public HairSlot slot = HairSlot.Back;

        [Header("Source Guides – authored in VRoid style")]
        public HairGuide[] guides = System.Array.Empty<HairGuide>();

        [Header("Default DNA – used when player hasn't customized")]
        [Range(0.5f, 1.8f)] public float defaultLength = 1f;
        [Range(0.3f, 2f)]   public float defaultDensity = 1f;
        [Range(0.5f, 1.5f)] public float defaultThickness = 1f;
        [Range(0f, 1f)]     public float defaultCurl = 0f;
        [Range(0f, 1f)]     public float defaultWave = 0f;
        [Range(0f, 1f)]     public float defaultFrizz = 0f;

        public HairColorGradient defaultColor = new HairColorGradient
        {
            rootColor = new Color(0.15f,0.1f,0.08f,1),
            tipColor = new Color(0.25f,0.18f,0.12f,1),
            rootFade = 0.3f,
            highlightColor = new Color(0.5f,0.35f,0.25f,1),
            highlightStrength = 0.15f
        };

        [Header("Per-Group defaults (0=Bangs,1=SideL,2=SideR,3=Back,4=Ahoge ...)")]
        public float[] groupLengthScale = new float[]{1,1,1,1,1,1,1,1};

        [Header("Rendering – HDRP")]
        public Material hairMaterial; // if null → auto find HDRP/Hair
        public bool twoSided = true;
        public bool useAnisotropic = true;

        [Header("Performance Budgets – Unity 6")]
        [Tooltip("Vertices per strand at LOD0")]
        [Range(8,32)] public int segmentsLOD0 = 16;
        [Range(4,16)] public int segmentsLOD1 = 8;
        [Range(3,8)]  public int segmentsLOD2 = 4;
        [Tooltip("Max total vertices for 1 character, safety cap")]
        public int maxVertices = 90000; // ~80k = safe for 20 chars on RTX3060

        [Header("Fallback – classic VRM mesh")]
        public HairPieceDefinition legacyVrmPiece; // if procedural fails

        // quick helpers
        public int GuideCount => guides != null ? guides.Length : 0;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (string.IsNullOrEmpty(displayName)) displayName = name;
            if (groupLengthScale == null || groupLengthScale.Length < 8)
                groupLengthScale = new float[8] {1,1,1,1,1,1,1,1};
        }

#if UNITY_EDITOR
        // Helper to convert existing VRM HairPieceDefinition → procedural guides (very rough: sample skinned mesh root bones)
        [ContextMenu("Auto-Extract Guides From Legacy VRM")]
        void ExtractGuidesFromLegacy()
        {
            if (legacyVrmPiece == null || legacyVrmPiece.prefab == null) { Debug.LogWarning("Set legacyVrmPiece first"); return; }
            var inst = Instantiate(legacyVrmPiece.prefab);
            try {
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var list = new List<HairGuide>();
                int idx=0;
                foreach (var smr in smrs)
                {
                    if (smr.sharedMesh == null) continue;
                    var verts = smr.sharedMesh.vertices;
                    // naive: take root-most vertices cluster
                    for (int g=0; g< Mathf.Min(40, verts.Length/150); g++)
                    {
                        Vector3 rootPos = verts[(g*137)%verts.Length] * 0.01f; // crude
                        var hg = HairGuide.CreateDefault("c_head", rootPos + Vector3.up * 1.55f, 0.22f);
                        hg.groupId = idx % 4;
                        list.Add(hg);
                        idx++;
                        if (list.Count >= 120) break;
                    }
                    if (list.Count >= 120) break;
                }
                guides = list.ToArray();
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[HairProc] Extracted {guides.Length} guides from legacy {legacyVrmPiece.id}", this);
            } finally {
                DestroyImmediate(inst);
            }
        }
#endif
    }
}
