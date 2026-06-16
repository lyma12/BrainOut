using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "Annoying Puzzle/Level Data")]
public class LevelData : ScriptableObject
{
    public string LevelName;
    public List<StageData> Stages = new List<StageData>();
    public List<ActionNodeData> ActionNodes = new List<ActionNodeData>();
    public List<ActionConnectionData> ActionConnections = new List<ActionConnectionData>();
    public List<TransitionData> Transitions = new List<TransitionData>();
    // Stages[0] is always the start stage — do not reorder or remove it
    public string StartStageID => Stages.Count > 0 ? Stages[0].StageID : "";
}
