using System;
using System.Collections;
using UnityEngine;

namespace ROOT.Scripts
{
    public class MoveToAction : BaseAction
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Transform _destination;
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector3 _originalPosition;

        protected override void OnExecute(Action onComplete)
        {
            if (_target == null || _destination == null) { onComplete(); return; }
            _originalPosition = _target.position;
            StartCoroutine(MoveCoroutine(onComplete));
        }

        private IEnumerator MoveCoroutine(Action onComplete)
        {
            float t = 0f;
            Vector3 from = _target.position;
            Vector3 to = _destination.position;

            while (t < 1f)
            {
                t += Time.deltaTime / _duration;
                _target.position = Vector3.LerpUnclamped(from, to, _curve.Evaluate(Mathf.Clamp01(t)));
                yield return null;
            }

            _target.position = to;
            onComplete();
        }

        protected override void OnReset()
        {
            if (_target != null)
                _target.position = _originalPosition;
        }
    }
}
