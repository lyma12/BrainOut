using UnityEngine;
using UnityEngine.Events;

namespace ROOT.Scripts
{
    /// <summary>
    /// Gắn vào bất kỳ GameObject 2D nào để có thể click/tap.
    /// Không tự handle input — InputController gọi OnTap().
    /// Yêu cầu: Collider2D để InputController raycast tìm thấy.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Clickable : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private bool _clickOnce = false;

        [Header("Events")]
        public UnityEvent OnClicked;

        public bool IsInteractable { get; set; } = true;
        public int ClickCount { get; private set; }

        // ──────────────────────────────────────────────
        // API — gọi bởi InputController
        // ──────────────────────────────────────────────

        public void OnTap()
        {
            if (!IsInteractable) return;
            if (_clickOnce && ClickCount > 0) return;

            ClickCount++;
            OnClicked.Invoke();
        }

        public void ResetClickCount()
        {
            ClickCount = 0;
        }
    }
}
