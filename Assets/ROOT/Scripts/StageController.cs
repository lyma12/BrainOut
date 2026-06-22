using System;
using System.Collections.Generic;
using UnityEngine;

public class StageController : MonoBehaviour
{
    public StageData Data { get; private set; }

    public event Action OnStageComplete;
    public event Action<string> OnRequirementFulfilled;

    private LevelData _levelData;
    private Dictionary<string, bool> _fulfilled = new Dictionary<string, bool>();
    private ActionExecutor _executor;

    public void Initialize(StageData data, LevelData levelData)
    {
        Data = data;
        _levelData = levelData;
        _fulfilled.Clear();

        foreach (var req in data.Requirements)
            _fulfilled[req.RequirementID] = false;

        _executor = GetComponent<ActionExecutor>();
        if (_executor == null)
            _executor = gameObject.AddComponent<ActionExecutor>();
    }

    public void FulfillRequirement(string requirementID)
    {
        if (!_fulfilled.ContainsKey(requirementID)) return;
        if (_fulfilled[requirementID]) return;

        if (Data.Sequential)
        {
            int index = Data.Requirements.FindIndex(r => r.RequirementID == requirementID);
            for (int i = 0; i < index; i++)
            {
                if (!_fulfilled[Data.Requirements[i].RequirementID])
                    return;
            }
        }

        _fulfilled[requirementID] = true;
        OnRequirementFulfilled?.Invoke(requirementID);

        TriggerActionsForRequirement(requirementID);
        CheckCompletion();
    }

    private void TriggerActionsForRequirement(string requirementID)
    {
        if (_levelData == null) return;

        var actions = new List<ActionData>();
        foreach (var conn in _levelData.ActionConnections)
        {
            if (conn.RequirementID != requirementID) continue;

            var nodeData = _levelData.ActionNodes.Find(n => n.ActionNodeID == conn.ActionNodeID);
            if (nodeData?.Action != null)
                actions.Add(nodeData.Action);
        }

        if (actions.Count > 0)
            _executor.ExecuteActions(actions, BuildTargetMap(), null);
    }

    private Dictionary<string, GameObject> BuildTargetMap()
    {
        var map = new Dictionary<string, GameObject>();
        foreach (var t in FindObjectsOfType<ActionTargetID>())
            if (!string.IsNullOrEmpty(t.ID))
                map[t.ID] = t.gameObject;
        return map;
    }

    public HashSet<string> GetFulfilledIDs()
    {
        var result = new HashSet<string>();
        foreach (var pair in _fulfilled)
            if (pair.Value) result.Add(pair.Key);
        return result;
    }

    private void CheckCompletion()
    {
        bool complete = Data.CompletionMode == CompletionMode.Any
            ? CheckAny()
            : CheckAll();

        if (complete)
            OnStageComplete?.Invoke();
    }

    private bool CheckAll()
    {
        foreach (var pair in _fulfilled)
            if (!pair.Value) return false;
        return true;
    }

    private bool CheckAny()
    {
        foreach (var pair in _fulfilled)
            if (pair.Value) return true;
        return false;
    }
}
