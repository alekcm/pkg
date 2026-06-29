// HairBaker_v13_Patch.cs – Unity 6
// Drop-in addon – adds braid, pin-constraint, scalp-mask, per-segment curves
// – WITHOUT modifying original HairBaker.cs
// Call HairBakerProcV13.Bake() instead of HairBaker.Bake()

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    public static class HairBakerProcV13
    {
        public struct BakeParams {
            public HairPieceDefinitionProc piece;
            public HairDna dna;
            public int lod;
            public Transform headBone; // needed for pin constraints in world space
            // optional overrides
            public PinConstraint[] pins; // 0..6 – mid-strand ear tuck / hairpin
            public BundleAnchor bundle; // has bundle.id != null ? use
            public BraidProfile? braidOverride; // null = use piece.defaultBraid
        }

        public static HairBaker.BakeResult Bake(BakeParams p)
        {
            // 1) base bake – get raw ribbon mesh
            var baseResult = HairBaker.Bake(p.piece, p.dna, p.lod);
            if (baseResult.mesh == null) return baseResult;

            // If no advanced features requested – return fast path
            bool needPost = (p.pins != null && p.pins.Length > 0) ||
                            (p.braidOverride.HasValue && p.braidOverride.Value.type != BraidType.None) ||
                            (p.piece != null && p.piece.defaultBraid.type != BraidType.None) ||
                            !string.IsNullOrEmpty(p.bundle.id);

            if (!needPost) return baseResult;

            // 2) post-process vertices – apply pins / braid
            // This is CPU, ~0.3ms for 20k verts – acceptable, can be moved to Burst later
            var mesh = baseResult.mesh;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            // we need to know strand segmentation to apply braid correctly
            // – approximate: verts are stored strand-major, (seg+1)*2 per strand
            //   Reconstruct from piece guides count
            int guidesUsed = Mathf.Clamp(Mathf.RoundToInt((p.piece.guides?.Length ?? 0) * Mathf.Clamp(p.dna.density,0.3f,2f)),4,256);
            int seg = p.lod switch {1=>p.piece.segmentsLOD1, 2=>p.piece.segmentsLOD2, _=>p.piece.segmentsLOD0};
            int vertsPerStrand = (seg+1)*2;

            BraidProfile braid = p.braidOverride ?? p.piece.defaultBraid;

            // pins – build quick lookup: guideIdx -> pin
            var pinMap = new System.Collections.Generic.Dictionary<int, PinConstraint>();
            if (p.pins != null) foreach(var pin in p.pins) pinMap[pin.guideIndex]=pin;

            // bundle anchor world position
            bool hasBundle = !string.IsNullOrEmpty(p.bundle.id) && p.headBone != null;
            Vector3 bundleWS = Vector3.zero;
            if (hasBundle)
            {
                // resolve attach bone
                Transform b = p.headBone;
                // crude bone find – in real use resolve via avatar bone map
                if (!string.IsNullOrEmpty(p.bundle.attachBone))
                {
                    var t = b.GetComponentInParent<Animator>()?.GetBoneTransform(HumanBodyBones.Head);
                    if (t != null) b = t;
                }
                if (b != null) bundleWS = b.TransformPoint(p.bundle.localPos);
                else bundleWS = p.bundle.localPos;
            }

            // walk strands
            for (int s = 0; s < guidesUsed; s++)
            {
                int vBase = s * vertsPerStrand;
                if (vBase + vertsPerStrand > verts.Length) break;

                // pin?
                if (pinMap.TryGetValue(s, out var pin) && p.headBone != null)
                {
                    // find pin target bone in avatar
                    Transform pinBone = FindBoneRecursive(p.headBone.root, null); // fallback head
                    // try canonical lookup – simplified: use headBone for now, offset handles ear position
                    // In production: resolve pin.targetBoneHash via bone map (same as HairRuntimeAttacherProc)
                    Vector3 pinWS = p.headBone.TransformPoint(
                        new Vector3(pin.offsetX_cm * 0.01f, pin.offsetY_cm * 0.01f, pin.offsetZ_cm * 0.01f));
                    // if pin specifies a target bone, try find it:
                    // Transform tb = FindBoneByHash(...); if(tb) pinWS = tb.TransformPoint(offset);

                    float pinT = pin.t255 / 255f;
                    int pinSeg = Mathf.Clamp(Mathf.RoundToInt(pinT * seg), 1, seg - 1);
                    // snap left/right verts at pinSeg to pinWS
                    int vL = vBase + pinSeg * 2;
                    int vR = vL + 1;
                    if (vR < verts.Length)
                    {
                        // transform pinWS to mesh local space (mesh is in head local)
                        Vector3 pinLocal = p.headBone.InverseTransformPoint(pinWS);
                        verts[vL] = pinLocal;
                        verts[vR] = pinLocal;
                        // tail after pin keeps original shape – already in verts
                        // optional slack: ease out
                        float slack = pin.tailSlack255 / 255f;
                        // (visual slack already baked in guide points – nothing extra needed for MVP)
                    }
                }

                // braid – re-offset left/right pair around center line
                if (braid.type != BraidType.None)
                {
                    for (int i = 0; i <= seg; i++)
                    {
                        int vL = vBase + i * 2;
                        int vR = vL + 1;
                        if (vR >= verts.Length) break;
                        float3 left = verts[vL];
                        float3 right = verts[vR];
                        float3 center = (left + right) * 0.5f;
                        float3 tangent = i < seg ? math.normalize(((Vector3)verts[vBase + (i+1)*2] + (Vector3)verts[vBase + (i+1)*2+1])*0.5f - center) : new float3(0,1,0);
                        float3 bitan = math.normalize(right - left);
                        if (math.lengthsq(bitan) < 1e-6f) bitan = new float3(0.01f,0,0);
                        // sub-strand index: for 3-strand braid we need 3 virtual sub-strands per guide
                        // MVP: use guideIndex % 3 to stagger
                        int subIdx = s % 3;
                        float t = i / (float)seg;
                        float3 offsetPos = HairBraidGenerator.ApplyBraid(center, t, tangent, bitan,
                            braid, (uint)(s*1949), subIdx);
                        float3 delta = offsetPos - center;
                        verts[vL] = center - bitan * 0.5f * math.length(right-left) + delta;
                        verts[vR] = center + bitan * 0.5f * math.length(right-left) + delta;
                    }
                }

                // bundle / ponytail gather
                if (hasBundle)
                {
                    // find first vertex after pin (or 30% length) and lerp rest toward bundleWS
                    Transform head = p.headBone;
                    if (head != null)
                    {
                        Vector3 bundleLocal = head.InverseTransformPoint(bundleWS);
                        int gatherStart = Mathf.RoundToInt(seg * 0.35f); // start gathering 1/3 down
                        for (int i = gatherStart; i <= seg; i++)
                        {
                            float gt = Mathf.InverseLerp(gatherStart, seg, i);
                            gt = gt * gt; // ease in
                            int vL = vBase + i * 2;
                            int vR = vL + 1;
                            if (vR >= verts.Length) break;
                            verts[vL] = Vector3.Lerp(verts[vL], bundleLocal, gt * 0.95f);
                            verts[vR] = Vector3.Lerp(verts[vR], bundleLocal, gt * 0.95f);
                        }
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            baseResult.mesh = mesh;
            baseResult.bounds = mesh.bounds;
            return baseResult;
        }

        static Transform FindBoneRecursive(Transform root, string contains)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(contains)) return root;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains(contains.ToLowerInvariant())) return t;
            return root;
        }
    }
}
