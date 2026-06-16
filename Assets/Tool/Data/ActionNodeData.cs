using System;
using UnityEngine;

[Serializable]
public class ActionNodeData
{
    public string ActionNodeID;
    [SerializeReference] public ActionData Action;
    public Vector2 NodePosition;
}
