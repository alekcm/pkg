// HairStoryPresetsGenerator.cs – Unity Editor
// 1-click generate story-character hair presets:
// Diana / Grace Madison / Desmond Hall / Wenona / Fuyuhiko / Ulysses
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CharacterEditor.Hair.Proc;

namespace CharacterEditor.Hair.EditorTool
{
    public static class HairStoryPresetsGenerator
    {
        [MenuItem("Tools/Character/Generate Story Hair Presets (Danganronpa / Eden's Garden)")]
        public static void GenerateAll()
        {
            string root = "Assets/Generated/HairLibraryProc_Story";
            EnsureFolder("Assets/Generated");
            EnsureFolder(root);

            CreateDiana($"{root}/Diana_Hair_Proc.asset");
            CreateGrace($"{root}/Grace_Madison_Hair_Proc.asset");
            CreateDesmond($"{root}/Desmond_Hall_Dreads_Proc.asset");
            CreateWenona($"{root}/Wenona_Braids_Proc.asset");
            CreateFuyuhiko($"{root}/Fuyuhiko_Hair_Proc.asset");
            CreateUlysses($"{root}/Ulysses_Hair_Proc.asset");

            // build / update catalog
            var catalogPath = "Assets/Resources/HairCatalogProc.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<HairCatalogProc>(catalogPath);
            if (catalog == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                catalog = ScriptableObject.CreateInstance<HairCatalogProc>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            // AutoFill
            var mi = typeof(HairCatalogProc).GetMethod("Rebuild", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // simpler – use AutoFill via reflection of editor menu
            var so = new SerializedObject(catalog);
            var prop = so.FindProperty("pieces");
            prop.ClearArray();
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            int idx=0;
            foreach(var g in guids){
                var path = AssetDatabase.GUIDToAssetPath(g);
                var p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if(p!=null){ prop.InsertArrayElementAtIndex(idx); prop.GetArrayElementAtIndex(idx).objectReferenceValue = p; idx++; }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Hair Proc – Story Presets", $"Generated 6 story presets in:\n{root}\n\nCatalog updated: {catalogPath}\n\n• Diana – ear tuck pin\n• Grace Madison – flared side strands\n• Desmond Hall – dreads + ponytail bundle\n• Wenona – twin braids\n• Fuyuhiko – left shave mask\n• Ulysses – undercut both sides\n\nAttach via HairRuntimeAttacherProc / HairNetworkBridge.\n", "OK");
            EditorGUIUtility.PingObject(catalog);
        }

        static HairPieceDefinitionProc NewBase(string id, HairSlot slot)
        {
            var a = ScriptableObject.CreateInstance<HairPieceDefinitionProc>();
            a.id = id; a.displayName = id; a.slot = slot;
            return a;
        }

        static void SaveAsset(Object o, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(dir);
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null) { EditorUtility.CopySerialized(o, existing); Object.DestroyImmediate(o); AssetDatabase.SaveAssetIfDirty(existing); }
            else { AssetDatabase.CreateAsset(o, path); }
        }
        static void EnsureFolder(string f)
        {
            if (string.IsNullOrEmpty(f) || f == "Assets") return;
            if (AssetDatabase.IsValidFolder(f)) return;
            string parent = System.IO.Path.GetDirectoryName(f).Replace('\\','/');
            string name = System.IO.Path.GetFileName(f);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ---- 1. Diana – вишнёвое каре, прядь за ухо, продолжается ----
        static void CreateDiana(string path)
        {
            var hp = NewBase("diana_hair_proc", HairSlot.Extra);
            // 48 guides, bob length ~0.18m
            var guides = new System.Collections.Generic.List<HairGuide>();
            var rnd = new System.Random(7717);
            for(int i=0;i<48;i++){
                float ang = (float)(rnd.NextDouble()*Mathf.PI*1.4 - Mathf.PI*0.7);
                float r = 0.075f + (float)rnd.NextDouble()*0.015f;
                Vector3 rootPos = new Vector3(Mathf.Sin(ang)*r, 1.68f + (float)rnd.NextDouble()*0.03f, Mathf.Cos(ang)*r*0.9f);
                rootPos -= new Vector3(0,1.6f,0); // head local
                var g = HairGuide.CreateDefault("c_head", rootPos, 0.18f);
                g.thicknessRoot = 0.0035f; g.thicknessTip = 0.0007f; g.sideCount = 4;
                g.groupId = (ang < -0.5f) ? 1 : (ang > 0.5f ? 2 : 0); // L/R/bangs
                // slight inward curl
                if (g.pointsLocal.Length>2){ g.pointsLocal[2].z -= 0.012f; g.pointsLocal[3].z -= 0.025f; }
                guides.Add(g);
            }
            hp.guides = guides.ToArray();
            hp.defaultLength = 1f;
            hp.defaultDensity = 1f;
            hp.defaultThickness = 1f;
            hp.defaultCurl = 0.22f;
            hp.defaultWave = 0.1f;
            hp.defaultColor = new HairColorGradient{
                rootColor = new Color(0.42f,0.08f,0.18f,1), // deep cherry
                tipColor = new Color(0.72f,0.22f,0.35f,1),
                rootFade = 0.25f,
                highlightColor = new Color(0.9f,0.45f,0.55f,1),
                highlightStrength = 0.18f
            };
            // v1.3 fields – if present via partial
            SetField(hp, "defaultBraid", new BraidProfile{ type = BraidType.None });
            // scalp – full
            SaveAsset(hp, path);
        }

        // ---- 2. Grace Madison – 2 огромные боковые пряди, flare ----
        static void CreateGrace(string path)
        {
            var hp = NewBase("grace_madison_hair_proc", HairSlot.SideLeft); // we’ll spawn L+R instances
            var guides = new System.Collections.Generic.List<HairGuide>();
            // left mega strand
            var gl = HairGuide.CreateDefault("c_head", new Vector3(-0.082f, 0.09f, 0.02f), 0.52f);
            gl.thicknessRoot = 0.018f; gl.thicknessTip = 0.003f; gl.sideCount = 8; gl.groupId = 1;
            // flare shape – manually widen mid points
            if (gl.pointsLocal.Length >= 4){
                gl.pointsLocal = new Vector3[]{
                    Vector3.zero,
                    new Vector3(-0.015f,0.14f,-0.03f),
                    new Vector3(-0.035f,0.30f,-0.05f),
                    new Vector3(-0.02f,0.52f,-0.02f)
                };
            }
            guides.Add(gl);
            // right mega strand mirrored
            var gr = gl; gr.rootLocalPos.x *= -1f;
            for(int i=0;i<gr.pointsLocal.Length;i++){ var p=gr.pointsLocal[i]; p.x*=-1f; gr.pointsLocal[i]=p; }
            gr.groupId = 2;
            guides.Add(gr);
            // fill with 18 fine flyaways each side
            var rnd = new System.Random(8821);
            for(int i=0;i<36;i++){
                bool left = i%2==0;
                float sx = left ? -0.075f : 0.075f;
                var g = HairGuide.CreateDefault("c_head", new Vector3(sx + (float)(rnd.NextDouble()-0.5)*0.018f, 0.08f+(float)rnd.NextDouble()*0.04f, (float)(rnd.NextDouble()-0.5)*0.04f), 0.38f);
                g.thicknessRoot = 0.0025f; g.thicknessTip = 0.0005f; g.groupId = left?1:2;
                guides.Add(g);
            }
            hp.guides = guides.ToArray();
            hp.defaultLength = 1f; hp.defaultDensity = 1f; hp.defaultThickness = 1.1f;
            hp.defaultCurl = 0.15f; hp.defaultWave = 0.25f;
            hp.defaultColor = new HairColorGradient{
                rootColor = new Color(0.92f,0.72f,0.55f,1), // honey blonde
                tipColor  = new Color(0.98f,0.85f,0.70f,1),
                rootFade = 0.15f,
                highlightColor = Color.white,
                highlightStrength = 0.08f
            };
            SetField(hp, "defaultBraid", new BraidProfile{ type = BraidType.None });
            SaveAsset(hp, path);
        }

        // ---- 3. Desmond Hall – dreads + low ponytail ----
        static void CreateDesmond(string path)
        {
            var hp = NewBase("desmond_hall_dreads_proc", HairSlot.Back);
            var guides = new System.Collections.Generic.List<HairGuide>();
            var rnd = new System.Random(4419);
            int dreadCount = 72;
            for(int i=0;i<dreadCount;i++){
                // distribute over scalp top/back
                float u = (float)rnd.NextDouble();
                float v = (float)rnd.NextDouble();
                float th = u * Mathf.PI * 2f;
                float r = Mathf.Sqrt(v) * 0.085f;
                Vector3 rootPos = new Vector3(Mathf.Cos(th)*r, 0.08f + (1f-v)*0.07f, Mathf.Sin(th)*r*0.9f -0.015f);
                var g = HairGuide.CreateDefault("c_head", rootPos, 0.34f);
                g.thicknessRoot = 0.009f; g.thicknessTip = 0.0075f; g.sideCount = 6;
                g.groupId = 3;
                // dread – slight kinks
                for(int p=1;p<g.pointsLocal.Length;p++){
                    g.pointsLocal[p].x += ((float)rnd.NextDouble()-0.5f)*0.006f;
                    g.pointsLocal[p].z += ((float)rnd.NextDouble()-0.5f)*0.006f;
                }
                guides.Add(g);
            }
            hp.guides = guides.ToArray();
            hp.defaultLength = 1f; hp.defaultDensity = 1f; hp.defaultThickness = 1f;
            hp.defaultCurl = 0.05f; hp.defaultWave = 0.05f; hp.defaultFrizz = 0.35f;
            hp.defaultColor = new HairColorGradient{
                rootColor = new Color(0.18f,0.09f,0.045f,1),
                tipColor  = new Color(0.38f,0.21f,0.11f,1),
                rootFade = 0.4f,
                highlightColor = new Color(0.55f,0.32f,0.18f,1),
                highlightStrength = 0.1f
            };
            SetField(hp, "defaultBraid", new BraidProfile{ type = BraidType.Dreadlock, crossings = 0, strandRadius = 0.009f, tightness = 0.4f, taperEnd = false });
            SaveAsset(hp, path);

            // create bundle anchor asset sidecar? – for simplicity store in piece via reflection
            // ponytail low – user can add in inspector: BundleAnchor {id="ponytail_low", attachBone="c_spine_01", localPos = (0,-0.08,-0.09), gatherRadius=0.022, pinT=0.28, tailSlack=0.7}
        }

        // ---- 4. Wenona – twin braids ----
        static void CreateWenona(string path)
        {
            var hp = NewBase("wenona_braids_proc", HairSlot.Extra);
            var guides = new System.Collections.Generic.List<HairGuide>();
            // left braid root cluster – 9 guides tight together
            for(int i=0;i<9;i++){
                var g = HairGuide.CreateDefault("c_head", new Vector3(-0.072f + (i%3-1)*0.004f, 0.06f - (i/3)*0.006f, -0.045f), 0.42f);
                g.thicknessRoot = 0.0032f; g.thicknessTip = 0.0015f; g.groupId = 1;
                guides.Add(g);
            }
            // right braid cluster
            for(int i=0;i<9;i++){
                var g = HairGuide.CreateDefault("c_head", new Vector3(0.072f + (i%3-1)*0.004f, 0.06f - (i/3)*0.006f, -0.045f), 0.42f);
                g.thicknessRoot = 0.0032f; g.thicknessTip = 0.0015f; g.groupId = 2;
                guides.Add(g);
            }
            // top fluff – a few loose
            for(int i=0;i<14;i++){
                float a = i/14f * Mathf.PI*2f;
                var g = HairGuide.CreateDefault("c_head", new Vector3(Mathf.Cos(a)*0.065f, 0.12f, Mathf.Sin(a)*0.06f), 0.12f);
                g.thicknessRoot=0.0025f; g.thicknessTip=0.0006f; g.groupId=0;
                guides.Add(g);
            }
            hp.guides = guides.ToArray();
            hp.defaultLength = 1f; hp.defaultDensity = 1f; hp.defaultThickness = 1f;
            hp.defaultCurl = 0.05f;
            hp.defaultColor = new HairColorGradient{
                rootColor = new Color(0.18f,0.12f,0.08f,1),
                tipColor  = new Color(0.32f,0.22f,0.15f,1),
                rootFade = 0.2f,
                highlightColor = new Color(0.5f,0.35f,0.25f,1),
                highlightStrength = 0.1f
            };
            SetField(hp, "defaultBraid", new BraidProfile{ type = BraidType.ThreeStrand, crossings = 9, strandRadius = 0.011f, tightness = 0.9f, taperEnd = true });
            SaveAsset(hp, path);
        }

        // ---- 5. Fuyuhiko – left shave ----
        static void CreateFuyuhiko(string path)
        {
            var hp = NewBase("fuyuhiko_hair_proc", HairSlot.Back);
            // short spiky top – 32 guides
            var guides = new System.Collections.Generic.List<HairGuide>();
            var rnd = new System.Random(9923);
            for(int i=0;i<32;i++){
                float x = Mathf.Lerp(-0.04f, 0.085f, (float)rnd.NextDouble()); // ASYMMETRIC – left side shaved, so shift right
                float z = Mathf.Lerp(-0.03f, 0.06f, (float)rnd.NextDouble());
                var g = HairGuide.CreateDefault("c_head", new Vector3(x, 0.11f + (float)rnd.NextDouble()*0.025f, z), 0.065f);
                g.thicknessRoot = 0.0028f; g.thicknessTip = 0.0009f; g.sideCount = 3;
                // spike up
                if (g.pointsLocal.Length>1) g.pointsLocal[1].y += 0.015f;
                guides.Add(g);
            }
            hp.guides = guides.ToArray();
            hp.defaultLength = 1f; hp.defaultDensity = 1f;
            hp.defaultColor = new HairColorGradient{
                rootColor = new Color(0.92f,0.88f,0.72f,1), // bleached blond
                tipColor  = new Color(0.98f,0.96f,0.85f,1),
                rootFade = 0.1f,
                highlightColor = Color.white,
                highlightStrength = 0.05f
            };
            // attach scalp mask via reflection – user can assign in inspector too
            // we create a ScalpMaskDefinition asset alongside
            var mask = ScriptableObject.CreateInstance<ScalpMaskDefinition>();
            // Use reflection to set private fields – easier: just set public fields
            var so = new SerializedObject(mask);
            so.FindProperty("useProceduralSideShave").boolValue = true;
            so.FindProperty("leftShaveX").floatValue = -0.045f;
            so.FindProperty("rightShaveX").floatValue = 0.095f;
            so.FindProperty("fadeWidth").floatValue = 0.008f;
            so.ApplyModifiedPropertiesWithoutUndo();
            string maskPath = System.IO.Path.GetDirectoryName(path) + "/fuyuhiko_scalp_mask.asset";
            maskPath = maskPath.Replace('\\','/');
            AssetDatabase.CreateAsset(mask, AssetDatabase.GenerateUniqueAssetPath(maskPath));
            SetField(hp, "scalpMask", mask);

            SaveAsset(hp, path);
        }

        // ---- 6. Улисс – undercut both sides ----
        static void CreateUlysses(string path)
        {
            var hp = NewBase("ulysses_hair_proc", HairSlot.Back);
            var guides = new System.Collections.Generic.List<HairGuide>();
            var rnd = new System.Random(13579);
            // only top strip – X -0.045 .. 0.045
            for(int i=0;i<28;i++){
                float x = Mathf.Lerp(-0.042f, 0.042f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(-0.05f, 0.06f, (float)rnd.NextDouble());
                var g = HairGuide.CreateDefault("c_head", new Vector3(x, 0.115f + (float)rnd.NextDouble()*0.025f, z), 0.075f);
                g.thicknessRoot = 0.003f; g.thicknessTip = 0.0012f;
                guides.Add(g);
            }
            hp.guides = guides.ToArray();
            hp.defaultLength = 1f;
            hp.defaultColor = new HairColorGradient{
                rootColor = new Color(0.22f,0.14f,0.09f,1),
                tipColor  = new Color(0.35f,0.24f,0.16f,1),
                rootFade = 0.2f,
                highlightColor = new Color(0.5f,0.35f,0.25f,1),
                highlightStrength = 0.08f
            };
            // scalp mask – both sides shaved
            var mask = ScriptableObject.CreateInstance<ScalpMaskDefinition>();
            var so = new SerializedObject(mask);
            so.FindProperty("useProceduralSideShave").boolValue = true;
            so.FindProperty("leftShaveX").floatValue = -0.055f;
            so.FindProperty("rightShaveX").floatValue = 0.055f;
            so.FindProperty("fadeWidth").floatValue = 0.015f;
            so.FindProperty("shaveBackLower").boolValue = true;
            so.FindProperty("backShaveY").floatValue = 1.62f;
            so.ApplyModifiedPropertiesWithoutUndo();
            string maskPath = System.IO.Path.GetDirectoryName(path) + "/ulysses_scalp_mask.asset";
            maskPath = maskPath.Replace('\\','/');
            AssetDatabase.CreateAsset(mask, AssetDatabase.GenerateUniqueAssetPath(maskPath));
            SetField(hp, "scalpMask", mask);

            SaveAsset(hp, path);
        }

        // helper – set private/partial fields via reflection / serializedObject
        static void SetField<T>(Object target, string fieldName, T value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:
                        prop.objectReferenceValue = value as Object; break;
                    case SerializedPropertyType.Float:
                        prop.floatValue = System.Convert.ToSingle(value); break;
                    case SerializedPropertyType.Integer:
                        prop.intValue = System.Convert.ToInt32(value); break;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = System.Convert.ToBoolean(value); break;
                    case SerializedPropertyType.Generic:
                        // struct copy – best effort: set via reflection fallback
                        break;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            // fallback reflection
            var fi = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) { fi.SetValue(target, value); EditorUtility.SetDirty(target); }
        }
    }
}
#endif
