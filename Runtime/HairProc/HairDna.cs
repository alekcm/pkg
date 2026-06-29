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
        static ushort FloatToHalf(float f)
        {
            uint x = (uint)BitConverter.SingleToInt32Bits(f);
            uint sign = (x >> 16) & 0x8000u;
            int exp = (int)((x >> 23) & 0xFFu) - 127 + 15;
            uint mant = x & 0x7FFFFFu;

            if (exp <= 0)
            {
                if (exp < -10) return (ushort)sign;
                mant = (mant | 0x800000u) >> (1 - exp);
                return (ushort)(sign | ((mant + 0x1000u) >> 13));
            }
            if (exp >= 31)
            {
                return (ushort)(sign | 0x7C00u | (mant != 0 ? 1u : 0u));
            }
            return (ushort)(sign | ((uint)exp << 10) | ((mant + 0x1000u) >> 13));
        }

        static float HalfToFloat(ushort h)
        {
            uint sign = (uint)(h & 0x8000) << 16;
            uint exp = (uint)(h & 0x7C00) >> 10;
            uint mant = (uint)(h & 0x03FF);

            uint bits;
            if (exp == 0)
            {
                if (mant == 0) bits = sign;
                else
                {
                    exp = 1;
                    while ((mant & 0x0400u) == 0) { mant <<= 1; exp--; }
                    mant &= 0x03FFu;
                    bits = sign | ((exp + 127u - 15u) << 23) | (mant << 13);
                }
            }
            else if (exp == 31)
            {
                bits = sign | 0x7F800000u | (mant << 13);
            }
            else
            {
                bits = sign | ((exp + 127u - 15u) << 23) | (mant << 13);
            }
            return BitConverter.Int32BitsToSingle((int)bits);
        }

        public bool Equals(HairDna other)
        {
            return pieceHash == other.pieceHash
                && Mathf.Approximately(lengthScale, other.lengthScale)
                && Mathf.Approximately(density, other.density)
                && Mathf.Approximately(thickness, other.thickness)
                && Mathf.Approximately(curl, other.curl)
                && Mathf.Approximately(wave, other.wave)
                && Mathf.Approximately(frizz, other.frizz)
                && rootColor.Equals(other.rootColor)
                && tipColor.Equals(other.tipColor)
                && rootFade255 == other.rootFade255
                && highlightColor.Equals(other.highlightColor)
                && highlightStrength255 == other.highlightStrength255
                && g0 == other.g0 && g1 == other.g1 && g2 == other.g2 && g3 == other.g3
                && g4 == other.g4 && g5 == other.g5 && g6 == other.g6 && g7 == other.g7
                && overrideCount == other.overrideCount
                && OverrideEquals(o0, other.o0)
                && OverrideEquals(o1, other.o1)
                && OverrideEquals(o2, other.o2)
                && OverrideEquals(o3, other.o3)
                && OverrideEquals(o4, other.o4)
                && OverrideEquals(o5, other.o5)
                && OverrideEquals(o6, other.o6)
                && OverrideEquals(o7, other.o7);
        }

        static bool OverrideEquals(GuideOverrideNet a, GuideOverrideNet b)
        {
            return a.guideIndex == b.guideIndex && a.len == b.len && a.curl == b.curl && a.thick == b.thick
                && a.dx1 == b.dx1 && a.dy1 == b.dy1 && a.dz1 == b.dz1
                && a.dx2 == b.dx2 && a.dy2 == b.dy2 && a.dz2 == b.dz2
                && a.dx3 == b.dx3 && a.dy3 == b.dy3 && a.dz3 == b.dz3
                && a.mask == b.mask;
        }

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
            n.curl = (sbyte)Mathf.Clamp(Mathf.RoundToInt(o.curlStrength*100f), -100,100);
            n.thick = (sbyte)Mathf.Clamp(Mathf.RoundToInt((o.thicknessScale-1f)*100f),-50,50);

            // Convention: dx1/dy1/dz1 -> control point 1 (mask bit 1),
            // dx2/dy2/dz2 -> point 2 (bit 2), dx3/dy3/dz3 -> point 3 (bit 4).
            Vector3 q1 = o.GetPointDelta(1);
            n.dx1 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q1.x*100f), -127,127);
            n.dy1 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q1.y*100f), -127,127);
            n.dz1 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q1.z*100f), -127,127);
            Vector3 q2 = o.GetPointDelta(2);
            n.dx2 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q2.x*100f), -127,127);
            n.dy2 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q2.y*100f), -127,127);
            n.dz2 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q2.z*100f), -127,127);
            Vector3 q3 = o.GetPointDelta(3);
            n.dx3 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q3.x*100f), -127,127);
            n.dy3 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q3.y*100f), -127,127);
            n.dz3 = (sbyte)Mathf.Clamp(Mathf.RoundToInt(q3.z*100f), -127,127);
            n.mask = o.mask;
            return n;
        }
        public GuideOverride ToGuideOverride()
        {
            var o = new GuideOverride{ lengthScale = 1f + len/100f, thicknessScale=1f+thick/100f, curlStrength=curl/100f, mask=0 };
            if ((mask & 1)!=0) o.SetPointDelta(1, new Vector3(dx1/100f, dy1/100f, dz1/100f));
            if ((mask & 2)!=0) o.SetPointDelta(2, new Vector3(dx2/100f, dy2/100f, dz2/100f));
            if ((mask & 4)!=0) o.SetPointDelta(3, new Vector3(dx3/100f, dy3/100f, dz3/100f));
            return o;
        }
    }
}
