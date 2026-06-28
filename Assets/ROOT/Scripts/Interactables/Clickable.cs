using UnityEngine;
using UnityEngine.Events;

namespace ROOT.Scripts
{
    /// <summary>
    /// Gắn vào bất kỳ GameObject 2D nào để có thể click/tap.
    /// Không tự handle input — InputController gọi OnClickDrown/OnClickUp.
    /// Tap được xác nhận khi khoảng cách down→up nhỏ hơn _tapMaxDistance.
    /// Yêu cầu: Collider2D để InputController raycast tìm thấy.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Clickable : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private bool _clickOnce = false;
        [SerializeField] private float _tapMaxDistance = 0.3f;

        [Header("Events")]
        public UnityEvent OnClicked;

        public bool IsInteractable { get; set; } = true;
        public int ClickCount { get; private set; }

        private Vector3 _downPosition;

        // ──────────────────────────────────────────────
        // API — gọi bởi InputController
        // ──────────────────────────────────────────────

        public void OnClickDrown(Vector3 worldPos)
        {
            _downPosition = worldPos;
        }

        public void OnDrag(Vector3 worldPos) { }

        public void OnClickUp(Vector3 worldPos)
        {
            if (!IsInteractable) return;
            if (_clickOnce && ClickCount > 0) return;
            if (Vector3.Distance(_downPosition, worldPos) > _tapMaxDistance) return;

            ClickCount++;
            OnClicked.Invoke();
        }

        public void ResetClickCount()
        {
            ClickCount = 0;
        }
    }
}
