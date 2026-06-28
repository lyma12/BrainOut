using System;

[Serializable]
public class ActionConnectionData
{
    // NodeID of the RequirementNodeData whose fulfillment triggers the action
    public string RequirementNodeID;
    public string ActionNodeID;
}
