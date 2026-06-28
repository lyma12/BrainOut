using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class LogicGateNodeView : Node
{
    public LogicGateNodeData Data { get; private set; }

    /// <summary>Accepts connections from RequirementNode outputs.</summary>
    public Port InputPort { get; private set; }

    /// <summary>Connects to Stage ConditionInputPort.</summary>
    public Port OutputPort { get; private set; }

    private LevelData _levelData;

    private static readonly Color AndColor = new Color(0.08f, 0.28f, 0.45f);
    private static readonly Color OrColor  = new Color(0.28f, 0.08f, 0.45f);

    public LogicGateNodeView(LogicGateNodeData data, LevelData levelData)
    {
        Data       = data;
        _levelData = levelData;

        RefreshAppearance();

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "Reqs";
        inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "→ Stage";
        outputContainer.Add(OutputPort);

        BuildContents();
        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
        RefreshExpandedState();
        RefreshPorts();
    }

    private void RefreshAppearance()
    {
        title = Data.GateType == LogicGateType.And ? "AND Gate" : "OR Gate";
        var color = Data.GateType == LogicGateType.And ? AndColor : OrColor;
        titleContainer.style.backgroundColor = new StyleColor(color);
    }

    private void BuildContents()
    {
        extensionContainer.Clear();

        var hint = new Label(Data.GateType == LogicGateType.And
            ? "All connected requirements must pass"
            : "Any connected requirement is enough");
        hint.style.fontSize  = 10;
        hint.style.color     = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        hint.style.paddingLeft  = 8;
        hint.style.paddingRight = 8;
        hint.style.paddingTop   = 4;
        hint.style.paddingBottom = 6;
        hint.style.whiteSpace   = WhiteSpace.Normal;
        extensionContainer.Add(hint);

        RefreshExpandedState();
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction(
            Data.GateType == LogicGateType.And ? "Switch to OR Gate" : "Switch to AND Gate",
            _ =>
            {
                Undo.RecordObject(_levelData, "Switch Gate Type");
                Data.GateType = Data.GateType == LogicGateType.And
                    ? LogicGateType.Or
                    : LogicGateType.And;
                EditorUtility.SetDirty(_levelData);
                RefreshAppearance();
                BuildContents();
            });

        evt.menu.AppendAction("Delete", _ => GetFirstAncestorOfType<LevelGraphView>()?.DeleteSelection());
    }
}
