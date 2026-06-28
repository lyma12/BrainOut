using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageData
{
    public string StageID;
    public string DisplayName;

    // Requirements are now standalone RequirementNodeData nodes connected via LogicGate nodes.
    // CompletionMode (AND/OR) is expressed by the connected LogicGateNodeData.GateType.

    public bool Sequential;
    [UnityEngine.SerializeReference] public List<ActionData> Actions = new List<ActionData>();
    public Vector2 NodePosition;
}
