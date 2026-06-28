using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ROOT.Scripts
{
    /// <summary>
    /// Vùng nhận kéo thả. Gắn vào GameObject với Collider2D.
    /// AcceptedItemIDs rỗng = chấp nhận tất cả.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DropZone : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private List<string> _acceptedItemIDs = new List<string>();
        [SerializeField] private bool _acceptOnce = true;
        [SerializeField] private Transform _snapPoint;

        [Header("Events")]
        public UnityEvent<Snappable> OnItemAccepted;
        public UnityEvent<Snappable> OnItemRejected;

        public bool IsInteractable { get; set; } = true;
        public Transform SnapPoint => _snapPoint;
        public Snappable CurrentItem { get; private set; }
        public bool HasItem => CurrentItem != null;

        private bool _isOccupied;
        private UnityEngine.Events.UnityAction _liftedListener;

        public bool TryAccept(Snappable snappable)
        {
            if (!IsInteractable) return false;
            if (_acceptOnce && _isOccupied) return false;

            if (_acceptedItemIDs.Count > 0)
            {
                var atid = snappable.GetComponent<ActionTargetID>();
                if (atid == null || !_acceptedItemIDs.Contains(atid.ID))
                {
                    OnItemRejected.Invoke(snappable);
                    return false;
                }
            }

            // Unsubscribe from previous item if any
            if (CurrentItem != null)
                CurrentItem.OnPickedUp.RemoveListener(HandleItemLifted);

            _isOccupied = _acceptOnce;
            CurrentItem = snappable;

            // Auto-clear when the item is lifted back out
            _liftedListener = HandleItemLifted;
            snappable.OnPickedUp.AddListener(_liftedListener);

            OnItemAccepted.Invoke(snappable);
            return true;
        }

        private void HandleItemLifted()
        {
            if (CurrentItem != null)
                CurrentItem.OnPickedUp.RemoveListener(HandleItemLifted);
            _isOccupied = false;
            CurrentItem = null;
        }

        public void Clear()
        {
            if (CurrentItem != null)
                CurrentItem.OnPickedUp.RemoveListener(HandleItemLifted);
            _isOccupied = false;
            CurrentItem = null;
        }

        public bool Accepts(string itemID)
        {
            return _acceptedItemIDs.Count == 0 || _acceptedItemIDs.Contains(itemID);
        }
    }
}
