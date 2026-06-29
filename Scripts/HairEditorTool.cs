// HairEditorTool.cs – in-game VRoid-style strand editor
// Attach to your character-creator camera rig.
// Allows: select strand → drag with mouse → length / curl / color sliders
using UnityEngine;
using CharacterEditor.Hair.Proc;

namespace CharacterEditor.Hair.EditorTool
{
    public class HairEditorTool : MonoBehaviour
    {
        [Header("Refs")]
        public Camera editCamera;
        public HairRuntimeAttacherProc attacher;
        public HairPieceDefinitionProc currentPiece;
        public LayerMask hairHitMask = ~0;

        [Header("Live DNA – bind these to your UI sliders")]
        [Range(0.5f,1.8f)] public float length = 1f;
        [Range(0.3f,2f)]   public float density = 1f;
        [Range(0.5f,1.5f)] public float thickness = 1f;
        [Range(0f,1f)]     public float curl = 0f;
        [Range(0f,1f)]     public float wave = 0f;
        public Color rootColor = new Color(0.15f,0.1f,0.08f,1);
        public Color tipColor  = new Color(0.25f,0.18f,0.12f,1);

        [Header("Per-strand edit")]
        public bool strandDragEnabled = true;
        public float dragStrength = 1f;
        public KeyCode grabKey = KeyCode.Mouse0;

        HairDna _dna;
        int _selectedGuide = -1;
        bool _dragging;
        Plane _dragPlane;
        Vector3 _grabOffset;

        // simple cache to find closest strand – we raycast against head sphere then pick nearest guide root
        Transform _headBone;

        void Start()
        {
            if (editCamera == null) editCamera = Camera.main;
            if (attacher == null) attacher = FindObjectOfType<HairRuntimeAttacherProc>();
            if (currentPiece != null)
                _dna = HairDna.Default(currentPiece.id);
            Apply();
        }

        void Update()
        {
            // live sliders → DNA
            bool dirty = false;
            float nl = length, nd = density, nt = thickness, nc = curl, nw = wave;
            // (in real UI you'd read from Slider.onValueChanged – here we poll public fields for simplicity)
            if (!Mathf.Approximately(_dna.lengthScale, nl)) { _dna.lengthScale = nl; dirty = true; }
            if (!Mathf.Approximately(_dna.density, nd)) { _dna.density = nd; dirty = true; }
            if (!Mathf.Approximately(_dna.thickness, nt)) { _dna.thickness = nt; dirty = true; }
            if (!Mathf.Approximately(_dna.curl, nc)) { _dna.curl = nc; dirty = true; }
            if (!Mathf.Approximately(_dna.wave, nw)) { _dna.wave = nw; dirty = true; }

            // color
            var rc32 = (Color32)rootColor; var tc32 = (Color32)tipColor;
            if (!ColorsEqual(_dna.rootColor, rc32) || !ColorsEqual(_dna.tipColor, tc32))
            { _dna.rootColor = rc32; _dna.tipColor = tc32; dirty = true; }

            if (dirty) Apply();

            HandleStrandDrag();
        }

