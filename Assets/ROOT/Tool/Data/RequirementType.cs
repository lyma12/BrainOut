/// <summary>
/// All event types that the runtime can fire via LevelManager.FulfillRequirement().
/// GDs pick from this list — no free-text IDs needed.
/// </summary>
public enum RequirementType
{
    // ── Interaction ────────────────────────────────────────────────────────
    Clicked,            // Player tapped / clicked an object            (needs Clickable)
    DragComplete,       // Player dragged object and released            (needs Draggable)
    DropAccepted,       // A draggable was dropped onto the zone         (needs DropZone)

    // ── Time ───────────────────────────────────────────────────────────────
    TimerExpired,       // Countdown timer reached zero                  (needs TimerTrigger)

    // ── Escape hatch ───────────────────────────────────────────────────────
    Custom,             // Manually fired by game code — ID is set by dev in SourceObjectID field
}

/// <summary>
/// Whether ALL requirements must be fulfilled or ANY one of them is enough.
/// </summary>
public enum CompletionMode
{
    All,    // AND — every requirement in the list must be fulfilled
    Any,    // OR  — at least one requirement fulfilled is enough
}
