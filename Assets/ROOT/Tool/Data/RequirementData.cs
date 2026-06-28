using System;
using System.Collections.Generic;

[Serializable]
public class RequirementData
{
    public string RequirementID;    // Auto-generated — do not edit manually

    // What kind of event fulfills this requirement
    public RequirementType Type;

    // [Clicked only] Số lần click cần thiết để fulfill
    public int ClickCount = 1;

    // ActionTargetID.ID of the primary source object:
    //   Clicked / DragComplete → the interactable object
    //   DropAccepted           → the drop zone
    //   TimerExpired           → leave empty (stage-level)
    //   Custom                 → set manually by dev
    public string SourceObjectID;

    // [DropAccepted only] Danh sách Snappable được chấp nhận, kèm config riêng.
    // Empty = accept any snappable.
    public List<SnappableEntryData> AcceptedSnappables = new List<SnappableEntryData>();

    // [DropAccepted only] Zone chỉ nhận một item rồi đóng lại.
    public bool DropAcceptOnce = true;

    // [DropAccepted only] Nếu true, item bị lock (không kéo ra được) sau khi drop đúng.
    public bool DropLockOnAccept = false;

    // [DropAccepted only] ActionTargetID của transform dùng làm snap point.
    public string DropSnapPointID = "";

    // The interaction mechanic that must be set up on SourceObjectID.
    // Derived automatically from Type — used by ComponentRequirementRegistry.
    public MechanicType MechanicType => Type switch
    {
        RequirementType.Clicked      => MechanicType.Click,
        RequirementType.DropAccepted => MechanicType.DropTarget,
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
