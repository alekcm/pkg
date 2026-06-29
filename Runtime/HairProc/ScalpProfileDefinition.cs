using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    public enum ScalpZoneId : byte
    {
        Top = 0,
        BangsFront = 1,
        SideLeftTop = 2,
        SideRightTop = 3,
        BackCrown = 4,
        BraidLeftTop = 5,
        BraidRightTop = 6,
        NapeBack = 7
    }

    /// <summary>
    /// Hair growth/scalp profile built on top of HeadCollisionMaskDefinition.
    /// HeadCollisionMask says "where the skull is"; ScalpProfile says "where hair is allowed to start".
    /// Coordinates are in Head-bone local space.
    /// </summary>
    [CreateAssetMenu(menuName = "Character/Hair Scalp Profile", fileName = "ScalpProfile_")]
    public class ScalpProfileDefinition : ScriptableObject
    {
        [Header("Base head collision")]
        public HeadCollisionMaskDefinition headMask;

        [Header("Surface")]
        [Range(0f, 0.02f)] public float rootSurfaceOffset = 0.004f;

        [Header("Hairline landmarks in head-local space")]
        public Vector3 crown = new Vector3(0f, 0.145f, -0.015f);
        public Vector3 frontCenter = new Vector3(0f, 0.100f, 0.070f);
        public Vector3 leftTemple = new Vector3(-0.070f, 0.078f, 0.045f);
        public Vector3 rightTemple = new Vector3(0.070f, 0.078f, 0.045f);
        public Vector3 leftSideTop = new Vector3(-0.078f, 0.092f, -0.006f);
        public Vector3 rightSideTop = new Vector3(0.078f, 0.092f, -0.006f);
        public Vector3 backCrown = new Vector3(0f, 0.108f, -0.076f);
        public Vector3 nape = new Vector3(0f, 0.018f, -0.078f);

        [Header("Zone spread")]
        [Range(0.005f, 0.08f)] public float frontWidth = 0.060f;
        [Range(0.005f, 0.08f)] public float sideWidth = 0.035f;
        [Range(0.005f, 0.10f)] public float backWidth = 0.075f;
        [Range(0.005f, 0.08f)] public float topRadius = 0.055f;

        public Vector3 GetRoot(ScalpZoneId zone, float u, float v)
        {
            u = Mathf.Clamp(u, -1f, 1f);
            v = Mathf.Clamp(v, -1f, 1f);

            Vector3 candidate;
            switch (zone)
            {
                case ScalpZoneId.BangsFront:
                    candidate = Vector3.Lerp(leftTemple, rightTemple, (u + 1f) * 0.5f);
                    candidate = Vector3.Lerp(candidate, frontCenter + new Vector3(u * frontWidth * 0.35f, 0f, 0f), 0.55f);
                    candidate += new Vector3(0f, v * 0.010f, Mathf.Abs(v) * 0.006f);
                    break;

                case ScalpZoneId.SideLeftTop:
                    candidate = leftSideTop + new Vector3(v * sideWidth * 0.35f, u * sideWidth * 0.40f, u * sideWidth * 0.70f);
                    break;

                case ScalpZoneId.SideRightTop:
                    candidate = rightSideTop + new Vector3(v * sideWidth * 0.35f, u * sideWidth * 0.40f, u * sideWidth * 0.70f);
                    break;

                case ScalpZoneId.BackCrown:
                    candidate = backCrown + new Vector3(u * backWidth, v * 0.018f, v * 0.025f);
                    break;

                case ScalpZoneId.BraidLeftTop:
                    candidate = Vector3.Lerp(leftSideTop, backCrown + Vector3.left * 0.045f, 0.55f)
                              + new Vector3(u * 0.018f, v * 0.012f, v * 0.016f);
                    break;

                case ScalpZoneId.BraidRightTop:
                    candidate = Vector3.Lerp(rightSideTop, backCrown + Vector3.right * 0.045f, 0.55f)
                              + new Vector3(u * 0.018f, v * 0.012f, v * 0.016f);
                    break;

                case ScalpZoneId.NapeBack:
                    candidate = nape + new Vector3(u * backWidth * 0.75f, Mathf.Abs(v) * 0.020f, v * 0.010f);
                    break;

                default:
                    candidate = crown + new Vector3(u * topRadius, -Mathf.Abs(v) * 0.015f, v * topRadius);
                    break;
            }

            return ProjectToSurface(candidate);
        }

        public Vector3 GetNormal(Vector3 headLocalPoint)
        {
            if (headMask == null)
                return Vector3.up;

            Vector3 c = headMask.center;
            Vector3 r = headMask.SafeRadii;
            Vector3 n = new Vector3(
                (headLocalPoint.x - c.x) / Mathf.Max(0.001f, r.x * r.x),
                (headLocalPoint.y - c.y) / Mathf.Max(0.001f, r.y * r.y),
                (headLocalPoint.z - c.z) / Mathf.Max(0.001f, r.z * r.z)
            );
            return n.sqrMagnitude > 0.0001f ? n.normalized : Vector3.up;
        }

        public Vector3 ProjectToSurface(Vector3 candidate)
        {
            if (headMask == null)
                return candidate;

            Vector3 c = headMask.center;
            Vector3 r = headMask.SafeRadii;
            Vector3 q = new Vector3(
                (candidate.x - c.x) / Mathf.Max(0.001f, r.x),
                (candidate.y - c.y) / Mathf.Max(0.001f, r.y),
                (candidate.z - c.z) / Mathf.Max(0.001f, r.z)
            );

            if (q.sqrMagnitude < 0.0001f)
                q = Vector3.up;

            q.Normalize();
            Vector3 surface = c + new Vector3(q.x * r.x, q.y * r.y, q.z * r.z);
            return surface + GetNormal(surface) * rootSurfaceOffset;
        }

#if UNITY_EDITOR
        [ContextMenu("Preset from Head Mask - Anime Default")]
        public void PresetFromMaskAnimeDefault()
        {
            if (headMask != null)
            {
                Vector3 c = headMask.center;
                Vector3 r = headMask.SafeRadii;
                crown = c + new Vector3(0f, r.y * 0.95f, -r.z * 0.10f);
                frontCenter = c + new Vector3(0f, r.y * 0.40f, r.z * 0.92f);
                leftTemple = c + new Vector3(-r.x * 0.86f, r.y * 0.25f, r.z * 0.56f);
                rightTemple = c + new Vector3(r.x * 0.86f, r.y * 0.25f, r.z * 0.56f);
                leftSideTop = c + new Vector3(-r.x * 1.00f, r.y * 0.45f, -r.z * 0.05f);
                rightSideTop = c + new Vector3(r.x * 1.00f, r.y * 0.45f, -r.z * 0.05f);
                backCrown = c + new Vector3(0f, r.y * 0.52f, -r.z * 0.96f);
                nape = c + new Vector3(0f, -r.y * 0.30f, -r.z * 0.96f);
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
