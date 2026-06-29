// HairBaker.cs – Unity 6 / Burst / HDRP
// Procedural strand → mesh
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace CharacterEditor.Hair.Proc
{
    public static class HairBaker
    {
        const int MAX_STRANDS = 256;
        const int MAX_SEG = 32;

        public struct BakeResult
        {
            public Mesh mesh;
            public Bounds bounds;
            public int vertexCount;
            public int ms;
        }

        public static BakeResult Bake(HairPieceDefinitionProc def, HairDna dna, int lod = 0)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (def == null || def.guides == null || def.guides.Length == 0)
                return default;

            int guideCount = def.guides.Length;
            float density = Mathf.Clamp(dna.density > 0 ? dna.density : def.defaultDensity, 0.3f, 2f);
            int useGuides = Mathf.Clamp(Mathf.RoundToInt(guideCount * density), 4, MAX_STRANDS);
            useGuides = Mathf.Min(useGuides, guideCount);

            int seg = lod switch { 1 => def.segmentsLOD1, 2 => def.segmentsLOD2, _ => def.segmentsLOD0 };
            seg = math.clamp(seg, 4, MAX_SEG);

            // Build real lock meshes, not just guide/ribbon lines.
            // Per-piece cross-section lets us support both round dreads and flat anime clumps.
            int radial = Mathf.Clamp(def.strandSides <= 0 ? 6 : def.strandSides, 3, 10);
            int vertsPerStrand = (seg + 1) * radial;
            int trisPerStrand = seg * radial * 2;
            int totalVerts = useGuides * vertsPerStrand;
            int totalIdx = trisPerStrand * 3 * useGuides;

            if (totalVerts > def.maxVertices)
            {
                // auto-downscale density to fit budget – critical for 20 players
                float scale = (float)def.maxVertices / totalVerts;
                useGuides = Mathf.Max(4, Mathf.FloorToInt(useGuides * scale));
                totalVerts = useGuides * vertsPerStrand;
                totalIdx = trisPerStrand * 3 * useGuides;
            }

            var positions = new NativeArray<float3>(totalVerts, Allocator.TempJob);
            var normals = new NativeArray<float3>(totalVerts, Allocator.TempJob);
            var uvs = new NativeArray<float2>(totalVerts, Allocator.TempJob);
            var colors = new NativeArray<Color32>(totalVerts, Allocator.TempJob);

            // pack guide data
            var guideRoots = new NativeArray<float3>(useGuides, Allocator.TempJob);
            var guideP = new NativeArray<float3>(useGuides * 8, Allocator.TempJob); // max 8 control points
            var guidePointCount = new NativeArray<int>(useGuides, Allocator.TempJob);
            var guideThick = new NativeArray<float2>(useGuides, Allocator.TempJob);
            var guideGroup = new NativeArray<int>(useGuides, Allocator.TempJob);

            for (int i = 0; i < useGuides; i++)
            {
                var g = def.guides[i % guideCount];
                guideRoots[i] = g.rootLocalPos;
                int pc = g.pointsLocal != null ? math.min(g.pointsLocal.Length, 8) : 0;
                guidePointCount[i] = pc;
                for (int p = 0; p < pc; p++) guideP[i * 8 + p] = g.pointsLocal[p];
                guideThick[i] = new float2(g.thicknessRoot, g.thicknessTip);
                guideGroup[i] = g.groupId;
            }

            // DNA uniforms
            float globalLen = dna.lengthScale > 0 ? dna.lengthScale : def.defaultLength;
            float globalThick = dna.thickness > 0 ? dna.thickness : def.defaultThickness;
            float curl = math.saturate(dna.curl > 0 ? dna.curl : def.defaultCurl);
            float wave = math.saturate(dna.wave > 0 ? dna.wave : def.defaultWave);
            float frizz = math.saturate(dna.frizz > 0 ? dna.frizz : def.defaultFrizz);

            // per-group length
            var groupScale = new NativeArray<float>(8, Allocator.TempJob);
            for (int gr = 0; gr < 8; gr++)
            {
                float gs = dna.GetGroupScale(gr);
                if (math.abs(gs - 1f) < 0.001f) gs = def.groupLengthScale != null && gr < def.groupLengthScale.Length ? def.groupLengthScale[gr] : 1f;
                groupScale[gr] = gs;
            }

            // per-guide in-game strand edits (drag/length/thickness/curl overrides)
            var guideOverrides = new NativeArray<GuideOverrideNet>(8, Allocator.TempJob);
            int overrideCount = Mathf.Clamp(dna.overrideCount, 0, 8);
            if (overrideCount > 0) guideOverrides[0] = dna.o0;
            if (overrideCount > 1) guideOverrides[1] = dna.o1;
            if (overrideCount > 2) guideOverrides[2] = dna.o2;
            if (overrideCount > 3) guideOverrides[3] = dna.o3;
            if (overrideCount > 4) guideOverrides[4] = dna.o4;
            if (overrideCount > 5) guideOverrides[5] = dna.o5;
            if (overrideCount > 6) guideOverrides[6] = dna.o6;
            if (overrideCount > 7) guideOverrides[7] = dna.o7;

            HeadCollisionMaskDefinition headMask = def.headCollisionMask;
            bool hasHeadMask = headMask != null;
            Vector3 maskCenter = hasHeadMask ? headMask.center : Vector3.zero;
            Vector3 maskRadii = hasHeadMask ? headMask.SafeRadii : Vector3.one;

            bool enableStrandSeparation = def.enableStrandSeparation && def.strandSeparationStrength > 0f;
            float strandSeparationRadius = Mathf.Max(0.001f, def.strandSeparationRadius);
            float strandSeparationStrength = Mathf.Clamp01(def.strandSeparationStrength);

            var job = new StrandBakeJob
            {
                guideCount = useGuides,
                segments = seg,
                radialSides = radial,
                positions = positions,
                normals = normals,
                uvs = uvs,
                colors32 = colors,
                guideRoots = guideRoots,
                guidePoints = guideP,
                guidePointCounts = guidePointCount,
                guideThickness = guideThick,
                guideGroup = guideGroup,
                groupScale = groupScale,
                guideOverrides = guideOverrides,
                overrideCount = overrideCount,
                lengthScale = globalLen,
                thicknessScale = globalThick,
                curlStrength = curl,
                waveStrength = wave,
                frizzStrength = frizz,
                rootColor = new float4(dna.rootColor.r / 255f, dna.rootColor.g / 255f, dna.rootColor.b / 255f, 1),
                tipColor = new float4(dna.tipColor.r / 255f, dna.tipColor.g / 255f, dna.tipColor.b / 255f, 1),
                rootFade = dna.rootFade255 / 255f,
                hasHeadMask = hasHeadMask ? 1 : 0,
                headMaskCenter = maskCenter,
                headMaskRadii = maskRadii,
                headMaskSoftness = hasHeadMask ? headMask.softness : 0f,
                headMaskAffectUntilT = hasHeadMask ? headMask.affectUntilT : 0f,
                enableStrandSeparation = enableStrandSeparation ? 1 : 0,
                strandSeparationRadius = strandSeparationRadius,
                strandSeparationStrength = strandSeparationStrength,
                strandWidthScale = Mathf.Max(0.05f, def.strandWidthScale),
                strandDepthScale = Mathf.Max(0.05f, def.strandDepthScale)
            };

            var handle = job.Schedule(useGuides, 4);
            handle.Complete();

            // build mesh on main thread
            var mesh = new Mesh { name = "HairProc_" + def.id, indexFormat = totalVerts > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            var verts = new Vector3[totalVerts];
            var norms = new Vector3[totalVerts];
            var uvArr = new Vector2[totalVerts];
            var colArr = new Color32[totalVerts];
            for (int i = 0; i < totalVerts; i++)
            {
                verts[i] = positions[i];
                norms[i] = normals[i];
                uvArr[i] = uvs[i];
                colArr[i] = colors[i];
            }
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvArr);
            mesh.SetColors(colArr);

            int[] idx = new int[totalIdx];
            int ii = 0;
            int vStride = (seg + 1) * radial;
            for (int s = 0; s < useGuides; s++)
            {
                int baseV = s * vStride;
                for (int t = 0; t < seg; t++)
                {
                    int ring0 = baseV + t * radial;
                    int ring1 = baseV + (t + 1) * radial;
                    for (int r = 0; r < radial; r++)
                    {
                        int rn = (r + 1) % radial;
                        int v00 = ring0 + r;
                        int v01 = ring0 + rn;
                        int v10 = ring1 + r;
                        int v11 = ring1 + rn;
                        idx[ii++] = v00; idx[ii++] = v10; idx[ii++] = v01;
                        idx[ii++] = v01; idx[ii++] = v10; idx[ii++] = v11;
                    }
                }
            }
            mesh.SetIndices(idx, MeshTopology.Triangles, 0, true, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);

            // dispose
            positions.Dispose(); normals.Dispose(); uvs.Dispose(); colors.Dispose();
            guideRoots.Dispose(); guideP.Dispose(); guidePointCount.Dispose(); guideThick.Dispose(); guideGroup.Dispose(); groupScale.Dispose(); guideOverrides.Dispose();

            sw.Stop();
            return new BakeResult { mesh = mesh, bounds = mesh.bounds, vertexCount = totalVerts, ms = (int)sw.ElapsedMilliseconds };
        }

        // Bake a single guide – used by in-game strand drag (0.2ms)
        public static void BakeGuideInPlace(Mesh mesh, HairPieceDefinitionProc def, HairDna dna, int guideIndex) { /* TODO: fast partial update – v1 rebakes full */ }

        [BurstCompile]
        struct StrandBakeJob : IJobParallelFor
        {
            public int guideCount;
            public int segments;
            public int radialSides;
            [ReadOnly] public NativeArray<float3> guideRoots;
            [ReadOnly] public NativeArray<float3> guidePoints;
            [ReadOnly] public NativeArray<int> guidePointCounts;
            [ReadOnly] public NativeArray<float2> guideThickness;
            [ReadOnly] public NativeArray<int> guideGroup;
            [ReadOnly] public NativeArray<float> groupScale;
            [ReadOnly] public NativeArray<GuideOverrideNet> guideOverrides;
            public int overrideCount;

            public float lengthScale;
            public float thicknessScale;
            public float curlStrength;
            public float waveStrength;
            public float frizzStrength;
            public float4 rootColor;
            public float4 tipColor;
            public float rootFade;

            public int hasHeadMask;
            public float3 headMaskCenter;
            public float3 headMaskRadii;
            public float headMaskSoftness;
            public float headMaskAffectUntilT;

            public int enableStrandSeparation;
            public float strandSeparationRadius;
            public float strandSeparationStrength;
            public float strandWidthScale;
            public float strandDepthScale;

            // Each parallel job processes one guide/strand, but one strand owns a whole
            // contiguous vertex range: guideIdx * ((segments + 1) * 2) ...
            // Unity's job safety system otherwise allows writing only array[jobIndex]
            // and throws IndexOutOfRangeException in Burst.
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> positions;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> normals;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float2> uvs;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Color32> colors32;

            public void Execute(int guideIdx)
            {
                int pointCount = guidePointCounts[guideIdx];
                if (pointCount < 2) pointCount = 2;
                float3 root = guideRoots[guideIdx];
                float2 thick = guideThickness[guideIdx] * thicknessScale;
                int grp = guideGroup[guideIdx];
                float grpScale = (grp >= 0 && grp < groupScale.Length) ? groupScale[grp] : 1f;
                float lenScale = lengthScale * grpScale;

                GuideOverrideNet ov = default;
                bool hasOverride = TryGetOverride(guideIdx, out ov);
                if (hasOverride)
                {
                    lenScale *= 1f + ov.len / 100f;
                    thick *= 1f + ov.thick / 100f;
                }

                int sides = math.max(3, radialSides);
                int vBase = guideIdx * (segments + 1) * sides;

                uint rng = (uint)(guideIdx * 1664525 + 1013904223);

                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;

                    float3 pos = SampleScaled(guideIdx, t, pointCount, root, lenScale);

                    float curlOffset = 0f;
                    if (curlStrength > 0.001f)
                    {
                        float ang = t * 25f + guideIdx * 0.73f;
                        curlOffset = math.sin(ang) * curlStrength * 0.015f * t;
                    }
                    float waveOffset = 0f;
                    if (waveStrength > 0.001f)
                    {
                        waveOffset = math.sin(t * 12f + guideIdx) * waveStrength * 0.01f;
                    }
                    float frizzX = 0f, frizzZ = 0f;
                    if (frizzStrength > 0.001f)
                    {
                        rng = rng * 1664525u + 1013904223u;
                        float r1 = ((rng & 0xFFFF) / 65535f - 0.5f);
                        rng = rng * 1664525u + 1013904223u;
                        float r2 = ((rng & 0xFFFF) / 65535f - 0.5f);
                        frizzX = r1 * frizzStrength * 0.008f * t;
                        frizzZ = r2 * frizzStrength * 0.008f * t;
                    }
                    pos.x += curlOffset + waveOffset + frizzX;
                    pos.z += frizzZ;

                    // Centerline must stay outside the head by at least the lock radius.
                    // Otherwise a thick tube/dread can still visually cut through the skull
                    // even when its center is barely outside the collision ellipsoid.
                    float th = math.max(0.0002f, math.lerp(thick.x, thick.y, t));
                    if (hasHeadMask != 0)
                        pos = PushOutsideHeadMask(pos, t, th);

                    if (enableStrandSeparation != 0)
                        pos = ApplyStrandSeparation(pos, guideIdx, t, th);

                    float tPrev = math.max(0f, t - 1f / segments);
                    float tNext = math.min(1f, t + 1f / segments);
                    float3 pPrev = SampleScaled(guideIdx, tPrev, pointCount, root, lenScale);
                    float3 pNext = SampleScaled(guideIdx, tNext, pointCount, root, lenScale);
                    float3 tangent = math.normalize(pNext - pPrev);
                    if (math.lengthsq(tangent) < 1e-6f) tangent = new float3(0, -1, 0);

                    // Build a stable cross-section basis around the strand tangent.
                    float3 refAxis = math.abs(tangent.y) > 0.85f ? new float3(1, 0, 0) : new float3(0, 1, 0);
                    float3 right = math.normalize(math.cross(refAxis, tangent));
                    if (math.lengthsq(right) < 1e-6f) right = new float3(1, 0, 0);
                    float3 up = math.normalize(math.cross(tangent, right));

                    int ringBase = vBase + s * sides;

                    float g = math.smoothstep(rootFade, 1f, t);
                    float4 col = math.lerp(rootColor, tipColor, g);
                    byte cr = (byte)math.clamp(col.x * 255f, 0, 255);
                    byte cg = (byte)math.clamp(col.y * 255f, 0, 255);
                    byte cb = (byte)math.clamp(col.z * 255f, 0, 255);
                    var c32 = new Color32(cr, cg, cb, 255);

                    for (int side = 0; side < sides; side++)
                    {
                        float a = (side / (float)sides) * math.PI * 2f;
                        float3 offset = right * (math.cos(a) * th * strandWidthScale) + up * (math.sin(a) * th * strandDepthScale);
                        float3 n = math.normalize(offset);
                        int v = ringBase + side;
                        positions[v] = pos + offset;
                        normals[v] = n;
                        uvs[v] = new float2(side / (float)sides, t);
                        colors32[v] = c32;
                    }
                }
            }

            float3 ApplyStrandSeparation(float3 pos, int guideIdx, float strandT, float lockRadius)
            {
                // Approximate lock-vs-lock collision. Exact strand physics is expensive,
                // but centerline separation is enough to stop most visual intersections.
                // Roots stay mostly fixed on scalp; lower parts separate more.
                float tWeight = math.smoothstep(0.08f, 0.85f, strandT);
                if (tWeight <= 0.0001f)
                    return pos;

                float minDist = math.max(strandSeparationRadius, lockRadius * 2.2f);
                float minDistSq = minDist * minDist;
                float3 push = float3.zero;
                int hits = 0;

                for (int other = 0; other < guideCount; other++)
                {
                    if (other == guideIdx)
                        continue;

                    int otherPc = guidePointCounts[other];
                    if (otherPc < 2) otherPc = 2;

                    float otherGroupScale = 1f;
                    int otherGrp = guideGroup[other];
                    if (otherGrp >= 0 && otherGrp < groupScale.Length)
                        otherGroupScale = groupScale[otherGrp];

                    float3 otherRoot = guideRoots[other];
                    float3 otherPos = SampleScaled(other, strandT, otherPc, otherRoot, lengthScale * otherGroupScale);

                    // Separation in XZ plane is more stable for hanging hair. Keep Y untouched
                    // so locks do not climb upward/downward when avoiding each other.
                    float3 d = new float3(pos.x - otherPos.x, 0f, pos.z - otherPos.z);
                    float distSq = math.lengthsq(d);
                    if (distSq < 0.0000001f || distSq >= minDistSq)
                        continue;

                    float dist = math.sqrt(distSq);
                    float amount = (minDist - dist) / minDist;
                    push += (d / dist) * amount;
                    hits++;
                }

                if (hits == 0)
                    return pos;

                push /= hits;
                float strength = strandSeparationStrength * tWeight;
                return pos + push * minDist * strength;
            }

            float3 PushOutsideHeadMask(float3 pos, float strandT, float lockRadius)
            {
                if (strandT > headMaskAffectUntilT)
                    return pos;

                // Inflate the collision ellipsoid by the visible lock radius so the
                // surface of the tube does not intersect the head.
                float extra = math.max(0f, lockRadius);
                float3 radii = math.max(headMaskRadii + new float3(extra, extra, extra), new float3(0.001f, 0.001f, 0.001f));
                float3 d = pos - headMaskCenter;
                float3 q = d / radii;
                float len = math.length(q);
                if (len <= 0.00001f)
                    return pos;

                // Push points that are inside the head to the ellipsoid surface.
                // Also push points that are barely above the surface a little outward, because
                // the tube radius itself can still intersect the skin.
                if (len >= 1.03f)
                    return pos;

                float3 surface = headMaskCenter + (q / len) * radii;
                float insideAmount = math.saturate(1.03f - len);
                float blend = math.lerp(1f, insideAmount, math.saturate(headMaskSoftness));
                return math.lerp(pos, surface, blend);
            }

            bool TryGetOverride(int guideIdx, out GuideOverrideNet ov)
            {
                int count = math.min(overrideCount, guideOverrides.Length);
                for (int i = 0; i < count; i++)
                {
                    GuideOverrideNet candidate = guideOverrides[i];
                    if (candidate.guideIndex == guideIdx)
                    {
                        ov = candidate;
                        return true;
                    }
                }
                ov = default;
                return false;
            }

            float3 OverrideDelta(GuideOverrideNet ov, int pointIdx)
            {
                // Mask convention: bit 1 => point 1/dx1, bit 2 => point 2/dx2,
                // bit 4 => point 3/dx3. Deltas are quantized centimeters.
                if (pointIdx == 1 && (ov.mask & 1) != 0)
                    return new float3(ov.dx1 / 100f, ov.dy1 / 100f, ov.dz1 / 100f);
                if (pointIdx == 2 && (ov.mask & 2) != 0)
                    return new float3(ov.dx2 / 100f, ov.dy2 / 100f, ov.dz2 / 100f);
                if (pointIdx == 3 && (ov.mask & 4) != 0)
                    return new float3(ov.dx3 / 100f, ov.dy3 / 100f, ov.dz3 / 100f);
                return float3.zero;
            }

            float3 SampleScaled(int guideIdx, float t, int pointCount, float3 root, float lenScale)
            {
                float3 p = SampleCatmull(guideIdx, t, pointCount);
                return root + (p - root) * lenScale;
            }

            float3 SampleCatmull(int guideIdx, float t, int pointCount)
            {
                // map t 0..1 to Catmull segment
                if (pointCount <= 1) return guideRoots[guideIdx];
                float ft = t * (pointCount - 1);
                int i = (int)math.floor(ft);
                float lt = ft - i;
                // fetch 4 points with clamp
                float3 p0 = GetGuidePoint(guideIdx, math.clamp(i - 1, 0, pointCount - 1));
                float3 p1 = GetGuidePoint(guideIdx, math.clamp(i, 0, pointCount - 1));
                float3 p2 = GetGuidePoint(guideIdx, math.clamp(i + 1, 0, pointCount - 1));
                float3 p3 = GetGuidePoint(guideIdx, math.clamp(i + 2, 0, pointCount - 1));
                // Catmull-Rom 0.5
                float lt2 = lt * lt;
                float lt3 = lt2 * lt;
                float3 a = 2f * p1;
                float3 b = p2 - p0;
                float3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
                float3 d = -p0 + 3f * p1 - 3f * p2 + p3;
                return 0.5f * (a + b * lt + c * lt2 + d * lt3);
            }
            float3 GetGuidePoint(int guideIdx, int pointIdx)
            {
                int baseIdx = guideIdx * 8 + pointIdx;
                if (pointIdx == 0) return guideRoots[guideIdx]; // root override – ensures exact scalp attach

                float3 p = guideRoots[guideIdx] + guidePoints[baseIdx];
                if (TryGetOverride(guideIdx, out GuideOverrideNet ov))
                    p += OverrideDelta(ov, pointIdx);
                return p;
            }
        }
    }
}
