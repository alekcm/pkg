// DanganronpaCloth.shader
// AAA Specialized Toon Hatching Shader to replicate the iconic 2.5D sprite art style
// of Danganronpa (Rui Komatsuzaki's character cross-hatching and bold outlines).
// Highly refined elegant hatching that looks like authentic intentional design strokes.
// Incorporates adjustable Light Intensity to ensure pristine, dazzling white clothes.
// Fully supports Unity 6 HDRP and URP.

Shader "XWear/DanganronpaCloth"
{
    Properties
    {
        [Header(Base Textures)]
        _BaseMap("Base Map", 2D) = "white" {}
        _MainTex("Main Tex Fallback", 2D) = "white" {}
        _BaseColorMap("HDRP Base Color Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor("Shade Color (Clean Toon Shadow)", Color) = (0.78, 0.74, 0.76, 1)
        
        [Header(Cel Lighting)]
        _LightIntensity("Light Intensity (Brightness)", Range(0.5, 2.5)) = 1.2
        _ShadowThreshold("Shadow Threshold", Range(-1, 1)) = -0.05
        _ShadowSmoothness("Shadow Ramp Smoothness", Range(0.01, 1)) = 0.1
        
        [Header(Artistic Cross Hatching)]
        _HatchingDensity("Hatching Density (Scale)", Range(10, 600)) = 250
        _HatchingSharpness("Hatching Line Width", Range(0.01, 1)) = 0.12
        _HatchingBlend("Hatching Opacity", Range(0, 1)) = 0.65
        
        [Header(Sprite Bold Outline)]
        _OutlineColor("Outline Color", Color) = (0.1, 0.05, 0.05, 1)
        _OutlineWidth("Outline Width", Range(0, 0.02)) = 0.002
        
        [Header(Transparency)]
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip Enable", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2 // Back
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue"      = "Geometry" 
        }

        // --- PASS 1: BOLD SPRITE OUTLINE ---
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front // Inside-out back faces
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ALPHATEST_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float _Cutoff;
            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = normalize(v.normal);
                float3 pos = v.vertex.xyz + norm * _OutlineWidth;
                
                o.pos = UnityObjectToClipPos(float4(pos, 1.0));
                o.uv  = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                float4 texCol = tex2D(_BaseMap, i.uv);
                clip(texCol.a - _Cutoff);
                #endif

                return _OutlineColor;
            }
            ENDHLSL
        }

        // --- PASS 2: ELEGANT DANGANRONPA ARTISTIC HATCHING (URP / FORWARD) ---
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ALPHATEST_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 worldNorm : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ShadeColor;
            
            float _LightIntensity;
            float _Cutoff;
            float _ShadowThreshold;
            float _ShadowSmoothness;
            
            float _HatchingDensity;
            float _HatchingSharpness;
            float _HatchingBlend;

            float4 _SunDirection;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos       = UnityObjectToClipPos(v.vertex);
                o.uv        = TRANSFORM_TEX(v.uv, _BaseMap);
                o.worldNorm = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 texCol = tex2D(_BaseMap, i.uv);
                
                #if defined(_ALPHATEST_ON)
                clip(texCol.a - _Cutoff);
                #endif

                float4 baseCol = texCol * _BaseColor;

                // --- LIGHTING ---
                float3 normal = normalize(i.worldNorm);
                
                float3 lightDir = normalize(float3(0.4, 0.8, -0.4));
                if (length(_SunDirection.xyz) > 0.1)
                {
                    lightDir = normalize(_SunDirection.xyz);
                }

                float NdotL = dot(normal, lightDir);

                // Dazzling crisp lit area
                float3 brightLitColor = baseCol.rgb * _LightIntensity;

                // Toon ramp boundary
                float brightMask = smoothstep(_ShadowThreshold, _ShadowThreshold + _ShadowSmoothness, NdotL);
                float deepMask   = smoothstep(_ShadowThreshold - 0.35, _ShadowThreshold - 0.1, NdotL);

                // Screen space Hatching coordinates
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                screenUV.x *= _ScreenParams.x / _ScreenParams.y;

                // Diagonal hatching 1 (/)
                float hatchCoord1 = (screenUV.x + screenUV.y) * _HatchingDensity;
                float line1 = abs(frac(hatchCoord1) - 0.5) * 2.0;
                float hatchMask1 = smoothstep(_HatchingSharpness, _HatchingSharpness * 0.4, line1);

                // Diagonal hatching 2 (\ Cross hatch)
                float hatchCoord2 = (screenUV.x - screenUV.y) * _HatchingDensity;
                float line2 = abs(frac(hatchCoord2) - 0.5) * 2.0;
                float hatchMask2 = smoothstep(_HatchingSharpness, _HatchingSharpness * 0.4, line2);

                // --- ELEGANT DANGANRONPA SPRITE MIX ---
                float3 shadowRGB = baseCol.rgb * _ShadeColor.rgb;
                float3 inkStroke = shadowRGB * 0.55;

                float3 midShadow  = lerp(shadowRGB, inkStroke, hatchMask1 * _HatchingBlend);
                float crossMask   = max(hatchMask1, hatchMask2) * _HatchingBlend;
                float3 deepShadow = lerp(shadowRGB * 0.85, inkStroke * 0.85, crossMask);

                float3 artisticShadow = lerp(deepShadow, midShadow, deepMask);
                float3 finalRGB       = lerp(artisticShadow, brightLitColor, brightMask);

                return float4(finalRGB, baseCol.a);
            }
            ENDHLSL
        }

        // --- PASS 3: HDRP FORWARD ONLY PASS ---
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ALPHATEST_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 worldNorm : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ShadeColor;
            
            float _LightIntensity;
            float _Cutoff;
            float _ShadowThreshold;
            float _ShadowSmoothness;
            
            float _HatchingDensity;
            float _HatchingSharpness;
            float _HatchingBlend;

            float4 _SunDirection;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos       = UnityObjectToClipPos(v.vertex);
                o.uv        = TRANSFORM_TEX(v.uv, _BaseMap);
                o.worldNorm = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 texCol = tex2D(_BaseMap, i.uv);
                
                #if defined(_ALPHATEST_ON)
                clip(texCol.a - _Cutoff);
                #endif

                float4 baseCol = texCol * _BaseColor;

                float3 normal = normalize(i.worldNorm);
                float3 lightDir = normalize(float3(0.4, 0.8, -0.4));
                if (length(_SunDirection.xyz) > 0.1)
                {
                    lightDir = normalize(_SunDirection.xyz);
                }

                float NdotL = dot(normal, lightDir);
                float3 brightLitColor = baseCol.rgb * _LightIntensity;

                float brightMask = smoothstep(_ShadowThreshold, _ShadowThreshold + _ShadowSmoothness, NdotL);
                float deepMask   = smoothstep(_ShadowThreshold - 0.35, _ShadowThreshold - 0.1, NdotL);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                screenUV.x *= _ScreenParams.x / _ScreenParams.y;

                float hatchCoord1 = (screenUV.x + screenUV.y) * _HatchingDensity;
                float line1 = abs(frac(hatchCoord1) - 0.5) * 2.0;
                float hatchMask1 = smoothstep(_HatchingSharpness, _HatchingSharpness * 0.4, line1);

                float hatchCoord2 = (screenUV.x - screenUV.y) * _HatchingDensity;
                float line2 = abs(frac(hatchCoord2) - 0.5) * 2.0;
                float hatchMask2 = smoothstep(_HatchingSharpness, _HatchingSharpness * 0.4, line2);

                float3 shadowRGB = baseCol.rgb * _ShadeColor.rgb;
                float3 inkStroke = shadowRGB * 0.55;

                float3 midShadow = lerp(shadowRGB, inkStroke, hatchMask1 * _HatchingBlend);
                float crossMask = max(hatchMask1, hatchMask2) * _HatchingBlend;
                float3 deepShadow = lerp(shadowRGB * 0.85, inkStroke * 0.85, crossMask);

                float3 artisticShadow = lerp(deepShadow, midShadow, deepMask);
                float3 finalRGB = lerp(artisticShadow, brightLitColor, brightMask);

                return float4(finalRGB, baseCol.a);
            }
            ENDHLSL
        }
    }
}
