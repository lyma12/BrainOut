using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Fulfilled khi một BaseAction đạt trạng thái Completed.
    /// Dùng khi requirement là "hoàn thành action X" (ví dụ: animation xong, di chuyển xong...).
    /// </summary>
    public class ActionRequirementLinker : RequirementLinker
    {
        [SerializeField] private BaseAction _action;

        protected override void RegisterListeners()
        {
            if (_action == null)
                _action = GetComponent<BaseAction>();

            if (_action != null)
                _action.OnStatusChanged += OnStatusChanged;
        }

        private void OnStatusChanged(ActionStatus status)
        {
            if (status == ActionStatus.Completed)
                Fulfill();
        }

        protected override void OnReset()
        {
            _action?.Reset();
        }

        private void OnDestroy()
        {
            if (_action != null)
                _action.OnStatusChanged -= OnStatusChanged;
        }
    }
}
