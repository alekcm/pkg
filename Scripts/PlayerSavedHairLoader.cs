using System.Collections;
using CharacterEditor.Hair.Net;
using CharacterEditor.Hair.Proc;
using CharacterEditor.Hair.Profile;
using Unity.Netcode;
using UnityEngine;

namespace CharacterEditor.Hair.Profile
{
    /// <summary>
    /// Put this on the network player prefab.
    /// When the local player's NetworkObject spawns, it loads saved hair DNA from PlayerPrefs,
    /// applies it locally, and pushes it through HairNetworkBridge so server/other clients see it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerSavedHairLoader : NetworkBehaviour
    {
        public HairNetworkBridge networkBridge;
        public HairRuntimeAttacherProc procAttacher;
        public HairCatalogProc catalog;
        public string playerPrefsKey = HairCharacterProfileStore.DefaultKey;
        public float applyDelaySeconds = 0.1f;

        private void Awake()
        {
            if (networkBridge == null)
                networkBridge = GetComponent<HairNetworkBridge>() ?? GetComponentInChildren<HairNetworkBridge>(true);
            if (procAttacher == null)
                procAttacher = GetComponent<HairRuntimeAttacherProc>() ?? GetComponentInChildren<HairRuntimeAttacherProc>(true);
            if (catalog == null)
                catalog = Resources.Load<HairCatalogProc>("HairCatalogProc");
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
                return;

            StartCoroutine(ApplySavedHairNextFrame());
        }

        private IEnumerator ApplySavedHairNextFrame()
        {
            if (applyDelaySeconds > 0f)
                yield return new WaitForSeconds(applyDelaySeconds);
            else
                yield return null;

            if (!HairCharacterProfileStore.TryLoad(out HairDna dna, playerPrefsKey))
            {
                Debug.Log("[PlayerSavedHairLoader] No saved hair profile found for local player.", this);
                yield break;
            }

            if (catalog == null)
                catalog = Resources.Load<HairCatalogProc>("HairCatalogProc");

            HairPieceDefinitionProc piece = catalog != null ? catalog.ResolveByHash(dna.pieceHash) : null;
            if (piece == null)
            {
                Debug.LogWarning($"[PlayerSavedHairLoader] Saved hair hash {dna.pieceHash:X8} was not found in HairCatalogProc.", this);
                yield break;
            }

            if (procAttacher == null && networkBridge != null)
                procAttacher = networkBridge.attacher;

            if (procAttacher != null)
                procAttacher.ApplyHair(piece, dna, 0);

            if (networkBridge == null)
                networkBridge = GetComponent<HairNetworkBridge>() ?? GetComponentInChildren<HairNetworkBridge>(true);

            if (networkBridge != null)
                networkBridge.PushLocalDna(dna);
            else
                Debug.LogWarning("[PlayerSavedHairLoader] HairNetworkBridge not found. Hair was applied locally but not sent to network.", this);
        }
    }
}
