using UnityEngine;
using CharacterEditor.Hair;

namespace CharacterEditor.Hair.EditorImport
{
    [CreateAssetMenu(menuName = "Character Editor/VRM Hair Extraction Settings")]
    public class VrmHairExtractionSettings : ScriptableObject
    {
        public string sourceFolder = "Assets/Raw/VRMHair";
        public string outputFolder = "Assets/Generated/HairLibrary";
        public HairSlot slotFromFolderFallback = HairSlot.Unknown;
        public Shader materialShader;

        public string[] includeKeywords = { "hair", "_hair", "kami" };
        public string[] excludeKeywords = { "body", "face", "skin", "eye", "mouth", "teeth", "cloth", "wear", "brow", "lash" };

        public bool importSkinning = true;
        public bool flipXForUnity = true;
        public bool reverseTriangleWinding = true;
        public bool writeDiagnosticJson = true;
    }
}
