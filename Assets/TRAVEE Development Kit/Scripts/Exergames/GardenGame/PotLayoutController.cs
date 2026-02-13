using System.Collections.Generic;
using UnityEngine;
using FruitsGame;

namespace GardenGame
{
    /// <summary>
    /// - Pots: placed in 2 rows (normally back = ceil(n/2), front = rest).
    ///   Special case: if n==1, the single pot is placed on the FRONT row.
    /// - Tools: moved ONLY along table local X, keeping their initial local Y/Z.
    /// - Tool X is computed from FRONT row span (so fewer front pots => tools not too "aerated").
    /// </summary>
    public class PotLayoutController : MonoBehaviour
    {
        [Header("Refs")]
        public GardenController Garden;

        public Transform[] Pots;

        public Transform Center;

        [Header("Tools")]
        public Transform SeedbagRoot;
        public Transform SeedbagAnchor;       

        public Transform WateringCanRoot;
        public Transform WateringCanAnchor;    

        [Header("Layout")]
        [Tooltip("Distance between the two rows")]
        public float RowDepthM = 0.22f;

        [Tooltip("Minimum spacing between pots on local X")]
        public float MinSpacingXM = 0.18f;

        [Tooltip("Reserve per side INSIDE working distance for each tool (meters)")]
        public float ToolReservePerSideM = 0.22f;

        [Tooltip("Extra offset when tools must be placed OUTSIDE the pot span (meters)")]
        public float ToolOutsideOffsetM = 0.12f;

        private float _tableLocalY;
        private bool _yCached;


        private Vector3 _seedbagLocal0;
        private Vector3 _wateringLocal0;
        private bool _toolsCached;

        private GardenInteractable _seedbagInteractable;
        private GardenInteractable _wateringInteractable;

        void Awake()
        {
            CacheTableLocalY();
            CacheToolLocals();
            if (SeedbagRoot != null)
                _seedbagInteractable = SeedbagRoot.GetComponent<GardenInteractable>();
            if (WateringCanRoot != null)
                _wateringInteractable = WateringCanRoot.GetComponent<GardenInteractable>();
        }

        private void CacheTableLocalY()
        {
            _yCached = false;
            if (Center == null || Pots == null) return;

            var ys = new List<float>();
            for (int i = 0; i < Pots.Length; i++)
            {
                if (Pots[i] == null) continue;
                var spot = Pots[i].GetComponentInChildren<PlantSpot>(true);
                if (spot == null) continue;

                Vector3 local = Center.InverseTransformPoint(spot.transform.position);
                ys.Add(local.y);
            }

            if (ys.Count == 0) return;
            ys.Sort();
            _tableLocalY = ys[ys.Count / 2];
            _yCached = true;
        }

        private void CacheToolLocals()
        {
            _toolsCached = false;
            if (Center == null) return;

            if (SeedbagRoot != null)
                _seedbagLocal0 = Center.InverseTransformPoint(SeedbagRoot.position);

            if (WateringCanRoot != null)
                _wateringLocal0 = Center.InverseTransformPoint(WateringCanRoot.position);

            _toolsCached = true;

        }

        public void ApplyFromInput(InputData input)
        {
    

            if (!_yCached) CacheTableLocalY();
            if (!_toolsCached) CacheToolLocals();
            if (Garden == null) Garden = FindObjectOfType<GardenController>();

            int n = Mathf.Clamp(Mathf.Min(5, Pots.Length), 1, Mathf.Min(7, Pots.Length));
            float workingM = 0.60f;

            if (input != null)
            {
                if (input.PotCount > 0)
                    n = Mathf.Clamp(input.PotCount, 1, Mathf.Min(7, Pots.Length));

                if (input.WorkingDistanceCm > 0)
                    workingM = input.WorkingDistanceCm * 0.01f;
            }


            for (int i = 0; i < Pots.Length; i++)
                if (Pots[i] != null)
                    Pots[i].gameObject.SetActive(i < n);

            int backCount, frontCount;
            if (n == 1)
            {
                backCount = 0;
                frontCount = 1;
            }
            else
            {
                backCount = (n + 1) / 2;
                frontCount = n - backCount;
            }

            int maxRow = Mathf.Max(backCount, frontCount);

            float potsSpanInside = workingM - 2f * Mathf.Max(0f, ToolReservePerSideM);

            float spacingX;
            if (maxRow <= 1)
            {
                spacingX = 0f;
            }
            else
            {
                float candidate = potsSpanInside / (maxRow - 1);
                spacingX = Mathf.Max(candidate, MinSpacingXM);
            }

            float potSpanNeeded = (maxRow <= 1) ? 0f : spacingX * (maxRow - 1);

            float frontSpan = (frontCount <= 1) ? 0f : spacingX * (frontCount - 1);
            float frontHalfSpan = frontSpan * 0.5f;

            bool toolsFitInsideWorking = (potSpanNeeded + 2f * ToolReservePerSideM) <= workingM + 1e-5f;

            int potIndex = 0;
            if (backCount > 0)
                PlacePotRow(backCount, ref potIndex, +RowDepthM * 0.5f, spacingX);

            if (frontCount > 0)
                PlacePotRow(frontCount, ref potIndex, -RowDepthM * 0.5f, spacingX);

            PlaceToolsXOnly_FrontBased(workingM, frontHalfSpan, toolsFitInsideWorking);

            if (Garden != null) Garden.Target = n;
        }

