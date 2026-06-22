using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Annoying Puzzle/Level Data")]
public class LevelData : ScriptableObject
{
    public string LevelName;

    // Prefab instantiated when this level is loaded at runtime
    public GameObject LevelPrefab;

    public List<StageData> Stages = new List<StageData>();
    public List<ActionNodeData> ActionNodes = new List<ActionNodeData>();
    public List<ActionConnectionData> ActionConnections = new List<ActionConnectionData>();
    public List<TransitionData> Transitions = new List<TransitionData>();

    // Set by the Start node connection in the Level Editor
    public string StartStageID;

    // Stages in this list trigger OnLevelComplete when they finish
    public List<string> EndStageIDs = new List<string>();

    // Positions of the Start / End anchor nodes in the graph
    public Vector2 StartNodePosition = new Vector2(50, 200);
    public Vector2 EndNodePosition   = new Vector2(700, 200);
}
