using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField] private LevelData _levelData;

    public StageData CurrentStage { get; private set; }
    public event Action<string> OnStageChanged;
    public event Action OnLevelComplete;

    private StageController _stageController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (_levelData != null)
            LoadLevel(_levelData);
    }

    public void LoadLevel(LevelData data)
    {
        _levelData = data;

        _stageController = GetComponent<StageController>();
        if (_stageController == null)
            _stageController = gameObject.AddComponent<StageController>();

        _stageController.OnStageComplete += OnStageComplete;

        var startStage = data.Stages.Count > 0 ? data.Stages[0] : null;

        if (startStage != null)
            AdvanceToStage(startStage);
    }

    private void AdvanceToStage(StageData stage)
    {
        CurrentStage = stage;
        _stageController.Initialize(stage, _levelData);
        OnStageChanged?.Invoke(stage.StageID);
    }

    private void OnStageComplete()
    {
        if (CurrentStage == null || _levelData == null) return;

        var transition = FindMatchingTransition(CurrentStage.StageID);
        if (transition == null)
        {
            OnLevelComplete?.Invoke();
            return;
        }

        StartCoroutine(ExecuteTransition(transition));
    }

    private TransitionData FindMatchingTransition(string fromStageID)
    {
        var candidates = _levelData.Transitions.FindAll(t => t.FromStageID == fromStageID);
        var fulfilled = GetFulfilledIDs();

        foreach (var t in candidates)
        {
            if (t.RequiredFulfilledIDs == null || t.RequiredFulfilledIDs.Count == 0)
                return t;

            bool allMet = true;
            foreach (var id in t.RequiredFulfilledIDs)
            {
                if (!fulfilled.Contains(id)) { allMet = false; break; }
            }

            if (allMet) return t;
        }

        return null;
    }

    private HashSet<string> GetFulfilledIDs()
    {
        // StageController exposes fulfilled state via reflection-free approach
        // For now, check via a public method we add to StageController
        return _stageController.GetFulfilledIDs();
    }

    private IEnumerator ExecuteTransition(TransitionData transition)
    {
        if (transition.TimeDelayNext > 0f)
            yield return new WaitForSeconds(transition.TimeDelayNext);

        var nextStage = _levelData.Stages.Find(s => s.StageID == transition.ToStageID);
        if (nextStage != null)
            AdvanceToStage(nextStage);
    }

    public void FulfillRequirement(string requirementID)
    {
        _stageController?.FulfillRequirement(requirementID);
    }
}
