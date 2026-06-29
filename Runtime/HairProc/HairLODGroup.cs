using UnityEngine;

namespace CharacterEditor.Hair.Proc
{
    /// <summary>
    /// Unity 6 – crowd LOD for 20+ networked characters with procedural hair.
    /// Attach next to HairRuntimeAttacherProc.
    /// </summary>
    [RequireComponent(typeof(HairRuntimeAttacherProc))]
    [DisallowMultipleComponent]
    public class HairLODGroupProc : MonoBehaviour
    {
        public Transform observerCamera; // null = Camera.main
        [Header("Distance LOD (meters)")]
        public float lod0Dist = 8f;
        public float lod1Dist = 18f;
        public float lod2Dist = 35f;
        public float cullDist = 55f;

        [Header("Update")]
        public int checkEveryNFrames = 7; // stagger for 20 characters
        public bool disableSpringWhenFar = true;

        HairRuntimeAttacherProc _attacher;
        int _currentLod = -1;
        int _frameOffset;

        void Awake()
        {
            _attacher = GetComponent<HairRuntimeAttacherProc>();
            _frameOffset = Random.Range(0, checkEveryNFrames);
            if (observerCamera == null && Camera.main != null) observerCamera = Camera.main.transform;
        }

        void Update()
        {
            if ((Time.frameCount + _frameOffset) % checkEveryNFrames != 0) return;
            if (_attacher == null || _attacher.currentPiece == null) return;
            var cam = observerCamera != null ? observerCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;

            float d = Vector3.Distance(cam.position, transform.position);
            int targetLod = d < lod0Dist ? 0 : d < lod1Dist ? 1 : d < lod2Dist ? 2 : 3;

            if (targetLod != _currentLod)
            {
                _currentLod = targetLod;
                if (targetLod >= 3)
                {
                    // cull – disable renderers
                    _attacher.ClearSlot("");
                }
                else
                {
                    _attacher.SetLOD(targetLod);
                }
            }
        }
    }
}
