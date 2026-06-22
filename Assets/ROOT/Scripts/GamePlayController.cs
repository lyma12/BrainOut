using System.Collections;
using System.Collections.Generic;
using AdOne.Core.Runtime.Singleton;
using UnityEngine;
using UnityEngine.Events;

namespace ROOT.Scripts
{
    /// <summary>
    /// Điều phối toàn bộ game logic của một level:
    ///   - Load LevelData → quản lý Stage flow
    ///   - Nhận FulfillRequirement() từ các RequirementLinker trong scene
    ///   - Tìm ActionLinker tương ứng → Execute actions
    ///   - Kiểm tra điều kiện Transition → chuyển Stage
    ///
    /// Designer chỉ cần:
    ///   1. Kéo LevelData asset vào _levelData
    ///   2. Đặt tất cả RequirementLinker + ActionLinker vào scene đúng ID
    /// </summary>
    public class GamePlayController : Singleton<GamePlayController>
    {
        [Header("Data")]
        [SerializeField] private LevelData _levelData;

        [Header("Scene")]
        [SerializeField] private GameObject _levelContent;

        [Header("Events")]
        public UnityEvent<string> OnStageEntered;
        public UnityEvent<string> OnStageExited;
        public UnityEvent OnLevelComplete;

        // Runtime state
        private StageData _currentStage;
        private readonly Dictionary<string, bool> _fulfilled = new Dictionary<string, bool>();
        private GameObject _spawnedLevel;

        // Scene object registries (auto-populated at Start)
        private readonly Dictionary<string, List<ActionLinker>> _actionLinkers = new Dictionary<string, List<ActionLinker>>();
        private readonly List<RequirementLinker> _requirementLinkers = new List<RequirementLinker>();


        private void Start()
        {
            if (_levelData != null)
                LoadLevel(_levelData);
        }

        private void CollectSceneObjects()
        {
            _actionLinkers.Clear();
            _requirementLinkers.Clear();

            foreach (var linker in FindObjectsByType<ActionLinker>(FindObjectsSortMode.None))
            {
                if (!_actionLinkers.ContainsKey(linker.ActionNodeID))
                    _actionLinkers[linker.ActionNodeID] = new List<ActionLinker>();
                _actionLinkers[linker.ActionNodeID].Add(linker);
            }

            foreach (var req in FindObjectsByType<RequirementLinker>(FindObjectsSortMode.None))
                _requirementLinkers.Add(req);
        }

        public void LoadLevel(LevelData data)
        {
            _levelData = data;
            if (_levelData == null || _levelData.Stages.Count == 0) return;

            SpawnLevelPrefab();
            CollectSceneObjects();

            // Use explicit StartStageID set by the Start node; fallback to Stages[0]
            StageData startStage = null;
            if (!string.IsNullOrEmpty(_levelData.StartStageID))
                startStage = _levelData.Stages.Find(s => s.StageID == _levelData.StartStageID);
            startStage ??= _levelData.Stages[0];

            EnterStage(startStage);
        }

        private void SpawnLevelPrefab()
        {
            if (_spawnedLevel != null)
                Destroy(_spawnedLevel);

            if (_levelData.LevelPrefab == null) return;

            _spawnedLevel = Instantiate(_levelData.LevelPrefab, _levelContent != null ? _levelContent.transform : null);
            _spawnedLevel.name = _levelData.LevelPrefab.name;
        }

        public void UnloadLevel()
        {
            if (_spawnedLevel != null)
            {
                Destroy(_spawnedLevel);
                _spawnedLevel = null;
            }
            _currentStage = null;
            _fulfilled.Clear();
            _actionLinkers.Clear();
            _requirementLinkers.Clear();
        }

        // ──────────────────────────────────────────────
        // API gọi từ RequirementLinker
        // ──────────────────────────────────────────────

