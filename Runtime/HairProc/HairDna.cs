using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    /// <summary>
    /// Compact, network-serializable hair DNA – VRoid style.
    /// ~80 bytes base + ~24 bytes per edited strand.
    /// 20 players = ~2-7KB initial burst.
    /// </summary>
    [Serializable]
    public struct HairDna : INetworkSerializable, IEquatable<HairDna>
    {
        // piece id – 4-byte FNV hash to save bandwidth, resolve via HairCatalogProc
        public uint pieceHash;

        // global sliders – Half precision is enough
        public float lengthScale;      // 0.5 .. 1.8
        public float density;          // 0.3 .. 2.0
        public float thickness;        // 0.5 .. 1.5
        public float curl;             // 0 .. 1
        public float wave;             // 0 .. 1
        public float frizz;            // 0 .. 1

        // color – 8 bit per channel is fine for network, HDRP expands
        public Color32 rootColor;
        public Color32 tipColor;
        public byte rootFade255; // 0..255
        public Color32 highlightColor;
        public byte highlightStrength255;

        // per-group length overrides – 8 groups × sbyte (-50..+50 → 0.5..1.5)
        public sbyte g0, g1, g2, g3, g4, g5, g6, g7;

        // per-strand overrides – variable length, max 12 to keep packet small
        // stored externally in List<GuideOverride> – networked separately if needed
        // For initial MVP we sync first 8 overrides inline:
        public byte overrideCount;
        public GuideOverrideNet o0, o1, o2, o3, o4, o5, o6, o7;

        public static HairDna Default(string pieceId)
        {
            return new HairDna
            {
                pieceHash = Hash32(pieceId),
                lengthScale = 1f,
                density = 1f,
                thickness = 1f,
                curl = 0f,
                wave = 0f,
                frizz = 0f,
                rootColor = new Color32(38,26,20,255),
                tipColor = new Color32(64,46,30,255),
                rootFade255 = 77,
                highlightColor = new Color32(128,90,64,255),
                highlightStrength255 = 38,
                g0=0,g1=0,g2=0,g3=0,g4=0,g5=0,g6=0,g7=0,
                overrideCount = 0
            };
        }

        public float GetGroupScale(int group)
        {
            sbyte v = group switch {0=>g0,1=>g1,2=>g2,3=>g3,4=>g4,5=>g5,6=>g6,7=>g7, _=>0};
            return 1f + v / 100f; // -0.5 .. +0.5
        }
        public void SetGroupScale(int group, float scale)
        {
            sbyte v = (sbyte)Mathf.Clamp(Mathf.RoundToInt((scale-1f)*100f), -50, 50);
            switch(group){ case 0:g0=v; break; case 1:g1=v; break; case 2:g2=v; break; case 3:g3=v; break; case 4:g4=v; break; case 5:g5=v; break; case 6:g6=v; break; case 7:g7=v; break; }
        }

        // ---------- INetworkSerializable ----------
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref pieceHash);
            // quantize floats to Half to save bandwidth
            ushort l = FloatToHalf(lengthScale), d = FloatToHalf(density), th = FloatToHalf(thickness);
            ushort cr = FloatToHalf(curl), wv = FloatToHalf(wave), fz = FloatToHalf(frizz);
            s.SerializeValue(ref l); s.SerializeValue(ref d); s.SerializeValue(ref th);
            s.SerializeValue(ref cr); s.SerializeValue(ref wv); s.SerializeValue(ref fz);
            if (s.IsReader)
            {
                lengthScale = HalfToFloat(l); density = HalfToFloat(d); thickness = HalfToFloat(th);
                curl = HalfToFloat(cr); wave = HalfToFloat(wv); frizz = HalfToFloat(fz);
            }
            s.SerializeValue(ref rootColor);
            s.SerializeValue(ref tipColor);
            s.SerializeValue(ref rootFade255);
            s.SerializeValue(ref highlightColor);
            s.SerializeValue(ref highlightStrength255);
            s.SerializeValue(ref g0); s.SerializeValue(ref g1); s.SerializeValue(ref g2); s.SerializeValue(ref g3);
            s.SerializeValue(ref g4); s.SerializeValue(ref g5); s.SerializeValue(ref g6); s.SerializeValue(ref g7);
            s.SerializeValue(ref overrideCount);
            // only serialize used overrides – simple unrolled for Burst-friendliness
            if (overrideCount > 0) s.SerializeValue(ref o0);
            if (overrideCount > 1) s.SerializeValue(ref o1);
            if (overrideCount > 2) s.SerializeValue(ref o2);
            if (overrideCount > 3) s.SerializeValue(ref o3);
            if (overrideCount > 4) s.SerializeValue(ref o4);
            if (overrideCount > 5) s.SerializeValue(ref o5);
            if (overrideCount > 6) s.SerializeValue(ref o6);
            if (overrideCount > 7) s.SerializeValue(ref o7);
            // clamp
            if (overrideCount > 8) overrideCount = 8;
        }

        // ---------- helpers ----------
        public static uint Hash32(string s)
        {
            unchecked{
                uint h = 2166136261;
                foreach(char c in s){ h ^= c; h *= 16777619; }
                return h;
            }
        }
        static ushort FloatToHalf(float f) => (ushort)Mathf.FloatToHalf(f);
        static float HalfToFloat(ushort h) => Mathf.HalfToFloat(h);

        public bool Equals(HairDna other) => pieceHash == other.pieceHash && Mathf.Approximately(lengthScale, other.lengthScale);
        public override int GetHashCode() => (int)pieceHash;
    }

    // Network-friendly compact override – 24 bytes
    [Serializable]
    public struct GuideOverrideNet : INetworkSerializable
    {
        public byte guideIndex; // 0..255
        public sbyte len;   // length delta -50..+50
        public sbyte curl;  // 0..100
        public sbyte thick; // -50..+50
        // 3 control point deltas, quantized to sbyte cm (-12.7 .. +12.7 cm)
        public sbyte dx1, dy1, dz1;
        public sbyte dx2, dy2, dz2;
        public sbyte dx3, dy3, dz3;
        // flags
        public byte mask; // which points overridden

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref guideIndex);
            s.SerializeValue(ref len); s.SerializeValue(ref curl); s.SerializeValue(ref thick);
            s.SerializeValue(ref dx1); s.SerializeValue(ref dy1); s.SerializeValue(ref dz1);
            s.SerializeValue(ref dx2); s.SerializeValue(ref dy2); s.SerializeValue(ref dz2);
            s.SerializeValue(ref dx3); s.SerializeValue(ref dy3); s.SerializeValue(ref dz3);
            s.SerializeValue(ref mask);
        }

        public static GuideOverrideNet From(GuideOverride o, int guideIdx)
        {
            var n = new GuideOverrideNet{ guideIndex=(byte)Mathf.Clamp(guideIdx,0,255) };
            n.len = (sbyte)Mathf.Clamp(Mathf.RoundToInt((o.lengthScale-1f)*100f), -50,50);
            n.curl = (byte)0; n.thick = (sbyte)Mathf.Clamp(Mathf.RoundToInt((o.thicknessScale-1f)*100f),-50,50);
            Vector3 q1 = o.GetPointDelta(2); // middle point
            n.dx1 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q1.x*100f), -127,127);
            n.dy1 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q1.y*100f), -127,127);
            n.dz1 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q1.z*100f), -127,127);
            Vector3 q2 = o.GetPointDelta(3);
            n.dx2 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q2.x*100f), -127,127);
            n.dy2 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q2.y*100f), -127,127);
            n.dz2 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q2.z*100f), -127,127);
            n.mask = o.mask;
            return n;
        }
        public GuideOverride ToGuideOverride()
        {
            var o = new GuideOverride{ lengthScale = 1f + len/100f, thicknessScale=1f+thick/100f, curlStrength=curl/100f, mask=mask };
            if ((mask & 4)!=0) o.SetPointDelta(2, new Vector3(dx1/100f, dy1/100f, dz1/100f));
            if ((mask & 8)!=0) o.SetPointDelta(3, new Vector3(dx2/100f, dy2/100f, dz2/100f));
            return o;
        }
    }
}
