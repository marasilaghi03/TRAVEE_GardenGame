using UnityEngine;

namespace FruitsGame
{
    public class HomeUseController : GameManagerBase
    {
        [SerializeField]
        private GardenGame.GardenController _gardenController;

        [SerializeField]
        private FinishExercisePanelsController _finishExercisePanelsController;

        [SerializeField] private GardenGame.PotLayoutController _potLayout;


        private InputData _inputData;

        public void Init(InputData inputData)
        {
            _inputData = inputData;

            if (_gardenController != null)
                _gardenController.InitFromInput(_inputData);

            StartIteration();
        }

        protected void StartIteration()
        {
            _finishExercisePanelsController.Hide(
                FinishExercisePanelType.PANEL_TYPE_HOME_USE
            );

            _timerController.StartTimer();

            if (_potLayout != null)
                _potLayout.ApplyFromInput(_inputData);

            if (_gardenController != null)
                _gardenController.StartFlow();
        }

        protected void StopGame()
        {
            OnGameFinished();
            ExitGame();
        }

        protected void ExitGame()
        {
            SessionContainer.OnStop();
        }

        protected void OnGameFinished()
        {
            if (_gardenController != null)
                _gardenController.StopFlow();

            _finishExercisePanelsController.Show(
                FinishExercisePanelType.PANEL_TYPE_HOME_USE
            );

            _timerController.StopTimer();

            int planted = _gardenController != null ? _gardenController.Planted : 0;
            int watered = _gardenController != null ? _gardenController.Watered : 0;
            int harvested = _gardenController != null ? _gardenController.Harvested : 0;

            _outputDataController.PushGameSession(
                new GameSessionOutputData()
                {
                    StartTime = _timerController.StartDateTime,
                    FinishTime = _timerController.FinishDateTime.Value,
                    BodySide = _inputData.BodySide.ToString(),
                    LoadedFruitCount = harvested
                }
            );

            SessionContainer.OnIterationCompleted();
        }

        private SessionContainer SessionContainer
        {
            get
            {
                return GameObject
                    .Find("SessionContainer")
                    .GetComponent<SessionContainer>();
            }
        }
    }
}
