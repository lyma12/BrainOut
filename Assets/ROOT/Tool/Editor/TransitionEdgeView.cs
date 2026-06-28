using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class TransitionEdgeView : Edge
{
    private TransitionData _transitionData;
    private LevelData _levelData;
    private StageData _fromStage;
    private Label _edgeLabel;

    public TransitionEdgeView()
    {
        _edgeLabel = new Label();
        _edgeLabel.style.position = Position.Absolute;
        _edgeLabel.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        _edgeLabel.style.paddingLeft = 4;
        _edgeLabel.style.paddingRight = 4;
        _edgeLabel.style.paddingTop = 2;
        _edgeLabel.style.paddingBottom = 2;
        _edgeLabel.style.fontSize = 10;
        Add(_edgeLabel);

        this.RegisterCallback<MouseDownEvent>(OnMouseDown);
        RegisterCallback<GeometryChangedEvent>(_ => UpdateLabelPosition());
    }

    public void SetTransitionData(TransitionData data, LevelData levelData, StageData fromStage)
    {
        _transitionData = data;
        _levelData = levelData;
        _fromStage = fromStage;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (_transitionData == null) { _edgeLabel.text = ""; return; }

        string dir = _transitionData.Direction == TransitionDirection.None ? "" : _transitionData.Direction.ToString() + " ";
        string delay = _transitionData.TimeDelayNext > 0 ? $"+{_transitionData.TimeDelayNext}s " : "";
        string trans = $"{_transitionData.TimeTransition}s";
        int condCount = _transitionData.RequiredFulfilledIDs?.Count ?? 0;
        string conds = condCount > 0 ? $" [{condCount} cond]" : "";

        _edgeLabel.text = $"{dir}{delay}{trans}{conds}";
    }

    private void UpdateLabelPosition()
    {
        if (edgeControl == null) return;
        var mid = (edgeControl.from + edgeControl.to) * 0.5f;
        _edgeLabel.style.left = mid.x - _edgeLabel.resolvedStyle.width * 0.5f;
        _edgeLabel.style.top = mid.y - _edgeLabel.resolvedStyle.height * 0.5f;
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.clickCount == 2 && _transitionData != null)
        {
            OpenPopup(evt.mousePosition);
            evt.StopPropagation();
        }
    }

    private void OpenPopup(Vector2 position)
    {
        var popup = new TransitionPopupWindow(_transitionData, _levelData, _fromStage, () =>
        {
            EditorUtility.SetDirty(_levelData);
            UpdateLabel();
        });

        UnityEditor.PopupWindow.Show(new Rect(position, Vector2.zero), popup);
    }
}

public class TransitionPopupWindow : PopupWindowContent
{
    private TransitionData _data;
    private LevelData _levelData;
    private StageData _fromStage;
    private System.Action _onChanged;

    public TransitionPopupWindow(TransitionData data, LevelData levelData, StageData fromStage, System.Action onChanged)
    {
        _data = data;
        _levelData = levelData;
        _fromStage = fromStage;
        _onChanged = onChanged;
    }

    public override Vector2 GetWindowSize() => new Vector2(280, 300);

    public override void OnGUI(Rect rect)
    {
        EditorGUI.BeginChangeCheck();

        GUILayout.Label("Transition Settings", EditorStyles.boldLabel);
        _data.TimeDelayNext = EditorGUILayout.FloatField("Delay Next Stage", _data.TimeDelayNext);
        _data.TimeTransition = EditorGUILayout.FloatField("Transition Time", _data.TimeTransition);
        _data.Direction = (TransitionDirection)EditorGUILayout.EnumPopup("Direction", _data.Direction);

        GUILayout.Space(8);
        GUILayout.Label("Required Conditions (AND logic):", EditorStyles.boldLabel);
        GUILayout.Label("All checked requirements must be fulfilled.", EditorStyles.miniLabel);

        if (_levelData != null && _levelData.RequirementNodes != null && _levelData.RequirementNodes.Count > 0)
        {
            foreach (var reqNode in _levelData.RequirementNodes)
            {
                var req = reqNode.Data;
                bool isCond = _data.RequiredFulfilledIDs.Contains(req.RequirementID);
                var label = string.IsNullOrEmpty(req.SourceObjectID)
                    ? $"{req.Type} [{reqNode.NodeID[..6]}]"
                    : $"{req.Type} ({req.SourceObjectID})";
                bool newVal = EditorGUILayout.ToggleLeft(label, isCond);
                if (newVal != isCond)
                {
                    Undo.RecordObject(_levelData, "Change Transition Condition");
                    if (newVal) _data.RequiredFulfilledIDs.Add(req.RequirementID);
                    else _data.RequiredFulfilledIDs.Remove(req.RequirementID);
                }
            }
        }
        else
        {
            GUILayout.Label("(No requirement nodes in level)", EditorStyles.miniLabel);
        }

        if (EditorGUI.EndChangeCheck())
            _onChanged?.Invoke();
    }
}
