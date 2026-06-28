using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CharacterEditor.Hair.EditorImport
{
    public sealed class VrmGlb
    {
        public J json;
        public byte[] bin;
        public string jsonText;
    }

    public static class VrmGlbReader
    {
        public static VrmGlb Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 20) throw new InvalidDataException("File is too small for GLB/VRM.");

            uint magic = U32(bytes, 0);
            if (magic != 0x46546C67) throw new InvalidDataException("Not a binary glTF/VRM file. Expected magic 'glTF'.");
            uint version = U32(bytes, 4);
            if (version != 2) throw new InvalidDataException($"Unsupported glTF version {version}; expected 2.");

            int offset = 12;
            string json = null;
            byte[] bin = Array.Empty<byte>();

            while (offset + 8 <= bytes.Length)
            {
                int chunkLength = (int)U32(bytes, offset); offset += 4;
                uint chunkType = U32(bytes, offset); offset += 4;
                if (chunkLength < 0 || offset + chunkLength > bytes.Length) break;

                if (chunkType == 0x4E4F534A) // JSON
                    json = Encoding.UTF8.GetString(bytes, offset, chunkLength).TrimEnd('\0', ' ', '\r', '\n', '\t');
                else if (chunkType == 0x004E4942) // BIN
                {
                    bin = new byte[chunkLength];
                    Buffer.BlockCopy(bytes, offset, bin, 0, chunkLength);
                }
                offset += chunkLength;
            }

            if (string.IsNullOrEmpty(json)) throw new InvalidDataException("GLB JSON chunk not found.");
            return new VrmGlb { jsonText = json, json = J.Parse(json), bin = bin };
        }

        private static uint U32(byte[] b, int o) => BitConverter.ToUInt32(b, o);
    }
}
