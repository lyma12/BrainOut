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
        Data       = data;
        _levelData = levelData;
        _fulfilled.Clear();

        // Collect all requirements that feed into gates connected to this stage
        foreach (var gateConn in levelData.LogicGateConnections)
        {
            if (gateConn.StageID != data.StageID) continue;
            foreach (var reqConn in levelData.RequirementConnections)
            {
                if (reqConn.LogicGateNodeID != gateConn.LogicGateNodeID) continue;
                var reqNode = levelData.RequirementNodes.Find(r => r.NodeID == reqConn.RequirementNodeID);
                if (reqNode != null && !_fulfilled.ContainsKey(reqNode.Data.RequirementID))
                    _fulfilled[reqNode.Data.RequirementID] = false;
            }
        }

        _executor = GetComponent<ActionExecutor>();
        if (_executor == null)
            _executor = gameObject.AddComponent<ActionExecutor>();
    }

    public void FulfillRequirement(string requirementID)
    {
        if (!_fulfilled.ContainsKey(requirementID)) return;
        if (_fulfilled[requirementID]) return;

        _fulfilled[requirementID] = true;
        OnRequirementFulfilled?.Invoke(requirementID);

        TriggerActionsForRequirement(requirementID);
        CheckCompletion();
    }

    private void TriggerActionsForRequirement(string requirementID)
    {
        if (_levelData == null) return;

        var reqNode = _levelData.RequirementNodes.Find(r => r.Data.RequirementID == requirementID);
        if (reqNode == null) return;

        var actions = new List<ActionData>();
        foreach (var conn in _levelData.ActionConnections)
        {
            if (conn.RequirementNodeID != reqNode.NodeID) continue;
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
        if (EvaluateStageGates())
            OnStageComplete?.Invoke();
    }

    /// <summary>Stage passes if at least one connected gate evaluates to true.</summary>
    private bool EvaluateStageGates()
    {
        foreach (var gateConn in _levelData.LogicGateConnections)
        {
            if (gateConn.StageID != Data.StageID) continue;
            var gate = _levelData.LogicGateNodes.Find(g => g.NodeID == gateConn.LogicGateNodeID);
            if (gate != null && EvaluateGate(gate))
                return true;
        }
        return false;
    }

    private bool EvaluateGate(LogicGateNodeData gate)
    {
        var reqConns = _levelData.RequirementConnections.FindAll(c => c.LogicGateNodeID == gate.NodeID);
        if (reqConns.Count == 0) return false;

        foreach (var reqConn in reqConns)
        {
            var reqNode = _levelData.RequirementNodes.Find(r => r.NodeID == reqConn.RequirementNodeID);
            if (reqNode == null) continue;

            bool done = _fulfilled.TryGetValue(reqNode.Data.RequirementID, out bool v) && v;

            if (gate.GateType == LogicGateType.And && !done) return false;
            if (gate.GateType == LogicGateType.Or  &&  done) return true;
        }

        return gate.GateType == LogicGateType.And;
    }
}
