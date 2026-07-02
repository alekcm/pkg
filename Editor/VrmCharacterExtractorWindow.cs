using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.VrmImport
{
    /// <summary>
    /// Editor window: Tools → Character Editor → VRM Character Extractor
    /// 
    /// Reads .vrm files from Raw/VRMHair (or any configured folder),
    /// splits each VRM into layered parts (body, face, clothing layers,
    /// hair parts: front/back/side/ahoge/extra), and saves them as
    /// separate meshes + materials + prefabs.
    /// </summary>
    public sealed class VrmCharacterExtractorWindow : EditorWindow
    {
        private VrmCharacterExtractionSettings settings;
        private Vector2 scrollLog;
        private Vector2 scrollPreview;
        private readonly List<string> log = new();

        // Last extraction result for preview
        private VrmCharacterExtractor.ExtractionResult lastResult;

        [MenuItem("Tools/Character Editor/VRM Character Extractor")]
        public static void Open() => GetWindow<VrmCharacterExtractorWindow>("VRM Character Extractor");

        private void OnGUI()
        {
            // ── Header ──
            EditorGUILayout.HelpBox(
                "VRM Character Extractor\n\n" +
                "Extracts a full VRM character into separate layered parts:\n" +
                "• Body & Face meshes\n" +
                "• Clothing layers: shoes, socks, pants/shorts/skirt, tops, jacket, etc.\n" +
                "• Hair parts: front (bangs), back, side, ahoge, extra\n\n" +
                "Place .vrm files in the Source Folder and click Extract.\n" +
                "Each primitive is classified by material/mesh/node name keywords.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // ── Settings ──
            settings = (VrmCharacterExtractionSettings)EditorGUILayout.ObjectField(
                "Settings", settings, typeof(VrmCharacterExtractionSettings), false);

            if (settings == null)
            {
                if (GUILayout.Button("Create Default Settings Asset"))
                    CreateSettings();
                return;
            }

            // ── Editable fields ──
            EditorGUI.BeginChangeCheck();
            settings.sourceFolder = EditorGUILayout.TextField("Source Folder", settings.sourceFolder);
            settings.outputFolder = EditorGUILayout.TextField("Output Folder", settings.outputFolder);
            settings.materialShader = (Shader)EditorGUILayout.ObjectField(
                "Material Shader", settings.materialShader, typeof(Shader), false);
            settings.importSkinning = EditorGUILayout.Toggle("Import Skinning", settings.importSkinning);
            settings.flipXForUnity = EditorGUILayout.Toggle("Flip X For Unity", settings.flipXForUnity);
            settings.reverseTriangleWinding = EditorGUILayout.Toggle("Reverse Triangles", settings.reverseTriangleWinding);
            settings.writeDiagnostic = EditorGUILayout.Toggle("Write Diagnostic", settings.writeDiagnostic);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(settings);

            EditorGUILayout.Space(8);

            // ── Action buttons ──
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Extract Selected VRM(s)", GUILayout.Height(30)))
                ExtractSelected();
            if (GUILayout.Button("Extract All From Source Folder", GUILayout.Height(30)))
                ExtractFolder();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Repair Generated Materials Shader"))
                RepairMaterials();

            // ── Last extraction preview ──
            if (lastResult != null && lastResult.parts.Count > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Last Extraction Result", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"ID: {lastResult.id}  |  Parts: {lastResult.parts.Count}");

                scrollPreview = EditorGUILayout.BeginScrollView(scrollPreview, GUILayout.MaxHeight(200));
                var grouped = lastResult.parts
                    .GroupBy(p => p.slot)
                    .OrderBy(g => g.Key);
                foreach (var g in grouped)
                {
                    EditorGUILayout.LabelField($"  {g.Key}:", EditorStyles.boldLabel);
                    foreach (var p in g)
                        EditorGUILayout.LabelField($"    {p.partName}  (mat: {(p.material != null ? p.material.name : "—")})");
                }
                EditorGUILayout.EndScrollView();

                if (lastResult.prefab != null)
                {
                    EditorGUILayout.ObjectField("Prefab", lastResult.prefab, typeof(GameObject), false);
                }
            }

            // ── Log ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            scrollLog = EditorGUILayout.BeginScrollView(scrollLog, GUILayout.MinHeight(100));
            foreach (var l in log)
                EditorGUILayout.LabelField(l, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────
        //  Actions
        // ──────────────────────────────────────────────

        private void CreateSettings()
        {
            var s = CreateInstance<VrmCharacterExtractionSettings>();
            EnsureFolder("Assets/Editor");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/VrmCharacterExtractionSettings.asset");
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            settings = s;
            Log("Created settings: " + path);
        }

        private void ExtractSelected()
        {
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".vrm", System.StringComparison.OrdinalIgnoreCase)) continue;
                ExtractOne(path);
            }
        }

        private void ExtractFolder()
        {
            string fs = ToFsPath(settings.sourceFolder);
            if (!Directory.Exists(fs))
            {
                Log("Source folder does not exist: " + settings.sourceFolder);
                return;
            }
            var files = Directory.GetFiles(fs, "*.vrm", SearchOption.AllDirectories);
            Log($"Found {files.Length} .vrm file(s) in {settings.sourceFolder}");
            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = ToAssetPath(files[i]);
                EditorUtility.DisplayProgressBar("VRM Character Extractor", assetPath,
                    files.Length == 0 ? 1 : (float)i / files.Length);
                ExtractOne(assetPath);
            }
            EditorUtility.ClearProgressBar();
        }

        private void ExtractOne(string assetPath)
        {
            try
            {
                var result = VrmCharacterExtractor.Extract(assetPath, settings);
                lastResult = result;

                // Save a CharacterDefinition ScriptableObject
                var charDef = CreateInstance<CharacterDefinition>();
                charDef.characterId = result.id;
                charDef.displayName = Path.GetFileNameWithoutExtension(assetPath);
                charDef.sourceVrmPath = assetPath;
                charDef.prefab = result.prefab;
                foreach (var p in result.parts)
                {
                    charDef.parts.Add(new CharacterPartInfo
                    {
                        slot = p.slot,
                        partName = p.partName,
                        mesh = p.mesh,
                        material = p.material,
                    });
                }
                string outRoot = settings.outputFolder.TrimEnd('/') + "/" + result.id;
                EnsureFolder(outRoot);
                string defPath = outRoot + "/" + result.id + "_CharacterDef.asset";
                var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(defPath);
                if (existing != null)
                {
                    existing.characterId = charDef.characterId;
                    existing.displayName = charDef.displayName;
                    existing.sourceVrmPath = charDef.sourceVrmPath;
                    existing.prefab = charDef.prefab;
                    existing.parts = charDef.parts;
                    EditorUtility.SetDirty(existing);
                    DestroyImmediate(charDef);
                }
                else
                {
                    AssetDatabase.CreateAsset(charDef, defPath);
                }
                AssetDatabase.SaveAssets();

                var grouped = result.parts.GroupBy(p => p.slot).OrderBy(g => g.Key);
                string summary = string.Join(", ", grouped.Select(g => $"{g.Key}:{g.Count()}"));
                Log($"OK: {assetPath} → {result.id} | {result.parts.Count} parts [{summary}]");
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                Log($"ERROR: {assetPath} | {ex.Message}");
            }
        }

        private void RepairMaterials()
        {
            var shader = settings != null && settings.materialShader != null
                ? settings.materialShader
                : Shader.Find("HDRP/Lit")
                  ?? Shader.Find("HDRenderPipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");

            if (shader == null)
            {
                Log("No usable shader found.");
                return;
            }

            string folder = settings != null ? settings.outputFolder : "Assets/Generated/CharacterLibrary";
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            int changed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == shader) continue;

                Texture main = null;
                Color color = Color.white;
                if (mat.HasProperty("_BaseColorMap")) main = mat.GetTexture("_BaseColorMap");
                if (main == null && mat.HasProperty("_BaseMap")) main = mat.GetTexture("_BaseMap");
                if (main == null && mat.HasProperty("_MainTex")) main = mat.GetTexture("_MainTex");
                if (mat.HasProperty("_BaseColor")) color = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");

                mat.shader = shader;
                if (main != null)
                {
                    if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", main);
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", main);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", main);
                }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
                EditorUtility.SetDirty(mat);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Log($"Repaired {changed} material(s), shader={shader.name}");
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        private void Log(string s)
        {
            log.Add(s);
            Debug.Log("[VRM Character Extractor] " + s);
            Repaint();
        }

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

        private static string ToFsPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private static string ToAssetPath(string fsPath)
        {
            fsPath = Path.GetFullPath(fsPath).Replace('\\', '/');
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/') + "/";
            return fsPath.StartsWith(root) ? fsPath.Substring(root.Length) : fsPath;
        }
    }
}
