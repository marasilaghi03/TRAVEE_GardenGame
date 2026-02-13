using System.Collections;
using UnityEngine;
using TMPro;
using GardenGame;

public class ScorePanelController : MonoBehaviour
{
    [Header("Refs")]
    public GardenController Garden;
    public TMP_Text StageText;
    public TMP_Text ProgressText;

    [Header("Targets")]
    public int PlantTarget = 5;
    public int WaterTarget = 5;
    public int HarvestTarget = 5;

    [Header("Stage transition UI")]
    public float StageClearHoldSeconds = 1.5f;
    public Color ClearColor = Color.green;

    private int _plantedCount = 0;
    private int _wateredCount = 0;
    private int _harvestedCount = 0;

    private GardenStage _lastStage;

    private Color _stageTextDefaultColor;
    private bool _clearShownThisStage = false;

    private Coroutine _transitionCo;

    private Color _defaultStageColor;
    private bool _defaultStageColorCached = false;

    private bool _finishedUi;

    void Start()
    {
        if (Garden == null)
            Garden = FindObjectOfType<GardenController>();

        if (StageText != null)
            _stageTextDefaultColor = StageText.color;

        SyncTargetsFromGarden();

        _lastStage = (Garden != null) ? Garden.Stage : default;

        _clearShownThisStage = IsStageComplete(_lastStage);

        RefreshUI(force: true);

        if (_lastStage == GardenStage.Finished)
        {
            EnsureFinishedUi();
            return;
        }

        if (_clearShownThisStage)
            ShowStageClear(_lastStage);
    }

    void Update()
    {
        if (Garden == null) return;

        SyncTargetsFromGarden();

        if (Garden.Stage == GardenStage.Finished)
        {
            EnsureFinishedUi();
            return;
        }

        if (Garden.Stage != _lastStage)
        {
            var oldStage = _lastStage;
            var newStage = Garden.Stage;
            _lastStage = newStage;

            _clearShownThisStage = IsStageComplete(newStage);

            if (_transitionCo != null) StopCoroutine(_transitionCo);
            _transitionCo = StartCoroutine(StageTransitionRoutine(oldStage, newStage));
            return;
        }

        if (!_clearShownThisStage && IsStageComplete(Garden.Stage))
        {
            _clearShownThisStage = true;
            ShowStageClear(Garden.Stage);
        }
    }

    private void SyncTargetsFromGarden()
    {
        if (Garden == null) return;

        int t = Garden.Target;
        PlantTarget = t;
        WaterTarget = t;
        HarvestTarget = t;
    }

    public void OnSeedPlanted()
    {
        if (_finishedUi) return;

        _plantedCount = Mathf.Clamp(_plantedCount + 1, 0, PlantTarget);
        RefreshUI();
        TryShowClearIfComplete();
    }

    public void OnPlantWatered()
    {
        if (_finishedUi) return;

        _wateredCount = Mathf.Clamp(_wateredCount + 1, 0, WaterTarget);
        RefreshUI();
        TryShowClearIfComplete();
    }

    public void OnFlowerHarvested()
    {
        if (_finishedUi) return;

        _harvestedCount = Mathf.Clamp(_harvestedCount + 1, 0, HarvestTarget);
        RefreshUI();
        TryShowClearIfComplete();
    }

    public void ResetPlantProgress()
    {
        if (_finishedUi) return;
        _plantedCount = 0;
        RefreshUI(force: true);
    }

    public void ResetWaterProgress()
    {
        if (_finishedUi) return;
        _wateredCount = 0;
        RefreshUI(force: true);
    }

    public void ResetHarvestProgress()
    {
        if (_finishedUi) return;
        _harvestedCount = 0;
        RefreshUI(force: true);
    }

    public void OnStageChanged(GardenStage stage, int target, int planted, int watered, int harvested)
    {
        if (stage == GardenStage.Finished)
        {
            EnsureFinishedUi();
            return;
        }

        if (!_defaultStageColorCached && StageText != null)
        {
            _defaultStageColor = StageText.color;
            _defaultStageColorCached = true;
        }

        if (StageText != null)
            StageText.color = _defaultStageColor;

        RefreshFromState(stage, target, planted, watered, harvested);
    }

    public void OnProgressChanged(GardenStage stage, int target, int planted, int watered, int harvested)
    {
        if (stage == GardenStage.Finished)
        {
            EnsureFinishedUi();
            return;
        }

        RefreshFromState(stage, target, planted, watered, harvested);
    }

