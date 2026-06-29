// HairStrandDefinition.cs – Unity 6 / HDRP
// Procedural hair core types – VRoid-style
using System;
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    [Serializable]
    public struct HairGuide
    {
        [Tooltip("Canonical bone name to attach root, e.g. c_head")]
        public string attachBone;
        public Vector3 rootLocalPos;      // in bone local space
        public Vector3 rootLocalNormal;   // outward
        // Catmull-Rom control points, in bone LOCAL space, root = points[0]
        public Vector3[] pointsLocal;     // 4..8 points typical

        [Range(0.0005f, 0.02f)] public float thicknessRoot;
        [Range(0.0001f, 0.01f)] public float thicknessTip;
        [Range(3, 12)] public int sideCount; // tube sides, 4 = ribbon
        public int groupId; // bangs=0, sideL=1, sideR=2, back=3, etc – for per-group sliders

        public static HairGuide CreateDefault(string bone, Vector3 rootPos, float length = 0.25f)
        {
            return new HairGuide
            {
                attachBone = bone,
                rootLocalPos = rootPos,
                rootLocalNormal = Vector3.up,
                pointsLocal = new Vector3[]
                {
                    Vector3.zero,
                    // Hair grows away from the scalp and then falls down in head-local space.
                    // Y is up on Unity humanoid/Mixamo heads, so length must be negative Y.
                    new Vector3(0, -length*0.33f, -0.02f),
                    new Vector3(0, -length*0.66f, -0.04f),
                    new Vector3(0, -length, -0.06f)
                },
                thicknessRoot = 0.004f,
                thicknessTip = 0.0008f,
                sideCount = 4,
                groupId = 0
            };
        }
    }

    [Serializable]
    public struct GuideOverride
    {
        public int guideIndex;
        // sparse per-point delta, in bone local space, in CENTIMETERS to allow sbyte quantize
        public Vector3 p1, p2, p3, p4, p5, p6, p7;
        public byte mask; // bit0..6 -> which points are overridden
        public float lengthScale; // 0.5..1.5, 0 = use global
        public float curlStrength; // 0..1, 0 = use global
        public float thicknessScale; // 0.5..1.5

        public Vector3 GetPointDelta(int idx)
        {
            switch(idx){
                case 1: return ((mask & 1)!=0) ? p1 : Vector3.zero;
                case 2: return ((mask & 2)!=0) ? p2 : Vector3.zero;
                case 3: return ((mask & 4)!=0) ? p3 : Vector3.zero;
                case 4: return ((mask & 8)!=0) ? p4 : Vector3.zero;
                case 5: return ((mask & 16)!=0) ? p5 : Vector3.zero;
                case 6: return ((mask & 32)!=0) ? p6 : Vector3.zero;
                case 7: return ((mask & 64)!=0) ? p7 : Vector3.zero;
                default: return Vector3.zero;
            }
        }
        public void SetPointDelta(int idx, Vector3 v)
        {
            switch(idx){
                case 1: p1=v; mask|=(byte)1; break;
                case 2: p2=v; mask|=(byte)2; break;
                case 3: p3=v; mask|=(byte)4; break;
                case 4: p4=v; mask|=(byte)8; break;
                case 5: p5=v; mask|=(byte)16; break;
                case 6: p6=v; mask|=(byte)32; break;
                case 7: p7=v; mask|=(byte)64; break;
            }
        }
    }

    [Serializable]
    public struct HairColorGradient
    {
        public Color rootColor;
        public Color tipColor;
        [Range(0f,1f)] public float rootFade; // 0..1 where tip starts
        public Color highlightColor;
        [Range(0f,1f)] public float highlightStrength;
    }
}
