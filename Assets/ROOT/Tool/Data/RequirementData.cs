using System;
using System.Collections.Generic;

[Serializable]
public class RequirementData
{
    public string RequirementID;    // Auto-generated — do not edit manually

    // What kind of event fulfills this requirement
    public RequirementType Type;

    // ActionTargetID.ID of the primary source object:
    //   Clicked / DragComplete → the interactable object
    //   DropAccepted           → the drop zone
    //   TimerExpired           → leave empty (stage-level)
    //   Custom                 → set manually by dev
    public string SourceObjectID;

    // [DropAccepted only] IDs of draggable objects that are allowed into this zone.
    // Empty list = accept any draggable.
    public List<string> AcceptedDraggableIDs = new List<string>();

    // The interaction mechanic that must be set up on SourceObjectID.
    // Derived automatically from Type — used by ComponentRequirementRegistry.
    public MechanicType MechanicType => Type switch
    {
        RequirementType.Clicked      => MechanicType.Click,
        RequirementType.DragComplete => MechanicType.Draggable,
        RequirementType.DropAccepted => MechanicType.DropTarget,
        RequirementType.TimerExpired => MechanicType.Timer,
        _                            => MechanicType.None,
    };

    /// <summary>
    /// Regenerates RequirementID from Type + SourceObjectID.
    /// Call this whenever Type or SourceObjectID changes.
    /// </summary>
    public void RegenerateID()
    {
        if (Type == RequirementType.Custom)
            return;

        RequirementID = string.IsNullOrEmpty(SourceObjectID)
            ? Type.ToString()
            : $"{Type}_{SourceObjectID}";
    }
}
