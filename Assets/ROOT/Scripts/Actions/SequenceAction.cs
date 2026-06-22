using System;
using System.Collections;
using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Chạy danh sách actions lần lượt, chờ cái trước xong mới chạy cái sau.
    /// Designer kéo các BaseAction component vào list _steps.
    /// </summary>
    public class SequenceAction : BaseAction
    {
        [SerializeField] private BaseAction[] _steps;

        protected override void OnExecute(Action onComplete)
        {
            StartCoroutine(RunSequence(onComplete));
        }

        private IEnumerator RunSequence(Action onComplete)
        {
            foreach (var step in _steps)
            {
                if (step == null) continue;

                bool done = false;
                step.Execute(() => done = true);
                yield return new WaitUntil(() => done);
            }
            onComplete();
        }

        protected override void OnReset()
        {
            foreach (var step in _steps)
                step?.Reset();
        }
    }
}
