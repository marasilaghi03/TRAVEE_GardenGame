using UnityEngine;

namespace GardenGame
{
    public enum FlowerType
    {
        Random = 0,
        RedRose = 1,
        BlackRose = 2,
        Dandelion = 3,
        Hyacinth = 4,
        Daisy = 5
    }

    public class FlowerVisualController : MonoBehaviour
    {
        [Header("Refs")]
        public GardenController Garden;
        public Transform FlowerYawPivot;

        [Header("Variants")]
        public GameObject RedRose;
        public GameObject BlackRose;
        public GameObject Dandelion;
        public GameObject Hyacinth;
        public GameObject Daisy;

        [Header("Random yaw (internal)")]
        public float RandomYawRangeDeg = 25f;

        private bool _typeChosen;
        private FlowerType _chosenType;
        private GameObject _activeGo;

        void Awake()
        {
            if (Garden == null)
                Garden = FindObjectOfType<GardenController>(true);
        }

        void Start()
        {
            HideAll();
        }

        public void ResetForNewRound()
        {
            _typeChosen = false;
            _chosenType = FlowerType.Random;
            _activeGo = null;

            if (FlowerYawPivot != null)
                FlowerYawPivot.localRotation = Quaternion.identity;

            HideAll();

            ResetAnimators(RedRose);
            ResetAnimators(BlackRose);
            ResetAnimators(Dandelion);
            ResetAnimators(Hyacinth);
            ResetAnimators(Daisy);
        }

        private void HideAll()
        {
            if (RedRose) RedRose.SetActive(false);
            if (BlackRose) BlackRose.SetActive(false);
            if (Dandelion) Dandelion.SetActive(false);
            if (Hyacinth) Hyacinth.SetActive(false);
            if (Daisy) Daisy.SetActive(false);
        }

        private static void ResetAnimators(GameObject go)
        {
            if (go == null) return;

            var anims = go.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] == null) continue;
                
                anims[i].Rebind();
                anims[i].Update(0f);

                anims[i].ResetTrigger("Grow");
            }
        }

        public void EnsureFlowerChosen()
        {
            if (_typeChosen) return;

            int type = (Garden != null && Garden.Input != null) ? Garden.Input.FlowerType : 0;
            _chosenType = (FlowerType)Mathf.Clamp(type, 0, 5);

            if (_chosenType == FlowerType.Random)
                _chosenType = FlowerRandomizer.Next();

            _activeGo = _chosenType switch
            {
                FlowerType.RedRose => RedRose,
                FlowerType.BlackRose => BlackRose,
                FlowerType.Dandelion => Dandelion,
                FlowerType.Hyacinth => Hyacinth,
                FlowerType.Daisy => Daisy,
                _ => null
            };

            HideAll();

            if (_activeGo != null)
                _activeGo.SetActive(true);

            _typeChosen = true;
        }

        public void ApplyRandomYaw()
        {
            if (FlowerYawPivot == null) return;

            float r = Mathf.Max(0f, RandomYawRangeDeg);
            float yaw = Random.Range(-r, r);

            FlowerYawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public Animator GetCurrentAnimator()
        {
            EnsureFlowerChosen();
            if (_activeGo == null) return null;

            var a = _activeGo.GetComponent<Animator>();
            if (a != null) return a;

            return _activeGo.GetComponentInChildren<Animator>(true);
        }
    }
}
