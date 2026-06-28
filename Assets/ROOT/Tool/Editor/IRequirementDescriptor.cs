using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Describes how a specific RequirementType integrates with the Level Editor.
/// Implement and decorate with [InitializeOnLoad] to self-register — no existing file needs editing.
/// </summary>
public interface IRequirementDescriptor
{
    // ── Linker baking ──────────────────────────────────────────────────────────

    /// <summary>MonoBehaviour type to add/configure on the source prefab. Null = no linker.</summary>
    Type LinkerType { get; }

    /// <summary>
    /// Write type-specific serialized fields onto the linker component.
    /// <paramref name="go"/> is provided for descriptors that also configure sibling components (e.g. DropZone._snapPoint).
    /// </summary>
    void BakeFields(GameObject go, SerializedObject linkerSO, RequirementData req);

    // ── Editor UI ─────────────────────────────────────────────────────────────

    /// <summary>Label for the primary scene-object picker row. Null = no picker shown.</summary>
    string SourcePickerLabel { get; }

    /// <summary>MechanicType to apply to the source GameObject when it is assigned.</summary>
    MechanicType SourceMechanic { get; }

    /// <summary>Whether to render the component-status badge row below the fields.</summary>
    bool ShowStatus { get; }

    /// <summary>
    /// Build any extra UI rows that appear below the source-object picker.
    /// <paramref name="onBakeNeeded"/> — call when data changed and the prefab linker should be re-baked.
    /// <paramref name="onRebuildUI"/> — call when the entire node details section must be rebuilt.
    /// Return null if no extra UI is needed.
    /// </summary>
    VisualElement BuildExtraUI(RequirementData req, LevelData levelData,
        Action onBakeNeeded, Action onRebuildUI);

    /// <summary>
    /// Extra objects (besides SourceObjectID) to include in the status badge row.
    /// Yields (ActionTargetID, display-label-fallback, MechanicType).
    /// </summary>
    IEnumerable<(string id, string label, MechanicType mechanic)> GetExtraStatusTargets(RequirementData req);
}
