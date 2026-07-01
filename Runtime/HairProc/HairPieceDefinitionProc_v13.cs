// HairPieceDefinitionProc_v13_FIX.cs - Исправленные умолчания для аниме-волос
// Замените ваш HairPieceDefinitionProc_v13.cs этим файлом (или добавьте рядом)
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    // Исправленная версия — аниме-умолчания вместо круглых трубок
    public partial class HairPieceDefinitionProc
    {
        [Header("v1.3 – Braids / Ponytail / Dreads")]
        public BraidProfile defaultBraid = new BraidProfile
        {
            type = BraidType.None,
            crossings = 9,
            strandRadius = 0.008f,
            tightness = 0.85f,
            taperEnd = true
        };

        [Header("v1.3 – Scalp / Shave")]
        public ScalpMaskDefinition scalpMask;

        [Header("v1.3 – Head collision / wrap mask")]
        public HeadCollisionMaskDefinition headCollisionMask;

        [Header("v1.5 – Scalp growth profile")]
        public ScalpProfileDefinition scalpProfile;

        [Header("v1.5 – Strand shape (ANIME style defaults)")]
        [Tooltip("Cross-section sides. 3-4 = anime sheet/clump, 6-8 = round locks/dreads.")]
        [Range(3, 10)] public int strandSides = 4; // БЫЛО 6 → СТАЛО 4 (аниме-пряди)

        [Tooltip("Width multiplier. 2-4 for flat anime hair sheets.")]
        [Range(0.25f, 5f)] public float strandWidthScale = 2.5f; // БЫЛО 1 → СТАЛО 2.5

        [Tooltip("Depth multiplier. 0.2-0.6 for flat anime hair.")]
        [Range(0.1f, 3f)] public float strandDepthScale = 0.4f; // БЫЛО 1 → СТАЛО 0.4

        [Header("v1.4 – Strand self separation")]
        [Tooltip("Keeps neighboring tubes from sharing the same centerline.")]
        public bool enableStrandSeparation = true;

        [Tooltip("Minimum centerline distance between locks.")]
        [Range(0.004f, 0.06f)] public float strandSeparationRadius = 0.024f;

        [Tooltip("How strongly a lock is pushed away from nearby locks.")]
        [Range(0f, 1f)] public float strandSeparationStrength = 0.55f;

        [Header("v1.3 – Bundle / Ponytail anchors")]
        public BundleAnchor[] bundleAnchors;
    }

    [System.Serializable]
    public struct BundleAnchor
    {
        public string id;
        public string attachBone;
        public Vector3 localOffset;
        [Range(0.005f, 0.06f)] public float gatherRadius;
        [Range(0f, 1f)] public float pinT;
        [Range(0f, 1f)] public float tailSlack;

        public Vector3 localPos { get => localOffset; set => localOffset = value; }
    }

    [System.Serializable]
    public struct PinConstraint
    {
        public int guideIndex;
        public uint targetBoneHash;
        public byte t255;
        public sbyte offsetX_cm, offsetY_cm, offsetZ_cm;
        public byte tailSlack255;
    }

    [System.Serializable]
    public struct HairDnaV13
    {
        public HairDna baseDna;
        public BraidType braidOverride;
        public byte braidTightness255;

        public static HairDnaV13 FromBase(HairDna d) =>
            new HairDnaV13 { baseDna = d, braidOverride = BraidType.None };
    }
}