using UnityEngine;

namespace CharacterEditor.VrmImport
{
    // CharacterPartSlot enum is defined in Runtime/CharacterPartDefinition.cs
    // so it is accessible from both Editor and Runtime code.

    [CreateAssetMenu(menuName = "Character Editor/VRM Character Extraction Settings")]
    public class VrmCharacterExtractionSettings : ScriptableObject
    {
        [Header("Paths")]
        [Tooltip("Folder where raw .vrm files are placed.")]
        public string sourceFolder = "Assets/Raw/VRMHair";

        [Tooltip("Root output folder for extracted character assets.")]
        public string outputFolder = "Assets/Generated/CharacterLibrary";

        [Header("Rendering")]
        [Tooltip("Override shader for generated materials. Leave null for auto-detect (HDRP/Lit → URP/Lit → Standard).")]
        public Shader materialShader;

        [Header("Mesh Import")]
        public bool importSkinning = true;
        public bool flipXForUnity = true;
        public bool reverseTriangleWinding = true;

        [Header("Diagnostics")]
        public bool writeDiagnostic = true;

        [Header("Hair Part Detection — Material Name Keywords")]
        [Tooltip("Keywords in material/node/mesh name to classify as HairFront (bangs).")]
        public string[] hairFrontKeywords = { "hairfront", "hair_front", "bangs", "bang", "fringe", "челк" };

        [Tooltip("Keywords for HairBack.")]
        public string[] hairBackKeywords = { "hairback", "hair_back" };

        [Tooltip("Keywords for HairSide.")]
        public string[] hairSideKeywords = { "hairside", "hair_side", "sideleft", "sideright", "side_l", "side_r" };

        [Tooltip("Keywords for HairAhoge.")]
        public string[] hairAhogeKeywords = { "ahoge", "antenna", "アホ毛" };

        [Tooltip("Keywords for HairExtra (VRoid 'hanege' = extra strands).")]
        public string[] hairExtraKeywords = { "hairextra", "hair_extra", "hanege", "extension" };

        [Tooltip("Generic hair keywords — any mesh/material containing these is 'some hair'.")]
        public string[] hairGenericKeywords = { "hair", "kami", "髪" };

        [Header("Clothing Detection — Material Name Keywords")]
        public string[] underwearKeywords = { "underwear", "inner", "bra", "panties", "белье" };
        public string[] socksKeywords = { "socks", "sock", "носки", "stocking", "чулки" };
        public string[] tightsKeywords = { "tights", "колготки", "pantyhose" };
        public string[] shoesKeywords = { "shoes", "shoe", "boots", "boot", "обувь", "ботинки", "туфли", "кросс" };
        public string[] pantsKeywords = { "pants", "pant", "shorts", "short", "bottom", "штаны", "шорты", "брюки" };
        public string[] skirtKeywords = { "skirt", "юбка" };
        public string[] topsKeywords = { "tops", "top", "shirt", "tshirt", "blouse", "верх", "рубашка", "футболка", "майка" };
        public string[] jacketKeywords = { "jacket", "coat", "hoodie", "куртка", "жакет", "пальто" };
        public string[] glovesKeywords = { "glove", "gloves", "перчатки" };
        public string[] hatKeywords = { "hat", "cap", "helmet", "шапка", "шляпа", "кепка" };
        public string[] accessoryKeywords = { "accessory", "acc", "ribbon", "bow", "glasses", "necklace", "ring", "earring", "аксессуар" };

        [Header("Body / Face Detection")]
        public string[] bodyKeywords = { "body", "тело" };
        public string[] faceKeywords = { "face", "лицо", "mouth", "eye", "eyelash", "eyeline", "brow", "teeth", "tongue" };

        [Header("Mesh-Level Name Detection (VRoid baked mesh names)")]
        [Tooltip("VRoid Studio exports meshes named like 'Face.baked', 'Body.baked', 'Hair001.baked'. " +
                 "These are used as a first-pass category when material name is ambiguous.")]
        public string[] meshNameFace = { "face" };
        public string[] meshNameBody = { "body" };
        public string[] meshNameHair = { "hair" };
    }
}
