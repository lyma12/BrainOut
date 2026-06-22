using System;

namespace ROOT.Scripts
{
    public enum ActionStatus { NotStarted, InProgress, Completed }

    public interface IAction
    {
        ActionStatus Status { get; }
        void Execute(Action onComplete = null);
        void Reset();
    }

    public interface IRequirement
    {
        string RequirementID { get; }
        bool IsComplete();
    }

    public interface IInteractable
    {
        bool IsInteractable { get; set; }
    }
}
