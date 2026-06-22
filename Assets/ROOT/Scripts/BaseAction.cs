using System;
using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Base class cho mọi Action trong game.
    /// Subclass chỉ cần override OnExecute() và OnReset().
    /// Designer kéo component vào GameObject, không cần code thêm.
    /// </summary>
    public abstract class BaseAction : MonoBehaviour, IAction
    {
        public ActionStatus Status { get; private set; } = ActionStatus.NotStarted;

        public event Action<ActionStatus> OnStatusChanged;

        public void Execute(Action onComplete = null)
        {
            if (Status == ActionStatus.InProgress) return;

            SetStatus(ActionStatus.InProgress);
            OnExecute(() =>
            {
                SetStatus(ActionStatus.Completed);
                onComplete?.Invoke();
            });
        }

        public void Reset()
        {
            SetStatus(ActionStatus.NotStarted);
            OnReset();
        }

        private void SetStatus(ActionStatus status)
        {
            Status = status;
            OnStatusChanged?.Invoke(status);
        }

        protected abstract void OnExecute(Action onComplete);
        protected virtual void OnReset() { }
    }
}
