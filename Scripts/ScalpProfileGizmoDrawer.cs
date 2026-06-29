using CharacterEditor.Hair.Proc;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CharacterEditor.Hair.DebugTools
{
    /// <summary>
    /// Temporary visual debugger for ScalpProfileDefinition.
    /// Attach to the character or any scene object, assign Head Bone and Scalp Profile.
    /// It draws the head mask, hairline landmarks, and sampled scalp zones in Scene view.
    /// </summary>
    [ExecuteAlways]
    public class ScalpProfileGizmoDrawer : MonoBehaviour
    {
        [Header("References")]
        public Transform headBone;
        public ScalpProfileDefinition scalpProfile;

        [Header("Auto-find")]
        public bool autoFindHeadBone = true;
        public bool autoFindProfileFromHair = true;

        [Header("Scale Fix")]
        [Tooltip("Enable when the imported Mixamo/FBX armature has scale 100/100/100. Hair/profile coordinates are authored in compensated head-local meters, so gizmos must use the same compensation as HairRuntimeAttacherProc.")]
        public bool compensateScaledAvatar = true;
        public float scaleCompensationThreshold = 0.01f;

        [Header("Draw toggles")]
        public bool drawGizmos = true;
        public bool drawHeadMask = true;
        public bool drawHairline = true;
        public bool drawZoneSamples = true;
        public bool drawLandmarks = true;
        public bool drawNormals = true;
        public bool drawLabels = true;

        [Header("Style")]
        [Range(4, 64)] public int zoneSamplesPerAxis = 7;
        [Range(0.001f, 0.02f)] public float pointRadius = 0.004f;
        [Range(0.001f, 0.05f)] public float normalLength = 0.018f;
        public Color headMaskColor = new Color(0.2f, 0.65f, 1f, 0.55f);
        public Color hairlineColor = new Color(1f, 0.9f, 0.1f, 1f);
        public Color landmarkColor = new Color(1f, 1f, 1f, 1f);
        public Color normalColor = new Color(1f, 1f, 1f, 0.55f);

        private void Reset()
        {
            TryAutoFind();
        }

        private void OnValidate()
        {
            if (autoFindHeadBone || autoFindProfileFromHair)
                TryAutoFind();
        }

        private void TryAutoFind()
        {
            if (autoFindHeadBone && headBone == null)
                headBone = FindHeadBone(transform);

            if (autoFindProfileFromHair && scalpProfile == null)
            {
                HairRuntimeAttacherProc attacher = GetComponentInChildren<HairRuntimeAttacherProc>(true);
                if (attacher == null)
                    attacher = GetComponentInParent<HairRuntimeAttacherProc>();

                if (attacher != null && attacher.currentPiece != null)
                    scalpProfile = attacher.currentPiece.scalpProfile;
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || scalpProfile == null)
                return;

            if (headBone == null && autoFindHeadBone)
                headBone = FindHeadBone(transform);

            if (headBone == null)
                return;

            if (drawHeadMask && scalpProfile.headMask != null)
                DrawHeadMask(scalpProfile.headMask);

            if (drawHairline)
                DrawHairline();

            if (drawZoneSamples)
                DrawAllZones();

            if (drawLandmarks)
                DrawLandmarks();
        }

        private Vector3 W(Vector3 headLocal)
        {
            Vector3 local = compensateScaledAvatar ? Vector3.Scale(headLocal, GetScaleCompensation()) : headLocal;
            return headBone != null ? headBone.TransformPoint(local) : transform.TransformPoint(local);
        }

        private Vector3 GetScaleCompensation()
        {
            Transform t = headBone != null ? headBone : transform;
            Vector3 s = t.lossyScale;
            return new Vector3(
                NeedsScaleCompensation(s.x) ? 1f / s.x : 1f,
                NeedsScaleCompensation(s.y) ? 1f / s.y : 1f,
                NeedsScaleCompensation(s.z) ? 1f / s.z : 1f
            );
        }

        private bool NeedsScaleCompensation(float scale)
        {
            return Mathf.Abs(scale) > 0.0001f && Mathf.Abs(scale - 1f) > scaleCompensationThreshold;
        }

        private void DrawHeadMask(HeadCollisionMaskDefinition mask)
        {
            Vector3 c = mask.center;
            Vector3 r = mask.SafeRadii;
            Gizmos.color = headMaskColor;

            DrawEllipse(c, r, Axis.XY);
            DrawEllipse(c, r, Axis.XZ);
            DrawEllipse(c, r, Axis.YZ);

#if UNITY_EDITOR
            if (drawLabels)
                Handles.Label(W(c + Vector3.up * (r.y + 0.015f)), "Head collision ellipsoid");
#endif
        }

        private enum Axis { XY, XZ, YZ }

        private void DrawEllipse(Vector3 c, Vector3 r, Axis axis)
        {
            const int steps = 72;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                Vector3 p;
                switch (axis)
                {
                    case Axis.XY:
                        p = c + new Vector3(Mathf.Cos(a) * r.x, Mathf.Sin(a) * r.y, 0f);
                        break;
                    case Axis.XZ:
                        p = c + new Vector3(Mathf.Cos(a) * r.x, 0f, Mathf.Sin(a) * r.z);
                        break;
                    default:
                        p = c + new Vector3(0f, Mathf.Cos(a) * r.y, Mathf.Sin(a) * r.z);
                        break;
                }

                if (i > 0)
                    Gizmos.DrawLine(W(prev), W(p));
                prev = p;
            }
        }

        private void DrawHairline()
        {
            Gizmos.color = hairlineColor;

            // Approximate visible hairline loop: temples -> front -> temples -> side/top -> back crown.
            DrawLocalLine(scalpProfile.leftTemple, scalpProfile.frontCenter);
            DrawLocalLine(scalpProfile.frontCenter, scalpProfile.rightTemple);
            DrawLocalLine(scalpProfile.leftTemple, scalpProfile.leftSideTop);
            DrawLocalLine(scalpProfile.rightTemple, scalpProfile.rightSideTop);
            DrawLocalLine(scalpProfile.leftSideTop, scalpProfile.backCrown + Vector3.left * scalpProfile.backWidth * 0.6f);
            DrawLocalLine(scalpProfile.rightSideTop, scalpProfile.backCrown + Vector3.right * scalpProfile.backWidth * 0.6f);
            DrawLocalLine(scalpProfile.backCrown + Vector3.left * scalpProfile.backWidth * 0.6f, scalpProfile.nape + Vector3.left * scalpProfile.backWidth * 0.45f);
            DrawLocalLine(scalpProfile.backCrown + Vector3.right * scalpProfile.backWidth * 0.6f, scalpProfile.nape + Vector3.right * scalpProfile.backWidth * 0.45f);

#if UNITY_EDITOR
            if (drawLabels)
                Handles.Label(W(scalpProfile.frontCenter + Vector3.up * 0.015f), "hairline / scalp profile");
#endif
        }

        private void DrawLocalLine(Vector3 a, Vector3 b)
        {
            Gizmos.DrawLine(W(a), W(b));
        }

        private void DrawLandmarks()
        {
            DrawLandmark("Crown", scalpProfile.crown);
            DrawLandmark("Front", scalpProfile.frontCenter);
            DrawLandmark("L Temple", scalpProfile.leftTemple);
            DrawLandmark("R Temple", scalpProfile.rightTemple);
            DrawLandmark("L Side", scalpProfile.leftSideTop);
            DrawLandmark("R Side", scalpProfile.rightSideTop);
            DrawLandmark("Back Crown", scalpProfile.backCrown);
            DrawLandmark("Nape", scalpProfile.nape);
        }

        private void DrawLandmark(string label, Vector3 p)
        {
            Gizmos.color = landmarkColor;
            Gizmos.DrawSphere(W(p), pointRadius * 1.35f);
#if UNITY_EDITOR
            if (drawLabels)
                Handles.Label(W(p + Vector3.up * 0.008f), label);
#endif
        }

        private void DrawAllZones()
        {
            DrawZone(ScalpZoneId.Top, new Color(0.2f, 1f, 0.25f, 0.9f), "Top");
            DrawZone(ScalpZoneId.BangsFront, new Color(1f, 0.85f, 0.1f, 0.95f), "BangsFront");
            DrawZone(ScalpZoneId.SideLeftTop, new Color(1f, 0.35f, 0.35f, 0.9f), "SideLeftTop");
            DrawZone(ScalpZoneId.SideRightTop, new Color(0.35f, 0.55f, 1f, 0.9f), "SideRightTop");
            DrawZone(ScalpZoneId.BackCrown, new Color(0.85f, 0.25f, 1f, 0.9f), "BackCrown");
            DrawZone(ScalpZoneId.BraidLeftTop, new Color(1f, 0.55f, 0.15f, 0.9f), "BraidLeftTop");
            DrawZone(ScalpZoneId.BraidRightTop, new Color(0.15f, 1f, 0.9f, 0.9f), "BraidRightTop");
            DrawZone(ScalpZoneId.NapeBack, new Color(0.65f, 0.65f, 0.65f, 0.9f), "NapeBack");
        }

        private void DrawZone(ScalpZoneId zone, Color color, string label)
        {
            Gizmos.color = color;
            int n = Mathf.Max(2, zoneSamplesPerAxis);
            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = n == 1 ? 0f : Mathf.Lerp(-1f, 1f, x / (float)(n - 1));
                    float v = n == 1 ? 0f : Mathf.Lerp(-1f, 1f, y / (float)(n - 1));
                    if (u * u + v * v > 1.05f)
                        continue;

                    Vector3 p = scalpProfile.GetRoot(zone, u, v);
                    Vector3 wp = W(p);
                    Gizmos.DrawSphere(wp, pointRadius);
                    sum += p;
                    count++;

                    if (drawNormals)
                    {
                        Gizmos.color = normalColor;
                        Vector3 nrm = scalpProfile.GetNormal(p);
                        Gizmos.DrawLine(wp, W(p + nrm * normalLength));
                        Gizmos.color = color;
                    }
                }
            }

#if UNITY_EDITOR
            if (drawLabels && count > 0)
            {
                Vector3 center = sum / count;
                Handles.Label(W(center + Vector3.up * 0.012f), label);
            }
#endif
        }

        private static Transform FindHeadBone(Transform root)
        {
            if (root == null)
                return null;

            Transform best = null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "head" || n.EndsWith("_head") || n.Contains("c_head"))
                    return t;
                if (best == null && n.Contains("head"))
                    best = t;
            }
            return best;
        }
    }
}
