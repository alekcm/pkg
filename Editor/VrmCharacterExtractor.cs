using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CharacterEditor.Hair.EditorImport;   // J, VrmGlbReader, GltfAccessorReader
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.VrmImport
{
    /// <summary>
    /// Extracts a full VRM character (body, face, clothing layers, hair parts)
    /// into separate Unity meshes/materials/prefabs.
    /// 
    /// Re-uses the existing glTF parsing infrastructure:
    ///   - J (MiniJson.cs)            — lightweight JSON DOM
    ///   - VrmGlbReader               — reads GLB binary container
    ///   - GltfAccessorReader         — typed accessor reads (vec2/3/4, mat4, indices)
    /// </summary>
    public static class VrmCharacterExtractor
    {
        // ──────────────────────────────────────────────
        //  Internal helpers — same pattern as VrmHairExtractor
        // ──────────────────────────────────────────────

        private sealed class NodeInfo
        {
            public int index;
            public string name;
            public int mesh = -1;
            public int skin = -1;
            public int parent = -1;
            public List<int> children = new();
            public GameObject go;
            public Vector3 localPosition = Vector3.zero;
            public Quaternion localRotation = Quaternion.identity;
            public Vector3 localScale = Vector3.one;
            public bool hasMatrix;
            public Matrix4x4 localMatrix = Matrix4x4.identity;
        }

        private sealed class BuiltMaterial
        {
            public Material material;
            public string name;
        }

        /// <summary>One extracted primitive — carries mesh + slot + material.</summary>
        public sealed class ExtractedPart
        {
            public CharacterPartSlot slot;
            public string partName;   // e.g. "HairFront", "Shoes", "Body"
            public Mesh mesh;
            public Material material;
            public int nodeIndex;
            public int skinIndex;
        }

        /// <summary>Result of a full extraction.</summary>
        public sealed class ExtractionResult
        {
            public string id;
            public GameObject prefab;
            public List<ExtractedPart> parts = new();
            public string diagnosticText;
        }

        // ══════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Extract the entire VRM file at <paramref name="vrmAssetPath"/>
        /// into separate layered parts under <paramref name="settings"/>.outputFolder.
        /// </summary>
        public static ExtractionResult Extract(string vrmAssetPath, VrmCharacterExtractionSettings settings)
        {
            if (settings == null)
                settings = ScriptableObject.CreateInstance<VrmCharacterExtractionSettings>();

            string id = Path.GetFileNameWithoutExtension(vrmAssetPath);
            string safeId = Sanitize(id);
            string outRoot = CombineAsset(settings.outputFolder, safeId);
            EnsureFolder(outRoot);
            EnsureFolder(CombineAsset(outRoot, "Textures"));
            EnsureFolder(CombineAsset(outRoot, "Materials"));
            EnsureFolder(CombineAsset(outRoot, "Meshes"));

            // ── Parse GLB ──
            var glb = VrmGlbReader.Read(vrmAssetPath);
            var reader = new GltfAccessorReader(glb.json, glb.bin);
            var nodes = ReadNodes(glb.json);
            var materials = BuildMaterials(glb.json, reader, outRoot, settings);

            // ── Build skeleton hierarchy ──
            var root = new GameObject(safeId);
            BuildNodeHierarchy(nodes, root.transform, settings);

            // ── Classify & extract every primitive ──
            var result = new ExtractionResult { id = safeId };
            var usedMaterials = new List<Material>();
            var sourceBoneNames = new List<string>();
            var diag = new StringBuilder();
            diag.AppendLine($"VRM Character Extraction: {vrmAssetPath}");
            diag.AppendLine($"VRM version: {DetectVrmVersion(glb.json)}");
            diag.AppendLine();

            // Per-slot counters for unique naming
            var slotCounters = new Dictionary<CharacterPartSlot, int>();

            foreach (var n in nodes)
            {
                if (n.mesh < 0) continue;
                string nodeName = n.name;
                var meshJson = glb.json["meshes"][n.mesh];
                string meshName = meshJson["name"].S;
                if (!meshJson.Has("primitives")) continue;

                diag.AppendLine($"node[{n.index}] '{nodeName}' mesh[{n.mesh}] '{meshName}' skin={n.skin}");

                // Determine mesh-level category hint (VRoid: "Face.baked", "Body.baked", "Hair001.baked")
                CharacterPartSlot meshLevelHint = GuessMeshLevelSlot(meshName, settings);

                int primIdx = 0;
                foreach (var prim in meshJson["primitives"].arr)
                {
                    int matIndex = prim.Has("material") ? prim["material"].I : -1;
                    string matName = matIndex >= 0 && matIndex < materials.Count ? materials[matIndex].name : "";
                    Material mat = matIndex >= 0 && matIndex < materials.Count ? materials[matIndex].material : null;

                    // Classify this primitive
                    CharacterPartSlot slot = ClassifyPrimitive(nodeName, meshName, matName, meshLevelHint, settings);

                    diag.AppendLine($"  prim[{primIdx}] mat[{matIndex}] '{matName}' → {slot}");

                    // Build mesh
                    if (!slotCounters.ContainsKey(slot)) slotCounters[slot] = 0;
                    int counter = slotCounters[slot]++;
                    string partName = $"{safeId}_{slot}_{counter}";
                    var mesh = BuildMesh(prim, reader, settings, partName);
                    if (mesh == null) { primIdx++; continue; }

                    // Save mesh asset
                    string meshAssetPath = CombineAsset(outRoot, "Meshes/" + mesh.name + ".asset");
                    AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(meshAssetPath));

                    // Create GameObject for this part
                    GameObject partGo = new GameObject(partName);
                    partGo.transform.SetParent(root.transform, false);

                    if (mat != null && !usedMaterials.Contains(mat))
                        usedMaterials.Add(mat);

                    if (settings.importSkinning && n.skin >= 0)
                    {
                        var smr = partGo.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterials = mat != null ? new[] { mat } : Array.Empty<Material>();
                        ApplySkin(glb.json, reader, nodes, n.skin, smr, sourceBoneNames, settings);
                    }
                    else
                    {
                        var mf = partGo.AddComponent<MeshFilter>();
                        var mr = partGo.AddComponent<MeshRenderer>();
                        mf.sharedMesh = mesh;
                        mr.sharedMaterials = mat != null ? new[] { mat } : Array.Empty<Material>();
                    }

                    result.parts.Add(new ExtractedPart
                    {
                        slot = slot,
                        partName = partName,
                        mesh = mesh,
                        material = mat,
                        nodeIndex = n.index,
                        skinIndex = n.skin,
                    });
                    primIdx++;
                }
            }

            // ── Group parts by slot into child GameObjects ──
            // Re-parent: create slot-group parents so the hierarchy is clean
            var slotGroups = new Dictionary<CharacterPartSlot, Transform>();
            foreach (var part in result.parts)
            {
                if (!slotGroups.TryGetValue(part.slot, out var groupTf))
                {
                    var groupGo = new GameObject(part.slot.ToString());
                    groupGo.transform.SetParent(root.transform, false);
                    slotGroups[part.slot] = groupGo.transform;
                    groupTf = groupGo.transform;
                }
                // Find the part GO and re-parent it under the group
                var partTf = root.transform.Find(part.partName);
                if (partTf != null)
                    partTf.SetParent(groupTf, false);
            }

            // ── Save prefab ──
            string prefabPath = CombineAsset(outRoot, safeId + ".prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            result.prefab = prefab;

            // ── Diagnostic ──
            diag.AppendLine();
            diag.AppendLine("=== SUMMARY ===");
            var grouped = result.parts.GroupBy(p => p.slot).OrderBy(g => g.Key);
            foreach (var g in grouped)
                diag.AppendLine($"  {g.Key}: {g.Count()} primitive(s)");
            result.diagnosticText = diag.ToString();

            if (settings.writeDiagnostic)
            {
                string diagPath = CombineAsset(outRoot, safeId + "_diagnostic.txt");
                File.WriteAllText(ToFsPath(diagPath), result.diagnosticText, Encoding.UTF8);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        // ══════════════════════════════════════════════
        //  CLASSIFICATION LOGIC
        // ══════════════════════════════════════════════

        /// <summary>
        /// Guess the mesh-level slot from the VRoid-baked mesh name.
        /// VRoid exports meshes named "Face.baked", "Face(Clone).baked", "Body.baked", "Hair001.baked", etc.
        /// </summary>
        private static CharacterPartSlot GuessMeshLevelSlot(string meshName, VrmCharacterExtractionSettings s)
        {
            string lower = meshName.ToLowerInvariant();
            foreach (var kw in s.meshNameFace)
                if (lower.Contains(kw.ToLowerInvariant())) return CharacterPartSlot.Face;
            foreach (var kw in s.meshNameBody)
                if (lower.Contains(kw.ToLowerInvariant())) return CharacterPartSlot.Body;
            foreach (var kw in s.meshNameHair)
                if (lower.Contains(kw.ToLowerInvariant())) return CharacterPartSlot.HairAll;
            return CharacterPartSlot.Unknown;
        }

        /// <summary>
        /// Classify a single mesh primitive into a CharacterPartSlot.
        /// Priority: material name → mesh name → node name → mesh-level hint.
        /// </summary>
        private static CharacterPartSlot ClassifyPrimitive(
            string nodeName, string meshName, string matName,
            CharacterPartSlot meshLevelHint,
            VrmCharacterExtractionSettings s)
        {
            // Combine all identifying strings for matching
            string combined = $"{matName} {meshName} {nodeName}".ToLowerInvariant();

            // ── Hair sub-parts (most specific first) ──
            if (MatchesAny(combined, s.hairFrontKeywords)) return CharacterPartSlot.HairFront;
            if (MatchesAny(combined, s.hairBackKeywords))  return CharacterPartSlot.HairBack;
            if (MatchesAny(combined, s.hairSideKeywords))  return CharacterPartSlot.HairSide;
            if (MatchesAny(combined, s.hairAhogeKeywords)) return CharacterPartSlot.HairAhoge;
            if (MatchesAny(combined, s.hairExtraKeywords)) return CharacterPartSlot.HairExtra;

            // ── Clothing (before generic body/face check) ──
            if (MatchesAny(combined, s.underwearKeywords)) return CharacterPartSlot.Underwear;
            if (MatchesAny(combined, s.tightsKeywords))    return CharacterPartSlot.Tights;
            if (MatchesAny(combined, s.socksKeywords))     return CharacterPartSlot.Socks;
            if (MatchesAny(combined, s.shoesKeywords))     return CharacterPartSlot.Shoes;
            if (MatchesAny(combined, s.skirtKeywords))     return CharacterPartSlot.Skirt;
            if (MatchesAny(combined, s.pantsKeywords))     return CharacterPartSlot.Pants;
            if (MatchesAny(combined, s.jacketKeywords))    return CharacterPartSlot.Jacket;
            if (MatchesAny(combined, s.topsKeywords))      return CharacterPartSlot.Tops;
            if (MatchesAny(combined, s.glovesKeywords))    return CharacterPartSlot.Gloves;
            if (MatchesAny(combined, s.hatKeywords))       return CharacterPartSlot.Hat;
            if (MatchesAny(combined, s.accessoryKeywords)) return CharacterPartSlot.Accessory;

            // ── Body / Face ──
            if (MatchesAny(combined, s.faceKeywords)) return CharacterPartSlot.Face;
            if (MatchesAny(combined, s.bodyKeywords)) return CharacterPartSlot.Body;

            // ── Generic hair (after specific parts, catches any remaining hair prims) ──
            if (MatchesAny(combined, s.hairGenericKeywords))
            {
                // Try to sub-classify from material name patterns used by VRoid:
                // e.g. "N00_000_00_HairBack_00_HAIR", "N00_000_Hair_00"
                return SubClassifyHairFromVRoidName(combined, s);
            }

            // ── Fallback to mesh-level hint ──
            if (meshLevelHint != CharacterPartSlot.Unknown)
            {
                // If mesh is "Hair*" but we couldn't determine part, use HairAll
                return meshLevelHint;
            }

            // VRoid typically labels clothing materials with _CLOTH suffix
            // and outfit materials with category names like "Tops", "Bottoms"
            if (combined.Contains("cloth") || combined.Contains("outfit") || combined.Contains("wear"))
                return CharacterPartSlot.Tops; // generic clothing fallback

            return CharacterPartSlot.Unknown;
        }

        /// <summary>
        /// Try to detect VRoid-specific hair part names from material names like:
        /// "N00_000_00_HairBack_00_HAIR", "HairFront_00", etc.
        /// </summary>
        private static CharacterPartSlot SubClassifyHairFromVRoidName(string lower, VrmCharacterExtractionSettings s)
        {
            // VRoid naming conventions:
            // HairBack / Hair_Back / hairback_
            if (lower.Contains("hairback") || lower.Contains("hair_back") || lower.Contains("back"))
                return CharacterPartSlot.HairBack;
            if (lower.Contains("hairfront") || lower.Contains("hair_front") || lower.Contains("front") ||
                lower.Contains("bang") || lower.Contains("fringe"))
                return CharacterPartSlot.HairFront;
            if (lower.Contains("hairside") || lower.Contains("hair_side") || lower.Contains("side"))
                return CharacterPartSlot.HairSide;
            if (lower.Contains("ahoge") || lower.Contains("antenna"))
                return CharacterPartSlot.HairAhoge;
            if (lower.Contains("hanege") || lower.Contains("extra") || lower.Contains("extension"))
                return CharacterPartSlot.HairExtra;

            return CharacterPartSlot.HairAll;
        }

        private static bool MatchesAny(string text, string[] keywords)
        {
            if (keywords == null) return false;
            foreach (var kw in keywords)
                if (!string.IsNullOrWhiteSpace(kw) && text.Contains(kw.ToLowerInvariant()))
                    return true;
            return false;
        }

        // ══════════════════════════════════════════════
        //  MESH BUILDING (same as VrmHairExtractor)
        // ══════════════════════════════════════════════

        // Rotation 180° around Y applied to mesh vertices/normals/tangents.
        // Quaternion.Euler(0,180,0) rotates (x,y,z) → (-x, y, -z).
        private static readonly Quaternion RotY180 = Quaternion.Euler(0f, 180f, 0f);

        private static Vector3 RotateY180Vec3(Vector3 v) => new Vector3(-v.x, v.y, -v.z);

        private static Mesh BuildMesh(J prim, GltfAccessorReader reader, VrmCharacterExtractionSettings settings, string name)
        {
            if (!prim.Has("attributes") || !prim["attributes"].Has("POSITION")) return null;
            var attrs = prim["attributes"];
            var pos = reader.ReadVec3(attrs["POSITION"].I);
            var mesh = new Mesh { name = name };
            if (pos.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            for (int i = 0; i < pos.Length; i++)
            {
                pos[i] = ConvertVec3(pos[i], settings.flipXForUnity);
                pos[i] = RotateY180Vec3(pos[i]);
            }
            mesh.vertices = pos;

            if (attrs.Has("NORMAL"))
            {
                var n = reader.ReadVec3(attrs["NORMAL"].I);
                for (int i = 0; i < n.Length; i++)
                {
                    n[i] = ConvertVec3(n[i], settings.flipXForUnity);
                    n[i] = RotateY180Vec3(n[i]).normalized;
                }
                mesh.normals = n;
            }
            if (attrs.Has("TANGENT"))
            {
                var t = reader.ReadVec4(attrs["TANGENT"].I);
                for (int i = 0; i < t.Length; i++)
                {
                    var v = ConvertVec3(new Vector3(t[i].x, t[i].y, t[i].z), settings.flipXForUnity);
                    v = RotateY180Vec3(v);
                    t[i] = new Vector4(v.x, v.y, v.z, t[i].w);
                }
                mesh.tangents = t;
            }
            if (attrs.Has("TEXCOORD_0"))
                mesh.uv = reader.ReadVec2(attrs["TEXCOORD_0"].I);
            if (attrs.Has("TEXCOORD_1"))
                mesh.uv2 = reader.ReadVec2(attrs["TEXCOORD_1"].I);

            // Blend shapes / morph targets
            if (prim.Has("targets"))
            {
                int targetIndex = 0;
                foreach (var target in prim["targets"].arr)
                {
                    string bsName = "blendShape_" + targetIndex;
                    // Try to get name from mesh extras/targetNames
                    Vector3[] deltaVerts = null;
                    Vector3[] deltaNormals = null;

                    if (target.Has("POSITION"))
                    {
                        deltaVerts = reader.ReadVec3(target["POSITION"].I);
                        for (int i = 0; i < deltaVerts.Length; i++)
                        {
                            deltaVerts[i] = ConvertVec3(deltaVerts[i], settings.flipXForUnity);
                            deltaVerts[i] = RotateY180Vec3(deltaVerts[i]);
                        }
                    }
                    if (target.Has("NORMAL"))
                    {
                        deltaNormals = reader.ReadVec3(target["NORMAL"].I);
                        for (int i = 0; i < deltaNormals.Length; i++)
                        {
                            deltaNormals[i] = ConvertVec3(deltaNormals[i], settings.flipXForUnity);
                            deltaNormals[i] = RotateY180Vec3(deltaNormals[i]);
                        }
                    }

                    if (deltaVerts != null)
                    {
                        mesh.AddBlendShapeFrame(bsName, 100f,
                            deltaVerts,
                            deltaNormals ?? new Vector3[deltaVerts.Length],
                            null);
                    }
                    targetIndex++;
                }
            }

            // Skinning data
            if (attrs.Has("JOINTS_0") && attrs.Has("WEIGHTS_0"))
            {
                var joints = reader.ReadVec4(attrs["JOINTS_0"].I);
                var weights = reader.ReadVec4(attrs["WEIGHTS_0"].I);
                var bw = new BoneWeight[pos.Length];
                for (int i = 0; i < bw.Length; i++)
                {
                    bw[i].boneIndex0 = Mathf.RoundToInt(joints[i].x);
                    bw[i].boneIndex1 = Mathf.RoundToInt(joints[i].y);
                    bw[i].boneIndex2 = Mathf.RoundToInt(joints[i].z);
                    bw[i].boneIndex3 = Mathf.RoundToInt(joints[i].w);
                    float sum = weights[i].x + weights[i].y + weights[i].z + weights[i].w;
                    if (sum < 1e-5f) { bw[i].weight0 = 1; }
                    else
                    {
                        bw[i].weight0 = weights[i].x / sum;
                        bw[i].weight1 = weights[i].y / sum;
                        bw[i].weight2 = weights[i].z / sum;
                        bw[i].weight3 = weights[i].w / sum;
                    }
                }
                mesh.boneWeights = bw;
            }

            int[] indices = prim.Has("indices") ? reader.ReadIndices(prim["indices"].I) : MakeSequential(pos.Length);
            if (settings.reverseTriangleWinding)
            {
                for (int i = 0; i + 2 < indices.Length; i += 3)
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            }
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            if (mesh.normals == null || mesh.normals.Length == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ══════════════════════════════════════════════
        //  SKINNING
        // ══════════════════════════════════════════════

        private static void ApplySkin(J gltf, GltfAccessorReader reader, List<NodeInfo> nodes,
            int skinIndex, SkinnedMeshRenderer smr, List<string> sourceBoneNames,
            VrmCharacterExtractionSettings settings)
        {
            var skin = gltf["skins"][skinIndex];
            var joints = skin["joints"].arr;
            var bones = new Transform[joints.Count];
            for (int i = 0; i < joints.Count; i++)
            {
                int nodeIndex = joints[i].I;
                if (nodeIndex >= 0 && nodeIndex < nodes.Count)
                {
                    bones[i] = nodes[nodeIndex].go.transform;
                    if (!sourceBoneNames.Contains(nodes[nodeIndex].name))
                        sourceBoneNames.Add(nodes[nodeIndex].name);
                }
            }
            smr.bones = bones;

            if (skin.Has("skeleton"))
            {
                int skeleton = skin["skeleton"].I;
                if (skeleton >= 0 && skeleton < nodes.Count)
                    smr.rootBone = nodes[skeleton].go.transform;
            }
            else if (bones.Length > 0)
                smr.rootBone = bones[0];

            if (skin.Has("inverseBindMatrices"))
            {
                var bindposes = reader.ReadMat4(skin["inverseBindMatrices"].I);
                if (settings.flipXForUnity)
                {
                    for (int i = 0; i < bindposes.Length; i++)
                        bindposes[i] = ConvertMatrixFlipX(bindposes[i]);
                }
                if (smr.sharedMesh != null)
                {
                    smr.sharedMesh.bindposes = bindposes;
                    smr.sharedMesh.RecalculateBounds();
                    EditorUtility.SetDirty(smr.sharedMesh);
                }
            }

            // Root bone safety
            if (smr.rootBone != null && bones != null)
            {
                bool found = false;
                foreach (var b in bones) if (b == smr.rootBone) { found = true; break; }
                if (!found && bones.Length > 0) smr.rootBone = bones[0];
            }
            if (smr.sharedMesh != null)
            {
                smr.sharedMesh.RecalculateBounds();
                smr.localBounds = smr.sharedMesh.bounds;
            }
            smr.updateWhenOffscreen = true;
            smr.quality = SkinQuality.Bone4;
            smr.skinnedMotionVectors = true;
            smr.allowOcclusionWhenDynamic = false;
            smr.forceMatrixRecalculationPerRender = true;

            // Save mesh after bindpose
            if (smr.sharedMesh != null)
            {
                string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    EditorUtility.SetDirty(smr.sharedMesh);
                    AssetDatabase.SaveAssetIfDirty(smr.sharedMesh);
                }
            }
        }

        // ══════════════════════════════════════════════
        //  MATERIALS
        // ══════════════════════════════════════════════

        private static List<BuiltMaterial> BuildMaterials(J gltf, GltfAccessorReader reader,
            string outRoot, VrmCharacterExtractionSettings settings)
        {
            var result = new List<BuiltMaterial>();
            var shader = settings.materialShader != null ? settings.materialShader : FindBestShader();

            int count = gltf.Has("materials") ? gltf["materials"].Count : 0;
            for (int i = 0; i < count; i++)
            {
                var mj = gltf["materials"][i];
                string name = string.IsNullOrEmpty(mj["name"].S) ? $"material_{i}" : Sanitize(mj["name"].S);
                var mat = new Material(shader) { name = name };

                Color baseColor = Color.white;
                if (mj.Has("pbrMetallicRoughness") && mj["pbrMetallicRoughness"].Has("baseColorFactor"))
                {
                    var c = mj["pbrMetallicRoughness"]["baseColorFactor"];
                    baseColor = new Color(c[0].F, c[1].F, c[2].F, c[3].F);
                }
                SetColor(mat, baseColor);

                int texIndex = -1;
                if (mj.Has("pbrMetallicRoughness") && mj["pbrMetallicRoughness"].Has("baseColorTexture"))
                    texIndex = mj["pbrMetallicRoughness"]["baseColorTexture"]["index"].I;
                if (texIndex >= 0)
                {
                    var tex = LoadTexture(gltf, reader, texIndex, outRoot, name + "_BaseColor", false);
                    if (tex != null) SetMainTexture(mat, tex);
                }

                if (mj.Has("normalTexture"))
                {
                    int ni = mj["normalTexture"]["index"].I;
                    var tex = LoadTexture(gltf, reader, ni, outRoot, name + "_Normal", true);
                    if (tex != null)
                    {
                        if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", tex);
                        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", tex);
                    }
                }

                if (mj.Has("emissiveFactor"))
                {
                    var e = mj["emissiveFactor"];
                    Color ec = new Color(e[0].F, e[1].F, e[2].F, 1);
                    if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", ec);
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", ec);
                }

                // Alpha mode
                if (mj.Has("alphaMode"))
                {
                    string mode = mj["alphaMode"].S;
                    if (mode == "MASK")
                    {
                        float cutoff = mj.Has("alphaCutoff") ? mj["alphaCutoff"].F : 0.5f;
                        if (mat.HasProperty("_AlphaCutoff")) mat.SetFloat("_AlphaCutoff", cutoff);
                        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", cutoff);
                        if (mat.HasProperty("_AlphaCutoffEnable")) mat.SetFloat("_AlphaCutoffEnable", 1f);
                        if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = 2450;
                    }
                    else if (mode == "BLEND")
                    {
                        if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f);
                        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = 3000;
                    }
                }

                // Double-sided
                if (mj.Has("doubleSided") && mj["doubleSided"].B)
                {
                    if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
                    if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
                    mat.EnableKeyword("_DOUBLESIDED_ON");
                }

                string matPath = CombineAsset(outRoot, "Materials/" + name + ".mat");
                AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(matPath));
                result.Add(new BuiltMaterial { material = mat, name = mj["name"].S ?? name });
            }
            return result;
        }

        // ══════════════════════════════════════════════
        //  NODE HIERARCHY
        // ══════════════════════════════════════════════

        private static List<NodeInfo> ReadNodes(J gltf)
        {
            var list = new List<NodeInfo>();
            int count = gltf.Has("nodes") ? gltf["nodes"].Count : 0;
            for (int i = 0; i < count; i++)
            {
                var n = gltf["nodes"][i];
                var info = new NodeInfo
                {
                    index = i,
                    name = string.IsNullOrEmpty(n["name"].S) ? "node_" + i : n["name"].S,
                    mesh = n.Has("mesh") ? n["mesh"].I : -1,
                    skin = n.Has("skin") ? n["skin"].I : -1,
                };
                ReadNodeTransform(n, info);
                list.Add(info);
            }
            for (int i = 0; i < count; i++)
            {
                var n = gltf["nodes"][i];
                if (!n.Has("children")) continue;
                foreach (var c in n["children"].arr)
                {
                    int child = c.I;
                    if (child >= 0 && child < list.Count)
                    {
                        list[i].children.Add(child);
                        list[child].parent = i;
                    }
                }
            }
            return list;
        }

        private static void BuildNodeHierarchy(List<NodeInfo> nodes, Transform root,
            VrmCharacterExtractionSettings settings)
        {
            foreach (var n in nodes) n.go = new GameObject(n.name);
            foreach (var n in nodes)
            {
                Transform parent = n.parent >= 0 ? nodes[n.parent].go.transform : root;
                n.go.transform.SetParent(parent, false);
                ApplyNodeTransform(n.go.transform, n, settings.flipXForUnity);
            }
        }

        private static void ApplyNodeTransform(Transform t, NodeInfo n, bool flipX)
        {
            if (n.hasMatrix)
            {
                Matrix4x4 m = flipX ? ConvertMatrixFlipX(n.localMatrix) : n.localMatrix;
                t.localPosition = m.GetColumn(3);
                t.localRotation = m.rotation;
                t.localScale = m.lossyScale;
                return;
            }
            t.localPosition = flipX ? ConvertVec3(n.localPosition, true) : n.localPosition;
            t.localRotation = flipX ? ConvertQuatFlipX(n.localRotation) : n.localRotation;
            t.localScale = n.localScale;
        }

        private static void ReadNodeTransform(J node, NodeInfo info)
        {
            if (node.Has("matrix") && node["matrix"].Count >= 16)
            {
                var m = new Matrix4x4();
                for (int c = 0; c < 4; c++)
                    for (int r = 0; r < 4; r++)
                        m[r, c] = node["matrix"][c * 4 + r].F;
                info.localMatrix = m;
                info.hasMatrix = true;
                return;
            }
            if (node.Has("translation") && node["translation"].Count >= 3)
                info.localPosition = new Vector3(node["translation"][0].F, node["translation"][1].F, node["translation"][2].F);
            if (node.Has("rotation") && node["rotation"].Count >= 4)
                info.localRotation = new Quaternion(node["rotation"][0].F, node["rotation"][1].F, node["rotation"][2].F, node["rotation"][3].F);
            if (node.Has("scale") && node["scale"].Count >= 3)
                info.localScale = new Vector3(node["scale"][0].F, node["scale"][1].F, node["scale"][2].F);
        }

        // ══════════════════════════════════════════════
        //  TEXTURES
        // ══════════════════════════════════════════════

        private static Texture2D LoadTexture(J gltf, GltfAccessorReader reader, int textureIndex,
            string outRoot, string name, bool isNormalMap)
        {
            if (!gltf.Has("textures") || textureIndex < 0 || textureIndex >= gltf["textures"].Count) return null;
            int imageIndex = gltf["textures"][textureIndex]["source"].I;
            if (!gltf.Has("images") || imageIndex < 0 || imageIndex >= gltf["images"].Count) return null;
            var img = gltf["images"][imageIndex];
            byte[] bytes = null;
            if (img.Has("bufferView")) bytes = reader.ReadBufferViewBytes(img["bufferView"].I);
            if (bytes == null || bytes.Length == 0) return null;

            string ext = img["mimeType"].S.Contains("jpeg") || img["mimeType"].S.Contains("jpg") ? ".jpg" : ".png";
            string path = AssetDatabase.GenerateUniqueAssetPath(CombineAsset(outRoot, "Textures/" + Sanitize(name) + ext));
            File.WriteAllBytes(ToFsPath(path), bytes);
            AssetDatabase.ImportAsset(path);
            if (isNormalMap)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) tex.name = Sanitize(name);
            return tex;
        }

        // ══════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════

        private static Shader FindBestShader()
        {
            return Shader.Find("HDRP/Lit")
                ?? Shader.Find("HDRenderPipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
        }

        private static string DetectVrmVersion(J gltf)
        {
            if (gltf.Has("extensions"))
            {
                if (gltf["extensions"].Has("VRM")) return "VRM 0.x";
                if (gltf["extensions"].Has("VRMC_vrm")) return "VRM 1.0";
            }
            return "unknown glTF/VRM";
        }

        private static Vector3 ConvertVec3(Vector3 v, bool flipX) =>
            flipX ? new Vector3(-v.x, v.y, v.z) : v;

        private static Quaternion ConvertQuatFlipX(Quaternion q) =>
            new Quaternion(q.x, -q.y, -q.z, q.w);

        private static Matrix4x4 ConvertMatrixFlipX(Matrix4x4 m)
        {
            var r = m;
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    float sr = row == 0 ? -1f : 1f;
                    float sc = col == 0 ? -1f : 1f;
                    r[row, col] = m[row, col] * sr * sc;
                }
            return r;
        }

        private static int[] MakeSequential(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            return a;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
        private static void SetMainTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseColorMap")) m.SetTexture("_BaseColorMap", t);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_').Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }
        private static string CombineAsset(string a, string b) =>
            (a.TrimEnd('/') + "/" + b.TrimStart('/')).Replace('\\', '/');
        private static string ToFsPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        private static void EnsureFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
