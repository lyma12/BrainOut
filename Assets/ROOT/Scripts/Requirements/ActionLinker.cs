using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Kết nối một ActionNodeID trong LevelData với các BaseAction thực tế trong scene.
    /// Khi GamePlayController cần chạy action node đó → tìm ActionLinker có ID khớp → Execute tất cả _actions.
    ///
    /// Designer:
    ///   1. Điền ActionNodeID khớp với node trong Level Editor
    ///   2. Kéo các BaseAction component vào list _actions
    /// </summary>
    public class ActionLinker : MonoBehaviour
    {
        [SerializeField] private string _actionNodeID;
        [SerializeField] private BaseAction[] _actions;

        public string ActionNodeID => _actionNodeID;

        public void Execute(System.Action onAllComplete = null)
        {
            if (_actions == null || _actions.Length == 0)
            {
                onAllComplete?.Invoke();
                return;
            }

            StartCoroutine(RunAll(onAllComplete));
        }

        private System.Collections.IEnumerator RunAll(System.Action onAllComplete)
        {
            foreach (var action in _actions)
            {
                if (action == null) continue;
                bool done = false;
                action.Execute(() => done = true);
                yield return new UnityEngine.WaitUntil(() => done);
            }
            onAllComplete?.Invoke();
        }

        public void ResetAll()
        {
            foreach (var action in _actions)
                action?.Reset();
        }
    }
}
