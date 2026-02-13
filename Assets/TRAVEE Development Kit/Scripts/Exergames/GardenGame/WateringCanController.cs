using UnityEngine;
using FruitsGame;

namespace GardenGame
{
    public class WateringCanController : MonoBehaviour, ISpotTool
    {
        public enum GestureMode
        {
            AnyAxisRotation,
            YawAroundNeutralUp
        }

        [Header("Refs")]
        public GardenController Garden;
        public Transform LeftHandRef;
        public Transform RightHandRef;
        public Transform Nozzle;

        public Transform SpotProbe => Nozzle != null ? Nozzle : transform;

        [Header("Gesture")]
        public GestureMode Mode = GestureMode.YawAroundNeutralUp;
        private float AngleToWaterDeg;
        public float HysteresisDeg = 5f;

        [Header("Timing")]
        public float CooldownSeconds = 0.6f;

        [Header("Water Fx")]
        public GameObject DropPrefab;
        public float DropsPerSecond = 25f;
        public float DropDownSpeed = 0.6f;
        public float DropLifeSeconds = 0.4f;
        public float DropSpread = 0.02f;

        [Header("Override")]
        public bool AllowWateringInAnyStage = true;

        [Header("Auto mode (small angles)")]
        [Tooltip("If WaterAngleDeg <= this, we use auto-water mode (no wrist gesture required).")]
        public float AutoWaterAngleThresholdDeg = 5f;

        [Tooltip("In auto mode, wait this long after entering a spot before watering.")]
        public float AutoWaterDelaySeconds = 0.20f;

        [Tooltip("In auto mode, the nozzle/probe must be within this distance of the spot center to water (meters).")]
        public float AutoWaterMaxDistanceM = 0.08f;

        [Tooltip("In auto mode, spawn drops above spot center instead of nozzle (more accurate when pivots/meshes are odd).")]
        public bool AutoDropsAtSpotCenter = true;

        [Tooltip("Vertical offset above spot center for drop spawn in auto mode (meters).")]
        public float AutoDropHeightM = 0.10f;

        private PlantSpot _spot;
        private float _nextAllowedTime;

        private Quaternion _neutralRot;
        private Vector3 _neutralUp;
        private bool _hasNeutral;
        private bool _primed;

        private float _nextDropAt;

        // auto mode state
        private float _autoArmedAt = -1f;

        private float _fxUntilTime = -1f;

        void Start()
        {
            if (Garden == null)
                Garden = FindObjectOfType<GardenController>();

            if (Garden != null)
                AngleToWaterDeg = Garden.WaterAngleDeg;
        }

        public void StopPouring()
        {
            _spot = null;
            _hasNeutral = false;
            _primed = false;
            _nextDropAt = float.PositiveInfinity;
            _autoArmedAt = -1f;
        }

        public void SetCurrentSpot(PlantSpot spot)
        {
            _spot = spot;
            CalibrateNeutral();

            if (IsAutoMode())
            {
                _primed = true;
                _autoArmedAt = Time.time;
                return;
            }

            float delta0 = GetDeltaDeg();
            _primed = delta0 <= (AngleToWaterDeg - HysteresisDeg);
        }

        public void ClearCurrentSpot(PlantSpot spot)
        {
            if (_spot != spot) return;

            _spot = null;
            _hasNeutral = false;
            _primed = false;
            _autoArmedAt = -1f;
        }

        public void PlayFxFor(float seconds) // keep drops flowing for a bit at the end
        {
            _fxUntilTime = Mathf.Max(_fxUntilTime, Time.time + Mathf.Max(0f, seconds));
            _nextDropAt = 0f; // start immediately
        }

