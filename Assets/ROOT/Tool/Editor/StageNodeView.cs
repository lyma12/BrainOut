using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class StageNodeView : Node
{
    public StageData Data { get; private set; }
    public Port TransitionInputPort  { get; private set; }
    public Port TransitionOutputPort { get; private set; }

    /// <summary>Accepts connections from LogicGate output ports.</summary>
    public Port ConditionInputPort { get; private set; }

    private LevelData _levelData;

    private static readonly Color NodeColor   = new Color(0.239f, 0.169f, 0.122f);
    private static readonly Color ActiveColor = new Color(0.1f,   0.5f,   0.1f);

    public StageNodeView(StageData data, LevelData levelData)
    {
        Data       = data;
        _levelData = levelData;

        bool isStart = levelData.Stages.Count > 0 && levelData.Stages[0].StageID == data.StageID;
        title = isStart ? $"▶ {data.DisplayName}" : data.DisplayName;
        titleContainer.style.backgroundColor = new StyleColor(NodeColor);

        TransitionInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        TransitionInputPort.portName = "In";
        inputContainer.Add(TransitionInputPort);

        ConditionInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        ConditionInputPort.portName = "Condition";
        inputContainer.Add(ConditionInputPort);

        TransitionOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        TransitionOutputPort.portName = "Out";
        outputContainer.Add(TransitionOutputPort);

        BuildContents();
        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
        RefreshExpandedState();
        RefreshPorts();
    }

    private void BuildContents()
    {
        extensionContainer.Clear();

        var seqRow = new VisualElement();
        seqRow.style.flexDirection = FlexDirection.Row;
        seqRow.style.paddingLeft   = 6;
        seqRow.style.paddingTop    = 4;
        seqRow.style.paddingBottom = 4;

        var seqLabel = new Label("Sequential:");
        seqLabel.style.marginRight = 4;
        var seqToggle = new Toggle { value = Data.Sequential };
        seqToggle.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Toggle Sequential");
            Data.Sequential = evt.newValue;
            EditorUtility.SetDirty(_levelData);
        });
        seqRow.Add(seqLabel);
        seqRow.Add(seqToggle);
        extensionContainer.Add(seqRow);

        RefreshExpandedState();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetActiveHighlight(bool active)
    {
        titleContainer.style.backgroundColor = new StyleColor(active ? ActiveColor : NodeColor);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private bool IsStartStage() =>
        _levelData.Stages.Count > 0 && _levelData.Stages[0].StageID == Data.StageID;

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Rename", _ =>
        {
            var existing = titleContainer.Q<TextField>();
            if (existing != null) return;
            var tf = new TextField { value = Data.DisplayName };
            tf.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(_levelData, "Rename Stage");
                Data.DisplayName = e.newValue;
                bool isStart = IsStartStage();
                title = isStart ? $"▶ {e.newValue}" : e.newValue;
                EditorUtility.SetDirty(_levelData);
            });
            titleContainer.Add(tf);
            tf.Focus();
        });

        if (!IsStartStage())
            evt.menu.AppendAction("Delete", _ => GetFirstAncestorOfType<LevelGraphView>()?.DeleteSelection());
        else
            evt.menu.AppendAction("Delete", _ => { }, DropdownMenuAction.AlwaysDisabled);
    }
}
