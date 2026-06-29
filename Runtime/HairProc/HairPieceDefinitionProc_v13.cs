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

        [Header("v1.3 – Head collision / wrap mask")]
        public HeadCollisionMaskDefinition headCollisionMask;

        [Header("v1.5 – Scalp growth profile")]
        public ScalpProfileDefinition scalpProfile;

        [Header("v1.5 – Strand shape")]
        [Tooltip("Cross-section sides for generated locks. 3-4 = anime sheet/clump, 6-8 = round locks/dreads.")]
        [Range(3, 10)] public int strandSides = 6;
        [Tooltip("Width multiplier across the lock. Use 2-4 for flat anime hair sheets.")]
        [Range(0.25f, 5f)] public float strandWidthScale = 1f;
        [Tooltip("Depth multiplier through the lock. Use 0.2-0.6 for flat anime hair sheets.")]
        [Range(0.1f, 3f)] public float strandDepthScale = 1f;

        [Header("v1.4 – Strand self separation")]
        [Tooltip("Approximate collision between procedural locks. This keeps neighboring tubes from sharing the same centerline.")]
        public bool enableStrandSeparation = true;
        [Tooltip("Minimum centerline distance between locks in meters. Increase if locks still intersect; decrease if hair explodes outward.")]
        [Range(0.004f, 0.06f)] public float strandSeparationRadius = 0.024f;
        [Tooltip("How strongly a lock is pushed away from nearby locks. 0 disables, 1 is strong.")]
        [Range(0f, 1f)] public float strandSeparationStrength = 0.55f;

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

        // Backward-compatible alias: older comments/code used localPos.
        public Vector3 localPos { get => localOffset; set => localOffset = value; }
    }

    [System.Serializable]
    public struct PinConstraint
    {
        public int guideIndex;
        public uint targetBoneHash;
        public byte t255;
        public sbyte offsetX_cm;
        public sbyte offsetY_cm;
        public sbyte offsetZ_cm;
        public byte tailSlack255;
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
