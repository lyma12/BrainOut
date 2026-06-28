using System;
using UnityEngine;

[Serializable]
public class RequirementNodeData
{
    public string NodeID;
    public RequirementData Data = new RequirementData();
    public Vector2 NodePosition;
}
