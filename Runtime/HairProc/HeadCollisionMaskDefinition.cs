using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    /// <summary>
    /// Lightweight head collision/mask shape in Head-bone local space.
    /// Used by the procedural hair baker to keep generated locks outside the skull.
    /// </summary>
    [CreateAssetMenu(menuName = "Character/Hair Head Collision Mask", fileName = "HeadCollisionMask_")]
    public class HeadCollisionMaskDefinition : ScriptableObject
    {
        [Header("Ellipsoid in Head local space")]
        public Vector3 center = new Vector3(0f, 0.055f, 0f);
        public Vector3 radii = new Vector3(0.075f, 0.105f, 0.085f);

        [Header("Collision")]
        [Tooltip("Extra distance outside the head surface in meters.")]
        [Range(0f, 0.04f)] public float surfacePadding = 0.008f;

        [Tooltip("0 = hard snap to surface, 1 = softer blend. Usually 0.15-0.35 looks better.")]
        [Range(0f, 1f)] public float softness = 0.2f;

        [Tooltip("Only push strand points up to this normalized length. 1 = whole strand, 0.45 = roots/mid only.")]
        [Range(0f, 1f)] public float affectUntilT = 0.9f;

        [Header("Debug")]
        public Bounds sourceHeadLocalBounds;
        public string sourceModelPath;
        public string sourceHeadBoneName;

#if UNITY_EDITOR
        [ContextMenu("Preset - Mixamo/Anime Head Medium")]
        public void PresetMixamoAnimeMedium()
        {
            center = new Vector3(0f, 0.06f, 0f);
            radii = new Vector3(0.08f, 0.115f, 0.08f);
            surfacePadding = 0.012f;
            softness = 0.08f;
            affectUntilT = 1f;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Preset - Larger Safety Mask")]
        public void PresetLargerSafetyMask()
        {
            center = new Vector3(0f, 0.06f, 0f);
            radii = new Vector3(0.095f, 0.13f, 0.095f);
            surfacePadding = 0.015f;
            softness = 0.05f;
            affectUntilT = 1f;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public Vector3 SafeRadii
        {
            get
            {
                return new Vector3(
                    Mathf.Max(0.001f, radii.x + surfacePadding),
                    Mathf.Max(0.001f, radii.y + surfacePadding),
                    Mathf.Max(0.001f, radii.z + surfacePadding)
                );
            }
        }

        public Vector3 PushOutside(Vector3 headLocalPoint, float strandT)
        {
            if (strandT > affectUntilT)
                return headLocalPoint;

            Vector3 r = SafeRadii;
            Vector3 d = headLocalPoint - center;
            Vector3 q = new Vector3(d.x / r.x, d.y / r.y, d.z / r.z);
            float len = q.magnitude;
            if (len >= 1f || len <= 0.00001f)
                return headLocalPoint;

            Vector3 surface = center + new Vector3(q.x / len * r.x, q.y / len * r.y, q.z / len * r.z);
            float blend = Mathf.Lerp(1f, Mathf.Clamp01(1f - len), softness);
            return Vector3.Lerp(headLocalPoint, surface, blend);
        }
    }
}
