// DanganronpaStylizer.cs
// Runtime/Editor utility to instantly switch character & clothing materials
// into the highly refined, dazzling white Danganronpa 2.5D Toon Hatching style.

using System.Collections.Generic;
using UnityEngine;

namespace XWearImporter
{
    [ExecuteAlways]
    [AddComponentMenu("XWear/Danganronpa Stylizer")]
    public class DanganronpaStylizer : MonoBehaviour
    {
        [Tooltip("Enable to switch all child SkinnedMeshRenderer and MeshRenderer materials to the custom Danganronpa Toon Hatching shader.")]
        public bool enableDanganronpaStyle = true;

        [Header("Lighting & Brightness")]
        [Range(0.5f, 2.5f)] public float lightIntensity = 1.25f;
        [Range(-1f, 1f)] public float shadowThreshold = -0.05f;
        [Range(0.01f, 1f)] public float shadowSmoothness = 0.1f;

        [Header("Hatching & Shadow Style")]
        public Color globalShadeColor = new Color(0.78f, 0.74f, 0.76f, 1f);
        [Range(10f, 600f)] public float hatchingDensity = 250f;
        [Range(0.01f, 1f)] public float hatchingSharpness = 0.12f;
        [Range(0f, 1f)] public float hatchingBlend = 0.65f;

        [Header("Sprite Outline")]
        public Color outlineColor = new Color(0.1f, 0.05f, 0.05f, 1f);
        [Range(0f, 0.02f)] public float outlineWidth = 0.002f;

        [Header("Scene Sun")]
        [Tooltip("Optionally assign a Directional Light to act as the scene sun.")]
        public Light sceneSun;

        private readonly Dictionary<Material, Shader> _originalShaders = new Dictionary<Material, Shader>();
        private Shader _danganronpaShader;

        void OnEnable()
        {
            _danganronpaShader = Shader.Find("XWear/DanganronpaCloth");
            ApplyStyle();
        }

        void OnDisable()
        {
            RestoreOriginalShaders();
        }

        void OnValidate()
        {
            if (enableDanganronpaStyle) ApplyStyle();
            else RestoreOriginalShaders();
        }

        public void ApplyStyle()
        {
            if (!enableDanganronpaStyle || _danganronpaShader == null) return;

            var renderers = new List<Renderer>();
            renderers.AddRange(this.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            renderers.AddRange(this.GetComponentsInChildren<MeshRenderer>(true));

            foreach (var r in renderers)
            {
                if (r.sharedMaterials == null) continue;

                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;

                    if (!_originalShaders.ContainsKey(mat))
                    {
                        _originalShaders[mat] = mat.shader;
                    }

                    if (mat.shader != _danganronpaShader)
                    {
                        mat.shader = _danganronpaShader;
                    }

                    // Copy Texture references flawlessly
                    Texture tex = mat.GetTexture("_BaseColorMap");
                    if (tex == null) tex = mat.GetTexture("_BaseMap");
                    if (tex == null) tex = mat.GetTexture("_MainTex");

                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex", tex);
                        mat.SetTexture("_BaseColorMap", tex);
                    }
                    
                    Color col = Color.white;
                    if (mat.HasProperty("_BaseColor")) col = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color")) col = mat.GetColor("_Color");
                    mat.SetColor("_BaseColor", col);

                    // Apply refined knobs
                    mat.SetFloat("_LightIntensity", lightIntensity);
                    mat.SetFloat("_ShadowThreshold", shadowThreshold);
                    mat.SetFloat("_ShadowSmoothness", shadowSmoothness);
                    mat.SetColor("_ShadeColor", globalShadeColor);
                    
                    mat.SetFloat("_HatchingDensity", hatchingDensity);
                    mat.SetFloat("_HatchingSharpness", hatchingSharpness);
                    mat.SetFloat("_HatchingBlend", hatchingBlend);
                    
                    mat.SetColor("_OutlineColor", outlineColor);
                    mat.SetFloat("_OutlineWidth", outlineWidth);
                }
            }
        }

        public void RestoreOriginalShaders()
        {
            foreach (var kvp in _originalShaders)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Key.shader = kvp.Value;
                }
            }
            _originalShaders.Clear();
        }

        void Update()
        {
            if (!enableDanganronpaStyle) return;

            if (sceneSun == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Directional)
                    {
                        sceneSun = l;
                        break;
                    }
                }
            }

            if (sceneSun != null)
            {
                Shader.SetGlobalVector("_SunDirection", -sceneSun.transform.forward);
            }
            else
            {
                Shader.SetGlobalVector("_SunDirection", new Vector3(0.4f, 0.8f, -0.4f).normalized);
            }
        }
    }
}
