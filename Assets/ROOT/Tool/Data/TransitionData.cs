using System;
using System.Collections.Generic;

[Serializable]
public class TransitionData
{
    public string FromStageID;
    public string ToStageID;
    public float TimeDelayNext;
    public float TimeTransition = 1f;
    public TransitionDirection Direction;
    public List<string> RequiredFulfilledIDs = new List<string>();
}
