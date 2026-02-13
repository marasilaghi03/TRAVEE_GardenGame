using UnityEngine;

namespace FruitsGame
{
    public class ClinicalUseController : GameManagerBase
    {
        [SerializeField]
        private GardenGame.GardenController _gardenController;

        [SerializeField]
        private FinishExercisePanelsController _finishExercisePanelsController;

        [SerializeField] 
        private GardenGame.PotLayoutController _potLayout;


        private InputData _inputData;

        public void Init(InputData inputData)
        {
            _inputData = inputData;

            if (_gardenController != null)
                _gardenController.InitFromInput(_inputData);

            _eventsManager.EventName = "onNoGameIsPlaying";
        }

        public void UpdateGame(InputData inputData)
        {
            _inputData = inputData;
        }

        public void StartGame()
        {
            _finishExercisePanelsController.Hide(
                FinishExercisePanelType.PANEL_TYPE_CLINICAL_USE
            );

            _timerController.StartTimer();

            if (_potLayout != null)
                _potLayout.ApplyFromInput(_inputData);


            if (_gardenController != null)
                _gardenController.StartFlow();

            _eventsManager.EventName = "onGameIsPlaying";

            int target = _gardenController != null ? _gardenController.Target : 0;

            _outputDataController.SendNoteInformation(
                "Start sesiune joc."
                + " Numar total plante: " + target + "."
                + " Tip joc: GardenGame."
            );
        }

        public void StopGame()
        {
            OnGameFinished();
        }

        private void OnGameFinished()
        {
            if (_gardenController != null)
                _gardenController.StopFlow();

            _finishExercisePanelsController.Show(
                FinishExercisePanelType.PANEL_TYPE_CLINICAL_USE
            );

            _timerController.StopTimer();

            _eventsManager.EventName = "onNoGameIsPlaying";

            int planted = _gardenController != null ? _gardenController.Planted : 0;
            int watered = _gardenController != null ? _gardenController.Watered : 0;
            int harvested = _gardenController != null ? _gardenController.Harvested : 0;
            int target = _gardenController != null ? _gardenController.Target : 0;

            _outputDataController.SendNoteInformation(
                "Stop sesiune joc. "
                + "Timp executie: "
                + _timerController.GameDuration.ToString("m' min 's' sec'")
                + ". Plante plantate: " + planted + "/" + target + "."
                + " Plante udate: " + watered + "/" + target + "."
                + " Plante culese: " + harvested + "/" + target + "."
            );

            _outputDataController.PushGameSession(
                new GameSessionOutputData()
                {
                    StartTime = _timerController.StartDateTime,
                    FinishTime = _timerController.FinishDateTime.Value,
                    BodySide = _inputData.BodySide.ToString(),
                    LoadedFruitCount = harvested
                }
            );
        }
    }
}
