using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Annoying Puzzle/Level Data")]
public class LevelData : ScriptableObject
{
    public string LevelName;

    public GameObject LevelPrefab;

    public List<StageData> Stages = new List<StageData>();
    public List<ActionNodeData> ActionNodes = new List<ActionNodeData>();
    public List<ActionConnectionData> ActionConnections = new List<ActionConnectionData>();
    public List<TransitionData> Transitions = new List<TransitionData>();

    // Requirement & gate nodes (replace per-stage requirement lists)
    public List<RequirementNodeData> RequirementNodes = new List<RequirementNodeData>();
    public List<LogicGateNodeData> LogicGateNodes = new List<LogicGateNodeData>();
    public List<RequirementConnectionData> RequirementConnections = new List<RequirementConnectionData>();
    public List<LogicGateConnectionData> LogicGateConnections = new List<LogicGateConnectionData>();

    public string StartStageID;
    public List<string> EndStageIDs = new List<string>();

    public Vector2 StartNodePosition = new Vector2(50, 200);
    public Vector2 EndNodePosition   = new Vector2(700, 200);
}
