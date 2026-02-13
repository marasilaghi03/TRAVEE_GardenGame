using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using GardenGame;

namespace FruitsGame
{
    public class TouchInteractable : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] public Collider InteractableCollider;

        private bool _locked;
        private static bool _lockedFruit;

        private InputData _inputData;
        private BodySide _bodySide;

        private GardenController _garden;
        private UnityEvent _onStartVibration;

        private Coroutine _autoReleaseCo;

        public Transform GripPivot;

        private Transform _homeParent;
        private Vector3 _homeLocalPos;
        private Quaternion _homeLocalRot;

        void Awake()
        {
            _homeParent = transform.parent;
            _homeLocalPos = transform.localPosition;
            _homeLocalRot = transform.localRotation;
        }


        // Optional: if your old systems rely on Init being called, keep it.
        public void Init(
            InputData inputData,
            BodySide fruitBodySide,
            Transform basketTransform,                 // unused (kept for compatibility)
            FruitsGatherController fruitsGatherController, // unused (kept for compatibility)
            Transform fallTargetTransform,             // unused (kept for compatibility)
            UnityAction onStartVibrationAction)
        {
            _inputData = inputData;
            _bodySide = fruitBodySide;

            _garden = GameObject.Find("FruitsController")?.GetComponent<GardenController>();

            _lockedFruit = false;
            _locked = false;

            _onStartVibration = new UnityEvent();
            if (onStartVibrationAction != null)
                _onStartVibration.AddListener(onStartVibrationAction);
        }

        public void ForceLockToPalm(BodySide side)
        {
            Debug.Log($"[ForceLockToPalm_IgnoreSide] called on {name} tag={tag} side={side}");

            if (_locked || _lockedFruit) return;
            if (_bodySide != side) return;

            if (!IsAllowedByGarden())
                return;

            Transform palm = GetPalmTransformFor(side);
            if (palm == null) return;

            LockToPalm(palm);

            if (_inputData != null && _inputData.Haptic)
                _onStartVibration?.Invoke();
        }

        public void ForceLockToPalm_IgnoreSide(BodySide side)
        {
            if (_locked || _lockedFruit) return;

            if (!IsAllowedByGarden())
                return;

            Transform palm = GetPalmTransformFor(side);
            if (palm == null) return;

            LockToPalm(palm);

            // We don't have "other" here, so we find the correct hand detector by side
            var detectors = FindObjectsOfType<TriggerDetector>();
            for (int i = 0; i < detectors.Length; i++)
            {
                var d = detectors[i];
                if (d == null) continue;

                // Simplu pentru acum: seteaza pe toate (merge daca ai 1 detector activ / sau vrei debug rapid)
                if (CompareTag("SeedBag"))
                    d.HeldTool = GetComponent<GardenGame.SeedbagController>();
                else if (CompareTag("WateringCan"))
                    d.HeldTool = GetComponent<GardenGame.WateringCanController>();
            }


            // Register grab for analytics/stage logic (kept, as you had it here)
            if (_garden != null)
                _garden.RegisterGrab(_bodySide, gameObject.tag);

            // Auto-release only for non-tools
            if (!IsToolTag(gameObject.tag))
                AutoReleaseAfter(0.5f);

            if (_inputData != null && _inputData.Haptic)
                _onStartVibration?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_locked || _lockedFruit) return;

            int handTouchLayer = LayerMask.NameToLayer("HandTouch");
            if (handTouchLayer == -1)
            {
                Debug.LogError("[TouchInteractable] Layer 'HandTouch' nu exista!");
                return;
            }

            if (other.gameObject.layer != handTouchLayer)
                return;

            // Optional: keep left/right validation by name (as you had)
            if (!MatchesExpectedHand(other.transform))
                return;

            if (!IsAllowedByGarden())
                return;

            Transform palm = GetPalmTransformFor(_bodySide);
            if (palm == null)
            {
                Debug.LogError("[TouchInteractable] Palm transform NULL in FruitsGameContainer!");
                return;
            }

            LockToPalm(palm);

            // Wire held tool into TriggerDetector (spot detection follows the TOOL, not the hand)
            var detector = other.GetComponentInParent<TriggerDetector>();
            if (detector != null)
            {
                if (CompareTag("SeedBag"))
                    detector.HeldTool = GetComponent<GardenGame.SeedbagController>();
                else if (CompareTag("WateringCan"))
                    detector.HeldTool = GetComponent<GardenGame.WateringCanController>();
                // else if (CompareTag("Trowel")) detector.HeldTool = GetComponent<...>();
            }


            // Physics freeze
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (_inputData != null && _inputData.Haptic)
                _onStartVibration?.Invoke();

            // Auto-release for non-tools (kept behavior)
            if (!IsToolTag(gameObject.tag))
                AutoReleaseAfter(0.5f);

            Debug.Log($"[TouchInteractable] LOCKED {name} to {_bodySide} palm={palm.name}");
        }

        private void LockToPalm(Transform palm)
        {
            _locked = true;
            _lockedFruit = true;
            Debug.Log($"[LockToPalm] {name} grip={(GripPivot ? GripPivot.name : "NULL")} gripLocalRot={(GripPivot ? GripPivot.localRotation.eulerAngles : Vector3.zero)}");

            if (GripPivot == null)
            {
                transform.SetParent(palm, worldPositionStays: false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            // 1) păstrăm world pose curentă
            Vector3 rootPos = transform.position;
            Quaternion rootRot = transform.rotation;

            // 2) ce transform local are GripPivot față de root
            Vector3 gripLocalPos = GripPivot.localPosition;
            Quaternion gripLocalRot = GripPivot.localRotation;

            // 3) vrem: root * (gripLocal) == palm
            // => rootRot = palmRot * inverse(gripLocalRot)
            Quaternion targetRootRot = palm.rotation * Quaternion.Inverse(gripLocalRot);

            // poziția: palmPos = rootPos + rootRot * gripLocalPos
            // => rootPos = palmPos - rootRot * gripLocalPos
            Vector3 targetRootPos = palm.position - (targetRootRot * gripLocalPos);

            // 4) aplicăm și apoi parintează la palm, păstrând world
            transform.SetPositionAndRotation(targetRootPos, targetRootRot);
            transform.SetParent(palm, worldPositionStays: true);
        }



        private Transform GetPalmTransformFor(BodySide side)
        {
            var container = FruitsGameContainer.Instance;
            if (container == null) return null;

            return side == BodySide.BODY_SIDE_LEFT
                ? container.LeftHandPalmTransform
                : container.RightHandPalmTransform;
        }

        private bool IsAllowedByGarden()
        {
            if (_garden == null) return true;

            return true;
        }

        private static bool IsToolTag(string tag)
        {
            return tag == "SeedBag" || tag == "WateringCan" || tag == "Trowel";
        }

        private bool MatchesExpectedHand(Transform other)
        {
            bool isLeft = false;
            bool isRight = false;

            Transform p = other;
            while (p != null)
            {
                string n = p.name;

                if (n.Contains("Left") || n.Contains("_L") || n.EndsWith("L"))
                    isLeft = true;
                if (n.Contains("Right") || n.Contains("_R") || n.EndsWith("R"))
                    isRight = true;

                p = p.parent;
            }

            // if can't detect, allow (as you did)
            if (!isLeft && !isRight)
                return true;

            if (_bodySide == BodySide.BODY_SIDE_LEFT && isRight)
                return false;
            if (_bodySide == BodySide.BODY_SIDE_RIGHT && isLeft)
                return false;

            return true;
        }

        public void AutoReleaseAfter(float delaySeconds)
        {
            if (_autoReleaseCo != null) StopCoroutine(_autoReleaseCo);
            _autoReleaseCo = StartCoroutine(AutoReleaseRoutine(delaySeconds));
        }

        private IEnumerator AutoReleaseRoutine(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            ForceRelease();
            _autoReleaseCo = null;
        }

        public void ForceRelease()
        {
            if (!_locked) return;

            _locked = false;
            _lockedFruit = false;

            if (IsToolTag(gameObject.tag))
            {
                var detectors = FindObjectsOfType<TriggerDetector>();
                for (int i = 0; i < detectors.Length; i++)
                {
                    var d = detectors[i];
                    if (d != null && d.HeldTool != null && d.HeldTool.gameObject == gameObject)
                        d.HeldTool = null;
                }
            }


            transform.SetParent(null, true);

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void ReturnToHome(float delaySeconds)
        {
            StartCoroutine(ReturnToHomeRoutine(delaySeconds));
        }

        private IEnumerator ReturnToHomeRoutine(float delaySeconds)
        {
            if (CompareTag("WateringCan"))
            {
                var wc = GetComponent<GardenGame.WateringCanController>();
                if (wc != null) wc.StopPouring();
            }

            // elibereaza imediat din mana
            ForceRelease();

            // mic delay (feedback vizual pentru utilizator)
            yield return new WaitForSeconds(delaySeconds);

            // readuce la locul initial
            if (_homeParent != null)
                transform.SetParent(_homeParent, worldPositionStays: false);
            else
                transform.SetParent(null, true);

            transform.localPosition = _homeLocalPos;
            transform.localRotation = _homeLocalRot;

            // opreste fizica
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }


    }
}
