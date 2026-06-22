using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class LevelGraphView : GraphView
{
    private LevelData _levelData;
    private Dictionary<string, StageNodeView> _stageNodes  = new Dictionary<string, StageNodeView>();
    private Dictionary<string, ActionNodeView> _actionNodes = new Dictionary<string, ActionNodeView>();

    private StartNodeView _startNode;
    private EndNodeView   _endNode;

    public LevelGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        grid.StretchToParentSize();
        Insert(0, grid);

        graphViewChanged += OnGraphViewChanged;
        this.RegisterCallback<KeyDownEvent>(OnKeyDown);
        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
    }

    // ── Populate ──────────────────────────────────────────────────────────────

    public void PopulateFromLevel(LevelData data)
    {
        _levelData = data;
        _stageNodes.Clear();
        _actionNodes.Clear();
        _startNode = null;
        _endNode   = null;

        DeleteElements(graphElements);
        if (data == null) return;

        // Start / End anchor nodes
        _startNode = new StartNodeView(data.StartNodePosition);
        _endNode   = new EndNodeView(data.EndNodePosition);
        AddElement(_startNode);
        AddElement(_endNode);

        // Stage nodes
        foreach (var stage in data.Stages)
        {
            var node = CreateStageNodeView(stage);
            _stageNodes[stage.StageID] = node;
        }

        // Action nodes
        foreach (var actionNode in data.ActionNodes)
        {
            var node = CreateActionNodeView(actionNode);
            _actionNodes[actionNode.ActionNodeID] = node;
        }

        // Edges: requirement → action
        foreach (var conn in data.ActionConnections)
            ConnectRequirementToAction(conn);

        // Edges: stage → stage transitions
        foreach (var transition in data.Transitions)
            ConnectStageToStage(transition);

        // Edge: Start → first stage
        if (!string.IsNullOrEmpty(data.StartStageID) && _stageNodes.TryGetValue(data.StartStageID, out var startStage))
            ConnectStartToStage(startStage);

        // Edges: stage → End
        foreach (var endID in data.EndStageIDs)
        {
            if (_stageNodes.TryGetValue(endID, out var endStage))
                ConnectStageToEnd(endStage);
        }
    }

    // ── Port compatibility ────────────────────────────────────────────────────

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var result = new List<Port>();
        foreach (var port in ports)
        {
            if (port == startPort) continue;
            if (port.node == startPort.node) continue;
            if (port.direction == startPort.direction) continue;

            bool startIsReq = startPort.userData is string;
            bool endIsReq   = port.userData is string;

            if (startIsReq || endIsReq)
            {
                // Requirement output → ActionNode input only
                if (!startIsReq || endIsReq) continue;
                if (port.node is ActionNodeView) result.Add(port);
            }
            else
            {
                // Start → Stage
                if (startPort.node is StartNodeView && port.node is StageNodeView)
                { result.Add(port); continue; }
                if (startPort.node is StageNodeView && port.node is StartNodeView)
                { result.Add(port); continue; }

                // Stage → End
                if (startPort.node is StageNodeView && port.node is EndNodeView)
                { result.Add(port); continue; }
                if (startPort.node is EndNodeView && port.node is StageNodeView)
                { result.Add(port); continue; }

                // Stage → Stage transitions
                if (startPort.node is StageNodeView && port.node is StageNodeView)
                    result.Add(port);
            }
        }
        return result;
    }

    // ── Graph change handler ──────────────────────────────────────────────────

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if (_levelData == null) return change;

        if (change.edgesToCreate != null)
        {
            foreach (var edge in change.edgesToCreate)
            {
                Undo.RecordObject(_levelData, "Add Connection");
                ProcessNewEdge(edge);
                EditorUtility.SetDirty(_levelData);
            }
        }

        if (change.elementsToRemove != null)
        {
            Undo.RecordObject(_levelData, "Remove Element");
            foreach (var el in change.elementsToRemove)
            {
                if (el is Edge edge)             RemoveEdgeData(edge);
                else if (el is StageNodeView sn) RemoveStageNode(sn);
                else if (el is ActionNodeView an) RemoveActionNode(an);
            }
            EditorUtility.SetDirty(_levelData);
        }

        if (change.movedElements != null)
        {
            Undo.RecordObject(_levelData, "Move Node");
            foreach (var el in change.movedElements)
            {
                if (el is StageNodeView sn)       sn.Data.NodePosition = sn.GetPosition().position;
                else if (el is ActionNodeView an) an.Data.NodePosition = an.GetPosition().position;
                else if (el is StartNodeView)     _levelData.StartNodePosition = el.GetPosition().position;
                else if (el is EndNodeView)       _levelData.EndNodePosition   = el.GetPosition().position;
            }
            EditorUtility.SetDirty(_levelData);
        }

        return change;
    }

    private void ProcessNewEdge(Edge edge)
    {
        // Requirement → Action
        if (edge.output.userData is string requirementID && edge.input.node is ActionNodeView actionNode)
        {
            _levelData.ActionConnections.Add(new ActionConnectionData
            {
                RequirementID = requirementID,
                ActionNodeID  = actionNode.Data.ActionNodeID
            });
            return;
        }

        // Start → Stage
        if (edge.output.node is StartNodeView && edge.input.node is StageNodeView startTarget)
        {
            _levelData.StartStageID = startTarget.Data.StageID;
            return;
        }

        // Stage → End
        if (edge.output.node is StageNodeView endSource && edge.input.node is EndNodeView)
        {
            if (!_levelData.EndStageIDs.Contains(endSource.Data.StageID))
                _levelData.EndStageIDs.Add(endSource.Data.StageID);
            return;
        }

        // Stage → Stage transition
        if (edge.output.node is StageNodeView fromStage && edge.input.node is StageNodeView toStage)
        {
            _levelData.Transitions.Add(new TransitionData
            {
                FromStageID = fromStage.Data.StageID,
                ToStageID   = toStage.Data.StageID
            });
            var transitionEdge = edge as TransitionEdgeView ?? new TransitionEdgeView();
            transitionEdge.SetTransitionData(
                _levelData.Transitions[_levelData.Transitions.Count - 1], _levelData, fromStage.Data);
        }
    }

    private void RemoveEdgeData(Edge edge)
    {
        // Requirement → Action
        if (edge.output.userData is string requirementID && edge.input.node is ActionNodeView actionNode)
        {
            _levelData.ActionConnections.RemoveAll(c =>
                c.RequirementID == requirementID && c.ActionNodeID == actionNode.Data.ActionNodeID);
            return;
        }

        // Start → Stage
        if (edge.output.node is StartNodeView && edge.input.node is StageNodeView)
        {
            _levelData.StartStageID = "";
            return;
        }

        // Stage → End
        if (edge.output.node is StageNodeView endSource && edge.input.node is EndNodeView)
        {
            _levelData.EndStageIDs.Remove(endSource.Data.StageID);
            return;
        }

        // Stage → Stage
        if (edge.output.node is StageNodeView fromStage && edge.input.node is StageNodeView toStage)
        {
            _levelData.Transitions.RemoveAll(t =>
                t.FromStageID == fromStage.Data.StageID && t.ToStageID == toStage.Data.StageID);
        }
    }

    private void RemoveStageNode(StageNodeView node)
    {
        _levelData.Stages.Remove(node.Data);
        _levelData.Transitions.RemoveAll(t =>
            t.FromStageID == node.Data.StageID || t.ToStageID == node.Data.StageID);
        _levelData.EndStageIDs.Remove(node.Data.StageID);

        if (_levelData.StartStageID == node.Data.StageID)
            _levelData.StartStageID = "";

        _stageNodes.Remove(node.Data.StageID);
    }

    private void RemoveActionNode(ActionNodeView node)
    {
        _levelData.ActionNodes.Remove(node.Data);
        _levelData.ActionConnections.RemoveAll(c => c.ActionNodeID == node.Data.ActionNodeID);
        _actionNodes.Remove(node.Data.ActionNodeID);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Add Stage Node", a => AddStageNode(a.eventInfo.localMousePosition));
        evt.menu.AppendAction("Add Action Node", a => AddActionNode(a.eventInfo.localMousePosition));
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.S && evt.ctrlKey)
        {
            AssetDatabase.SaveAssets();
            evt.StopPropagation();
        }
    }

    // ── Add nodes ─────────────────────────────────────────────────────────────

    public void AddStageNode(Vector2 position)
    {
        if (_levelData == null) return;
        Undo.RecordObject(_levelData, "Add Stage Node");

        var data = new StageData
        {
            StageID      = Guid.NewGuid().ToString(),
            DisplayName  = "Stage " + _levelData.Stages.Count,
            NodePosition = position
        };

        _levelData.Stages.Add(data);
        EditorUtility.SetDirty(_levelData);

        var node = CreateStageNodeView(data);
        _stageNodes[data.StageID] = node;
    }

    public void AddActionNode(Vector2 position)
    {
        if (_levelData == null) return;
        Undo.RecordObject(_levelData, "Add Action Node");

        var data = new ActionNodeData
        {
            ActionNodeID = Guid.NewGuid().ToString(),
            Action       = new ActionData { Type = ActionType.PlayAnimation },
            NodePosition = position
        };

        _levelData.ActionNodes.Add(data);
        EditorUtility.SetDirty(_levelData);

        var node = CreateActionNodeView(data);
        _actionNodes[data.ActionNodeID] = node;
    }

    // ── Node factories ────────────────────────────────────────────────────────

    private StageNodeView CreateStageNodeView(StageData data)
    {
        var node = new StageNodeView(data, _levelData);
        node.SetPosition(new Rect(data.NodePosition, Vector2.zero));
        AddElement(node);
        return node;
    }

    private ActionNodeView CreateActionNodeView(ActionNodeData data)
    {
        var node = new ActionNodeView(data, _levelData);
        node.SetPosition(new Rect(data.NodePosition, Vector2.zero));
        AddElement(node);
        return node;
    }

    // ── Edge helpers ──────────────────────────────────────────────────────────

    private void ConnectRequirementToAction(ActionConnectionData conn)
    {
        Port outputPort = null;
        foreach (var stageNode in _stageNodes.Values)
        {
            outputPort = stageNode.GetRequirementPort(conn.RequirementID);
            if (outputPort != null) break;
        }
        if (outputPort == null) return;
        if (!_actionNodes.TryGetValue(conn.ActionNodeID, out var actionNode)) return;

        var edge = outputPort.ConnectTo(actionNode.InputPort);
        AddElement(edge);
    }

    private void ConnectStageToStage(TransitionData transition)
    {
        if (!_stageNodes.TryGetValue(transition.FromStageID, out var fromNode)) return;
        if (!_stageNodes.TryGetValue(transition.ToStageID,   out var toNode))   return;

        var edge = new TransitionEdgeView();
        edge.output = fromNode.TransitionOutputPort;
        edge.input  = toNode.TransitionInputPort;
        edge.output.Connect(edge);
        edge.input.Connect(edge);
        edge.SetTransitionData(transition, _levelData, fromNode.Data);
        AddElement(edge);
    }

    private void ConnectStartToStage(StageNodeView stageNode)
    {
        var edge = _startNode.OutputPort.ConnectTo(stageNode.TransitionInputPort);
        AddElement(edge);
    }

    private void ConnectStageToEnd(StageNodeView stageNode)
    {
        var edge = stageNode.TransitionOutputPort.ConnectTo(_endNode.InputPort);
        AddElement(edge);
    }

    // ── Highlight ─────────────────────────────────────────────────────────────

    public void HighlightActiveStage(string stageID)
    {
        foreach (var pair in _stageNodes)
            pair.Value.SetActiveHighlight(pair.Key == stageID);
    }

    public void ClearHighlight()
    {
        foreach (var pair in _stageNodes)
            pair.Value.SetActiveHighlight(false);
    }
}