        void HandleStrandDrag()
        {
            if (!strandDragEnabled || editCamera == null || currentPiece == null || currentPiece.guides == null) return;

            if (Input.GetKeyDown(grabKey))
            {
                Ray ray = editCamera.ScreenPointToRay(Input.mousePosition);
                // crude: raycast against head sphere, then find nearest guide root
                if (_headBone == null && attacher != null)
                {
                    // try find head bone via attacher
                    var f = typeof(HairRuntimeAttacherProc).GetField("headBone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (f != null) _headBone = f.GetValue(attacher) as Transform;
                }
                Transform head = _headBone != null ? _headBone : (attacher != null ? attacher.transform : transform);
                // sphere test
                Vector3 sphereCenter = head != null ? head.position + Vector3.up * 0.08f : transform.position + Vector3.up * 1.6f;
                if (RaySphereIntersect(ray, sphereCenter, 0.22f, out Vector3 hit))
                {
                    // pick nearest guide root
                    float bestDist = float.MaxValue; int bestIdx = -1;
                    var guides = currentPiece.guides;
                    Matrix4x4 headL2W = head != null ? head.localToWorldMatrix : Matrix4x4.identity;
                    for (int i = 0; i < guides.Length; i++)
                    {
                        Vector3 worldRoot = headL2W.MultiplyPoint3x4(guides[i].rootLocalPos);
                        float d = Vector3.SqrMagnitude(worldRoot - hit);
                        if (d < bestDist) { bestDist = d; _selectedGuide = i; bestIdx = i; }
                    }
                    if (bestIdx >= 0 && bestDist < 0.06f * 0.06f)
                    {
                        _dragging = true;
                        _dragPlane = new Plane(-editCamera.transform.forward, hit);
                        _grabOffset = Vector3.zero;
                    }
                }
            }
            if (_dragging && Input.GetKey(grabKey) && _selectedGuide >= 0)
            {
                Ray ray = editCamera.ScreenPointToRay(Input.mousePosition);
                if (_dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 worldPos = ray.GetPoint(enter);
                    // convert to head local
                    Transform head = _headBone != null ? _headBone : transform;
                    Vector3 localPos = head.InverseTransformPoint(worldPos);
                    // update override – push middle control point toward drag
                    SetGuideOverride(_selectedGuide, localPos);
                }
            }
            if (Input.GetKeyUp(grabKey))
            {
                _dragging = false;
                _selectedGuide = -1;
            }
        }

        void SetGuideOverride(int guideIdx, Vector3 worldTargetLocalToHead)
        {
            if (currentPiece == null || currentPiece.guides == null || guideIdx < 0 || guideIdx >= currentPiece.guides.Length) return;
            // find slot in DNA overrides (max 8)
            int slot = -1;
            // check existing
            var overrides = new GuideOverrideNet[] { _dna.o0, _dna.o1, _dna.o2, _dna.o3, _dna.o4, _dna.o5, _dna.o6, _dna.o7 };
            for (int i = 0; i < _dna.overrideCount && i < 8; i++)
                if (overrides[i].guideIndex == guideIdx) { slot = i; break; }
            if (slot < 0 && _dna.overrideCount < 8) slot = _dna.overrideCount++;

            if (slot < 0) return; // full

            // compute delta vs guide mid-point
            var g = currentPiece.guides[guideIdx];
            Vector3 midLocal = g.pointsLocal.Length > 2 ? g.pointsLocal[2] : g.pointsLocal[g.pointsLocal.Length - 1] * 0.5f;
            Vector3 targetLocal = worldTargetLocalToHead - g.rootLocalPos;
            Vector3 delta = (targetLocal - midLocal) * dragStrength;
            // quantize to cm sbyte
            sbyte dx = (sbyte)Mathf.Clamp(Mathf.RoundToInt(delta.x * 100f), -127, 127);
            sbyte dy = (sbyte)Mathf.Clamp(Mathf.RoundToInt(delta.y * 100f), -127, 127);
            sbyte dz = (sbyte)Mathf.Clamp(Mathf.RoundToInt(delta.z * 100f), -127, 127);

            var ov = new GuideOverrideNet
            {
                guideIndex = (byte)guideIdx,
                dx2 = dx, dy2 = dy, dz2 = dz,
                mask = 4, // point 2
                len = 0, curl = 0, thick = 0
            };
            switch (slot)
            {
                case 0: _dna.o0 = ov; break;
                case 1: _dna.o1 = ov; break;
                case 2: _dna.o2 = ov; break;
                case 3: _dna.o3 = ov; break;
                case 4: _dna.o4 = ov; break;
                case 5: _dna.o5 = ov; break;
                case 6: _dna.o6 = ov; break;
                case 7: _dna.o7 = ov; break;
            }
            Apply();
        }

        void Apply()
        {
            if (attacher != null && currentPiece != null)
                attacher.ApplyHair(currentPiece, _dna, lod: 0);

            // push to network if present
            var net = attacher != null ? attacher.GetComponent<CharacterEditor.Hair.Net.HairNetworkBridge>() : null;
            if (net != null && net.IsOwner) net.PushLocalDna(_dna);
        }

        static bool ColorsEqual(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        static bool RaySphereIntersect(Ray ray, Vector3 center, float radius, out Vector3 hit)
        {
            Vector3 oc = ray.origin - center;
            float b = Vector3.Dot(oc, ray.direction);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float h = b * b - c;
            if (h < 0) { hit = default; return false; }
            hit = ray.origin + ray.direction * (-b - Mathf.Sqrt(h));
            return true;
        }

        // UI helpers – call these from your BuildUiController sliders
        public void SetLength(float v) { length = v; }
        public void SetDensity(float v) { density = v; }
        public void SetThickness(float v) { thickness = v; }
        public void SetCurl(float v) { curl = v; }
        public void SetWave(float v) { wave = v; }
        public void SetRootColor(Color c) { rootColor = c; }
        public void SetTipColor(Color c) { tipColor = c; }
        public HairDna GetDna() => _dna;
    }
}
