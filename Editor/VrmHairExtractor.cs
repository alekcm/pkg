using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CharacterEditor.Hair;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorImport
{
    public static class VrmHairExtractor
    {
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

        public static HairPieceDefinition Extract(string vrmAssetPath, VrmHairExtractionSettings settings)
        {
            if (settings == null) settings = ScriptableObject.CreateInstance<VrmHairExtractionSettings>();
            string id = Path.GetFileNameWithoutExtension(vrmAssetPath);
            string safeId = Sanitize(id);
            string outRoot = CombineAsset(settings.outputFolder, safeId);
            EnsureFolder(outRoot);
            EnsureFolder(CombineAsset(outRoot, "Textures"));
            EnsureFolder(CombineAsset(outRoot, "Materials"));

            var glb = VrmGlbReader.Read(vrmAssetPath);
            var reader = new GltfAccessorReader(glb.json, glb.bin);
            var nodes = ReadNodes(glb.json);
            var materials = BuildMaterials(glb.json, reader, outRoot, settings);

            var root = new GameObject(safeId);
            BuildNodeHierarchy(nodes, root.transform, settings);

            int hairRenderers = 0;
            var usedMaterials = new List<Material>();
            var sourceBoneNames = new List<string>();
            var diagnostic = new StringBuilder();
            diagnostic.AppendLine($"VRM: {vrmAssetPath}");
            diagnostic.AppendLine($"Detected VRM version: {DetectVrmVersion(glb.json)}");
            diagnostic.AppendLine("\nNODES / MESHES / MATERIALS:");

            foreach (var n in nodes)
            {
                if (n.mesh < 0) continue;
                string nodeName = n.name;
                string meshName = glb.json["meshes"][n.mesh]["name"].S;
                diagnostic.AppendLine($"node[{n.index}] '{nodeName}' mesh[{n.mesh}] '{meshName}' skin={n.skin}");

                var meshJson = glb.json["meshes"][n.mesh];
                if (!meshJson.Has("primitives")) continue;

                int primitiveIndex = 0;
                foreach (var prim in meshJson["primitives"].arr)
                {
                    int matIndex = prim.Has("material") ? prim["material"].I : -1;
                    string matName = matIndex >= 0 && matIndex < materials.Count ? materials[matIndex].name : "";
                    diagnostic.AppendLine($"  primitive[{primitiveIndex}] material[{matIndex}] '{matName}'");
                    primitiveIndex++;

                    if (!IsHairCandidate(nodeName, meshName, matName, settings)) continue;

                    var mesh = BuildMesh(prim, reader, settings, safeId + "_mesh_" + hairRenderers);
                    if (mesh == null) continue;

                    string meshAssetPath = CombineAsset(outRoot, mesh.name + ".asset");
                    AssetDatabase.CreateAsset(mesh, meshAssetPath);

                    GameObject go = new GameObject(mesh.name);
                    go.transform.SetParent(root.transform, false);

                    Material mat = matIndex >= 0 && matIndex < materials.Count ? materials[matIndex].material : null;
                    if (mat != null && !usedMaterials.Contains(mat)) usedMaterials.Add(mat);

                    if (settings.importSkinning && n.skin >= 0)
                    {
                        var smr = go.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterials = mat != null ? new[] { mat } : Array.Empty<Material>();
                        ApplySkin(glb.json, reader, nodes, n.skin, smr, sourceBoneNames, settings);
                    }
                    else
                    {
                        var mf = go.AddComponent<MeshFilter>();
                        var mr = go.AddComponent<MeshRenderer>();
                        mf.sharedMesh = mesh;
                        mr.sharedMaterials = mat != null ? new[] { mat } : Array.Empty<Material>();
                    }
                    hairRenderers++;
                }
            }

            if (hairRenderers == 0)
            {
                Debug.LogWarning($"[VRM Hair Extractor] No hair meshes found in {vrmAssetPath}. Check include/exclude keywords. A placeholder prefab will still be created.");
            }

            string prefabPath = CombineAsset(outRoot, safeId + ".prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            var def = ScriptableObject.CreateInstance<HairPieceDefinition>();
            def.id = safeId;
            def.displayName = id;
            def.slot = GuessSlot(vrmAssetPath, settings.slotFromFolderFallback);
            def.prefab = prefab;
            def.materials = usedMaterials;
            def.sourceBoneNames = sourceBoneNames;
            def.sourceVrmPath = vrmAssetPath;

            string defPath = CombineAsset(outRoot, safeId + "_HairPiece.asset");
            AssetDatabase.CreateAsset(def, defPath);

            if (settings.writeDiagnosticJson)
            {
                File.WriteAllText(ToFsPath(CombineAsset(outRoot, safeId + "_diagnostic.txt")), diagnostic.ToString(), Encoding.UTF8);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return def;
        }

        private static Mesh BuildMesh(J prim, GltfAccessorReader reader, VrmHairExtractionSettings settings, string name)
        {
            if (!prim.Has("attributes") || !prim["attributes"].Has("POSITION")) return null;
            var attrs = prim["attributes"];
            var pos = reader.ReadVec3(attrs["POSITION"].I);
            var mesh = new Mesh { name = name };
            if (pos.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            for (int i = 0; i < pos.Length; i++) pos[i] = ConvertVec3(pos[i], settings.flipXForUnity);
            mesh.vertices = pos;

            if (attrs.Has("NORMAL"))
            {
                var n = reader.ReadVec3(attrs["NORMAL"].I);
                for (int i = 0; i < n.Length; i++) n[i] = ConvertVec3(n[i], settings.flipXForUnity).normalized;
                mesh.normals = n;
            }
            if (attrs.Has("TANGENT"))
            {
                var t = reader.ReadVec4(attrs["TANGENT"].I);
                for (int i = 0; i < t.Length; i++)
                {
                    var v = ConvertVec3(new Vector3(t[i].x, t[i].y, t[i].z), settings.flipXForUnity);
                    t[i] = new Vector4(v.x, v.y, v.z, t[i].w);
                }
                mesh.tangents = t;
            }
            if (attrs.Has("TEXCOORD_0")) mesh.uv = reader.ReadVec2(attrs["TEXCOORD_0"].I);

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

        private static void ApplySkin(J gltf, GltfAccessorReader reader, List<NodeInfo> nodes, int skinIndex, SkinnedMeshRenderer smr, List<string> sourceBoneNames, VrmHairExtractionSettings settings)
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
                    if (!sourceBoneNames.Contains(nodes[nodeIndex].name)) sourceBoneNames.Add(nodes[nodeIndex].name);
                }
            }
            smr.bones = bones;
            if (skin.Has("skeleton"))
            {
                int skeleton = skin["skeleton"].I;
                if (skeleton >= 0 && skeleton < nodes.Count) smr.rootBone = nodes[skeleton].go.transform;
            }
            else if (bones.Length > 0) smr.rootBone = bones[0];

            if (skin.Has("inverseBindMatrices"))
            {
                var bindposes = reader.ReadMat4(skin["inverseBindMatrices"].I);
                if (settings.flipXForUnity)
                {
                    for (int i = 0; i < bindposes.Length; i++)
                        bindposes[i] = ConvertMatrixFlipX(bindposes[i]);
                }
                smr.sharedMesh.bindposes = bindposes;
            }
        }

        private static List<BuiltMaterial> BuildMaterials(J gltf, GltfAccessorReader reader, string outRoot, VrmHairExtractionSettings settings)
        {
            var result = new List<BuiltMaterial>();
            var shader = settings.materialShader != null ? settings.materialShader : FindBestHairShader();

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
                    var tex = LoadTexture(gltf, reader, mj["normalTexture"]["index"].I, outRoot, name + "_Normal", true);
                    if (tex != null) SetTexture(mat, "_BumpMap", "_NormalMap", tex);
                }

                if (mj.Has("emissiveFactor"))
                {
                    var e = mj["emissiveFactor"];
                    SetEmission(mat, new Color(e[0].F, e[1].F, e[2].F, 1));
                }

                string matPath = CombineAsset(outRoot, "Materials/" + name + ".mat");
                AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(matPath));
                result.Add(new BuiltMaterial { material = mat, name = name });
            }
            return result;
        }


        private static Shader FindBestHairShader()
        {
            // User project target: Unity 6 HDRP. Standard renders pink/magenta in SRP projects.
            // Prefer HDRP/Lit. URP/simple fallbacks are kept only for reuse in other projects.
            return Shader.Find("HDRP/Lit")
                ?? Shader.Find("HDRenderPipeline/Lit")
                ?? Shader.Find("CharacterEditor/HairURPUnlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
        }

        private static Texture2D LoadTexture(J gltf, GltfAccessorReader reader, int textureIndex, string outRoot, string name, bool isNormalMap)
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

        private static void BuildNodeHierarchy(List<NodeInfo> nodes, Transform root, VrmHairExtractionSettings settings)
        {
            foreach (var n in nodes)
            {
                n.go = new GameObject(n.name);
            }
            foreach (var n in nodes)
            {
                Transform parent = n.parent >= 0 ? nodes[n.parent].go.transform : root;
                n.go.transform.SetParent(parent, false);
                ApplyNodeTransform(n.go.transform, n, settings);
            }
        }

        private static void ApplyNodeTransform(Transform t, NodeInfo n, VrmHairExtractionSettings settings)
        {
            if (n.hasMatrix)
            {
                Matrix4x4 m = settings.flipXForUnity ? ConvertMatrixFlipX(n.localMatrix) : n.localMatrix;
                t.localPosition = m.GetColumn(3);
                t.localRotation = m.rotation;
                t.localScale = m.lossyScale;
                return;
            }

            t.localPosition = settings.flipXForUnity ? ConvertVec3(n.localPosition, true) : n.localPosition;
            t.localRotation = settings.flipXForUnity ? ConvertQuatFlipX(n.localRotation) : n.localRotation;
            t.localScale = n.localScale;
        }

        private static void ReadNodeTransform(J node, NodeInfo info)
        {
            if (node.Has("matrix") && node["matrix"].Count >= 16)
            {
                var m = new Matrix4x4();
                // glTF stores matrices column-major, same semantic layout as Unity Matrix4x4 indexer [row,column].
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

        private static bool IsHairCandidate(string node, string mesh, string mat, VrmHairExtractionSettings s)
        {
            string text = (node + " " + mesh + " " + mat).ToLowerInvariant();
            foreach (var ex in s.excludeKeywords ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(ex) && text.Contains(ex.ToLowerInvariant())) return false;
            foreach (var inc in s.includeKeywords ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(inc) && text.Contains(inc.ToLowerInvariant())) return true;
            return false;
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

        private static Vector3 ConvertVec3(Vector3 v, bool flipX) => flipX ? new Vector3(-v.x, v.y, v.z) : v;

        private static Quaternion ConvertQuatFlipX(Quaternion q)
        {
            // Coordinate reflection S=diag(-1,1,1): R' = S * R * S.
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }

        private static Matrix4x4 ConvertMatrixFlipX(Matrix4x4 m)
        {
            // Homogeneous S * M * S where S=diag(-1,1,1,1).
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

        private static int[] MakeSequential(int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = i; return a; }

        private static HairSlot GuessSlot(string path, HairSlot fallback)
        {
            string s = path.ToLowerInvariant();
            if (s.Contains("bang" ) || s.Contains("front") || s.Contains("чел")) return HairSlot.Bangs;
            if (s.Contains("ahoge") || s.Contains("アホ毛")) return HairSlot.Ahoge;
            if (s.Contains("sideleft") || s.Contains("side_l")) return HairSlot.SideLeft;
            if (s.Contains("sideright") || s.Contains("side_r")) return HairSlot.SideRight;
            if (s.Contains("side")) return HairSlot.Extra;
            if (s.Contains("back")) return HairSlot.Back;
            if (s.Contains("pony")) return HairSlot.Ponytail;
            return fallback;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
        private static void SetMainTexture(Material m, Texture t)
        {
            // HDRP/Lit
            if (m.HasProperty("_BaseColorMap")) m.SetTexture("_BaseColorMap", t);
            // URP/Lit
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            // Built-in/legacy
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }
        private static void SetTexture(Material m, string a, string b, Texture t)
        {
            // HDRP normal map is _NormalMap. URP/Built-in commonly use _BumpMap.
            if (m.HasProperty("_NormalMap")) m.SetTexture("_NormalMap", t);
            if (m.HasProperty(a)) m.SetTexture(a, t);
            if (m.HasProperty(b)) m.SetTexture(b, t);
        }
        private static void SetEmission(Material m, Color c)
        {
            if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", c); // HDRP
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_').Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }
        private static string CombineAsset(string a, string b) => (a.TrimEnd('/') + "/" + b.TrimStart('/')).Replace('\\', '/');
        private static string ToFsPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
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
