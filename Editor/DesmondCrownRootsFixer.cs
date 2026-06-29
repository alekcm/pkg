#if UNITY_EDITOR
using System.Collections.Generic;
using CharacterEditor.Hair;
using CharacterEditor.Hair.Proc;
using UnityEditor;
using UnityEngine;

namespace CharacterEditor.Hair.EditorTool
{
    public static class DesmondCrownRootsFixer
    {
        [MenuItem("Tools/Character/Hair/Fix Desmond Dreads Roots To Crown")]
        public static void FixDesmondRootsToCrown()
        {
            string[] guids = AssetDatabase.FindAssets("t:HairPieceDefinitionProc desmond_hall_dreads_proc");
            HairPieceDefinitionProc piece = null;
            string piecePath = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                HairPieceDefinitionProc p = AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (p != null && (p.id == "desmond_hall_dreads_proc" || p.name.ToLowerInvariant().Contains("desmond")))
                {
                    piece = p;
                    piecePath = path;
                    break;
                }
            }

            if (piece == null)
            {
                EditorUtility.DisplayDialog("Desmond Crown Roots", "Could not find desmond_hall_dreads_proc HairPieceDefinitionProc. Generate Story Hair Presets first.", "OK");
                return;
            }

            HeadCollisionMaskDefinition existingMask = piece.headCollisionMask;
            Material existingMaterial = piece.hairMaterial;
            HairPieceDefinition legacy = piece.legacyVrmPiece;

            ApplyCrownDreads(piece);
            piece.headCollisionMask = existingMask;
            piece.hairMaterial = existingMaterial;
            piece.legacyVrmPiece = legacy;

            EditorUtility.SetDirty(piece);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(piece);
            Debug.Log($"[DesmondCrownRootsFixer] Updated crown-rooted dreads: {piecePath}", piece);
        }

        private static void ApplyCrownDreads(HairPieceDefinitionProc hp)
        {
            hp.id = "desmond_hall_dreads_proc";
            hp.displayName = "desmond_hall_dreads_proc";
            hp.slot = HairSlot.Back;

            var guides = new List<HairGuide>();
            var rnd = new System.Random(4419);
            int dreadCount = 46;
            float crownRadiusX = 0.052f;
            float crownRadiusZ = 0.060f;

            for (int i = 0; i < dreadCount; i++)
            {
                float u = (float)rnd.NextDouble();
                float v = (float)rnd.NextDouble();
                float th = u * Mathf.PI * 2f;
                float rr = Mathf.Sqrt(v);

                float x = Mathf.Cos(th) * crownRadiusX * rr;
                float z = Mathf.Sin(th) * crownRadiusZ * rr - 0.010f;
                float y = 0.145f - rr * rr * 0.020f + (float)(rnd.NextDouble() - 0.5f) * 0.008f;
                Vector3 rootPos = new Vector3(x, y, z);

                var g = HairGuide.CreateDefault("c_head", rootPos, 0.38f);
                g.thicknessRoot = 0.014f;
                g.thicknessTip = 0.010f;
                g.sideCount = 6;
                g.groupId = 3;

                Vector3 radial = new Vector3(rootPos.x / crownRadiusX, 0f, (rootPos.z + 0.010f) / crownRadiusZ);
                if (radial.sqrMagnitude < 0.08f)
                    radial = Vector3.back;
                radial.Normalize();

                Vector3 sideBias = new Vector3(Mathf.Sign(rootPos.x) * 0.018f, 0f, 0f);
                if (Mathf.Abs(rootPos.x) < 0.012f)
                    sideBias = Vector3.zero;

                Vector3 backBias = Vector3.back * Mathf.Lerp(0.015f, 0.070f, Mathf.InverseLerp(0.04f, -0.06f, rootPos.z));
                Vector3 faceAvoid = rootPos.z > 0.020f ? Vector3.back * 0.050f : Vector3.zero;
                float sideJitter = ((float)rnd.NextDouble() - 0.5f) * 0.010f;
                Vector3 jitter = new Vector3(sideJitter, 0f, ((float)rnd.NextDouble() - 0.5f) * 0.008f);

                g.pointsLocal = new Vector3[]
                {
                    Vector3.zero,
                    radial * 0.030f + sideBias * 0.5f + backBias * 0.3f + faceAvoid * 0.4f + Vector3.down * 0.055f + jitter * 0.3f,
                    radial * 0.060f + sideBias       + backBias * 0.8f + faceAvoid * 0.8f + Vector3.down * 0.190f + jitter * 0.7f,
                    radial * 0.075f + sideBias * 1.4f + backBias       + faceAvoid       + Vector3.down * 0.400f + jitter
                };

                for (int p = 1; p < g.pointsLocal.Length; p++)
                {
                    g.pointsLocal[p].x += ((float)rnd.NextDouble() - 0.5f) * 0.005f;
                    g.pointsLocal[p].z += ((float)rnd.NextDouble() - 0.5f) * 0.005f;
                }

                guides.Add(g);
            }

            hp.guides = guides.ToArray();
            hp.defaultLength = 1f;
            hp.defaultDensity = 1f;
            hp.defaultThickness = 1f;
            hp.defaultCurl = 0.05f;
            hp.defaultWave = 0.05f;
            hp.defaultFrizz = 0.35f;
            hp.defaultColor = new HairColorGradient
            {
                rootColor = new Color(0.18f, 0.09f, 0.045f, 1),
                tipColor = new Color(0.38f, 0.21f, 0.11f, 1),
                rootFade = 0.4f,
                highlightColor = new Color(0.55f, 0.32f, 0.18f, 1),
                highlightStrength = 0.1f
            };
            hp.defaultBraid = new BraidProfile { type = BraidType.Dreadlock, crossings = 0, strandRadius = 0.009f, tightness = 0.4f, taperEnd = false };
        }
    }
}
#endif
