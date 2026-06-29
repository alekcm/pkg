// HairProcIntegration.cs – bridge between old HairRuntimeAttacher / BuildUiController and new Proc system
// Drop-in – no changes to existing character creator required, just add this component next to HairRuntimeAttacher
using UnityEngine;
using CharacterEditor.Hair;
using CharacterEditor.Hair.Proc;
using CharacterEditor.Hair.Net;

namespace CharacterEditor.Hair.Proc.Integration
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class HairProcIntegration : MonoBehaviour
    {
        [Header("Legacy bridge")]
        public HairRuntimeAttacher legacyAttacher; // твой старый компонент – оставляем для fallback VRM
        public HairRuntimeAttacherProc procAttacher;

        [Header("Catalogs")]
        public HairCatalog legacyCatalog; // Assets/Resources/HairCatalog
        public HairCatalogProc procCatalog; // Assets/Resources/HairCatalogProc

        [Header("Default")]
        public string defaultProcId = "bangs_proc_01";

        HairDna _currentDna;
        HairPieceDefinitionProc _currentPiece;

        void Awake()
        {
            if (procAttacher == null) procAttacher = GetComponent<HairRuntimeAttacherProc>();
            if (procAttacher == null) procAttacher = gameObject.AddComponent<HairRuntimeAttacherProc>();
            if (legacyAttacher == null) legacyAttacher = GetComponent<HairRuntimeAttacher>();

            if (procCatalog == null) procCatalog = Resources.Load<HairCatalogProc>("HairCatalogProc");
            // auto-apply default on start (in editor – useful)
            if (Application.isEditor && !Application.isPlaying && currentPiece == null)
            {
                TryApplyDefaultEditor();
            }
        }

        public HairPieceDefinitionProc currentPiece => _currentPiece;

        void TryApplyDefaultEditor()
        {
#if UNITY_EDITOR
            if (procCatalog == null) return;
            var p = procCatalog.ResolveById(defaultProcId);
            if (p != null)
            {
                _currentPiece = p;
                _currentDna = HairDna.Default(p.id);
                // do NOT bake in edit mode automatically – too heavy
                // Editor tool will do it on demand
            }
#endif
        }

        // --- API compatible with your old HairRuntimeAttacher.Attach(HairPieceDefinition) ---
        // Allows BuildUiController to call the same method names

        public GameObject Attach(string pieceId)
        {
            // 1) try proc catalog first
            if (procCatalog != null)
            {
                var proc = procCatalog.ResolveById(pieceId);
                if (proc != null)
                {
                    var dna = HairDna.Default(proc.id);
                    var smr = procAttacher.ApplyHair(proc, dna, 0);
                    _currentPiece = proc;
                    _currentDna = dna;
                    return smr != null ? smr.gameObject : null;
                }
            }
            // 2) fallback to legacy VRM
            if (legacyAttacher != null && legacyCatalog != null)
            {
                // legacyCatalog is not public in your code – adapt here if needed
                // var legacyPiece = legacyCatalog.GetById(pieceId);
                // return legacyAttacher.Attach(legacyPiece);
            }
            Debug.LogWarning($"[HairProcIntegration] Piece '{pieceId}' not found in Proc nor Legacy catalogs", this);
            return null;
        }

        // Called by UI sliders
        public void SetLength(float v) { _currentDna.lengthScale = Mathf.Clamp(v, 0.5f, 1.8f); Rebuild(); }
        public void SetDensity(float v) { _currentDna.density = Mathf.Clamp(v, 0.3f, 2f); Rebuild(); }
        public void SetThickness(float v) { _currentDna.thickness = Mathf.Clamp(v, 0.5f, 1.5f); Rebuild(); }
        public void SetCurl(float v) { _currentDna.curl = Mathf.Clamp01(v); Rebuild(); }
        public void SetWave(float v) { _currentDna.wave = Mathf.Clamp01(v); Rebuild(); }
        public void SetFrizz(float v) { _currentDna.frizz = Mathf.Clamp01(v); Rebuild(); }
        public void SetRootColor(Color c) { _currentDna.rootColor = c; Rebuild(); }
        public void SetTipColor(Color c) { _currentDna.tipColor = c; Rebuild(); }

        // per-group – e.g. separate slider “Bangs length”
        public void SetGroupLength(int group, float scale)
        {
            _currentDna.SetGroupScale(group, scale);
            Rebuild();
        }

        void Rebuild()
        {
            if (_currentPiece == null) return;
            if (procAttacher != null)
                procAttacher.ApplyHair(_currentPiece, _currentDna, 0);

            // push to network if present
            var net = GetComponent<HairNetworkBridge>();
            if (net != null && net.IsOwner) net.PushLocalDna(_currentDna);
        }

        public HairDna GetDna() => _currentDna;

        // for loading saved character
        public void ApplyDna(HairDna dna)
        {
            _currentDna = dna;
            if (procCatalog == null) procCatalog = Resources.Load<HairCatalogProc>("HairCatalogProc");
            var piece = procCatalog != null ? procCatalog.ResolveByHash(dna.pieceHash) : null;
            if (piece == null && !string.IsNullOrEmpty(defaultProcId))
                piece = procCatalog?.ResolveById(defaultProcId);
            if (piece != null)
            {
                _currentPiece = piece;
                procAttacher?.ApplyHair(piece, dna, 0);
            }
        }
    }
}
