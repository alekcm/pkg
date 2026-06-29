// HairNetworkBridge.cs – NGO Netcode integration for procedural hair
// Works with your existing NetworkSessionManager / WorldReplicationMessageRouter
using Unity.Netcode;
using UnityEngine;
using CharacterEditor.Hair.Proc;

namespace CharacterEditor.Hair.Net
{
    /// <summary>
    /// Attach to player prefab (next to NetworkObject + ExplorerController).
    /// Syncs HairDna (~80-350 bytes) once on spawn, then on change.
    /// Designed for 20 players crowd – zero per-frame traffic.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class HairNetworkBridge : NetworkBehaviour
    {
        [Header("Refs")]
        public HairRuntimeAttacherProc attacher;
        public HairCatalogProc catalog; // ScriptableObject with all HairPieceDefinitionProc

        [Header("Network")]
        public bool autoApplyOnSpawn = true;

        // Replicated DNA – NetworkVariable is reliable, ~400 bytes max, perfect for initial spawn
        private NetworkVariable<HairDna> _netDna = new NetworkVariable<HairDna>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public HairDna LocalDna
        {
            get => _netDna.Value;
            set { if (IsOwner) _netDna.Value = value; }
        }

        public System.Action<HairDna> OnHairChanged;

        void Awake()
        {
            if (attacher == null) attacher = GetComponentInChildren<HairRuntimeAttacherProc>(true);
            if (attacher == null) attacher = gameObject.AddComponent<HairRuntimeAttacherProc>();
            if (catalog == null) catalog = Resources.Load<HairCatalogProc>("HairCatalogProc");
        }

        public override void OnNetworkSpawn()
        {
            _netDna.OnValueChanged += OnDnaChanged;
            if (autoApplyOnSpawn)
                ApplyDna(_netDna.Value);
        }
        public override void OnNetworkDespawn()
        {
            _netDna.OnValueChanged -= OnDnaChanged;
        }

        void OnDnaChanged(HairDna prev, HairDna next)
        {
            ApplyDna(next);
            OnHairChanged?.Invoke(next);
        }

        void ApplyDna(HairDna dna)
        {
            if (dna.pieceHash == 0) return;
            if (catalog == null) { Debug.LogWarning("[HairNet] No HairCatalogProc", this); return; }
            var piece = catalog.ResolveByHash(dna.pieceHash);
            if (piece == null)
            {
                Debug.LogWarning($"[HairNet] Hair piece hash {dna.pieceHash:X8} not found in catalog", this);
                return;
            }
            // LOD 0 for owner / close players – LODGroup will downgrade later
            attacher.ApplyHair(piece, dna, lod: 0);
        }

        // --- Local player API – called from your character creator UI ---

        /// <summary>Call from UI sliders: length/density/curl/color …</summary>
        public void PushLocalDna(HairDna dna)
        {
            if (!IsOwner && !IsServer) return; // only owner may write
            _netDna.Value = dna;
            // local immediate apply for zero-latency feedback
            ApplyDna(dna);
        }

        /// <summary>Change hair piece (e.g. player picks new bangs preset in catalog)</summary>
        public void ChangeHairPiece(HairPieceDefinitionProc newPiece)
        {
            if (newPiece == null || !IsOwner) return;
            var dna = HairDna.Default(newPiece.id);
            // copy over color etc. from previous if desired
            var prev = _netDna.Value;
            if (prev.pieceHash != 0)
            {
                dna.rootColor = prev.rootColor;
                dna.tipColor = prev.tipColor;
                dna.highlightColor = prev.highlightColor;
            }
            PushLocalDna(dna);
        }

        // Optional: ServerRpc for server-authoritative validation (anti-cheat for RP)
        [ServerRpc(RequireOwnership = true)]
        public void SubmitHairDnaServerRpc(HairDna dna)
        {
            // TODO: validate pieceHash is in allowlist, clamp sliders
            _netDna.Value = dna;
        }
    }
}
