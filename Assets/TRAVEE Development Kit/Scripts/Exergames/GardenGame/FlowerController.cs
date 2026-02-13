using System.Collections.Generic;
using UnityEngine;

namespace GardenGame
{
    public class FlowerController : MonoBehaviour
    {
        public enum StemAxis { Y_Up, Z_Forward }

        [Header("Refs")]
        public PlantSpot Spot;
        public Transform LeftGripPivot;
        public Transform RightGripPivot;
        public GameObject FlowerRoot;
        public Transform BouquetBendPivot;

        [Header("Model")]
        public StemAxis FlowerStemAxis = StemAxis.Z_Forward;

        [Header("Harvest")]
        public float HoldSeconds = 0.2f;

        // Fixed "good" values
        private const float SpreadRadiusM = 0.010f;
        private const float DownOffsetM = 0.008f;
        private const float ForwardOffsetM = 0.010f;
        private const float PosJitterM = 0.0015f;

        private const int CoreCount = 3;
        private const int Ring1Capacity = 4;
        private const int RingCapacityGrowth = 3;
        private const float RingRadiusStepM = 0.005f;

        private const float YawDeg = 18f;
        private const float PitchDeg = 10f;
        private const float RollDeg = 10f;

        private GardenController _garden;
        private Collider _handCol;
        private FruitsGame.BodySide _side;
        private float _enteredAt;
        private bool _done;

        private static int _bouquetLeft = 0;
        private static int _bouquetRight = 0;

        // cleanup
        private static readonly HashSet<FlowerController> _picked = new HashSet<FlowerController>();

        private Transform _homeParent;
        private Vector3 _homeLocalPos;
        private Quaternion _homeLocalRot;

        // NEW: keep original root scale (important! root scale must NOT stay 0)
        private Vector3 _homeLocalScale;

        private bool _inHand;

        private bool _hadRb;
        private bool _rbWasKinematic;
        private bool _rbUsedGravity;

        private Collider[] _allColliders;
        private bool[] _colliderWasEnabled;

        void Reset()
        {
            if (Spot == null) Spot = GetComponentInParent<PlantSpot>();
        }

        void Awake()
        {
            if (FlowerRoot == null) FlowerRoot = gameObject;
            if (BouquetBendPivot == null) BouquetBendPivot = FlowerRoot.transform;

            _garden = FindObjectOfType<GardenController>(true);

            CacheHome();
            CachePhysicsState();
        }

        private void CacheHome()
        {
            var t = FlowerRoot.transform;
            _homeParent = t.parent;
            _homeLocalPos = t.localPosition;
            _homeLocalRot = t.localRotation;

            // NEW: cache scale (so we can restore it for round 2+)
            _homeLocalScale = t.localScale;
        }

        private void CachePhysicsState()
        {
            var rb = FlowerRoot.GetComponent<Rigidbody>();
            _hadRb = rb != null;
            if (_hadRb)
            {
                _rbWasKinematic = rb.isKinematic;
                _rbUsedGravity = rb.useGravity;
            }

            _allColliders = FlowerRoot.GetComponentsInChildren<Collider>(true);
            _colliderWasEnabled = new bool[_allColliders.Length];
            for (int i = 0; i < _allColliders.Length; i++)
                _colliderWasEnabled[i] = _allColliders[i] != null && _allColliders[i].enabled;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_done) return;
            if (!CompareTag("Flower")) return;
            if (_garden != null && !_garden.CanHarvestNow()) return;

            int handLayer = LayerMask.NameToLayer("HandTouch");
            if (handLayer != -1 && other.gameObject.layer != handLayer) return;

            if (!TryGetSide(other.transform, out _side)) return;
            if (!IsAllowedHandByInput(_side)) return;

            _handCol = other;
            _enteredAt = Time.time;
        }

        void OnTriggerStay(Collider other)
        {
            if (_done) return;
            if (other != _handCol) return;
            if (Spot == null) return;

            if (_garden != null && !_garden.CanHarvestNow()) return;
            if (Time.time - _enteredAt < HoldSeconds) return;

            Harvest();
        }

