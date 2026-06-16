using UnityEngine;
using System;

public interface IAction
{
    void Execute(GameObject target, Action onComplete);
}