        void Update()
        {
            if (Garden == null) return;

            bool fxOverride = Time.time < _fxUntilTime;


            if (fxOverride)
            {
                SpawnDrops(autoMode: false);
                return;
            }

            //normal
            if (!Garden.CanWaterNow()) return;
            if (_spot == null) return;
            if (!_hasNeutral) return;
            if (Time.time < _nextAllowedTime) return;

            //unghi mic
            if (IsAutoMode())
            {
                if (_autoArmedAt > 0f && (Time.time - _autoArmedAt) < AutoWaterDelaySeconds)
                    return;

                if (!IsProbeNearSpotCenter(_spot))
                    return;

                SpawnDrops(autoMode: true);

                TryWater();
                return;
            }

            float delta = GetDeltaDeg();

            if (delta >= AngleToWaterDeg * 0.7f)
                SpawnDrops(autoMode: false);

            if (!_primed)
            {
                if (delta <= (AngleToWaterDeg - HysteresisDeg))
                    _primed = true;
                return;
            }

            if (delta >= AngleToWaterDeg)
            {
                TryWater();
                _primed = false;
            }
        }

        private bool IsAutoMode()
        {
            return AngleToWaterDeg <= AutoWaterAngleThresholdDeg;
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
            return d <= AutoWaterMaxDistanceM;
        }

        private Transform GetHandRef()
        {
            var side = (Garden != null && Garden.Input != null)
                ? Garden.Input.BodySide
                : FruitsGame.BodySide.BODY_SIDE_LEFT;

            if (side == FruitsGame.BodySide.BODY_SIDE_RIGHT)
                return RightHandRef != null
                    ? RightHandRef
                    : (LeftHandRef != null ? LeftHandRef : transform);

            return LeftHandRef != null
                ? LeftHandRef
                : (RightHandRef != null ? RightHandRef : transform);
        }

        private void CalibrateNeutral()
        {
            Transform src = GetHandRef();
            _neutralRot = src.rotation;
            _neutralUp = src.up;
            _hasNeutral = true;
        }

        private float GetDeltaDeg()
        {
            Transform src = GetHandRef();

            if (Mode == GestureMode.AnyAxisRotation)
                return Quaternion.Angle(_neutralRot, src.rotation);

            Vector3 axis = _neutralUp.sqrMagnitude > 0.0001f ? _neutralUp : Vector3.up;

            Vector3 v0 = _neutralRot * Vector3.right;
            Vector3 v1 = src.right;

            v0 = Vector3.ProjectOnPlane(v0, axis);
            v1 = Vector3.ProjectOnPlane(v1, axis);

            if (v0.sqrMagnitude < 0.0001f || v1.sqrMagnitude < 0.0001f)
                return 0f;

            return Mathf.Abs(Vector3.SignedAngle(v0.normalized, v1.normalized, axis));
        }

        private void TryWater()
        {
            if (_spot == null) return;
            if (!_spot.TryWater()) return;

            _nextAllowedTime = Time.time + CooldownSeconds;

            if (Garden != null)
                Garden.OnWateredSuccess();
        }

        private void SpawnDrops(bool autoMode)
        {
            if (DropPrefab == null)
                return;

            float interval = 1f / Mathf.Max(1f, DropsPerSecond);
            if (Time.time < _nextDropAt) return;
            _nextDropAt = Time.time + interval;

            Vector3 pos;

            if (autoMode && AutoDropsAtSpotCenter && _spot != null)
            {
                Vector3 center = _spot.transform.position;
                var spotCol = _spot.GetComponentInChildren<Collider>(true);
                if (spotCol != null)
                    center = spotCol.bounds.center;

                pos = center + Vector3.up * Mathf.Max(0f, AutoDropHeightM);
            }
            else
            {
                if (Nozzle == null) return;

                pos = Nozzle.position +
                      new Vector3(
                          Random.Range(-DropSpread, DropSpread),
                          0f,
                          Random.Range(-DropSpread, DropSpread)
                      );
            }

            GameObject drop = Instantiate(DropPrefab, pos, Quaternion.identity);

            if (drop.TryGetComponent<Rigidbody>(out var rb))
                rb.velocity = Vector3.down * DropDownSpeed;

            if (DropLifeSeconds > 0f)
                Destroy(drop, DropLifeSeconds);
        }
    }
}