        void OnTriggerExit(Collider other)
        {
            if (_done) return;
            if (other != _handCol) return;
            _handCol = null;
        }

        private bool IsAllowedHandByInput(FruitsGame.BodySide hand)
        {
            if (_garden == null || _garden.Input == null) return true;

            var wanted = _garden.Input.BodySide;
            if (wanted == FruitsGame.BodySide.BODY_SIDE_BOTH) return true;
            return wanted == hand;
        }

        private void Harvest()
        {
            if (_done) return;
            _done = true;

            if (!Spot.TryHarvest())
            {
                _done = false;
                return;
            }

            _garden?.OnHarvestedSuccess();

            var palm = GetPalm(_side);
            if (palm == null) return;

            int index = GetBouquetIndex(_side);

            AttachToPalm(palm, GetGripPivot(_side));
            ApplyBouquetPose(index, palm);
            MakeSafeInHand();

            _inHand = true;
            _picked.Add(this);
        }

        private int GetBouquetIndex(FruitsGame.BodySide side)
        {
            if (side == FruitsGame.BodySide.BODY_SIDE_RIGHT) return _bouquetRight++;
            return _bouquetLeft++;
        }

        private static void GetStablePalmFrame(Transform palm, out Vector3 up, out Vector3 fwd, out Vector3 right)
        {
            up = palm.up;

            fwd = Vector3.ProjectOnPlane(palm.forward, up).normalized;
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;

            right = Vector3.Cross(up, fwd).normalized;
            fwd = Vector3.Cross(right, up).normalized;
        }

        private void ApplyBouquetPose(int index, Transform palm)
        {
            BouquetBendPivot.localPosition = Vector3.zero;
            BouquetBendPivot.localRotation = Quaternion.identity;

            var rng = new System.Random(index * 92821 + 7);

            float angleDeg;
            int ringIndex;
            float ringRadius;

            if (index < CoreCount)
            {
                angleDeg = (CoreCount <= 1) ? 0f : (360f / CoreCount) * index;
                ringIndex = 0;
                ringRadius = 0f;
            }
            else
            {
                int k = index - CoreCount;

                int ring = 1;
                int cap = Mathf.Max(1, Ring1Capacity);
                int start = 0;

                while (k >= start + cap)
                {
                    start += cap;
                    ring++;
                    cap = Mathf.Max(1, Ring1Capacity + (ring - 1) * RingCapacityGrowth);
                }

                int posInRing = k - start;
                float u = (cap <= 1) ? 0f : posInRing / (float)cap;

                angleDeg = u * 360f;
                ringIndex = ring;
                ringRadius = ring * RingRadiusStepM;
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            float spread = Mathf.Min(SpreadRadiusM, SpreadRadiusM * 0.65f + ringRadius);

            Vector3 pos =
                new Vector3(Mathf.Cos(rad) * spread, 0f, Mathf.Sin(rad) * spread) +
                new Vector3(0f, -DownOffsetM, ForwardOffsetM);

            pos += new Vector3(
                RandomSigned(rng) * PosJitterM,
                RandomSigned(rng) * PosJitterM,
                RandomSigned(rng) * PosJitterM
            );

            BouquetBendPivot.localPosition = pos;

            GetStablePalmFrame(palm, out var up, out var fwd, out var right);

            Vector3 fromAxis = (FlowerStemAxis == StemAxis.Y_Up) ? Vector3.up : Vector3.forward;
            Quaternion alignToUp = Quaternion.FromToRotation(fromAxis, up);

            float yaw = RandomSigned(rng) * YawDeg;
            if (_side == FruitsGame.BodySide.BODY_SIDE_RIGHT) yaw = -yaw;

            float pitch = RandomSigned(rng) * PitchDeg * (ringIndex == 0 ? 0.6f : 1f);
            float roll = RandomSigned(rng) * RollDeg * (ringIndex == 0 ? 0.6f : 1f);

            Quaternion yawRot = Quaternion.AngleAxis(yaw, up);
            Quaternion pitchRot = Quaternion.AngleAxis(pitch, right);
            Quaternion rollRot = Quaternion.AngleAxis(roll, fwd);

            BouquetBendPivot.rotation = yawRot * pitchRot * rollRot * alignToUp * BouquetBendPivot.rotation;
        }

        private static float RandomSigned(System.Random rng)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        private void MakeSafeInHand()
        {
            var rb = FlowerRoot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var cols = _allColliders ?? FlowerRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = false;
        }

        public static void ClearAllPickedFlowers()
        {
            foreach (var f in _picked)
            {
                if (f == null) continue;
                f.ReturnToHome();
            }
            _picked.Clear();

            _bouquetLeft = 0;
            _bouquetRight = 0;
        }

        // in cazul in care se joaca iar
        private void ReturnToHome()
        {
            if (!_inHand) return;

            // reset bouquet bend
            if (BouquetBendPivot != null)
            {
                BouquetBendPivot.localPosition = Vector3.zero;
                BouquetBendPivot.localRotation = Quaternion.identity;
            }

            // IMPORTANT: root scale must be restored, not forced to 0
            var t = FlowerRoot.transform;
            t.SetParent(_homeParent, false);
            t.localPosition = _homeLocalPos;
            t.localRotation = _homeLocalRot;
            t.localScale = _homeLocalScale;

            // Make the bouquet disappear by scaling ONLY visible meshes to 0 (not the root)
            // This matches your setup: SM_* objects start with scale 0 until Grow anim.
            var renderers = FlowerRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].transform.localScale = Vector3.zero;
            }

