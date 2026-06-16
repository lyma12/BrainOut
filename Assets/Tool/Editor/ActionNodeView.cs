using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class ActionNodeView : Node
{
    public ActionNodeData Data { get; private set; }
    public Port InputPort { get; private set; }

    private LevelData _levelData;
    private static readonly Color NodeColor = new Color(0.106f, 0.227f, 0.361f);

    public ActionNodeView(ActionNodeData data, LevelData levelData)
    {
        Data = data;
        _levelData = levelData;

        title = data.Action != null ? data.Action.Type.ToString() : "Action";
        titleContainer.style.backgroundColor = new StyleColor(NodeColor);

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
        InputPort.portName = "In";
        inputContainer.Add(InputPort);

        BuildActionFields();
        RefreshExpandedState();
        RefreshPorts();
    }

    private void BuildActionFields()
    {
        extensionContainer.Clear();

        if (Data.Action == null) return;

        var typeField = new EnumField("Type", Data.Action.Type);
        typeField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Change Action Type");
            var newType = (ActionType)evt.newValue;
            Data.Action = CreateActionDataForType(newType);
            title = newType.ToString();
            BuildActionFields();
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(typeField);

        var targetField = new EnumField("Target", Data.Action.Target);
        targetField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Change Action Target");
            Data.Action.Target = (ActionTarget)evt.newValue;
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(targetField);

        if (Data.Action.Target == ActionTarget.Other)
        {
            var targetIDField = new TextField("Target ID") { value = Data.Action.TargetID ?? "" };
            targetIDField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_levelData, "Change Target ID");
                Data.Action.TargetID = evt.newValue;
                EditorUtility.SetDirty(_levelData);
            });
            extensionContainer.Add(targetIDField);
        }

        var delayField = new FloatField("Delay") { value = Data.Action.Delay };
        delayField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Change Delay");
            Data.Action.Delay = evt.newValue;
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(delayField);

        switch (Data.Action.Type)
        {
            case ActionType.PlayAnimation when Data.Action is PlayAnimationActionData pa:
                AddIntField("Anim ID", pa.AnimationID, v => pa.AnimationID = v);
                AddToggleField("Loop", pa.Loop, v => pa.Loop = v);
                break;

            case ActionType.SetScale when Data.Action is SetScaleActionData ss:
                AddVector3Field("Scale", ss.Scale, v => ss.Scale = v);
                break;

            case ActionType.SetPosition when Data.Action is SetPositionActionData sp:
                AddVector3Field("Position", sp.Position, v => sp.Position = v);
                break;

            case ActionType.SetActive when Data.Action is SetActiveActionData sa:
                AddToggleField("Active", sa.Active, v => sa.Active = v);
                break;
        }

        RefreshExpandedState();
    }

    private ActionData CreateActionDataForType(ActionType type)
    {
        return type switch
        {
            ActionType.PlayAnimation => new PlayAnimationActionData { Type = type },
            ActionType.SetScale => new SetScaleActionData { Type = type, Scale = Vector3.one },
            ActionType.SetPosition => new SetPositionActionData { Type = type },
            ActionType.SetActive => new SetActiveActionData { Type = type },
            _ => new ActionData { Type = type }
        };
    }

    private void AddIntField(string label, int value, System.Action<int> onChange)
    {
        var field = new IntegerField(label) { value = value };
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, $"Change {label}");
            onChange(evt.newValue);
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(field);
    }

    private void AddToggleField(string label, bool value, System.Action<bool> onChange)
    {
        var field = new Toggle(label) { value = value };
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, $"Change {label}");
            onChange(evt.newValue);
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(field);
    }

    private void AddVector3Field(string label, Vector3 value, System.Action<Vector3> onChange)
    {
        var field = new Vector3Field(label) { value = value };
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, $"Change {label}");
            onChange(evt.newValue);
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(field);
    }
}
