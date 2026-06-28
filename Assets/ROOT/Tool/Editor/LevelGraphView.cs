using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class LevelGraphView : GraphView
{
    private LevelData _levelData;
    private Dictionary<string, StageNodeView>    _stageNodes       = new Dictionary<string, StageNodeView>();
    private Dictionary<string, ActionNodeView>   _actionNodes      = new Dictionary<string, ActionNodeView>();
    private Dictionary<string, RequiredNodeView> _requirementNodes = new Dictionary<string, RequiredNodeView>();
    private Dictionary<string, LogicGateNodeView> _gateNodes       = new Dictionary<string, LogicGateNodeView>();

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
        _requirementNodes.Clear();
        _gateNodes.Clear();
        _startNode = null;
        _endNode   = null;

        DeleteElements(graphElements);
        if (data == null) return;

        _startNode = new StartNodeView(data.StartNodePosition);
        _endNode   = new EndNodeView(data.EndNodePosition);
        AddElement(_startNode);
        AddElement(_endNode);

        foreach (var stage in data.Stages)
        {
            var node = CreateStageNodeView(stage);
            _stageNodes[stage.StageID] = node;
        }

        foreach (var actionNode in data.ActionNodes)
        {
            var node = CreateActionNodeView(actionNode);
            _actionNodes[actionNode.ActionNodeID] = node;
        }

        foreach (var reqNode in data.RequirementNodes)
        {
            var node = CreateRequiredNodeView(reqNode);
            _requirementNodes[reqNode.NodeID] = node;
        }

        foreach (var gateNode in data.LogicGateNodes)
        {
            var node = CreateLogicGateNodeView(gateNode);
            _gateNodes[gateNode.NodeID] = node;
        }

        foreach (var conn in data.ActionConnections)
            ConnectRequirementToAction(conn);

        foreach (var conn in data.RequirementConnections)
            ConnectRequirementToGate(conn);

        foreach (var conn in data.LogicGateConnections)
            ConnectGateToStage(conn);

        foreach (var transition in data.Transitions)
            ConnectStageToStage(transition);

        if (!string.IsNullOrEmpty(data.StartStageID) && _stageNodes.TryGetValue(data.StartStageID, out var startStage))
            ConnectStartToStage(startStage);

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
            if (port == startPort || port.node == startPort.node) continue;
            if (port.direction == startPort.direction) continue;

            // RequirementNode ports: Gate output → LogicGate, Action output → ActionNode
            if (startPort.node is RequiredNodeView reqView)
            {
                if (startPort == reqView.GateOutputPort)
                {
                    if (port.node is LogicGateNodeView lg && port == lg.InputPort)
                        result.Add(port);
                }
                else if (startPort == reqView.ActionOutputPort)
                {
                    if (port.node is ActionNodeView)
                        result.Add(port);
                }
                continue;
            }
            if (port.node is RequiredNodeView reqView2)
            {
                if (port == reqView2.GateOutputPort && startPort.node is LogicGateNodeView lg2 && startPort == lg2.InputPort)
                    result.Add(port);
                else if (port == reqView2.ActionOutputPort && startPort.node is ActionNodeView)
                    result.Add(port);
                continue;
            }

            // LogicGate output → Stage ConditionInputPort
            if (startPort.node is LogicGateNodeView || port.node is LogicGateNodeView)
            {
                var gatePort  = startPort.node is LogicGateNodeView ? startPort : port;
                var otherPort = gatePort == startPort ? port : startPort;
                if (gatePort.direction != Direction.Output) continue;
                if (otherPort.node is StageNodeView sn && otherPort == sn.ConditionInputPort)
                    result.Add(port);
                continue;
            }

            // Transition connections: Start ↔ Stage, Stage ↔ Stage, Stage → End
            if (startPort.node is StartNodeView)
            {
                if (port.node is StageNodeView sn2 && port == sn2.TransitionInputPort)
                    result.Add(port);
                continue;
            }
            if (startPort.node is StageNodeView sn3 && startPort == sn3.TransitionOutputPort)
            {
                if (port.node is StageNodeView sn4 && port == sn4.TransitionInputPort) { result.Add(port); continue; }
                if (port.node is EndNodeView) { result.Add(port); continue; }
                continue;
            }
            if (startPort.node is StageNodeView sn5 && startPort == sn5.TransitionInputPort)
            {
                if (port.node is StartNodeView) result.Add(port);
                continue;
            }
            if (startPort.node is EndNodeView)
            {
                if (port.node is StageNodeView sn6 && port == sn6.TransitionOutputPort)
                    result.Add(port);
                continue;
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
                if (el is Edge edge)                   RemoveEdgeData(edge);
                else if (el is StageNodeView sn)       RemoveStageNode(sn);
                else if (el is ActionNodeView an)      RemoveActionNode(an);
                else if (el is RequiredNodeView rn)    RemoveRequirementNode(rn);
                else if (el is LogicGateNodeView gn)   RemoveGateNode(gn);
            }
            EditorUtility.SetDirty(_levelData);
        }

        if (change.movedElements != null)
        {
            Undo.RecordObject(_levelData, "Move Node");
            foreach (var el in change.movedElements)
            {
                if      (el is StageNodeView sn)       sn.Data.NodePosition      = sn.GetPosition().position;
                else if (el is ActionNodeView an)      an.Data.NodePosition      = an.GetPosition().position;
                else if (el is RequiredNodeView rn)    rn.Data.NodePosition      = rn.GetPosition().position;
                else if (el is LogicGateNodeView gn)   gn.Data.NodePosition      = gn.GetPosition().position;
                else if (el is StartNodeView)          _levelData.StartNodePosition = el.GetPosition().position;
                else if (el is EndNodeView)            _levelData.EndNodePosition   = el.GetPosition().position;
            }
            EditorUtility.SetDirty(_levelData);
        }

        return change;
    }

    private void ProcessNewEdge(Edge edge)
    {
        // Requirement Gate port → LogicGate
        if (edge.output.node is RequiredNodeView reqNode
            && edge.output == reqNode.GateOutputPort
            && edge.input.node is LogicGateNodeView gateIn)
        {
            _levelData.RequirementConnections.Add(new RequirementConnectionData
            {
                RequirementNodeID = reqNode.Data.NodeID,
                LogicGateNodeID   = gateIn.Data.NodeID
            });
            return;
        }

        // Requirement Action port → ActionNode
        if (edge.output.node is RequiredNodeView reqNode2
            && edge.output == reqNode2.ActionOutputPort
            && edge.input.node is ActionNodeView actionNode)
        {
            _levelData.ActionConnections.Add(new ActionConnectionData
            {
                RequirementNodeID = reqNode2.Data.NodeID,
                ActionNodeID      = actionNode.Data.ActionNodeID
            });
            return;
        }

        // Gate → Stage condition
        if (edge.output.node is LogicGateNodeView gateOut && edge.input.node is StageNodeView stageIn)
        {
            _levelData.LogicGateConnections.Add(new LogicGateConnectionData
            {
                LogicGateNodeID = gateOut.Data.NodeID,
                StageID         = stageIn.Data.StageID
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
        // Requirement Gate port → LogicGate
        if (edge.output.node is RequiredNodeView reqNode
            && edge.output == reqNode.GateOutputPort
            && edge.input.node is LogicGateNodeView gateIn)
        {
            _levelData.RequirementConnections.RemoveAll(c =>
                c.RequirementNodeID == reqNode.Data.NodeID && c.LogicGateNodeID == gateIn.Data.NodeID);
            return;
        }

        // Requirement Action port → ActionNode
        if (edge.output.node is RequiredNodeView reqNode2
            && edge.output == reqNode2.ActionOutputPort
            && edge.input.node is ActionNodeView actionNode)
        {
            _levelData.ActionConnections.RemoveAll(c =>
                c.RequirementNodeID == reqNode2.Data.NodeID && c.ActionNodeID == actionNode.Data.ActionNodeID);
            return;
        }

        // Gate → Stage condition
        if (edge.output.node is LogicGateNodeView gateOut && edge.input.node is StageNodeView stageIn)
        {
            _levelData.LogicGateConnections.RemoveAll(c =>
                c.LogicGateNodeID == gateOut.Data.NodeID && c.StageID == stageIn.Data.StageID);
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
        _levelData.LogicGateConnections.RemoveAll(c => c.StageID == node.Data.StageID);
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

    private void RemoveRequirementNode(RequiredNodeView node)
    {
        _levelData.RequirementNodes.Remove(node.Data);
        _levelData.RequirementConnections.RemoveAll(c => c.RequirementNodeID == node.Data.NodeID);
        _levelData.ActionConnections.RemoveAll(c => c.RequirementNodeID == node.Data.NodeID);
        _requirementNodes.Remove(node.Data.NodeID);
    }

    private void RemoveGateNode(LogicGateNodeView node)
    {
        _levelData.LogicGateNodes.Remove(node.Data);
        _levelData.RequirementConnections.RemoveAll(c => c.LogicGateNodeID == node.Data.NodeID);
        _levelData.LogicGateConnections.RemoveAll(c => c.LogicGateNodeID == node.Data.NodeID);
        _gateNodes.Remove(node.Data.NodeID);
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void BuildContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Add Stage Node",       a => AddStageNode(a.eventInfo.localMousePosition));
        evt.menu.AppendAction("Add Action Node",      a => AddActionNode(a.eventInfo.localMousePosition));
        evt.menu.AppendAction("Add Requirement Node", a => AddRequirementNode(a.eventInfo.localMousePosition));
        evt.menu.AppendAction("Add AND Gate",         a => AddLogicGateNode(a.eventInfo.localMousePosition, LogicGateType.And));
        evt.menu.AppendAction("Add OR Gate",          a => AddLogicGateNode(a.eventInfo.localMousePosition, LogicGateType.Or));
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
        _stageNodes[data.StageID] = CreateStageNodeView(data);
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
        _actionNodes[data.ActionNodeID] = CreateActionNodeView(data);
    }

    public void AddRequirementNode(Vector2 position)
    {
        if (_levelData == null) return;
        Undo.RecordObject(_levelData, "Add Requirement Node");
        var data = new RequirementNodeData
        {
            NodeID       = Guid.NewGuid().ToString(),
            Data         = new RequirementData { Type = RequirementType.Clicked },
            NodePosition = position
        };
        data.Data.RegenerateID();
        _levelData.RequirementNodes.Add(data);
        EditorUtility.SetDirty(_levelData);
        _requirementNodes[data.NodeID] = CreateRequiredNodeView(data);
    }

    public void AddLogicGateNode(Vector2 position, LogicGateType gateType)
    {
        if (_levelData == null) return;
        Undo.RecordObject(_levelData, "Add Logic Gate Node");
        var data = new LogicGateNodeData
        {
            NodeID       = Guid.NewGuid().ToString(),
            GateType     = gateType,
            NodePosition = position
        };
        _levelData.LogicGateNodes.Add(data);
        EditorUtility.SetDirty(_levelData);
        _gateNodes[data.NodeID] = CreateLogicGateNodeView(data);
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

    private RequiredNodeView CreateRequiredNodeView(RequirementNodeData data)
    {
        var node = new RequiredNodeView(data, _levelData);
        node.SetPosition(new Rect(data.NodePosition, Vector2.zero));
        AddElement(node);
        return node;
    }

    private LogicGateNodeView CreateLogicGateNodeView(LogicGateNodeData data)
    {
        var node = new LogicGateNodeView(data, _levelData);
        node.SetPosition(new Rect(data.NodePosition, Vector2.zero));
        AddElement(node);
        return node;
    }

    // ── Edge helpers ──────────────────────────────────────────────────────────

    private void ConnectRequirementToAction(ActionConnectionData conn)
    {
        if (!_requirementNodes.TryGetValue(conn.RequirementNodeID, out var reqNode)) return;
        if (!_actionNodes.TryGetValue(conn.ActionNodeID, out var actionNode)) return;
        var edge = reqNode.ActionOutputPort.ConnectTo(actionNode.InputPort);
        AddElement(edge);
    }

    private void ConnectRequirementToGate(RequirementConnectionData conn)
    {
        if (!_requirementNodes.TryGetValue(conn.RequirementNodeID, out var reqNode)) return;
        if (!_gateNodes.TryGetValue(conn.LogicGateNodeID, out var gateNode)) return;
        var edge = reqNode.GateOutputPort.ConnectTo(gateNode.InputPort);
        AddElement(edge);
    }

    private void ConnectGateToStage(LogicGateConnectionData conn)
    {
        if (!_gateNodes.TryGetValue(conn.LogicGateNodeID, out var gateNode)) return;
        if (!_stageNodes.TryGetValue(conn.StageID, out var stageNode)) return;
        var edge = gateNode.OutputPort.ConnectTo(stageNode.ConditionInputPort);
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
