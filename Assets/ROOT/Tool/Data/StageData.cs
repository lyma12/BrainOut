using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageData
{
    public string StageID;
    public string DisplayName;
    public List<RequirementData> Requirements = new List<RequirementData>();

    // ALL → every requirement must be fulfilled before actions/transitions fire
    // ANY → the first fulfilled requirement is enough
    public CompletionMode CompletionMode = CompletionMode.All;

    public bool Sequential;
    [UnityEngine.SerializeReference] public List<ActionData> Actions = new List<ActionData>();
    public Vector2 NodePosition;
}
