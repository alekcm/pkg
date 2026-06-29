using System;
using CharacterEditor.Hair.Proc;
using UnityEngine;

namespace CharacterEditor.Hair.Profile
{
    /// <summary>
    /// Local persistent storage for the player's selected procedural hair.
    /// Uses PlayerPrefs so it survives scene changes and game restarts.
    /// </summary>
    public static class HairCharacterProfileStore
    {
        public const string DefaultKey = "CharacterEditor.SelectedHairDna.v1";

        [Serializable]
        private struct SavedHairDna
        {
            public uint pieceHash;
            public float lengthScale;
            public float density;
            public float thickness;
            public float curl;
            public float wave;
            public float frizz;

            public Color32 rootColor;
            public Color32 tipColor;
            public byte rootFade255;
            public Color32 highlightColor;
            public byte highlightStrength255;

            public sbyte g0, g1, g2, g3, g4, g5, g6, g7;
            public byte overrideCount;
            public GuideOverrideNet o0, o1, o2, o3, o4, o5, o6, o7;
        }

        public static bool HasSaved(string key = DefaultKey)
        {
            return PlayerPrefs.HasKey(key);
        }

        public static void Save(HairDna dna, string key = DefaultKey)
        {
            SavedHairDna data = FromDna(dna);
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
            Debug.Log($"[HairProfile] Saved hair DNA to PlayerPrefs key '{key}'.");
        }

        public static bool TryLoad(out HairDna dna, string key = DefaultKey)
        {
            dna = default;
            if (!PlayerPrefs.HasKey(key))
                return false;

            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                SavedHairDna data = JsonUtility.FromJson<SavedHairDna>(json);
                dna = ToDna(data);
                return dna.pieceHash != 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HairProfile] Failed to load saved hair DNA: {e.Message}");
                return false;
            }
        }

        public static void Delete(string key = DefaultKey)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private static SavedHairDna FromDna(HairDna d)
        {
            return new SavedHairDna
            {
                pieceHash = d.pieceHash,
                lengthScale = d.lengthScale,
                density = d.density,
                thickness = d.thickness,
                curl = d.curl,
                wave = d.wave,
                frizz = d.frizz,
                rootColor = d.rootColor,
                tipColor = d.tipColor,
                rootFade255 = d.rootFade255,
                highlightColor = d.highlightColor,
                highlightStrength255 = d.highlightStrength255,
                g0 = d.g0, g1 = d.g1, g2 = d.g2, g3 = d.g3,
                g4 = d.g4, g5 = d.g5, g6 = d.g6, g7 = d.g7,
                overrideCount = d.overrideCount,
                o0 = d.o0, o1 = d.o1, o2 = d.o2, o3 = d.o3, o4 = d.o4, o5 = d.o5, o6 = d.o6, o7 = d.o7
            };
        }

        private static HairDna ToDna(SavedHairDna d)
        {
            return new HairDna
            {
                pieceHash = d.pieceHash,
                lengthScale = d.lengthScale,
                density = d.density,
                thickness = d.thickness,
                curl = d.curl,
                wave = d.wave,
                frizz = d.frizz,
                rootColor = d.rootColor,
                tipColor = d.tipColor,
                rootFade255 = d.rootFade255,
                highlightColor = d.highlightColor,
                highlightStrength255 = d.highlightStrength255,
                g0 = d.g0, g1 = d.g1, g2 = d.g2, g3 = d.g3,
                g4 = d.g4, g5 = d.g5, g6 = d.g6, g7 = d.g7,
                overrideCount = d.overrideCount,
                o0 = d.o0, o1 = d.o1, o2 = d.o2, o3 = d.o3, o4 = d.o4, o5 = d.o5, o6 = d.o6, o7 = d.o7
            };
        }
    }
}
