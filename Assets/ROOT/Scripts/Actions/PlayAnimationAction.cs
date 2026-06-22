using System;
using UnityEngine;

namespace ROOT.Scripts
{
    public class PlayAnimationAction : BaseAction
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _triggerName;
        [SerializeField] private int _animationID;
        [SerializeField] private bool _useID = false;
        [SerializeField] private float _waitDuration = 0f;

        protected override void OnExecute(Action onComplete)
        {
            if (_animator == null) { onComplete(); return; }

            if (_useID)
                _animator.SetInteger("AnimationID", _animationID);
            else
                _animator.SetTrigger(_triggerName);

            if (_waitDuration > 0f)
                StartCoroutine(WaitThenComplete(_waitDuration, onComplete));
            else
                onComplete();
        }

        protected override void OnReset()
        {
            if (_animator != null)
                _animator.Rebind();
        }

        private System.Collections.IEnumerator WaitThenComplete(float duration, Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            onComplete();
        }
    }
}