        public void FulfillRequirement(string requirementID)
        {
            if (!_fulfilled.ContainsKey(requirementID)) return;
            if (_fulfilled[requirementID]) return;

            if (_currentStage.Sequential)
            {
                int idx = _currentStage.Requirements.FindIndex(r => r.RequirementID == requirementID);
                for (int i = 0; i < idx; i++)
                {
                    if (!_fulfilled.TryGetValue(_currentStage.Requirements[i].RequirementID, out bool done) || !done)
                        return; // điều kiện trước chưa done, không accept
                }
            }

            _fulfilled[requirementID] = true;

            // Chạy action nodes kết nối với requirement này
            ExecuteActionsForRequirement(requirementID, () => CheckStageComplete());
        }

        // ──────────────────────────────────────────────
        // Internal stage flow
        // ──────────────────────────────────────────────

        private void EnterStage(StageData stage)
        {
            _currentStage = stage;
            _fulfilled.Clear();

            foreach (var req in stage.Requirements)
                _fulfilled[req.RequirementID] = false;

            OnStageEntered.Invoke(stage.StageID);

#if UNITY_EDITOR
            // Notify LevelEditorWindow for graph highlight
            UnityEditor.EditorApplication.delayCall += () =>
            {
                var mgr = FindAnyObjectByType<LevelManager>();
                mgr?.GetType()
                    .GetField("OnStageChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(mgr);
            };
#endif
        }

        private void ExecuteActionsForRequirement(string requirementID, System.Action onComplete)
        {
            if (_levelData == null) { onComplete?.Invoke(); return; }

            var nodeIDs = new List<string>();
            foreach (var conn in _levelData.ActionConnections)
                if (conn.RequirementID == requirementID)
                    nodeIDs.Add(conn.ActionNodeID);

            if (nodeIDs.Count == 0) { onComplete?.Invoke(); return; }

            StartCoroutine(ExecuteNodeList(nodeIDs, onComplete));
        }

        private IEnumerator ExecuteNodeList(List<string> nodeIDs, System.Action onComplete)
        {
            foreach (var nodeID in nodeIDs)
            {
                if (!_actionLinkers.TryGetValue(nodeID, out var linkers)) continue;
                foreach (var linker in linkers)
                {
                    bool done = false;
                    linker.Execute(() => done = true);
                    yield return new WaitUntil(() => done);
                }
            }
            onComplete?.Invoke();
        }

        private void CheckStageComplete()
        {
            bool complete = _currentStage.CompletionMode == CompletionMode.Any
                ? CheckAny()
                : CheckAll();

            if (!complete) return;
            OnStageExited.Invoke(_currentStage.StageID);
            StartCoroutine(HandleTransition());
        }

        private bool CheckAll()
        {
            foreach (var pair in _fulfilled)
                if (!pair.Value) return false;
            return true;
        }

        private bool CheckAny()
        {
            foreach (var pair in _fulfilled)
                if (pair.Value) return true;
            return false;
        }

        private IEnumerator HandleTransition()
        {
            // Check if this stage is connected to the End node
            if (_levelData.EndStageIDs != null && _levelData.EndStageIDs.Contains(_currentStage.StageID))
            {
                Debug.Log("[GamePlayController] Level complete!");
                OnLevelComplete.Invoke();
                yield break;
            }

            var transition = FindMatchingTransition(_currentStage.StageID);
            if (transition == null) yield break;

            if (transition.TimeDelayNext > 0f)
                yield return new WaitForSeconds(transition.TimeDelayNext);

            var nextStage = _levelData.Stages.Find(s => s.StageID == transition.ToStageID);
            if (nextStage != null)
                EnterStage(nextStage);
        }

        private TransitionData FindMatchingTransition(string fromStageID)
        {
            foreach (var t in _levelData.Transitions)
            {
                if (t.FromStageID != fromStageID) continue;

                if (t.RequiredFulfilledIDs == null || t.RequiredFulfilledIDs.Count == 0)
                    return t;

                bool allMet = true;
                foreach (var id in t.RequiredFulfilledIDs)
                {
                    if (!_fulfilled.TryGetValue(id, out bool done) || !done)
                    { allMet = false; break; }
                }

                if (allMet) return t;
            }
            return null;
        }

        // ──────────────────────────────────────────────
        // Utilities
        // ──────────────────────────────────────────────

        public bool IsRequirementFulfilled(string requirementID) =>
            _fulfilled.TryGetValue(requirementID, out bool done) && done;

        public StageData CurrentStage => _currentStage;
    }
}
