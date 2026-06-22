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
    public class DropZone : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private List<string> _acceptedItemIDs = new List<string>();
        [SerializeField] private bool _acceptOnce = true;
        [SerializeField] private Transform _snapPoint;

        [Header("Events")]
        public UnityEvent<Draggable> OnItemAccepted;
        public UnityEvent<Draggable> OnItemRejected;

        public bool IsInteractable { get; set; } = true;
        public Transform SnapPoint => _snapPoint;
        public Draggable CurrentItem { get; private set; }
        public bool HasItem => CurrentItem != null;

        private bool _isOccupied;

        public bool TryAccept(Draggable draggable)
        {
            if (!IsInteractable) return false;
            if (_acceptOnce && _isOccupied) return false;

            if (_acceptedItemIDs.Count > 0 && !_acceptedItemIDs.Contains(draggable.ItemID))
            {
                OnItemRejected.Invoke(draggable);
                return false;
            }

            _isOccupied = _acceptOnce;
            CurrentItem = draggable;
            OnItemAccepted.Invoke(draggable);
            return true;
        }

        public void Clear()
        {
            _isOccupied = false;
            CurrentItem = null;
        }

        public bool Accepts(string itemID)
        {
            return _acceptedItemIDs.Count == 0 || _acceptedItemIDs.Contains(itemID);
        }
    }
}
