/// <summary>
/// Describes the interaction mechanic that generates a RequirementData trigger.
/// Used by the editor to validate component setup on the level prefab.
/// </summary>
public enum MechanicType
{
    None,           // Triggered purely by code / other systems — no component check
    Draggable,      // Object must be draggable  →  requires Draggable + Collider2D
    DropTarget,     // Object acts as drop zone  →  requires DropZone  + Collider2D (trigger)
    Click,          // Player taps / clicks       →  requires Clickable + Collider2D
    Timer,          // Countdown fires trigger    →  requires TimerTrigger
}
