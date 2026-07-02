using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterEditor.VrmImport
{
    /// <summary>
    /// Slot categories for VRM character parts.
    /// Each mesh primitive gets classified into one of these.
    /// Defined here (Runtime) so both Editor and Runtime code can use it.
    /// </summary>
    public enum CharacterPartSlot
    {
        Unknown,
        // Body
        Body,
        Face,
        // Clothing layers
        Underwear,
        Socks,
        Tights,
        Shoes,
        Pants,
        Skirt,
        Tops,
        Jacket,
        Gloves,
        Hat,
        Accessory,
        // Hair parts (VRoid Studio groups)
        HairFront,
        HairBack,
        HairSide,
        HairAhoge,
        HairExtra,
        HairAll,
    }

    /// <summary>
    /// Describes a single extracted part of a VRM character.
    /// Each primitive becomes its own CharacterPartDefinition,
    /// grouped by slot (Body, Face, Shoes, HairFront, etc.)
    /// </summary>
    [Serializable]
    public class CharacterPartInfo
    {
        public CharacterPartSlot slot;
        public string partName;
        public Mesh mesh;
        public Material material;
    }

    /// <summary>
    /// ScriptableObject that stores the full extraction result for one VRM character.
    /// Contains the root prefab and a list of all classified parts.
    /// </summary>
    [CreateAssetMenu(menuName = "Character Editor/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        public string characterId;
        public string displayName;
        public string sourceVrmPath;
        public GameObject prefab;

        [Header("Extracted Parts")]
        public List<CharacterPartInfo> parts = new();

        /// <summary>Get all parts of a given slot.</summary>
        public List<CharacterPartInfo> GetPartsBySlot(CharacterPartSlot slot)
        {
            return parts.FindAll(p => p.slot == slot);
        }

        /// <summary>Get all unique slot categories present in this character.</summary>
        public HashSet<CharacterPartSlot> GetAvailableSlots()
        {
            var set = new HashSet<CharacterPartSlot>();
            foreach (var p in parts) set.Add(p.slot);
            return set;
        }
    }
}
