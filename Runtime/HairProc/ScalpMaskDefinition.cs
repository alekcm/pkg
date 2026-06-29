// ScalpMaskDefinition.cs – Unity 6
// Controls WHERE hair grows – shaved sides (Fuyuhiko / Улисс), fade, etc.
// Works with HairBaker – density *= mask.Sample(uv)
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    [CreateAssetMenu(menuName = "Character/Hair Scalp Mask", fileName = "ScalpMask_")]
    public class ScalpMaskDefinition : ScriptableObject
    {
        [Header("Texture – R = density 0..1")]
        public Texture2D densityMask; // 512x512, head UV space – white = full hair, black = shaved
        // UV layout: U = -left..+right (-0.5..0.5 → 0..1), V = neck (0) .. forehead top (1)
        // Paint in Photoshop/Substance: black zones = выбрито

        [Header("Procedural shave – no texture needed – Fuyuhiko / Улисс style")]
        public bool useProceduralSideShave = false;
        [Range(-0.15f,0.15f)] public float leftShaveX = -0.085f;  // X < leftShaveX → 0 density
        [Range(-0.15f,0.15f)] public float rightShaveX = 0.085f;  // X > rightShaveX → 0
        [Range(0f,0.05f)] public float fadeWidth = 0.012f; // soft edge
        public bool shaveBackLower = false; // undercut neck
        [Range(1.55f,1.7f)] public float backShaveY = 1.60f;

        [Header("Dread zones – Desmond Hall")]
        public bool useDreadZones = false;
        // simple: if mask R < 0.3 → skip strand (shaved skin shows)
        // if 0.3..0.7 → dread (thicker, frizz up)
        // >0.7 → normal

        // Runtime sampler – Burst compatible if needed – here simple C#
        public float SampleDensity(Vector3 headLocalPos)
        {
            // headLocalPos – in Head bone local space, origin ~ between eyes
            // X = right(+), left(-), Y = up, Z = forward
            float d = 1f;

            // texture mask first
            if (densityMask != null)
            {
                // map headLocalPos → UV
                // crude spherical head projection – good enough for shave masks
                float u = Mathf.InverseLerp(-0.095f, 0.095f, headLocalPos.x);
                float v = Mathf.InverseLerp(1.55f, 1.78f, headLocalPos.y); // adjust to your head rig
                // side fade toward back
                v = Mathf.Clamp01(v - Mathf.Abs(headLocalPos.z) * 0.9f);
                if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                {
                    // bilinear, no alloc in Burst – here editor/runtime simple
                    d *= densityMask.GetPixelBilinear(u, v).r;
                }
            }

            // procedural side shave – Fuyuhiko / Улисс
            if (useProceduralSideShave)
            {
                // left side
                float leftFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(leftShaveX - fadeWidth, leftShaveX + fadeWidth, headLocalPos.x));
                // right side
                float rightFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rightShaveX - fadeWidth, rightShaveX + fadeWidth, headLocalPos.x));
                d *= Mathf.Min(leftFade, rightFade);

                if (shaveBackLower && headLocalPos.y < backShaveY && headLocalPos.z < -0.02f)
                {
                    float backFade = Mathf.InverseLerp(backShaveY - 0.03f, backShaveY, headLocalPos.y);
                    d *= backFade;
                }
            }
            return Mathf.Clamp01(d);
        }

        // quick presets – call from editor
#if UNITY_EDITOR
        [ContextMenu("Preset – Fuyuhiko Left Shave")]
        void PresetFuyuhiko()
        {
            useProceduralSideShave = true;
            leftShaveX = -0.045f;  // выбрито почти до центра слева
            rightShaveX = 0.095f;  // правая сторона полная
            fadeWidth = 0.008f;
            shaveBackLower = false;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        [ContextMenu("Preset – Ulysses Undercut Both Sides")]
        void PresetUlysses()
        {
            useProceduralSideShave = true;
            leftShaveX = -0.055f;
            rightShaveX = 0.055f;
            fadeWidth = 0.015f;
            shaveBackLower = true;
            backShaveY = 1.62f;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        [ContextMenu("Preset – Desmond Dreads Zones")]
        void PresetDesmond()
        {
            useProceduralSideShave = false;
            useDreadZones = true;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
