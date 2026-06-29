// HairBraidGenerator.cs – Unity 6 / Burst / HDRP
// 3-strand plait + 2-strand twist – Wenona style
// Works on top of HairBaker – replaces strand positions post-Catmull
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    public enum BraidType : byte { None=0, ThreeStrand=1, TwoStrandTwist=2, Fishtail=3, Dread=4 }

    [System.Serializable]
    public struct BraidProfile
    {
        public BraidType type;
        [Range(3,18)] public int crossings; // сколько перекрестий на всю длину – Wenona ~9
        [Range(0.003f,0.025f)] public float strandRadius; // толщина каждой пряди косы
        [Range(0f,1f)] public float tightness; // 1 = туго, 0 = распушено
        public bool taperEnd; // сужать к концу – Grace-style нет, Wenona – да
    }

    public static class HairBraidGenerator
    {
        // Called from HairBaker after base Catmull sample, before ribbon extrude.
        // Modifies strandCenter in place.
        // left/right/up – orthonormal basis at current t
        public static float3 ApplyBraid(float3 basePos, float t, float3 tangent, float3 bitangent, BraidProfile p, uint seed, int subStrandIndex)
        {
            if (p.type == BraidType.None) return basePos;
            if (p.type == BraidType.Dreadlock)
            {
                // Desmond Hall – tube noise already in HairBaker frizz, just keep
                return basePos;
            }

            float crossings = math.max(3f, p.crossings);
            float phase = t * crossings * math.PI * 2f;

            // 3-strand classic – 120° offset per sub-strand
            if (p.type == BraidType.ThreeStrand)
            {
                float angleOffset = subStrandIndex * math.PI * 2f / 3f;
                float r = p.strandRadius * math.lerp(1f, 0.45f, p.tightness);
                // taper end (Wenona)
                if (p.taperEnd) r *= math.smoothstep(0f, 1f, 1f - t * 0.9f);
                float s = math.sin(phase + angleOffset);
                float c = math.cos(phase + angleOffset * 0.5f) * 0.35f; // slight forward/back
                // bitangent = left/right, tangent cross = up-ish
                float3 n = math.normalize(math.cross(tangent, bitangent));
                return basePos + bitangent * s * r + n * c * r * 0.6f;
            }

            // 2-strand twist
            if (p.type == BraidType.TwoStrandTwist)
            {
                float angle = phase + subStrandIndex * math.PI;
                float r = p.strandRadius * 0.9f;
                return basePos + bitangent * math.cos(angle) * r + math.cross(tangent, bitangent) * math.sin(angle) * r;
            }

            // fishtail – fast alternating
            if (p.type == BraidType.Fishtail)
            {
                float s = math.sin(phase * 2.1f + subStrandIndex);
                return basePos + bitangent * s * p.strandRadius * 0.7f;
            }

            return basePos;
        }

        // Bundle N guides into 1 braid trunk – ponytail / Desmond low gather
        // inputGuidePositions: float3[guideCount * samples]
        // returns center line (average) – call once per bundle
        public static void ComputeBundleCenter(
            Unity.Collections.NativeArray<float3> guidePos, int guideCount, int samples,
            Unity.Collections.NativeArray<float3> outCenter)
        {
            for (int s = 0; s < samples; s++)
            {
                float3 acc = float3.zero;
                int cnt = 0;
                for (int g = 0; g < guideCount; g++)
                {
                    int idx = g * samples + s;
                    if (idx < guidePos.Length) { acc += guidePos[idx]; cnt++; }
                }
                outCenter[s] = cnt > 0 ? acc / cnt : float3.zero;
            }
        }
    }
}
