using UnityEngine;

namespace GardenGame
{
    public class PlantSpot : MonoBehaviour
    {
        [Header("State")]
        public bool Planted;
        public bool Watered;
        public bool Harvested;

        public bool TryPlant()
        {
            if (Planted)
            {
                Debug.Log("[PlantSpot] Already planted", this);
                return false;
            }

            Planted = true;
            Debug.Log("[PlantSpot] Planted OK", this);
            return true;
        }

        public bool TryWater()
        {
            if (!Planted)
            {
                Debug.Log("[PlantSpot] Cannot water: not planted yet", this);
                return false;
            }

            if (Watered)
            {
                Debug.Log("[PlantSpot] Already watered", this);
                return false;
            }

            Watered = true;
            Debug.Log("[PlantSpot] Watered OK", this);

            PlayGrowAnimation();
            return true;
        }

        private void PlayGrowAnimation()
        {

            FlowerVisualController visual = null;

            if (transform.parent != null)
                visual = transform.parent.GetComponentInChildren<FlowerVisualController>(true);

            if (visual == null)
                visual = GetComponentInParent<FlowerVisualController>(true);

            if (visual == null)
            {
                Debug.LogWarning("[PlantSpot] FlowerVisualController not found", this);
                return;
            }

            visual.EnsureFlowerChosen();
            visual.ApplyRandomYaw();

            var anim = visual.GetCurrentAnimator();
            if (anim == null)
            {
                Debug.LogWarning("[PlantSpot] No animator on active flower mesh", this);
                return;
            }

            anim.ResetTrigger("Grow");
            anim.SetTrigger("Grow");
        }

        public bool TryHarvest()
        {
            if (!Watered)
            {
                Debug.Log("[PlantSpot] Cannot harvest: not watered", this);
                return false;
            }

            if (Harvested)
            {
                Debug.Log("[PlantSpot] Already harvested", this);
                return false;
            }

            Harvested = true;
            Debug.Log("[PlantSpot] Harvested OK", this);
            return true;
        }

        public void ResetSpot()
        {
            Planted = false;
            Watered = false;
            Harvested = false;
        }
    }
}