    public void OnStageCleared(GardenStage clearedStage)
    {
        if (_finishedUi) return;
        if (StageText == null) return;

        StageText.text = $"{clearedStage}: Clear!";
        StageText.color = ClearColor;
    }

    public void OnFinished(int target)
    {
        EnsureFinishedUi();
    }

    private void EnsureFinishedUi()
    {
        if (_finishedUi) return;
        _finishedUi = true;

        if (_transitionCo != null)
        {
            StopCoroutine(_transitionCo);
            _transitionCo = null;
        }

        if (StageText != null)
        {
            StageText.text = "     Finished!";
            StageText.color = ClearColor;
        }

        if (ProgressText != null)
            ProgressText.gameObject.SetActive(false);
    }

    private IEnumerator StageTransitionRoutine(GardenStage oldStage, GardenStage newStage)
    {
        if (_finishedUi)
        {
            _transitionCo = null;
            yield break;
        }

        if (IsStageComplete(oldStage))
            ShowStageClear(oldStage);
        else
        {
            SetStageNormal(oldStage);
            RefreshUI(force: true);
        }

        if (StageClearHoldSeconds > 0f)
            yield return new WaitForSeconds(StageClearHoldSeconds);

        if (newStage == GardenStage.Finished)
        {
            EnsureFinishedUi();
            _transitionCo = null;
            yield break;
        }

        SetStageNormal(newStage);
        RefreshUI(force: true);

        if (IsStageComplete(newStage))
            ShowStageClear(newStage);

        _transitionCo = null;
    }

    private void TryShowClearIfComplete()
    {
        if (Garden == null) return;
        if (_finishedUi) return;
        if (_transitionCo != null) return;

        if (!_clearShownThisStage && IsStageComplete(Garden.Stage))
        {
            _clearShownThisStage = true;
            ShowStageClear(Garden.Stage);
        }
    }

    private bool IsStageComplete(GardenStage stage)
    {
        switch (stage)
        {
            case GardenStage.Planting:
                return _plantedCount >= PlantTarget;
            case GardenStage.Watering:
                return _wateredCount >= WaterTarget;
            case GardenStage.Harvesting:
                return _harvestedCount >= HarvestTarget;
            default:
                return false;
        }
    }

    private void ShowStageClear(GardenStage stage)
    {
        if (StageText == null) return;

        StageText.color = ClearColor;
        StageText.text = $"{stage}: Clear!";
    }

    private void SetStageNormal(GardenStage stage)
    {
        if (StageText == null) return;

        StageText.color = _stageTextDefaultColor;
        StageText.text = $"Stage: {stage}";
    }

    private void RefreshFromState(GardenStage stage, int target, int planted, int watered, int harvested)
    {
        if (stage == GardenStage.Finished)
        {
            EnsureFinishedUi();
            return;
        }

        if (StageText == null || ProgressText == null) return;

        StageText.text = $"Stage: {stage}";
        ProgressText.gameObject.SetActive(true);

        if (stage == GardenStage.Planting)
            ProgressText.text = $"Seeds: {planted}/{target}";
        else if (stage == GardenStage.Watering)
            ProgressText.text = $"Watered: {watered}/{target}";
        else if (stage == GardenStage.Harvesting)
            ProgressText.text = $"Harvested: {harvested}/{target}";
        else
            ProgressText.gameObject.SetActive(false);
    }

    private void RefreshUI(bool force = false)
    {
        if (Garden == null || StageText == null || ProgressText == null) return;
        if (_finishedUi) return;

        if (!_clearShownThisStage && _transitionCo == null)
            SetStageNormal(Garden.Stage);

        if (Garden.Stage == GardenStage.Planting)
        {
            ProgressText.gameObject.SetActive(true);
            ProgressText.text = $"Seeds: {_plantedCount}/{PlantTarget}";
            return;
        }

        if (Garden.Stage == GardenStage.Watering)
        {
            ProgressText.gameObject.SetActive(true);
            ProgressText.text = $"Watered: {_wateredCount}/{WaterTarget}";
            return;
        }

        if (Garden.Stage == GardenStage.Harvesting)
        {
            ProgressText.gameObject.SetActive(true);
            ProgressText.text = $"Harvested: {_harvestedCount}/{HarvestTarget}";
            return;
        }

        ProgressText.gameObject.SetActive(false);
    }
}
