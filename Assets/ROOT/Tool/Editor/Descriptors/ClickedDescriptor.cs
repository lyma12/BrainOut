using System;
using System.Collections.Generic;
using ROOT.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public sealed class ClickedDescriptor : IRequirementDescriptor
{
    static ClickedDescriptor() =>
        RequirementTypeRegistry.Register(RequirementType.Clicked, new ClickedDescriptor());

    // ── Linker ────────────────────────────────────────────────────────────────

    public Type LinkerType => typeof(ClickRequirementLinker);

    public void BakeFields(GameObject go, SerializedObject linkerSO, RequirementData req)
        => linkerSO.FindProperty("_requiredClickCount").intValue = Mathf.Max(1, req.ClickCount);

    // ── UI ────────────────────────────────────────────────────────────────────

    public string SourcePickerLabel => "Object";
    public MechanicType SourceMechanic => MechanicType.Click;
    public bool ShowStatus => true;

    public VisualElement BuildExtraUI(RequirementData req, LevelData levelData,
        Action onBakeNeeded, Action onRebuildUI)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingBottom = 2;

        var lbl = MakeLabel("Clicks:");
        row.Add(lbl);

        var field = new IntegerField { value = req.ClickCount };
        field.style.flexGrow = 1;
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(levelData, "Change Click Count");
            req.ClickCount = Mathf.Max(1, evt.newValue);
            field.SetValueWithoutNotify(req.ClickCount);
            EditorUtility.SetDirty(levelData);
            onBakeNeeded();
        });
        row.Add(field);
        return row;
    }

    public IEnumerable<(string id, string label, MechanicType mechanic)> GetExtraStatusTargets(RequirementData req)
    { yield break; }

    // ── Shared label helper ───────────────────────────────────────────────────

    internal static Label MakeLabel(string text, float width = 62f)
    {
        var lbl = new Label(text);
        lbl.style.width    = width;
        lbl.style.fontSize = 10;
        lbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        return lbl;
    }
}