            var rb = FlowerRoot.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = _rbWasKinematic;
                rb.useGravity = _rbUsedGravity;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (_allColliders != null && _colliderWasEnabled != null)
            {
                int n = Mathf.Min(_allColliders.Length, _colliderWasEnabled.Length);
                for (int i = 0; i < n; i++)
                    if (_allColliders[i] != null) _allColliders[i].enabled = _colliderWasEnabled[i];
            }

            _inHand = false;
            _done = false;
            _handCol = null;
        }

        private Transform GetGripPivot(FruitsGame.BodySide side)
        {
            if (side == FruitsGame.BodySide.BODY_SIDE_RIGHT)
                return RightGripPivot != null ? RightGripPivot : LeftGripPivot;

            return LeftGripPivot != null ? LeftGripPivot : RightGripPivot;
        }

        private void AttachToPalm(Transform palm, Transform gripPivot)
        {
            var rootT = FlowerRoot.transform;

            if (gripPivot == null)
            {
                rootT.SetParent(palm, false);
                rootT.localPosition = Vector3.zero;
                rootT.localRotation = Quaternion.identity;
                return;
            }

            Vector3 gripLocalPos = rootT.InverseTransformPoint(gripPivot.position);
            Quaternion gripLocalRot = Quaternion.Inverse(rootT.rotation) * gripPivot.rotation;

            Quaternion targetRot = palm.rotation * Quaternion.Inverse(gripLocalRot);
            Vector3 targetPos = palm.position - (targetRot * gripLocalPos);

            rootT.SetPositionAndRotation(targetPos, targetRot);
            rootT.SetParent(palm, true);
        }

        private static Transform GetPalm(FruitsGame.BodySide side)
        {
            var rig = HandsRig.Instance;
            if (rig == null) return null;

            return side == FruitsGame.BodySide.BODY_SIDE_LEFT ? rig.LeftPalm : rig.RightPalm;
        }

        private static bool TryGetSide(Transform t, out FruitsGame.BodySide side)
        {
            bool left = false, right = false;

            for (Transform p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.Contains("Left") || n.Contains("_L") || n.EndsWith("L")) left = true;
                if (n.Contains("Right") || n.Contains("_R") || n.EndsWith("R")) right = true;
            }

            if (!left && !right)
            {
                side = FruitsGame.BodySide.BODY_SIDE_LEFT;
                return false;
            }

            side = right ? FruitsGame.BodySide.BODY_SIDE_RIGHT : FruitsGame.BodySide.BODY_SIDE_LEFT;
            return true;
        }
    }
}
