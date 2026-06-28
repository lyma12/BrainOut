using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public sealed class CustomDescriptor : IRequirementDescriptor
{
    static CustomDescriptor() =>
        RequirementTypeRegistry.Register(RequirementType.Custom, new CustomDescriptor());

    // ── Linker ────────────────────────────────────────────────────────────────

    public Type LinkerType => null;
    public void BakeFields(GameObject go, SerializedObject linkerSO, RequirementData req) { }

    // ── UI ────────────────────────────────────────────────────────────────────

    public string SourcePickerLabel => null;
    public MechanicType SourceMechanic => MechanicType.None;
    public bool ShowStatus => false;

    public VisualElement BuildExtraUI(RequirementData req, LevelData levelData,
        Action onBakeNeeded, Action onRebuildUI)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingBottom = 2;

        var lbl = new Label("Req ID:");
        lbl.style.width    = 62;
        lbl.style.fontSize = 10;
        lbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        row.Add(lbl);

        var field = new TextField { value = req.RequirementID ?? "" };
        field.style.flexGrow = 1;
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(levelData, "Change Custom Requirement ID");
            req.RequirementID = evt.newValue;
            EditorUtility.SetDirty(levelData);
        });
        row.Add(field);
        return row;
    }

    public IEnumerable<(string id, string label, MechanicType mechanic)> GetExtraStatusTargets(RequirementData req)
    { yield break; }
}
