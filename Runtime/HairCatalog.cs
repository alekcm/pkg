using System.Collections.Generic;
using UnityEngine;

namespace CharacterEditor.Hair
{
    [CreateAssetMenu(menuName = "Character Editor/Hair Catalog")]
    public class HairCatalog : ScriptableObject
    {
        public List<HairPieceDefinition> pieces = new();
    }
}
