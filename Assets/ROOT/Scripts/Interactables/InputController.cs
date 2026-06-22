using System.Collections.Generic;
using AdOne.Core.Runtime.Singleton;
using Lean.Touch;
using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Quản lý toàn bộ input của game thông qua LeanTouch.
    /// - 1 finger trên object: drag object
    /// - 1 finger trên vùng trống: pan camera
    /// - 2 finger: zoom (pinch) + pan camera
    /// - Tap ngắn không di chuyển: click object
    /// </summary>
    public class InputController : Singleton<InputController>
    {
        [Header("Raycast")]
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _interactableLayer = ~0;

        [Header("Camera Pan")]
        [SerializeField] private bool _enablePan = true;
        [SerializeField] private float _panSpeed = 1f;

        [Header("Camera Zoom")]
        [SerializeField] private bool _enableZoom = true;
        [SerializeField] private float _zoomSpeed = 0.05f;
        [SerializeField] private float _zoomMin = 2f;
        [SerializeField] private float _zoomMax = 20f;
        [SerializeField] private AnimationCurve _zoomSmoothing = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Camera Focus")]
        [SerializeField] private bool _enableFocus = true;
        [SerializeField] private float _focusDuration = 0.35f;

        [Header("Camera Bounds")]
        [SerializeField] private bool _enableBounds = false;
        [SerializeField] private Bounds _cameraBounds = new Bounds(Vector3.zero, new Vector3(20, 20, 0));

        // ── Drag state ──
        private Draggable _activeDraggable;
        private LeanFinger _activeFinger;
        private Vector3 _dragOffset;

        // ── Click detection ──
        private readonly Dictionary<int, Vector2> _fingerDownPositions = new Dictionary<int, Vector2>();
        private const float ClickMaxMoveDelta = 10f;

        // ── Camera pan state ──
        private readonly Dictionary<int, Vector3> _panStartWorldPos = new Dictionary<int, Vector3>();

        // ── Camera focus tween ──
        private Vector3 _focusTargetPos;
        private float _focusTimer = -1f;
        private Vector3 _focusFromPos;

        // ── Zoom state ──
        private float _lastPinchDist = -1f;

        // ── Raycast buffer ──
        private readonly Collider2D[] _raycastBuffer = new Collider2D[16];

        protected override void Init()
        {
            base.Init();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            LeanTouch.OnFingerDown   += HandleFingerDown;
            LeanTouch.OnFingerUpdate += HandleFingerUpdate;
            LeanTouch.OnFingerUp     += HandleFingerUp;
        }

        private void OnDisable()
        {
            LeanTouch.OnFingerDown   -= HandleFingerDown;
            LeanTouch.OnFingerUpdate -= HandleFingerUpdate;
            LeanTouch.OnFingerUp     -= HandleFingerUp;
        }

        private void Update()
        {
            UpdateFocusTween();
        }

        // ──────────────────────────────────────────────
        // Finger handlers
        // ──────────────────────────────────────────────

        private void HandleFingerDown(LeanFinger finger)
        {
            if (finger.IsOverGui) return;

            _fingerDownPositions[finger.Index] = finger.ScreenPosition;

            int activeFingers = LeanTouch.Fingers.Count;
            // Thử drag object trước, bất kể số finger
            if (_activeDraggable == null)
            {
                var draggable = RaycastFirst<Draggable>(finger.ScreenPosition);
                if (draggable != null && draggable.IsInteractable)
                {
                    _activeDraggable = draggable;
                    _activeFinger = finger;

                    var worldPos = ScreenToWorld(finger.ScreenPosition, draggable.transform.position.z);
                    _dragOffset = draggable.transform.position - worldPos;
                    draggable.OnBeginDrag(worldPos + _dragOffset);
                    return;
                }
            }

            // Không có object → pan camera với finger này
            if (_enablePan && _activeDraggable == null)
                _panStartWorldPos[finger.Index] = ScreenToWorld(finger.ScreenPosition, _camera.transform.position.z);

            // Reset pinch khi thêm finger
            _lastPinchDist = -1f;
            _focusTimer = -1f; // hủy focus tween nếu đang chạy
        }

        private void HandleFingerUpdate(LeanFinger finger)
        {
            if (finger.IsOverGui) return;

            int activeFingers = LeanTouch.Fingers.Count;
            // Update drag object
            if (_activeDraggable != null && _activeFinger?.Index == finger.Index)
            {
                var worldPos = ScreenToWorld(finger.ScreenPosition, _activeDraggable.transform.position.z);
                _activeDraggable.OnDrag(worldPos + _dragOffset);
                return;
            }

            // Zoom bằng pinch (2 finger)
            if (_enableZoom && activeFingers >= 2)
            {
                HandlePinchZoom();
            }

            // Pan camera
            if (_enablePan && _panStartWorldPos.ContainsKey(finger.Index) && activeFingers == 1)
            {
                var currentWorld = ScreenToWorld(finger.ScreenPosition, _camera.transform.position.z);
                var startWorld = _panStartWorldPos[finger.Index];
                var delta = startWorld - currentWorld;

                var newPos = _camera.transform.position + delta * _panSpeed;
                _camera.transform.position = ClampCameraPosition(newPos);

                // Cập nhật lại start để pan mượt
                _panStartWorldPos[finger.Index] = ScreenToWorld(finger.ScreenPosition, _camera.transform.position.z);
            }
        }

        private void HandleFingerUp(LeanFinger finger)
        {
            // Click detection
            if (_fingerDownPositions.TryGetValue(finger.Index, out var downPos))
            {
                _fingerDownPositions.Remove(finger.Index);
                float moved = Vector2.Distance(downPos, finger.ScreenPosition);

                if (moved <= ClickMaxMoveDelta)
                {
                    bool isDragFinger = _activeDraggable != null && _activeFinger?.Index == finger.Index;
                    if (!isDragFinger)
                    {
                        var clickable = RaycastFirst<Clickable>(finger.ScreenPosition);
                        clickable?.OnTap();
                    }
                }
            }

            // End drag
            if (_activeFinger?.Index == finger.Index && _activeDraggable != null)
            {
                var worldPos = ScreenToWorld(finger.ScreenPosition, _activeDraggable.transform.position.z);
                var dropZone = RaycastFirst<DropZone>(finger.ScreenPosition);
                _activeDraggable.OnEndDrag(worldPos, dropZone);
                _activeDraggable = null;
                _activeFinger = null;
            }

            _panStartWorldPos.Remove(finger.Index);

            // Reset pinch khi bớt finger
            _lastPinchDist = -1f;
        }

        // ──────────────────────────────────────────────
        // Camera — Zoom (pinch)
        // ──────────────────────────────────────────────

        private void HandlePinchZoom()
        {
            if (LeanTouch.Fingers.Count < 2) return;

            var f0 = LeanTouch.Fingers[0].ScreenPosition;
            var f1 = LeanTouch.Fingers[1].ScreenPosition;
            float dist = Vector2.Distance(f0, f1);

            if (_lastPinchDist < 0f) { _lastPinchDist = dist; return; }

            float delta = _lastPinchDist - dist;
            _lastPinchDist = dist;

            float newSize = _camera.orthographicSize + delta * _zoomSpeed;
            _camera.orthographicSize = Mathf.Clamp(newSize, _zoomMin, _zoomMax);

            // Sau khi zoom, clamp lại vị trí camera
            _camera.transform.position = ClampCameraPosition(_camera.transform.position);
        }

        // ──────────────────────────────────────────────
        // Camera — Focus
        // ──────────────────────────────────────────────

        /// <summary>
        /// Smooth move camera tới target. Gọi từ bên ngoài hoặc khi tap vào object.
        /// </summary>
        public void FocusOn(Vector3 worldPosition, float? overrideZ = null)
        {
            if (!_enableFocus) return;

            _focusFromPos = _camera.transform.position;
            _focusTargetPos = new Vector3(
                worldPosition.x,
                worldPosition.y,
                overrideZ ?? _camera.transform.position.z
            );
            _focusTargetPos = ClampCameraPosition(_focusTargetPos);
            _focusTimer = 0f;
        }

        public void FocusOn(Transform target) => FocusOn(target.position);

        private void UpdateFocusTween()
        {
            if (_focusTimer < 0f) return;

            _focusTimer += Time.deltaTime / _focusDuration;
            float t = _zoomSmoothing.Evaluate(Mathf.Clamp01(_focusTimer));
            _camera.transform.position = Vector3.LerpUnclamped(_focusFromPos, _focusTargetPos, t);

            if (_focusTimer >= 1f)
                _focusTimer = -1f;
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private T RaycastFirst<T>(Vector2 screenPos) where T : Component
        {
            var worldPos = ScreenToWorld(screenPos, 0f);
            int count = Physics2D.OverlapPointNonAlloc(worldPos, _raycastBuffer, _interactableLayer);

            T best = null;
            int bestOrder = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                var component = _raycastBuffer[i].GetComponent<T>();
                if (component == null) continue;

                int order = _raycastBuffer[i].GetComponent<SpriteRenderer>()?.sortingOrder ?? 0;
                if (order > bestOrder) { bestOrder = order; best = component; }
            }

            return best;
        }

        private Vector3 ScreenToWorld(Vector2 screenPos, float z)
        {
            var pos = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, _camera.nearClipPlane));
            pos.z = z;
            return pos;
        }

        private Vector3 ClampCameraPosition(Vector3 pos)
        {
            if (!_enableBounds) return pos;
            pos.x = Mathf.Clamp(pos.x, _cameraBounds.min.x, _cameraBounds.max.x);
            pos.y = Mathf.Clamp(pos.y, _cameraBounds.min.y, _cameraBounds.max.y);
            return pos;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_enableBounds) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawCube(_cameraBounds.center, _cameraBounds.size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(_cameraBounds.center, _cameraBounds.size);
        }
#endif
    }
}
