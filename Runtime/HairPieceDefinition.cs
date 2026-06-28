using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterEditor.Hair
{
    public enum HairSlot
    {
        Unknown,
        Bangs,
        SideLeft,
        SideRight,
        Back,
        Ahoge,
        Ponytail,
        Extra
    }

    [CreateAssetMenu(menuName = "Character Editor/Hair Piece Definition")]
    public class HairPieceDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public HairSlot slot;
        public GameObject prefab;
        public Texture2D thumbnail;
        public List<Material> materials = new();
        public List<string> sourceBoneNames = new();
        public string sourceVrmPath;
    }
}
