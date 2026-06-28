using System;

/// <summary>
/// Một Snappable được chấp nhận bởi DropZone, kèm config riêng của nó.
/// </summary>
[Serializable]
public class SnappableEntryData
{
    public string SnappableID         = "";
    public float  SnapDistance        = 1f;
    public bool   ReturnOnInvalidDrop = true;
}
