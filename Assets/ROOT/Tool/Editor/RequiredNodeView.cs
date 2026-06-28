using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class RequiredNodeView : Node
{
    public RequirementNodeData Data { get; private set; }

    /// <summary>Connects to LogicGate input (for completion condition).</summary>
    public Port GateOutputPort { get; private set; }

    /// <summary>Connects to ActionNode input (triggers actions when fulfilled).</summary>
    public Port ActionOutputPort { get; private set; }

    private LevelData _levelData;
    private VisualElement _detailsContainer;

    private static readonly Color NodeColor = new Color(0.45f, 0.08f, 0.08f);

    public RequiredNodeView(RequirementNodeData data, LevelData levelData)
    {
        Data       = data;
        _levelData = levelData;

        RefreshTitle();
        titleContainer.style.backgroundColor = new StyleColor(NodeColor);

        GateOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        GateOutputPort.portName = "Gate";
        outputContainer.Add(GateOutputPort);

        ActionOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
        ActionOutputPort.portName = "Action";
        outputContainer.Add(ActionOutputPort);

        BuildContents();
        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
        RefreshExpandedState();
        RefreshPorts();

        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        RegisterCallback<DetachFromPanelEvent>(_ =>
            EditorApplication.hierarchyChanged -= OnHierarchyChanged);
    }

    private void RefreshTitle() => title = $"Req: {Data.Data.Type}";

    private void OnHierarchyChanged()
    {
        if (_detailsContainer == null) return;
        RefreshStatusRow();
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildContents()
    {
        extensionContainer.Clear();

        var typeRow = new VisualElement();
        typeRow.style.flexDirection = FlexDirection.Row;
        typeRow.style.alignItems    = Align.Center;
        typeRow.style.paddingLeft   = 6;
        typeRow.style.paddingTop    = 6;

        var typeLbl = new Label("Type:");
        typeLbl.style.width    = 36;
        typeLbl.style.fontSize = 10;
        typeLbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        typeRow.Add(typeLbl);

        var typeField = new EnumField(Data.Data.Type);
        typeField.style.flexGrow = 1;
        typeField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Change Requirement Type");
            Data.Data.Type = (RequirementType)evt.newValue;
            Data.Data.RegenerateID();
            EditorUtility.SetDirty(_levelData);
            RefreshTitle();
            RebuildDetails();
        });
        typeRow.Add(typeField);
        extensionContainer.Add(typeRow);

        _detailsContainer = new VisualElement();
        _detailsContainer.style.paddingLeft  = 6;
        _detailsContainer.style.paddingRight = 6;
        _detailsContainer.style.paddingTop   = 4;
        extensionContainer.Add(_detailsContainer);

        RebuildDetails();
        RefreshExpandedState();
    }

    private void RebuildDetails()
    {
        _detailsContainer.Clear();
        var desc = RequirementTypeRegistry.Get(Data.Data.Type);

        if (desc?.SourcePickerLabel != null)
            _detailsContainer.Add(MakeObjectPickerRow(desc.SourcePickerLabel, desc.SourceMechanic));

        var extra = desc?.BuildExtraUI(Data.Data, _levelData, BakeLinkerToSource, RebuildDetails);
        if (extra != null)
            _detailsContainer.Add(extra);

        if (desc?.ShowStatus == true)
            AppendStatusRow(desc);
    }

    // ── Object picker ─────────────────────────────────────────────────────────

    private VisualElement MakeObjectPickerRow(string label, MechanicType mechanic)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingBottom = 2;

        var lbl = new Label(label + ":");
        lbl.style.width    = 62;
        lbl.style.fontSize = 10;
        lbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        row.Add(lbl);

        var objField = new ObjectField
        {
            objectType        = typeof(GameObject),
            allowSceneObjects = true,
            value             = NodeViewHelper.FindObjectByID(Data.Data.SourceObjectID, _levelData)
        };
        objField.style.flexGrow = 1;
        objField.RegisterValueChangedCallback(evt =>
        {
            var go = evt.newValue as GameObject;
            if (go == null)
            {
                Undo.RecordObject(_levelData, "Clear Requirement Object");
                Data.Data.SourceObjectID = "";
                Data.Data.RegenerateID();
                EditorUtility.SetDirty(_levelData);
                RebuildDetails();
                return;
            }

            var targetID = NodeViewHelper.EnsureActionTargetID(go);
            Undo.RecordObject(_levelData, "Set Requirement Object");
            Data.Data.SourceObjectID = targetID.ID;
            Data.Data.RegenerateID();
            EditorUtility.SetDirty(_levelData);
            NodeViewHelper.ApplyComponents(go, mechanic);
            NodeViewHelper.BakeRequirementLinker(go, Data.Data);
            RebuildDetails();
        });
        row.Add(objField);
        return row;
    }

    // ── Status badges ─────────────────────────────────────────────────────────

    private void AppendStatusRow(IRequirementDescriptor desc)
    {
        var container = new VisualElement();
        container.name = "statusRow";
        container.style.paddingTop  = 3;
        container.style.paddingLeft = 2;

        var rawTargets = new List<(string id, string label, MechanicType mechanic)>();
        if (!string.IsNullOrEmpty(Data.Data.SourceObjectID))
            rawTargets.Add((Data.Data.SourceObjectID, "Object", desc.SourceMechanic));
        foreach (var extra in desc.GetExtraStatusTargets(Data.Data))
            rawTargets.Add(extra);

        if (rawTargets.Count == 0) return;

        var allFixes = new List<(GameObject go, ComponentRequirementRegistry.ComponentInfo info)>();

        foreach (var (id, fallbackLabel, mechanic) in rawTargets)
        {
            var go          = NodeViewHelper.FindObjectByID(id, _levelData);
            var displayName = go != null ? go.name : fallbackLabel;

            var infos = new List<ComponentRequirementRegistry.ComponentInfo>(
                            ComponentRequirementRegistry.GetForMechanic(mechanic));
            if (infos.Count == 0) continue;

            var objLbl = new Label(go != null ? $"{displayName}:" : $"{displayName}: ⚠ not found");
            objLbl.style.fontSize = 9;
            objLbl.style.color    = new StyleColor(go != null
                ? new Color(0.6f, 0.6f, 0.6f)
                : new Color(1f, 0.5f, 0.2f));
            container.Add(objLbl);

            var badgeRow = new VisualElement();
            badgeRow.style.flexDirection = FlexDirection.Row;
            badgeRow.style.flexWrap      = Wrap.Wrap;
            badgeRow.style.paddingLeft   = 4;

            foreach (var info in infos)
            {
                bool missing = go == null || !ComponentRequirementRegistry.HasComponent(go, info);
                var badge = new Label(missing ? $"✕ {info.DisplayName}" : $"✓ {info.DisplayName}");
                badge.style.fontSize    = 9;
                badge.style.paddingLeft = badge.style.paddingRight  = 3;
                badge.style.paddingTop  = badge.style.paddingBottom = 1;
                badge.style.marginRight = badge.style.marginBottom  = 2;
                badge.style.color = new StyleColor(missing
                    ? new Color(1f, 0.4f, 0.4f)
                    : new Color(0.4f, 0.9f, 0.4f));
                badge.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.3f));
                badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
                badgeRow.Add(badge);

                if (missing && go != null)
                    allFixes.Add((go, info));
            }
            container.Add(badgeRow);
        }

        if (allFixes.Count > 0)
        {
            var fixBtn = new Button(() =>
            {
                foreach (var (go, info) in allFixes)
                {
                    ComponentRequirementRegistry.EnsureComponent(go, info);
                    EditorUtility.SetDirty(go);
                }
                RebuildDetails();
            });
            fixBtn.text    = $"⚙ Fix All ({allFixes.Count} missing)";
            fixBtn.tooltip = "Add all missing components";
            fixBtn.style.marginTop  = 3;
            fixBtn.style.height     = 20;
            fixBtn.style.fontSize   = 10;
            fixBtn.style.color      = new StyleColor(new Color(1f, 0.85f, 0.3f));
            fixBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.18f, 0.05f));
            fixBtn.style.borderTopLeftRadius = fixBtn.style.borderTopRightRadius =
            fixBtn.style.borderBottomLeftRadius = fixBtn.style.borderBottomRightRadius = 4;
            fixBtn.style.borderLeftColor = fixBtn.style.borderRightColor =
            fixBtn.style.borderTopColor  = fixBtn.style.borderBottomColor =
                new StyleColor(new Color(0.6f, 0.45f, 0.1f));
            fixBtn.style.borderLeftWidth = fixBtn.style.borderRightWidth =
            fixBtn.style.borderTopWidth  = fixBtn.style.borderBottomWidth = 1;
            container.Add(fixBtn);
        }

        _detailsContainer.Add(container);
    }

    private void RefreshStatusRow()
    {
        var old = _detailsContainer.Q<VisualElement>("statusRow");
        if (old != null) _detailsContainer.Remove(old);
        var desc = RequirementTypeRegistry.Get(Data.Data.Type);
        if (desc?.ShowStatus == true)
            AppendStatusRow(desc);
    }

    // ── Bake helper ───────────────────────────────────────────────────────────

    private void BakeLinkerToSource()
    {
        var go = NodeViewHelper.FindObjectByID(Data.Data.SourceObjectID, _levelData);
        if (go != null) NodeViewHelper.BakeRequirementLinker(go, Data.Data);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Delete", _ => GetFirstAncestorOfType<LevelGraphView>()?.DeleteSelection());
    }
}
