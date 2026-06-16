using System;
using UnityEngine;

[Serializable]
public class ActionData
{
    public ActionType Type;
    public ActionTarget Target;
    public string TargetID;
    public float Delay;
}

[Serializable]
public class PlayAnimationActionData : ActionData
{
    public int AnimationID;
    public bool Loop;
}

[Serializable]
public class SetScaleActionData : ActionData
{
    public Vector3 Scale = Vector3.one;
}

[Serializable]
public class SetPositionActionData : ActionData
{
    public Vector3 Position;
}

[Serializable]
public class SetActiveActionData : ActionData
{
    public bool Active;
}
