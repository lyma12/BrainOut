using UnityEngine;
using UnityEngine.Events;

namespace ROOT.Scripts
{
    /// <summary>
    /// Sorting layer states:
    ///   Static   — never touched, OR successfully placed in a DropZone
    ///   ItemUp   — currently being dragged
    ///   ItemDown — picked up at least once but not placed in a valid DropZone
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Draggable : MonoBehaviour, IInteractable
    {
        private const string LayerStatic  = "Static";
        private const string LayerItemUp  = "Up";
        private const string LayerItemDown = "Down";

        [Header("Config")]
        [SerializeField] private string _itemID;
        [SerializeField] private bool _returnOnInvalidDrop = true;

        [Header("Events")]
        public UnityEvent OnPickedUp;
        public UnityEvent OnDropped;
        public UnityEvent OnReturnedToOrigin;

        public string ItemID      => _itemID;
        public bool IsInteractable { get; set; } = true;
        public bool IsDragging    { get; private set; }

        // True once the player has picked this object up at least once
        private bool _hasBeenTouched;

        // True when successfully placed inside a DropZone
        private bool _isPlacedInZone;

        private Vector3 _originPosition;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            // Start in Static — object has not been interacted with yet
            ApplyLayer(LayerStatic);
        }

        // ── API — called by InputController ──────────────────────────────────

        public void OnBeginDrag(Vector3 worldPos)
        {
            _originPosition  = transform.position;
            IsDragging       = true;
            _hasBeenTouched  = true;
            _isPlacedInZone  = false; // lifted out of zone if it was there

            ApplyLayer(LayerItemUp);
            OnPickedUp.Invoke();
        }

        public void OnDrag(Vector3 worldPos)
        {
            if (!IsDragging) return;
            var pos = worldPos;
            pos.z = transform.position.z;
            transform.position = pos;
        }

        public void OnEndDrag(Vector3 worldPos, DropZone dropZone)
        {
            if (!IsDragging) return;
            IsDragging = false;

            if (dropZone != null && dropZone.TryAccept(this))
            {
                // Placed correctly → snap and go Static
                transform.position = dropZone.SnapPoint != null
                    ? dropZone.SnapPoint.position
                    : worldPos;

                _isPlacedInZone = true;
                ApplyLayer(LayerStatic);
                OnDropped.Invoke();
            }
            else if (_returnOnInvalidDrop)
            {
                // Return to origin → ItemDown (was touched but not placed)
                transform.position = _originPosition;
                ApplyLayer(LayerItemDown);
                OnReturnedToOrigin.Invoke();
            }
            else
            {
                // Free drop outside zone → ItemDown
                ApplyLayer(LayerItemDown);
                OnDropped.Invoke();
            }
        }

        public void ReturnToOrigin()
        {
            transform.position = _originPosition;
            ApplyLayer(_hasBeenTouched ? LayerItemDown : LayerStatic);
        }

        // ── Reset ─────────────────────────────────────────────────────────────

        public void ResetDraggable()
        {
            IsDragging      = false;
            _hasBeenTouched = false;
            _isPlacedInZone = false;
            transform.position = _originPosition;
            ApplyLayer(LayerStatic);
        }

        // ── Sorting layer ─────────────────────────────────────────────────────

        private void ApplyLayer(string layerName)
        {
            if (_spriteRenderer == null) return;

            int layer = SortingLayer.NameToID(layerName);
            if (layer == 0 && layerName != "Default")
            {
                Debug.LogWarning($"[Draggable] Sorting layer '{layerName}' not found on '{gameObject.name}'. " +
                                 "Add it in Edit → Project Settings → Tags and Layers.");
                return;
            }

            _spriteRenderer.sortingLayerID = layer;
        }
    }
}
