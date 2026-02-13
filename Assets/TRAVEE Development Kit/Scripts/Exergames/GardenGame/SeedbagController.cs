using UnityEngine;

namespace GardenGame
{
    public class SeedbagController : MonoBehaviour, ISpotTool
    {
        [Header("Refs")]
        public GardenController Garden;

        public Transform LeftHandRef;
        public Transform RightHandRef;

        [Header("Timing")]
        public float CooldownSeconds = 1.0f;

        [Header("Seed Drop")]
        public GameObject SeedPrefab;
        public Transform SeedSpawnPoint;
        public Vector3 SeedLocalOffset = new Vector3(0f, 0f, 0.05f);
        public float SeedDownImpulse = 0.05f;
        public float SeedLifeSeconds = 0.5f;

        [Header("Gesture")]
        public float HysteresisDeg = 5f;

        [Header("Auto mode (small angles)")]
        [Tooltip("If PlantAngleDeg <= this, we use auto-plant mode")]
        public float AutoPlantAngleThresholdDeg = 5f;

        [Tooltip("In auto mode, wait this long after entering a spot before planting")]
        public float AutoPlantDelaySeconds = 0.20f;

        [Tooltip("In auto mode, the probe must be within this distance of the spot center to plant (meters)")]
        public float AutoPlantMaxDistanceM = 0.07f;

        [Tooltip("If true, in auto mode spawn the seed above spot center (avoids falling beside the pot)")]
        public bool AutoSpawnAtSpotCenter = true;

        [Tooltip("Vertical offset above spot center for seed spawn in auto mode (meters).")]
        public float AutoSpawnHeightM = 0.06f;

        public Transform SpotProbe => SeedSpawnPoint != null ? SeedSpawnPoint : transform;

        private PlantSpot _spot;
        private float _nextActionTime;

        private Quaternion _neutralRot;
        private bool _hasNeutral;
        private bool _primed;

        private float _plantAngleDeg;

        // auto mode state
        private float _autoArmedAt = -1f;

        void Start()
        {
            if (Garden == null) Garden = FindObjectOfType<GardenController>();
            _plantAngleDeg = (Garden != null) ? Garden.PlantAngleDeg : 30f;
        }

        public void SetCurrentSpot(PlantSpot spot)
        {
            _spot = spot;

            var src = GetHandRef();
            _neutralRot = src.rotation;
            _hasNeutral = true;

            if (IsAutoMode())
            {
                _primed = true;
                _autoArmedAt = Time.time; 
                return;
            }

            float delta0 = GetDeltaDeg(src);
            _primed = delta0 <= (_plantAngleDeg - HysteresisDeg);
        }

        public void ClearCurrentSpot(PlantSpot spot)
        {
            if (_spot != spot) return;
            _spot = null;
            _hasNeutral = false;
            _primed = false;
            _autoArmedAt = -1f;
        }

        void Update()
        {
            if (Garden == null) return;
            if (!Garden.CanPlantNow()) return;

            if (_spot == null) return;
            if (!_hasNeutral) return;
            if (Time.time < _nextActionTime) return;

            var src = GetHandRef();

            if (IsAutoMode())
            {
                if (_autoArmedAt > 0f && (Time.time - _autoArmedAt) < AutoPlantDelaySeconds)
                    return;

                if (!IsProbeNearSpotCenter(_spot))
                    return;

                TryPlant(src, autoMode: true);
                return;
            }

            float delta = GetDeltaDeg(src);

            if (!_primed)
            {
                if (delta <= (_plantAngleDeg - HysteresisDeg))
                    _primed = true;
                return;
            }

            if (delta >= _plantAngleDeg)
            {
                TryPlant(src, autoMode: false);
                _primed = false;
            }
        }

        private bool IsAutoMode()
        {

            return _plantAngleDeg <= AutoPlantAngleThresholdDeg;
        }

        private bool IsProbeNearSpotCenter(PlantSpot spot)
        {
            if (spot == null) return false;

            Transform probe = SpotProbe != null ? SpotProbe : transform;
            Vector3 probePos = probe.position;

            Vector3 center = spot.transform.position;
            var spotCol = spot.GetComponentInChildren<Collider>(true);
            if (spotCol != null)
                center = spotCol.bounds.center;

            float d = Vector3.Distance(probePos, center);
            return d <= AutoPlantMaxDistanceM;
        }

        private Transform GetHandRef()
        {
            var side = (Garden != null && Garden.Input != null)
                ? Garden.Input.BodySide
                : FruitsGame.BodySide.BODY_SIDE_LEFT;

            if (side == FruitsGame.BodySide.BODY_SIDE_RIGHT)
                return RightHandRef != null ? RightHandRef : (LeftHandRef != null ? LeftHandRef : transform);

            return LeftHandRef != null ? LeftHandRef : (RightHandRef != null ? RightHandRef : transform);
        }

        private float GetDeltaDeg(Transform src)
        {
            return Quaternion.Angle(_neutralRot, src.rotation);
        }

        private void TryPlant(Transform src, bool autoMode)
        {
            if (_spot == null) return;
            if (_spot.Planted) return;
            if (!_spot.TryPlant()) return;

            _nextActionTime = Time.time + CooldownSeconds;

            DropSeed(src, autoMode);

            if (Garden != null)
                Garden.OnPlantedSuccess();
        }

        private void DropSeed(Transform src, bool autoMode)
        {
            if (SeedPrefab == null) return;

            Vector3 pos;
            Quaternion rot = Quaternion.identity;

            if (autoMode && AutoSpawnAtSpotCenter && _spot != null)
            {

                Vector3 center = _spot.transform.position;
                var spotCol = _spot.GetComponentInChildren<Collider>(true);
                if (spotCol != null)
                    center = spotCol.bounds.center;

                pos = center + Vector3.up * Mathf.Max(0f, AutoSpawnHeightM);
            }
            else
            {
                Transform spawn = SeedSpawnPoint != null ? SeedSpawnPoint : src;

                pos = (SeedSpawnPoint != null)
                    ? SeedSpawnPoint.position
                    : spawn.TransformPoint(SeedLocalOffset);

                rot = (SeedSpawnPoint != null) ? SeedSpawnPoint.rotation : Quaternion.identity;
            }

            GameObject seed = Instantiate(SeedPrefab, pos, rot);

            if (seed.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(Vector3.down * SeedDownImpulse, ForceMode.Impulse);
            }

            if (SeedLifeSeconds > 0f)
                Destroy(seed, SeedLifeSeconds);
        }
    }
}
