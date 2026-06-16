using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class StageNodeView : Node
{
    public StageData Data { get; private set; }
    public Port TransitionInputPort { get; private set; }
    public Port TransitionOutputPort { get; private set; }

    private LevelData _levelData;
    private VisualElement _requirementsContainer;
    private Dictionary<string, Port> _requirementPorts = new Dictionary<string, Port>();

    private static readonly Color NodeColor = new Color(0.239f, 0.169f, 0.122f);
    private static readonly Color ActiveColor = new Color(0.1f, 0.5f, 0.1f);

    public StageNodeView(StageData data, LevelData levelData)
    {
        Data = data;
        _levelData = levelData;

        bool isStart = levelData.Stages.Count > 0 && levelData.Stages[0].StageID == data.StageID;
        title = isStart ? $"▶ {data.DisplayName}" : data.DisplayName;
        titleContainer.style.backgroundColor = new StyleColor(NodeColor);

        TransitionInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        TransitionInputPort.portName = "Input";
        inputContainer.Add(TransitionInputPort);

        TransitionOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        TransitionOutputPort.portName = "Output";
        outputContainer.Add(TransitionOutputPort);

        BuildContents();

        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));

        RefreshExpandedState();
        RefreshPorts();
    }

    private void BuildContents()
    {
        extensionContainer.Clear();
        _requirementPorts.Clear();

        var sequentialRow = new VisualElement();
        sequentialRow.style.flexDirection = FlexDirection.Row;
        sequentialRow.style.paddingLeft = 6;
        sequentialRow.style.paddingTop = 4;

        var seqLabel = new Label("Sequential:");
        seqLabel.style.marginRight = 4;
        var seqToggle = new Toggle();
        seqToggle.value = Data.Sequential;
        seqToggle.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Toggle Sequential");
            Data.Sequential = evt.newValue;
            EditorUtility.SetDirty(_levelData);
        });
        sequentialRow.Add(seqLabel);
        sequentialRow.Add(seqToggle);
        extensionContainer.Add(sequentialRow);

        var reqHeader = new VisualElement();
        reqHeader.style.flexDirection = FlexDirection.Row;
        reqHeader.style.paddingLeft = 6;
        reqHeader.style.paddingTop = 6;
        reqHeader.style.paddingBottom = 2;

        var reqLabel = new Label("Requirements:");
        reqLabel.style.flexGrow = 1;
        reqLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        var addBtn = new Button(() => AddRequirement()) { text = "+" };
        addBtn.style.width = 20;
        reqHeader.Add(reqLabel);
        reqHeader.Add(addBtn);
        extensionContainer.Add(reqHeader);

        _requirementsContainer = new VisualElement();
        extensionContainer.Add(_requirementsContainer);

        foreach (var req in Data.Requirements)
            AddRequirementRow(req);

        RefreshExpandedState();
    }

    private void AddRequirement()
    {
        Undo.RecordObject(_levelData, "Add Requirement");

        var req = new RequirementData
        {
            RequirementID = Guid.NewGuid().ToString(),
            DisplayLabel = "New Requirement"
        };
        Data.Requirements.Add(req);
        EditorUtility.SetDirty(_levelData);

        AddRequirementRow(req);
        RefreshPorts();
    }

    private void AddRequirementRow(RequirementData req)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 6;
        row.style.paddingTop = 2;
        row.style.paddingBottom = 2;

        var field = new TextField();
        field.value = req.DisplayLabel;
        field.style.flexGrow = 1;
        field.style.minWidth = 100;
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Rename Requirement");
            req.DisplayLabel = evt.newValue;
            EditorUtility.SetDirty(_levelData);
        });

        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
        port.portName = "";
        port.userData = req.RequirementID;
        port.style.paddingLeft = 4;
        _requirementPorts[req.RequirementID] = port;

        var deleteBtn = new Button(() => RemoveRequirement(req, row, port)) { text = "×" };
        deleteBtn.style.width = 20;
        deleteBtn.style.color = new StyleColor(Color.red);

        row.Add(field);
        row.Add(port);
        row.Add(deleteBtn);
        _requirementsContainer.Add(row);

        outputContainer.Add(port);
    }

    private void RemoveRequirement(RequirementData req, VisualElement row, Port port)
    {
        Undo.RecordObject(_levelData, "Remove Requirement");
        Data.Requirements.Remove(req);
        _levelData.ActionConnections.RemoveAll(c => c.RequirementID == req.RequirementID);
        EditorUtility.SetDirty(_levelData);

        _requirementPorts.Remove(req.RequirementID);
        _requirementsContainer.Remove(row);

        // Disconnect edges on this port before removing
        port.DisconnectAll();
        outputContainer.Remove(port);

        RefreshPorts();
    }

    public Port GetRequirementPort(string requirementID)
    {
        _requirementPorts.TryGetValue(requirementID, out var port);
        return port;
    }

    public void SetActiveHighlight(bool active)
    {
        titleContainer.style.backgroundColor = new StyleColor(active ? ActiveColor : NodeColor);
    }

    private bool IsStartStage() =>
        _levelData.Stages.Count > 0 && _levelData.Stages[0].StageID == Data.StageID;

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Rename", _ =>
        {
            var field = titleContainer.Q<TextField>();
            if (field == null)
            {
                var tf = new TextField { value = Data.DisplayName };
                tf.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(_levelData, "Rename Stage");
                    Data.DisplayName = e.newValue;
                    title = IsStartStage() ? $"▶ {e.newValue}" : e.newValue;
                    EditorUtility.SetDirty(_levelData);
                });
                titleContainer.Add(tf);
                tf.Focus();
            }
        });

        if (!IsStartStage())
        {
            evt.menu.AppendAction("Delete", _ =>
            {
                var graph = GetFirstAncestorOfType<LevelGraphView>();
                graph?.DeleteSelection();
            });
        }
        else
        {
            evt.menu.AppendAction("Delete", _ => { }, DropdownMenuAction.AlwaysDisabled);
        }
    }
}
