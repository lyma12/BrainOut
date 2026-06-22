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
    public AnimationBackend Backend;

    // Spine: track index in SkeletonData
    public int SpineTrackIndex;
    public string SpineAnimationName;

    // Animator: state name to CrossFade to
    public string AnimatorStateName;

    // DOTween: sequence ID defined on the target component
    public string DOTweenSequenceID;

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
