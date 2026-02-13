using System.Collections;
using UnityEngine;
using FruitsGame;

namespace GardenGame
{
    public class GardenController : MonoBehaviour
    {
        [Header("Input")]
        public InputData Input;

        [Header("State")]
        public GardenStage Stage = GardenStage.Planting;
        public bool Running;

        [Header("Reps")]
        public int Target = 5;
        public int Planted;
        public int Watered;
        public int Harvested;

        [Header("Params")]
        [SerializeField] private float plantAngleDeg = 30f;
        [SerializeField] private float waterAngleDeg = 20f;

        public float PlantAngleDeg => plantAngleDeg;
        public float WaterAngleDeg => waterAngleDeg;

        private const float ClearDelaySeconds = 1.5f;
        private const float StageLockSeconds = 0.3f;

        private bool _inTransition;
        public bool InTransition => _inTransition;

        private GardenInteractable _seedBag;
        private GardenInteractable _wateringCan;

        public void InitFromInput(InputData input)
        {
            Input = input;

            if (Input != null && Input.PotCount > 0)
                Target = Mathf.Clamp(Input.PotCount, 1, 7);

            if (Input != null)
            {
                // accepta 0
                if (Input.PlantAngleDeg >= 0) plantAngleDeg = Input.PlantAngleDeg;
                if (Input.WaterAngleDeg >= 0) waterAngleDeg = Input.WaterAngleDeg;
            }
        }

        void Start()
        {
            CacheTools();
        }

        private void CacheTools()
        {
            // cautam o singura data 
            var all = FindObjectsOfType<GardenInteractable>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var gi = all[i];
                if (gi == null) continue;

                if (gi.CompareTag("SeedBag"))
                    _seedBag = gi;
                else if (gi.CompareTag("WateringCan"))
                    _wateringCan = gi;
            }
        }
        private void ResetAllSpots()
        {
            var spots = FindObjectsOfType<GardenGame.PlantSpot>(true);
            for (int i = 0; i < spots.Length; i++)
            {
                spots[i].Planted = false;
                spots[i].Watered = false;
                spots[i].Harvested = false;

                var flowerRoot = spots[i].GetComponentInChildren<FlowerController>(true);
                if (flowerRoot != null && flowerRoot.FlowerRoot != null)
                    flowerRoot.FlowerRoot.transform.localScale = Vector3.zero;
            }
        }
        public void StartFlow()
        {
            ResetAllSpots();
            Running = true;
            FlowerRandomizer.ResetDeck();
            Stage = GardenStage.Planting;
            _inTransition = false;

            if (Input == null)
            {
                Input = new InputData
                {
                    BodySide = FruitsGame.BodySide.BODY_SIDE_LEFT,
                    NrFructe = Target
                };
            }

            Planted = 0;
            Watered = 0;
            Harvested = 0;
            foreach (var v in FindObjectsOfType<GardenGame.FlowerVisualController>(true))
                v.ResetForNewRound();
            FlowerController.ClearAllPickedFlowers();
            CacheTools();

            UiStage();
            UiProgress();
        }

        public void StopFlow()
        {
            Running = false;

            _seedBag?.ForceRelease();
            _wateringCan?.ForceRelease();
        }

        public bool CanPlantNow() => Running && !_inTransition && Stage == GardenStage.Planting;
        public bool CanWaterNow() => Running && !_inTransition && Stage == GardenStage.Watering;
        public bool CanHarvestNow() => Running && !_inTransition && Stage == GardenStage.Harvesting;

        public void OnPlantedSuccess()
        {
            if (!CanPlantNow()) return;

            Planted = Mathf.Clamp(Planted + 1, 0, Target);
            UiProgress();

            if (Planted >= Target)
                StartCoroutine(GoTo(GardenStage.Watering));
        }

        public void OnWateredSuccess()
        {
            if (!CanWaterNow()) return;

            Watered = Mathf.Clamp(Watered + 1, 0, Target);
            UiProgress();

            if (Watered >= Target)
                StartCoroutine(GoTo(GardenStage.Harvesting));
        }

        public void OnHarvestedSuccess()
        {
            if (!CanHarvestNow()) return;

            Harvested = Mathf.Clamp(Harvested + 1, 0, Target);
            UiProgress();

            if (Harvested >= Target)
                StartCoroutine(GoTo(GardenStage.Finished));
        }

        private IEnumerator GoTo(GardenStage next)
        {
            if (_inTransition) yield break;
            _inTransition = true;

            var prev = Stage;

            UiClear(prev);

            // trimitem tool-ul inapoi dupa ce terminam cu el
            if (prev == GardenStage.Planting)
                _seedBag?.ReturnToHome(1.0f);
            else if (prev == GardenStage.Watering)
                _wateringCan?.ReturnToHome(1.0f);

            if (ClearDelaySeconds > 0f)
                yield return new WaitForSeconds(ClearDelaySeconds);

            Stage = next;
            UiStage();
            UiProgress();

            // lock mic dupa schimbarea stage-ului 
            if (StageLockSeconds > 0f)
                yield return new WaitForSeconds(StageLockSeconds);

            _inTransition = false;

            if (Stage == GardenStage.Finished)
                UiFinished();
        }

        public bool IsAllowedHand(FruitsGame.BodySide hand)
        {
            if (Input == null) return true;
            if (Input.BodySide == FruitsGame.BodySide.BODY_SIDE_BOTH) return true;
            return hand == Input.BodySide;
        }

        public void RegisterGrab(FruitsGame.BodySide hand, string tag)
        {
            if (!Running) return;
            if (!IsAllowedHand(hand)) return;
        }

        private void UiStage()
        {
            var hud = FindObjectOfType<ScorePanelController>();
            if (hud != null) hud.OnStageChanged(Stage, Target, Planted, Watered, Harvested);
        }

        private void UiProgress()
        {
            var hud = FindObjectOfType<ScorePanelController>();
            if (hud != null) hud.OnProgressChanged(Stage, Target, Planted, Watered, Harvested);
        }

        private void UiClear(GardenStage cleared)
        {
            var hud = FindObjectOfType<ScorePanelController>();
            if (hud != null) hud.OnStageCleared(cleared);
        }

        private void UiFinished()
        {
            var hud = FindObjectOfType<ScorePanelController>();
            if (hud != null) hud.OnFinished(Target);

            var panels = FindObjectOfType<FinishExercisePanelsController>();
            if (panels == null) return;

            int useType = (Input != null) ? Input.UseType : 0;

            FinishExercisePanelType panelType =
                (useType == 1)
                    ? FinishExercisePanelType.PANEL_TYPE_HOME_USE
                    : FinishExercisePanelType.PANEL_TYPE_CLINICAL_USE;

            panels.Show(panelType);
        }

    }
}
