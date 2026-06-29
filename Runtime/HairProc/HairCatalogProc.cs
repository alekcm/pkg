using System.Collections.Generic;
using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    [CreateAssetMenu(menuName = "Character/Hair Catalog Proc", fileName = "HairCatalogProc")]
    public class HairCatalogProc : ScriptableObject
    {
        public List<HairPieceDefinitionProc> pieces = new List<HairPieceDefinitionProc>();
        private Dictionary<uint, HairPieceDefinitionProc> _map;
        private Dictionary<string, HairPieceDefinitionProc> _idMap;

        void OnEnable() => Rebuild();
        public void Rebuild()
        {
            _map = new Dictionary<uint, HairPieceDefinitionProc>();
            _idMap = new Dictionary<string, HairPieceDefinitionProc>(System.StringComparer.OrdinalIgnoreCase);
            if (pieces == null) return;
            foreach (var p in pieces)
            {
                if (p == null) continue;
                uint h = HairDna.Hash32(!string.IsNullOrEmpty(p.id) ? p.id : p.name);
                if (!_map.ContainsKey(h)) _map.Add(h, p);
                string id = !string.IsNullOrEmpty(p.id) ? p.id : p.name;
                if (!_idMap.ContainsKey(id)) _idMap.Add(id, p);
            }
        }

        public HairPieceDefinitionProc ResolveByHash(uint hash)
        {
            if (_map == null) Rebuild();
            _map.TryGetValue(hash, out var v);
            return v;
        }
        public HairPieceDefinitionProc ResolveById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_idMap == null) Rebuild();
            _idMap.TryGetValue(id, out var v);
            return v;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Fill From Project")]
        void AutoFill()
        {
            pieces.Clear();
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:HairPieceDefinitionProc");
            foreach (var g in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                var p = UnityEditor.AssetDatabase.LoadAssetAtPath<HairPieceDefinitionProc>(path);
                if (p != null && !pieces.Contains(p)) pieces.Add(p);
            }
            Rebuild();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[HairCatalogProc] Found {pieces.Count} procedural hair pieces", this);
        }
#endif
    }
}
