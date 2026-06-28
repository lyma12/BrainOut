using UnityEngine;

namespace ROOT.Scripts
{
    /// <summary>
    /// Base class cho mọi requirement linker.
    /// Data (RequirementID, v.v.) được editor bake trực tiếp vào prefab — không cần wire lúc runtime.
    /// </summary>
    public abstract class RequirementLinker : MonoBehaviour, IRequirement
    {
        [SerializeField] private string _requirementID;

        public string RequirementID => _requirementID;
        public bool IsComplete() => _isFulfilled;

        protected bool _isFulfilled;

        protected virtual void Start()
        {
            RegisterListeners();
        }

        protected abstract void RegisterListeners();

        protected void Fulfill()
        {
            if (_isFulfilled) return;
            _isFulfilled = true;
            GamePlayController.Instance?.FulfillRequirement(_requirementID);
        }

        public void ResetLinker()
        {
            _isFulfilled = false;
            OnReset();
        }

        protected virtual void OnReset() { }
    }
}
