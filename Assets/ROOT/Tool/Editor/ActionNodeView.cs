using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
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
            BuildActionFields();
        });
        extensionContainer.Add(targetField);

        if (Data.Action.Target == ActionTarget.Other)
        {
            GameObject currentTarget = FindObjectByID(Data.Action.TargetID);

            var targetObjectField = new ObjectField("Object")
            {
                objectType        = typeof(GameObject),
                allowSceneObjects = true,
                value             = currentTarget
            };
            targetObjectField.RegisterValueChangedCallback(evt =>
            {
                var go = evt.newValue as GameObject;
                if (go == null)
                {
                    Undo.RecordObject(_levelData, "Clear Target Object");
                    Data.Action.TargetID = "";
                    EditorUtility.SetDirty(_levelData);
                    return;
                }

                // Ensure ActionTargetID exists
                var targetComp = go.GetComponent<ActionTargetID>();
                if (targetComp == null)
                {
                    targetComp    = Undo.AddComponent<ActionTargetID>(go);
                    targetComp.ID = $"{go.name}_{System.Guid.NewGuid().ToString("N")[..6]}";
                    EditorUtility.SetDirty(go);
                    Debug.Log($"[LevelEditor] Added ActionTargetID '{targetComp.ID}' to '{go.name}'.");
                }
                else if (string.IsNullOrEmpty(targetComp.ID))
                {
                    Undo.RecordObject(targetComp, "Set ActionTargetID");
                    targetComp.ID = $"{go.name}_{System.Guid.NewGuid().ToString("N")[..6]}";
                    EditorUtility.SetDirty(targetComp);
                }

                // Auto-add components required by this action type
                ApplyActionComponents(go, Data.Action);

                Undo.RecordObject(_levelData, "Set Target Object");
                Data.Action.TargetID = targetComp.ID;
                EditorUtility.SetDirty(_levelData);
            });
            extensionContainer.Add(targetObjectField);
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
                var backendField = new EnumField("Backend", pa.Backend);
                backendField.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_levelData, "Change Animation Backend");
                    pa.Backend = (AnimationBackend)evt.newValue;
                    EditorUtility.SetDirty(_levelData);
                    BuildActionFields();
                });
                extensionContainer.Add(backendField);

                switch (pa.Backend)
                {
                    case AnimationBackend.Spine:
                        AddIntField("Track", pa.SpineTrackIndex, v => pa.SpineTrackIndex = v);
                        AddTextField("Anim Name", pa.SpineAnimationName, v => pa.SpineAnimationName = v);
                        break;
                    case AnimationBackend.Animator:
                        AddTextField("State Name", pa.AnimatorStateName, v => pa.AnimatorStateName = v);
                        break;
                    case AnimationBackend.DOTween:
                        AddTextField("Sequence ID", pa.DOTweenSequenceID, v => pa.DOTweenSequenceID = v);
                        break;
                }
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

    // ── Auto-apply helpers ────────────────────────────────────────────────────

    private GameObject FindObjectByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var t in Object.FindObjectsOfType<ActionTargetID>())
            if (t.ID == id) return t.gameObject;

        if (_levelData?.LevelPrefab != null)
            foreach (var t in _levelData.LevelPrefab.GetComponentsInChildren<ActionTargetID>(true))
                if (t.ID == id) return t.gameObject;

        return null;
    }

    private static void ApplyActionComponents(GameObject go, ActionData action)
    {
        bool anyAdded = false;
        foreach (var info in ComponentRequirementRegistry.GetForAction(action))
        {
            if (ComponentRequirementRegistry.EnsureComponent(go, info))
            {
                Debug.Log($"[LevelEditor] Added {info.DisplayName} to '{go.name}'.");
                anyAdded = true;
            }
        }
        if (anyAdded) EditorUtility.SetDirty(go);
    }

    private void AddTextField(string label, string value, System.Action<string> onChange)
    {
        var field = new TextField(label) { value = value ?? "" };
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, $"Change {label}");
            onChange(evt.newValue);
            EditorUtility.SetDirty(_levelData);
        });
        extensionContainer.Add(field);
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
