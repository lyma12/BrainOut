using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionExecutor : MonoBehaviour
{
    public void ExecuteActions(List<ActionData> actions, Dictionary<string, GameObject> targets, Action onComplete)
    {
        StartCoroutine(ExecuteSequence(actions, targets, onComplete));
    }

    private IEnumerator ExecuteSequence(List<ActionData> actions, Dictionary<string, GameObject> targets, Action onComplete)
    {
        foreach (var action in actions)
        {
            if (action.Delay > 0f)
                yield return new WaitForSeconds(action.Delay);

            GameObject target = ResolveTarget(action, targets);
            if (target == null) continue;

            switch (action.Type)
            {
                case ActionType.PlayAnimation:
                    HandlePlayAnimation(action as PlayAnimationActionData, target);
                    break;
                case ActionType.SetScale:
                    HandleSetScale(action as SetScaleActionData, target);
                    break;
                case ActionType.SetPosition:
                    HandleSetPosition(action as SetPositionActionData, target);
                    break;
                case ActionType.SetActive:
                    HandleSetActive(action as SetActiveActionData, target);
                    break;
                case ActionType.Wait:
                    yield return new WaitForSeconds(action.Delay);
                    break;
            }
        }

        onComplete?.Invoke();
    }

    private GameObject ResolveTarget(ActionData action, Dictionary<string, GameObject> targets)
    {
        if (action.Target == ActionTarget.Self)
            return gameObject;

        if (!string.IsNullOrEmpty(action.TargetID) && targets.TryGetValue(action.TargetID, out var obj))
            return obj;

        return null;
    }

    private void HandlePlayAnimation(PlayAnimationActionData data, GameObject target)
    {
        if (data == null) return;
        var animator = target.GetComponent<Animator>();
        if (animator != null)
            animator.SetInteger("AnimationID", data.AnimationID);
    }

    private void HandleSetScale(SetScaleActionData data, GameObject target)
    {
        if (data == null) return;
        target.transform.localScale = data.Scale;
    }

    private void HandleSetPosition(SetPositionActionData data, GameObject target)
    {
        if (data == null) return;
        target.transform.localPosition = data.Position;
    }

    private void HandleSetActive(SetActiveActionData data, GameObject target)
    {
        if (data == null) return;
        target.SetActive(data.Active);
    }
}
