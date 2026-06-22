using System;
using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Set position/scale/rotation trực tiếp. Không cần Animator.
    /// </summary>
    public class SetTransformAction : BaseAction
    {
        [SerializeField] private Transform _target;
        [SerializeField] private bool _setPosition;
        [SerializeField] private Vector3 _position;
        [SerializeField] private bool _setScale;
        [SerializeField] private Vector3 _scale = Vector3.one;
        [SerializeField] private bool _setRotation;
        [SerializeField] private Vector3 _eulerAngles;

        private Vector3 _origPos, _origScale, _origRot;

        protected override void OnExecute(Action onComplete)
        {
            if (_target == null) { onComplete(); return; }

            _origPos = _target.localPosition;
            _origScale = _target.localScale;
            _origRot = _target.localEulerAngles;

            if (_setPosition) _target.localPosition = _position;
            if (_setScale) _target.localScale = _scale;
            if (_setRotation) _target.localEulerAngles = _eulerAngles;

            onComplete();
        }

        protected override void OnReset()
        {
            if (_target == null) return;
            if (_setPosition) _target.localPosition = _origPos;
            if (_setScale) _target.localScale = _origScale;
            if (_setRotation) _target.localEulerAngles = _origRot;
        }
    }
}
