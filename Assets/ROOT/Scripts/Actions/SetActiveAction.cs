using System;
using UnityEngine;

namespace ROOT.Scripts
{
    public class SetActiveAction : BaseAction
    {
        [SerializeField] private GameObject _target;
        [SerializeField] private bool _active;
        [SerializeField] private float _delay = 0f;

        protected override void OnExecute(Action onComplete)
        {
            if (_delay > 0f)
                StartCoroutine(DelayedExecute(onComplete));
            else
            {
                Apply();
                onComplete();
            }
        }

        private System.Collections.IEnumerator DelayedExecute(Action onComplete)
        {
            yield return new WaitForSeconds(_delay);
            Apply();
            onComplete();
        }

        private void Apply()
        {
            if (_target != null)
                _target.SetActive(_active);
        }

        protected override void OnReset()
        {
            if (_target != null)
                _target.SetActive(!_active);
        }
    }
}
