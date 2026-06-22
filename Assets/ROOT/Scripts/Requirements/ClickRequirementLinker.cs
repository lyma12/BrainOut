using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Fulfilled khi Clickable được click (một lần hoặc N lần).
    /// Gắn vào cùng GameObject với Clickable.
    /// </summary>
    public class ClickRequirementLinker : RequirementLinker
    {
        [SerializeField] private Clickable _clickable;
        [SerializeField] private int _requiredClickCount = 1;

        private int _currentCount;

        protected override void RegisterListeners()
        {
            if (_clickable == null)
                _clickable = GetComponent<Clickable>();

            if (_clickable != null)
                _clickable.OnClicked.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            _currentCount++;
            if (_currentCount >= _requiredClickCount)
                Fulfill();
        }

        protected override void OnReset()
        {
            _currentCount = 0;
            _clickable?.ResetClickCount();
        }
    }
}