        private void PlacePotRow(int count, ref int potIndex, float zOffset, float spacingX)
        {
            for (int i = 0; i < count; i++)
            {
                if (potIndex >= Pots.Length) return;

                Transform pot = Pots[potIndex];
                if (pot == null) { potIndex++; i--; continue; }

                float x = (count == 1) ? 0f : (i - (count - 1) * 0.5f) * spacingX;

                Vector3 local = new Vector3(x, _yCached ? _tableLocalY : 0f, zOffset);
                Vector3 targetWorld = Center.TransformPoint(local);

                var spot = pot.GetComponentInChildren<PlantSpot>(true);
                AlignRootByAnchorNoRotate(pot, spot != null ? spot.transform : null, targetWorld);

                FreezePhysics(pot);
                potIndex++;
            }
        }

        private void PlaceToolsXOnly_FrontBased(float workingM, float frontHalfSpan, bool fitInsideWorking)
        {
            if (Center == null) return;

            float halfW = workingM * 0.5f;

            float leftX;
            float rightX;

            if (fitInsideWorking)
            {
                float minOutsideFront = frontHalfSpan + ToolReservePerSideM;

                leftX = -Mathf.Max(minOutsideFront, 0f);
                rightX = +Mathf.Max(minOutsideFront, 0f);

                leftX = Mathf.Clamp(leftX, -halfW, +halfW);
                rightX = Mathf.Clamp(rightX, -halfW, +halfW);
            }
            else
            {
                leftX = -(frontHalfSpan + ToolOutsideOffsetM);
                rightX = +(frontHalfSpan + ToolOutsideOffsetM);
            }

            if (SeedbagRoot != null)
            {
                Vector3 local = _seedbagLocal0;
                local.x = leftX;

                Vector3 targetWorld = Center.TransformPoint(local);
                AlignRootByAnchorNoRotate(SeedbagRoot, SeedbagAnchor, targetWorld);
                FreezePhysics(SeedbagRoot);

                if (_seedbagInteractable == null)
                    _seedbagInteractable = SeedbagRoot.GetComponent<GardenInteractable>();
                _seedbagInteractable?.CaptureHome();
            }

            if (WateringCanRoot != null)
            {
                Vector3 local = _wateringLocal0;
                local.x = rightX;

                Vector3 targetWorld = Center.TransformPoint(local);
                AlignRootByAnchorNoRotate(WateringCanRoot, WateringCanAnchor, targetWorld);
                FreezePhysics(WateringCanRoot);

                if (_wateringInteractable == null)
                    _wateringInteractable = WateringCanRoot.GetComponent<GardenInteractable>();
                _wateringInteractable?.CaptureHome();
            }

            if (!fitInsideWorking)
            {
                Debug.LogWarning("[PotLayout] WorkingDistance too small");
            }
        }

        private void AlignRootByAnchorNoRotate(Transform root, Transform anchor, Vector3 targetWorld)
        {
            if (root == null) return;

            if (anchor != null)
                root.position += (targetWorld - anchor.position);
            else
                root.position = targetWorld;

        }

        private void FreezePhysics(Transform t)
        {
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
