using System;
using UnityEngine;

namespace ROOT.Scripts
{
    public class WaitAction : BaseAction
    {
        [SerializeField] private float _duration = 1f;

        protected override void OnExecute(Action onComplete)
        {
            StartCoroutine(Wait(onComplete));
        }

        private System.Collections.IEnumerator Wait(Action onComplete)
        {
            yield return new WaitForSeconds(_duration);
            onComplete();
        }
    }
}
