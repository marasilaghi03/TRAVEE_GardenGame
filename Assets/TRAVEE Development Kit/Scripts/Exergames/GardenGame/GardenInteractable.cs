using System.Collections;
using UnityEngine;
using FruitsGame;

namespace GardenGame
{
    public class GardenInteractable : MonoBehaviour
    {
        public enum HandSide { Left, Right }

        [Header("Refs")]
        [SerializeField] private Collider interactableCollider;

        [Header("Grip")]
        [SerializeField] private Transform leftGripPivot;
        [SerializeField] private Transform rightGripPivot;

        [Header("Config")]
        [SerializeField] private bool isTool = true;

        private bool _locked;
        //unde se intoarce unealta dupa folosire
        private Transform _homeParent;
        private Vector3 _homeLocalPos;
        private Quaternion _homeLocalRot;
        private bool _homeCaptured;

        private GardenController _garden;
        private Coroutine _returnCo;

        void Awake()
        {
            CaptureHome();
        }

        IEnumerator Start()
        {
            _garden = FindObjectOfType<GardenController>(true);

            yield return null;

            if (isTool)
                CaptureHome();
        }

        public void CaptureHome()
        {
            _homeParent = transform.parent;
            _homeLocalPos = transform.localPosition;
            _homeLocalRot = transform.localRotation;
            _homeCaptured = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_locked) return;
            if (!IsHandTouch(other)) return;
            if (!IsAllowedNow()) return;

            if (!TryGetHandSide(other, out var handSide)) return;
            if (!IsAllowedHand(handSide)) return;

            var palm = GetPalm(handSide);
            if (palm == null) return;

            AttachToPalm(palm, handSide);

            var detector = other.GetComponentInParent<TriggerDetector>();
            if (detector != null)
            {
                if (CompareTag("SeedBag"))
                    detector.HeldTool = GetComponent<SeedbagController>();
                else if (CompareTag("WateringCan"))
                    detector.HeldTool = GetComponent<WateringCanController>();
            }
        }

        public void ForceRelease()
        {
            if (!_locked) return;
            _locked = false;

            ClearHeldToolRefs();

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
            if (_returnCo != null) StopCoroutine(_returnCo);
            _returnCo = StartCoroutine(ReturnHomeRoutine(delaySeconds));
        }

        private IEnumerator ReturnHomeRoutine(float delaySeconds)
        {
            var wc = CompareTag("WateringCan") ? GetComponent<WateringCanController>() : null;

            if (wc != null)
                wc.PlayFxFor(delaySeconds);

            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            if (wc != null)
                wc.StopPouring();

            ForceRelease();

            _locked = false;
            ClearHeldToolRefs();

            if (!_homeCaptured)
                CaptureHome();

            if (_homeParent != null)
                transform.SetParent(_homeParent, false);
            else
                transform.SetParent(null, true);

            transform.localPosition = _homeLocalPos;
            transform.localRotation = _homeLocalRot;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            _returnCo = null;
        }

        private void ClearHeldToolRefs()
        {
            var detectors = FindObjectsOfType<TriggerDetector>(true);
            for (int i = 0; i < detectors.Length; i++)
            {
                var d = detectors[i];
                if (d != null && d.HeldTool != null && d.HeldTool.gameObject == gameObject)
                    d.HeldTool = null;
            }
        }

        private bool IsAllowedNow()
        {
            if (_garden == null) return true;
            if (!_garden.Running) return false;
            if (_garden.InTransition) return false;

            if (_garden.Stage == GardenStage.Planting) return CompareTag("SeedBag");
            if (_garden.Stage == GardenStage.Watering) return CompareTag("WateringCan");
            if (_garden.Stage == GardenStage.Harvesting) return CompareTag("Flower");

            return false;
        }

        private bool IsAllowedHand(HandSide hand)
        {
            if (_garden == null || _garden.Input == null) return true;

            var wanted = _garden.Input.BodySide;
            if (wanted == FruitsGame.BodySide.BODY_SIDE_BOTH) return true;

            if (wanted == FruitsGame.BodySide.BODY_SIDE_LEFT) return hand == HandSide.Left;
            if (wanted == FruitsGame.BodySide.BODY_SIDE_RIGHT) return hand == HandSide.Right;

            return true;
        }

        private static bool IsHandTouch(Collider other)
        {
            int handTouchLayer = LayerMask.NameToLayer("HandTouch");
            if (handTouchLayer == -1) return false;
            return other.gameObject.layer == handTouchLayer;
        }

        private Transform GetPalm(HandSide hand)
        {
            var rig = HandsRig.Instance;
            if (rig == null) return null;

            return hand == HandSide.Left ? rig.LeftPalm : rig.RightPalm;
        }

        private Transform GetGripPivot(HandSide hand)
        {
            return hand == HandSide.Left ? leftGripPivot : rightGripPivot;
        }

        private void AttachToPalm(Transform palm, HandSide hand)
        {
            _locked = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            var pivot = GetGripPivot(hand);
            if (pivot == null)
            {
                transform.SetParent(palm, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            Vector3 gripLocalPos = pivot.localPosition;
            Quaternion gripLocalRot = pivot.localRotation;

            Quaternion targetRot = palm.rotation * Quaternion.Inverse(gripLocalRot);
            Vector3 targetPos = palm.position - (targetRot * gripLocalPos);

            transform.SetPositionAndRotation(targetPos, targetRot);
            transform.SetParent(palm, true);
        }

        private static bool TryGetHandSide(Collider other, out HandSide handSide)
        {
            handSide = HandSide.Left;

            bool isLeft = false;
            bool isRight = false;

            Transform p = other.transform;
            while (p != null)
            {
                string n = p.name;

                if (n.Contains("Left") || n.Contains("_L") || n.EndsWith("L"))
                    isLeft = true;
                if (n.Contains("Right") || n.Contains("_R") || n.EndsWith("R"))
                    isRight = true;

                p = p.parent;
            }

            if (!isLeft && !isRight)
                return false;

            handSide = isRight ? HandSide.Right : HandSide.Left;
            return true;
        }
    }
}
