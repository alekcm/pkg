using System;
using UnityEngine;

namespace CharacterEditor.Hair.EditorImport
{
    public sealed class GltfAccessorReader
    {
        private readonly J _gltf;
        private readonly byte[] _bin;

        public GltfAccessorReader(J gltf, byte[] bin)
        {
            _gltf = gltf;
            _bin = bin ?? Array.Empty<byte>();
        }

        public int GetCount(int accessorIndex) => _gltf["accessors"][accessorIndex]["count"].I;

        public Vector3[] ReadVec3(int accessorIndex)
        {
            var a = _gltf["accessors"][accessorIndex];
            int count = a["count"].I;
            var result = new Vector3[count];
            ForEachElement(a, 12, (src, dst) =>
            {
                result[dst] = new Vector3(F32(src), F32(src + 4), F32(src + 8));
            });
            return result;
        }

        public Vector4[] ReadVec4(int accessorIndex)
        {
            var a = _gltf["accessors"][accessorIndex];
            int count = a["count"].I;
            var result = new Vector4[count];
            int comp = a["componentType"].I;
            ForEachElement(a, ElementSize(a), (src, dst) =>
            {
                if (comp == 5126) result[dst] = new Vector4(F32(src), F32(src + 4), F32(src + 8), F32(src + 12));
                else if (comp == 5123) result[dst] = new Vector4(U16(src), U16(src + 2), U16(src + 4), U16(src + 6));
                else if (comp == 5121) result[dst] = new Vector4(_bin[src], _bin[src + 1], _bin[src + 2], _bin[src + 3]);
            });
            return result;
        }

        public Vector2[] ReadVec2(int accessorIndex)
        {
            var a = _gltf["accessors"][accessorIndex];
            int count = a["count"].I;
            var result = new Vector2[count];
            ForEachElement(a, 8, (src, dst) => result[dst] = new Vector2(F32(src), 1f - F32(src + 4)));
            return result;
        }

        public Matrix4x4[] ReadMat4(int accessorIndex)
        {
            var a = _gltf["accessors"][accessorIndex];
            int count = a["count"].I;
            var result = new Matrix4x4[count];
            ForEachElement(a, 64, (src, dst) =>
            {
                var m = new Matrix4x4();
                for (int c = 0; c < 4; c++)
                    for (int r = 0; r < 4; r++)
                        m[r, c] = F32(src + (c * 4 + r) * 4);
                result[dst] = m;
            });
            return result;
        }

        public int[] ReadIndices(int accessorIndex)
        {
            var a = _gltf["accessors"][accessorIndex];
            int count = a["count"].I;
            int comp = a["componentType"].I;
            var result = new int[count];
            ForEachElement(a, ElementSize(a), (src, dst) =>
            {
                if (comp == 5125) result[dst] = (int)U32(src);
                else if (comp == 5123) result[dst] = U16(src);
                else if (comp == 5121) result[dst] = _bin[src];
            });
            return result;
        }

        public byte[] ReadBufferViewBytes(int bufferViewIndex)
        {
            var bv = _gltf["bufferViews"][bufferViewIndex];
            int offset = bv.Has("byteOffset") ? bv["byteOffset"].I : 0;
            int len = bv["byteLength"].I;
            var data = new byte[len];
            Buffer.BlockCopy(_bin, offset, data, 0, len);
            return data;
        }

        private void ForEachElement(J accessor, int packedElementSize, Action<int, int> read)
        {
            int count = accessor["count"].I;
            int accessorOffset = accessor.Has("byteOffset") ? accessor["byteOffset"].I : 0;
            var bv = _gltf["bufferViews"][accessor["bufferView"].I];
            int viewOffset = bv.Has("byteOffset") ? bv["byteOffset"].I : 0;
            int stride = bv.Has("byteStride") ? bv["byteStride"].I : packedElementSize;
            int start = viewOffset + accessorOffset;
            for (int i = 0; i < count; i++) read(start + i * stride, i);
        }

        private static int ElementSize(J a)
        {
            int compSize = a["componentType"].I switch { 5120 => 1, 5121 => 1, 5122 => 2, 5123 => 2, 5125 => 4, 5126 => 4, _ => 4 };
            int comps = a["type"].S switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => 1 };
            return compSize * comps;
        }

        private float F32(int o) => BitConverter.ToSingle(_bin, o);
        private ushort U16(int o) => BitConverter.ToUInt16(_bin, o);
        private uint U32(int o) => BitConverter.ToUInt32(_bin, o);
    }
}
