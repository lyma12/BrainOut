using System;

/// <summary>Connects a LogicGate output to a Stage's completion condition input.</summary>
[Serializable]
public class LogicGateConnectionData
{
    public string LogicGateNodeID;
    public string StageID;
}
