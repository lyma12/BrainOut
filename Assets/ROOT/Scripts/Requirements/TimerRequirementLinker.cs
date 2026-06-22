using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Fulfills a requirement when the TimerTrigger on the same GameObject expires.
    /// </summary>
    [RequireComponent(typeof(TimerTrigger))]
    public class TimerRequirementLinker : RequirementLinker
    {
        private TimerTrigger _timer;

        protected override void RegisterListeners()
        {
            _timer = GetComponent<TimerTrigger>();
            if (_timer != null)
                _timer.OnExpired += Fulfill;
            else
                Debug.LogWarning($"[TimerRequirementLinker] No TimerTrigger found on '{gameObject.name}'.");
        }

        protected override void OnReset()
        {
            _timer?.StartCountdown();
        }
    }
}
