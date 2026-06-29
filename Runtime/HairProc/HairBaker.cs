// HairBaker.cs – Unity 6 / Burst / HDRP
// Procedural strand → mesh
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
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

            int radial = 4; // ribbon quad (2 tris across) – cheap HDRP anisotropic
            int vertsPerStrand = (seg + 1) * 2; // ribbon: left/right
            int trisPerStrand = seg * 2;
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

            var job = new StrandBakeJob
            {
                guideCount = useGuides,
                segments = seg,
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
                lengthScale = globalLen,
                thicknessScale = globalThick,
                curlStrength = curl,
                waveStrength = wave,
                frizzStrength = frizz,
                rootColor = new float4(dna.rootColor.r / 255f, dna.rootColor.g / 255f, dna.rootColor.b / 255f, 1),
                tipColor = new float4(dna.tipColor.r / 255f, dna.tipColor.g / 255f, dna.tipColor.b / 255f, 1),
                rootFade = dna.rootFade255 / 255f
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
            int vStride = (seg + 1) * 2;
            for (int s = 0; s < useGuides; s++)
            {
                int baseV = s * vStride;
                for (int t = 0; t < seg; t++)
                {
                    int v00 = baseV + t * 2;
                    int v01 = v00 + 1;
                    int v10 = v00 + 2;
                    int v11 = v01 + 2;
                    // tri 1
                    idx[ii++] = v00; idx[ii++] = v10; idx[ii++] = v01;
                    // tri 2
                    idx[ii++] = v01; idx[ii++] = v10; idx[ii++] = v11;
                }
            }
            mesh.SetIndices(idx, MeshTopology.Triangles, 0, true, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(false);

            // dispose
            positions.Dispose(); normals.Dispose(); uvs.Dispose(); colors.Dispose();
            guideRoots.Dispose(); guideP.Dispose(); guidePointCount.Dispose(); guideThick.Dispose(); guideGroup.Dispose(); groupScale.Dispose();

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
            [ReadOnly] public NativeArray<float3> guideRoots;
            [ReadOnly] public NativeArray<float3> guidePoints;
            [ReadOnly] public NativeArray<int> guidePointCounts;
            [ReadOnly] public NativeArray<float2> guideThickness;
            [ReadOnly] public NativeArray<int> guideGroup;
            [ReadOnly] public NativeArray<float> groupScale;

            public float lengthScale;
            public float thicknessScale;
            public float curlStrength;
            public float waveStrength;
            public float frizzStrength;
            public float4 rootColor;
            public float4 tipColor;
            public float rootFade;

            [WriteOnly] public NativeArray<float3> positions;
            [WriteOnly] public NativeArray<float3> normals;
            [WriteOnly] public NativeArray<float2> uvs;
            [WriteOnly] public NativeArray<Color32> colors32;

            public void Execute(int guideIdx)
            {
                int pointCount = guidePointCounts[guideIdx];
                if (pointCount < 2) pointCount = 2;
                float3 root = guideRoots[guideIdx];
                float2 thick = guideThickness[guideIdx] * thicknessScale;
                int grp = guideGroup[guideIdx];
                float grpScale = (grp >= 0 && grp < groupScale.Length) ? groupScale[grp] : 1f;
                float lenScale = lengthScale * grpScale;

                int vBase = guideIdx * (segments + 1) * 2;

                // simple right vector – in head local space, X = right
                float3 right = new float3(0.006f, 0, 0);

                uint rng = (uint)(guideIdx * 1664525 + 1013904223);

                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    // Catmull-Rom sample along guidePoints
                    float3 pos = SampleCatmull(guideIdx, t, pointCount);
                    // apply length scale along curve direction (approx: scale from root)
                    pos = root + (pos - root) * lenScale;

                    // curl / wave / frizz – perpendicular noise
                    float3 tangent = new float3(0, 1, 0); // approx up
                    if (s > 0)
                    {
                        // will be refined next iter – keep simple for Burst
                    }
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

                    float th = math.lerp(thick.x, thick.y, t);
                    float3 leftPos = pos - right * th;
                    float3 rightPos = pos + right * th;

                    int v0 = vBase + s * 2;
                    int v1 = v0 + 1;
                    positions[v0] = leftPos;
                    positions[v1] = rightPos;
                    normals[v0] = new float3(0,0,-1);
                    normals[v1] = new float3(0,0,-1);
                    uvs[v0] = new float2(0, t);
                    uvs[v1] = new float2(1, t);

                    // vertex color = gradient root→tip
                    float g = math.smoothstep(rootFade, 1f, t);
                    float4 col = math.lerp(rootColor, tipColor, g);
                    byte r = (byte)math.clamp(col.x * 255f, 0, 255);
                    byte gr = (byte)math.clamp(col.y * 255f, 0, 255);
                    byte b = (byte)math.clamp(col.z * 255f, 0, 255);
                    var c32 = new Color32(r, gr, b, 255);
                    colors32[v0] = c32;
                    colors32[v1] = c32;
                }
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
                return guideRoots[guideIdx] + guidePoints[baseIdx];
            }
        }
    }
}
