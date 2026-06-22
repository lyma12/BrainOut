using System.Collections;
using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Countdown timer. Fires RequirementLinker.Fulfill() when duration expires.
    /// Add this alongside a TimerRequirementLinker on the same GameObject,
    /// or call Fulfill() manually from any RequirementLinker.
    /// </summary>
    public class TimerTrigger : MonoBehaviour
    {
        [Tooltip("Duration in seconds before the trigger fires.")]
        [SerializeField] private float _duration = 5f;

        [Tooltip("Start the countdown automatically when the GameObject becomes active.")]
        [SerializeField] private bool _autoStart = true;

        public float Duration => _duration;
        public float Remaining { get; private set; }
        public bool IsRunning { get; private set; }

        public event System.Action OnExpired;

        private Coroutine _routine;

        private void OnEnable()
        {
            if (_autoStart) StartCountdown();
        }

        private void OnDisable()
        {
            StopCountdown();
        }

        public void StartCountdown()
        {
            StopCountdown();
            Remaining = _duration;
            IsRunning = true;
            _routine  = StartCoroutine(Countdown());
        }

        public void StopCountdown()
        {
            if (_routine != null) StopCoroutine(_routine);
            IsRunning = false;
        }

        private IEnumerator Countdown()
        {
            while (Remaining > 0f)
            {
                yield return null;
                Remaining -= Time.deltaTime;
            }
            Remaining = 0f;
            IsRunning = false;
            OnExpired?.Invoke();
        }
    }
}
