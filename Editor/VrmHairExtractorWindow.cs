using System.Collections.Generic;
using System.IO;
using CharacterEditor.Hair;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorImport
{
    public sealed class VrmHairExtractorWindow : EditorWindow
    {
        private VrmHairExtractionSettings settings;
        private Vector2 scroll;
        private readonly List<string> log = new();

        [MenuItem("Tools/Character Editor/VRM Hair Extractor")]
        public static void Open() => GetWindow<VrmHairExtractorWindow>("VRM Hair Extractor");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Recommended export: VRM 0.x from VRoid Studio. VRM 1.0 can be parsed too, but this first importer focuses on glTF meshes/textures and VRM 0.x/MToon-style material names. Export one hair part on a no-hair base avatar per .vrm.",
                MessageType.Info);

            settings = (VrmHairExtractionSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(VrmHairExtractionSettings), false);
            if (settings == null)
            {
                if (GUILayout.Button("Create Default Settings Asset")) CreateSettings();
                return;
            }

            settings.sourceFolder = EditorGUILayout.TextField("Source Folder", settings.sourceFolder);
            settings.outputFolder = EditorGUILayout.TextField("Output Folder", settings.outputFolder);
            settings.materialShader = (Shader)EditorGUILayout.ObjectField("Material Shader", settings.materialShader, typeof(Shader), false);
            settings.importSkinning = EditorGUILayout.Toggle("Import Skinning", settings.importSkinning);
            settings.flipXForUnity = EditorGUILayout.Toggle("Flip X For Unity", settings.flipXForUnity);
            settings.reverseTriangleWinding = EditorGUILayout.Toggle("Reverse Triangles", settings.reverseTriangleWinding);
            settings.writeDiagnosticJson = EditorGUILayout.Toggle("Write Diagnostic", settings.writeDiagnosticJson);

            EditorGUILayout.Space();
            if (GUILayout.Button("Extract Selected VRM(s)")) ExtractSelected();
            if (GUILayout.Button("Rebuild Hair Library From Source Folder")) RebuildFolder();
            if (GUILayout.Button("Repair Generated Materials Shader")) RepairGeneratedMaterials();

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var l in log) EditorGUILayout.LabelField(l, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void CreateSettings()
        {
            var s = CreateInstance<VrmHairExtractionSettings>();
            EnsureFolder("Assets/Editor");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/VrmHairExtractionSettings.asset");
            AssetDatabase.CreateAsset(s, path);
            AssetDatabase.SaveAssets();
            settings = s;
        }

        private void ExtractSelected()
        {
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!path.EndsWith(".vrm", System.StringComparison.OrdinalIgnoreCase)) continue;
                ExtractOne(path);
            }
            BuildCatalog();
        }

        private void RebuildFolder()
        {
            string fs = ToFsPath(settings.sourceFolder);
            if (!Directory.Exists(fs))
            {
                Log("Source folder does not exist: " + settings.sourceFolder);
                return;
            }
            var files = Directory.GetFiles(fs, "*.vrm", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = ToAssetPath(files[i]);
                EditorUtility.DisplayProgressBar("VRM Hair Extractor", assetPath, files.Length == 0 ? 1 : (float)i / files.Length);
                ExtractOne(assetPath);
            }
            EditorUtility.ClearProgressBar();
            BuildCatalog();
        }

        private void ExtractOne(string assetPath)
        {
            try
            {
                var def = VrmHairExtractor.Extract(assetPath, settings);
                Log("OK: " + assetPath + " -> " + def.name);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                Log("ERROR: " + assetPath + " | " + ex.Message);
            }
        }


        private void RepairGeneratedMaterials()
        {
            var shader = settings != null && settings.materialShader != null
                ? settings.materialShader
                : Shader.Find("HDRP/Lit")
                  ?? Shader.Find("HDRenderPipeline/Lit")
                  ?? Shader.Find("CharacterEditor/HairURPUnlit")
                  ?? Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Standard");

            if (shader == null)
            {
                Log("No usable shader found. Check that HairURPUnlit.shader is copied into Assets.");
                return;
            }

            string folder = settings != null ? settings.outputFolder : "Assets/Generated/HairLibrary";
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            int changed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                Texture main = null;
                Color color = Color.white;
                if (mat.HasProperty("_BaseColorMap")) main = mat.GetTexture("_BaseColorMap");
                if (main == null && mat.HasProperty("_BaseMap")) main = mat.GetTexture("_BaseMap");
                if (main == null && mat.HasProperty("_MainTex")) main = mat.GetTexture("_MainTex");
                if (mat.HasProperty("_BaseColor")) color = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");

                if (mat.shader != shader)
                {
                    mat.shader = shader;
                    if (main != null)
                    {
                        if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", main);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", main);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", main);
                    }
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                    // HDRP/Lit opaque defaults. Keeps generated materials visible without manual setup.
                    if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 0f);
                    if (mat.HasProperty("_AlphaCutoffEnable")) mat.SetFloat("_AlphaCutoffEnable", 0f);
                    if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
                    EditorUtility.SetDirty(mat);
                    changed++;
                }
            }
            AssetDatabase.SaveAssets();
            Log($"Repaired material shaders: {changed}, shader={shader.name}");
        }

        private void BuildCatalog()
        {
            EnsureFolder(settings.outputFolder);
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinition", new[] { settings.outputFolder });
            var catalog = CreateInstance<HairCatalog>();
            foreach (string guid in guids)
            {
                var def = AssetDatabase.LoadAssetAtPath<HairPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !catalog.pieces.Contains(def)) catalog.pieces.Add(def);
            }
            string path = settings.outputFolder.TrimEnd('/') + "/HairCatalog.asset";
            var old = AssetDatabase.LoadAssetAtPath<HairCatalog>(path);
            if (old != null)
            {
                old.pieces = catalog.pieces;
                EditorUtility.SetDirty(old);
                DestroyImmediate(catalog);
            }
            else AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
            Log("Catalog updated: " + path + " pieces=" + catalog.pieces.Count);
        }

        private void Log(string s)
        {
            log.Add(s);
            Debug.Log("[VRM Hair Extractor] " + s);
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
        private static string ToFsPath(string assetPath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        private static string ToAssetPath(string fsPath)
        {
            fsPath = Path.GetFullPath(fsPath).Replace('\\', '/');
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/') + "/";
            return fsPath.StartsWith(root) ? fsPath.Substring(root.Length) : fsPath;
        }
    }
}
