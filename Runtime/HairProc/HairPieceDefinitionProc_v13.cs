// HairPieceDefinitionProc – v1.3 add-on
// Adds braid / scalp mask / per-segment curves – fully backward compatible
// Put this file next to HairPieceDefinitionProc.cs – it uses partial class

using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    // Extension via partial – if your original HairPieceDefinitionProc is NOT partial,
    // use this derived class instead, OR just copy the fields below manually into HairPieceDefinitionProc.cs
    // (3 fields, 12 lines)
    public partial class HairPieceDefinitionProc
    {
        [Header("v1.3 – Braids / Ponytail / Dreads")]
        public BraidProfile defaultBraid = new BraidProfile { type = BraidType.None, crossings = 9, strandRadius = 0.008f, tightness = 0.85f, taperEnd = true };
        // Example presets:
        // Wenona braid: type=ThreeStrand, crossings=9, strandRadius=0.011, tightness=0.9, taperEnd=true
        // Grace flared: type=None, use SegmentProfile thickness curve instead
        // Desmond dreads: type=Dreadlock, crossings=0, strandRadius=0.009, tightness=0.4, taperEnd=false

        [Header("v1.3 – Scalp / Shave")]
        public ScalpMaskDefinition scalpMask; // null = full hair
        // Fuyuhiko: assign asset with useProceduralSideShave=true, leftShaveX=-0.045
        // Улисс: leftShaveX=-0.055, rightShaveX=0.055, shaveBackLower=true

        [Header("v1.3 – Bundle / Ponytail anchors")]
        public BundleAnchor[] bundleAnchors;
    }

    [System.Serializable]
    public struct BundleAnchor
    {
        public string id; // "ponytail_low", "left_braid", "right_braid"
        public string attachBone; // e.g. "c_head", "c_spine_01", "c_neck"
        public Vector3 localOffset;
        [Range(0.005f,0.06f)] public float gatherRadius;
        [Tooltip("At what normalized strand length (0..1) do strands snap to this anchor – mid-strand pin")]
        [Range(0f,1f)] public float pinT; // Diana ear tuck = 0.42, hairpin mid = 0.55
        [Range(0f,1f)] public float tailSlack; // how much length remains swinging after pin – Diana 0.45
    }

    // ---------- DNA v1.3 extension ----------
    // Add to HairDna (manual – 3 small fields) – OR use this wrapper at runtime:
    [System.Serializable]
    public struct HairDnaV13
    {
        public HairDna baseDna;
        // per-strand pin overrides – reuse GuideOverrideNet.o0..o7 (already in base)
        // + bundle / braid mode
        public BraidType braidOverride; // 0 = use piece default
        public byte braidTightness255;
        // scalp mask is NOT in DNA – it's part of piece (character race / style)
        // color gradient already in baseDna

        public static HairDnaV13 FromBase(HairDna d) => new HairDnaV13{ baseDna=d, braidOverride=BraidType.None };
    }
}
