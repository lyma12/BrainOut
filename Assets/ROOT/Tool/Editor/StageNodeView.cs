using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
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
        TransitionInputPort.portName = "Input";
        inputContainer.Add(TransitionInputPort);

        TransitionOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        TransitionOutputPort.portName = "Output";
        outputContainer.Add(TransitionOutputPort);

        BuildContents();
        this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
        RefreshExpandedState();
        RefreshPorts();

        // Refresh status badges whenever the scene hierarchy changes (component added/removed)
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        RegisterCallback<DetachFromPanelEvent>(_ =>
            EditorApplication.hierarchyChanged -= OnHierarchyChanged);
    }

    private void OnHierarchyChanged()
    {
        if (_requirementsContainer == null) return;
        // Rebuild only the status row in each requirement card
        for (int i = 0; i < Data.Requirements.Count; i++)
        {
            var req  = Data.Requirements[i];
            var card = _requirementsContainer.ElementAt(i);
            if (card == null) continue;
            RefreshStatusRow(card, req);
        }
    }

    /// Removes the old status row and appends a fresh one at the bottom of the card.
    private void RefreshStatusRow(VisualElement card, RequirementData req)
    {
        // Status row is always the last child; remove it if it's a non-interactive element
        var last = card.childCount > 0 ? card[card.childCount - 1] : null;
        if (last != null && last.name == "statusRow") card.Remove(last);

        var row = MakeStatusRow(req);
        row.name = "statusRow";
        card.Add(row);
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildContents()
    {
        extensionContainer.Clear();
        _requirementPorts.Clear();

        // Sequential toggle
        var seqRow = new VisualElement();
        seqRow.style.flexDirection = FlexDirection.Row;
        seqRow.style.paddingLeft   = 6;
        seqRow.style.paddingTop    = 4;

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

        // Requirements header: label + ALL/ANY + add
        var reqHeader = new VisualElement();
        reqHeader.style.flexDirection = FlexDirection.Row;
        reqHeader.style.alignItems    = Align.Center;
        reqHeader.style.paddingLeft   = 6;
        reqHeader.style.paddingTop    = 6;
        reqHeader.style.paddingBottom = 2;

        var reqLabel = new Label("Requirements:");
        reqLabel.style.flexGrow = 1;
        reqLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        reqHeader.Add(reqLabel);

        var modeBtn = new Button();
        modeBtn.style.width    = 36;
        modeBtn.style.fontSize = 10;
        modeBtn.tooltip = "ALL: every requirement must be fulfilled\nANY: one is enough";
        RefreshModeBtn(modeBtn, Data.CompletionMode);
        modeBtn.clicked += () =>
        {
            Undo.RecordObject(_levelData, "Toggle Completion Mode");
            Data.CompletionMode = Data.CompletionMode == CompletionMode.All
                ? CompletionMode.Any : CompletionMode.All;
            EditorUtility.SetDirty(_levelData);
            RefreshModeBtn(modeBtn, Data.CompletionMode);
        };
        reqHeader.Add(modeBtn);

        var addBtn = new Button(() => AddRequirement()) { text = "+" };
        addBtn.style.width = 22;
        reqHeader.Add(addBtn);

        extensionContainer.Add(reqHeader);

        _requirementsContainer = new VisualElement();
        extensionContainer.Add(_requirementsContainer);

        foreach (var req in Data.Requirements)
            AddRequirementRow(req);

        RefreshExpandedState();
    }

    private static void RefreshModeBtn(Button btn, CompletionMode mode)
    {
        btn.text = mode == CompletionMode.All ? "ALL" : "ANY";
        btn.style.color = new StyleColor(
            mode == CompletionMode.All
                ? new Color(0.4f, 0.8f, 1f)
                : new Color(1f, 0.75f, 0.3f));
    }

    // ── Requirement rows ──────────────────────────────────────────────────────

    private void AddRequirement()
    {
        Undo.RecordObject(_levelData, "Add Requirement");
        var req = new RequirementData
        {
            Type         = RequirementType.Clicked,
            SourceObjectID = "",
        };
        req.RegenerateID();
        Data.Requirements.Add(req);
        EditorUtility.SetDirty(_levelData);
        AddRequirementRow(req);
        RefreshPorts();
    }

    private void AddRequirementRow(RequirementData req)
    {
        var card = new VisualElement();
        card.style.paddingLeft    = 4;
        card.style.paddingRight   = 4;
        card.style.paddingTop     = 4;
        card.style.paddingBottom  = 4;
        card.style.marginBottom   = 2;
        card.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
        card.style.borderTopLeftRadius     = 4;
        card.style.borderTopRightRadius    = 4;
        card.style.borderBottomLeftRadius  = 4;
        card.style.borderBottomRightRadius = 4;

        // ── Row 1: type dropdown + output port + delete ───────────────────────
        var topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.alignItems    = Align.Center;

        var typeField = new EnumField(req.Type);
        typeField.style.flexGrow = 1;
        typeField.style.minWidth = 90;
        typeField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Change Requirement Type");
            req.Type = (RequirementType)evt.newValue;
            req.RegenerateID();
            EditorUtility.SetDirty(_levelData);
            RebuildDetails(card, req);
        });
        topRow.Add(typeField);

        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
        port.portName = "";
        port.userData = req.RequirementID;
        port.style.paddingLeft = 2;
        _requirementPorts[req.RequirementID] = port;
        outputContainer.Add(port);
        topRow.Add(port);

        var delBtn = new Button(() => RemoveRequirement(req, card, port)) { text = "×" };
        delBtn.style.width = 20;
        StyleDeleteBtn(delBtn);
        topRow.Add(delBtn);

        card.Add(topRow);

        // ── Row 2+: detail fields (object picker, label…) ─────────────────────
        RebuildDetails(card, req);

        _requirementsContainer.Add(card);
    }

    // Clears and rebuilds detail rows (everything after topRow) inside a card.
    private void RebuildDetails(VisualElement card, RequirementData req)
    {
        while (card.childCount > 1)
            card.RemoveAt(1);

        switch (req.Type)
        {
            case RequirementType.Clicked:
            case RequirementType.DragComplete:
                card.Add(MakeObjectPickerRow("Object", req, req.MechanicType));
                break;

            case RequirementType.DropAccepted:
                card.Add(MakeObjectPickerRow("Drop Zone", req, MechanicType.DropTarget));
                card.Add(MakeDraggableListSection(req));
                break;

            case RequirementType.TimerExpired:
                break;

            case RequirementType.Custom:
                card.Add(MakeCustomIDRow(req));
                break;
        }

        // Component status badges
        if (req.Type != RequirementType.Custom && req.Type != RequirementType.TimerExpired)
        {
            var statusRow = MakeStatusRow(req);
            statusRow.name = "statusRow";
            card.Add(statusRow);
        }
    }

    // ── Object picker ─────────────────────────────────────────────────────────

    /// <summary>
    /// ObjectField that auto-applies required components when a GameObject is assigned.
    /// </summary>
    private VisualElement MakeObjectPickerRow(string label, RequirementData req, MechanicType mechanic)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingTop    = 2;

        var lbl = new Label(label + ":");
        lbl.style.width    = 62;
        lbl.style.fontSize = 10;
        lbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        row.Add(lbl);

        // Resolve current scene object from stored ID
        GameObject current = FindObjectByID(req.SourceObjectID);

        var objField = new ObjectField
        {
            objectType        = typeof(GameObject),
            allowSceneObjects = true,
            value             = current
        };
        objField.style.flexGrow = 1;

        objField.RegisterValueChangedCallback(evt =>
        {
            var go = evt.newValue as GameObject;
            if (go == null)
            {
                Undo.RecordObject(_levelData, "Clear Requirement Object");
                req.SourceObjectID = "";
                req.RegenerateID();
                EditorUtility.SetDirty(_levelData);
                RebuildDetails(row.parent, req);
                return;
            }

            // Ensure ActionTargetID exists and has an ID
            var targetID = EnsureActionTargetID(go);

            Undo.RecordObject(_levelData, "Set Requirement Object");
            req.SourceObjectID = targetID.ID;
            req.RegenerateID();
            EditorUtility.SetDirty(_levelData);

            // Auto-add required components
            ApplyComponents(go, mechanic);

            // Refresh status badges
            RebuildDetails(row.parent, req);
        });

        row.Add(objField);
        return row;
    }

    // ── Auto-apply components ─────────────────────────────────────────────────

    private static ActionTargetID EnsureActionTargetID(GameObject go)
    {
        var comp = go.GetComponent<ActionTargetID>();
        if (comp == null)
        {
            comp    = Undo.AddComponent<ActionTargetID>(go);
            comp.ID = GenerateObjectID(go);
        }
        else if (string.IsNullOrEmpty(comp.ID))
        {
            Undo.RecordObject(comp, "Set ActionTargetID");
            comp.ID = GenerateObjectID(go);
        }
        SaveObject(go);
        return comp;
    }

    /// Marks the object dirty and saves prefab asset if it's a prefab (not a scene instance).
    private static void SaveObject(UnityEngine.Object obj)
    {
        EditorUtility.SetDirty(obj);
        // If this object is part of a prefab asset (not an instance in scene), save the asset
        var go = obj is GameObject g ? g : (obj is Component c ? c.gameObject : null);
        if (go != null && UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null
            && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(go))
        {
            AssetDatabase.SaveAssetIfDirty(go);
        }
    }

    private static readonly MechanicType[] ItemUpMechanics =
    {
        MechanicType.Click,
        MechanicType.Draggable,
    };

    private static void ApplyComponents(GameObject go, MechanicType mechanic)
    {
        bool anyAdded = false;
        foreach (var info in ComponentRequirementRegistry.GetForMechanic(mechanic))
        {
            bool added = ComponentRequirementRegistry.EnsureComponent(go, info);
            if (added)
            {
                Debug.Log($"[LevelEditor] Added {info.DisplayName} to '{go.name}'.");
                anyAdded = true;
            }
        }

        // Objects that are clickable or draggable go on the "ItemUp" layer
        if (System.Array.IndexOf(ItemUpMechanics, mechanic) >= 0)
            EnsureLayer(go, "ItemUp");

        if (anyAdded) SaveObject(go);
    }

    private static void EnsureLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogWarning($"[LevelEditor] Layer '{layerName}' not found. Add it in Edit → Project Settings → Tags & Layers.");
            return;
        }
        if (go.layer == layer) return;

        Undo.RecordObject(go, $"Set Layer {layerName}");
        go.layer = layer;
        EditorUtility.SetDirty(go);
        Debug.Log($"[LevelEditor] Set layer of '{go.name}' to '{layerName}'.");
    }

    private static string GenerateObjectID(GameObject go)
    {
        // Stable ID: object name + short guid suffix to avoid collisions
        return $"{go.name}_{Guid.NewGuid().ToString("N")[..6]}";
    }

    private GameObject FindObjectByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        // Search scene first (Prefab Mode or Play mode)
        foreach (var t in UnityEngine.Object.FindObjectsOfType<ActionTargetID>())
            if (t.ID == id) return t.gameObject;

        // Search inside the level prefab asset
        if (_levelData?.LevelPrefab != null)
            foreach (var t in _levelData.LevelPrefab.GetComponentsInChildren<ActionTargetID>(true))
                if (t.ID == id) return t.gameObject;

        return null;
    }

    // ── Status badges ─────────────────────────────────────────────────────────

    private VisualElement MakeStatusRow(RequirementData req)
    {
        var container = new VisualElement();
        container.style.paddingTop  = 3;
        container.style.paddingLeft = 2;

        // Collect all (object, mechanic) pairs to check
        var targets = new System.Collections.Generic.List<(GameObject go, string label, MechanicType mechanic)>();

        // Primary source object (drop zone / draggable / clickable)
        if (!string.IsNullOrEmpty(req.SourceObjectID))
        {
            var go = FindObjectByID(req.SourceObjectID);
            targets.Add((go, "Zone", req.MechanicType));
        }

        // Accepted draggables (DropAccepted only)
        if (req.Type == RequirementType.DropAccepted && req.AcceptedDraggableIDs != null)
        {
            foreach (var id in req.AcceptedDraggableIDs)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var go = FindObjectByID(id);
                var name = go != null ? go.name : $"({id[..Mathf.Min(6, id.Length)]}…)";
                targets.Add((go, name, MechanicType.Draggable));
            }
        }

        if (targets.Count == 0) return container;

        // Track total missing count across all targets for the Fix All button
        var allFixes = new System.Collections.Generic.List<(GameObject go, ComponentRequirementRegistry.ComponentInfo info)>();

        foreach (var (go, label, mechanic) in targets)
        {
            var infos   = new System.Collections.Generic.List<ComponentRequirementRegistry.ComponentInfo>(
                              ComponentRequirementRegistry.GetForMechanic(mechanic));
            if (infos.Count == 0) continue;

            // Object label
            var objLabel = new Label(go != null ? $"{label}:" : $"{label}: ⚠ not found in scene");
            objLabel.style.fontSize = 9;
            objLabel.style.color    = new StyleColor(go != null
                ? new Color(0.6f, 0.6f, 0.6f)
                : new Color(1f, 0.5f, 0.2f));
            objLabel.style.paddingTop    = 1;
            objLabel.style.paddingBottom = 1;
            container.Add(objLabel);

            // Badge row for this object
            var badgeRow = new VisualElement();
            badgeRow.style.flexDirection = FlexDirection.Row;
            badgeRow.style.flexWrap      = Wrap.Wrap;
            badgeRow.style.paddingLeft   = 4;

            foreach (var info in infos)
            {
                bool isMissing = go == null || !ComponentRequirementRegistry.HasComponent(go, info);
                var badge = new Label(isMissing ? $"✕ {info.DisplayName}" : $"✓ {info.DisplayName}");
                badge.style.fontSize    = 9;
                badge.style.paddingLeft = badge.style.paddingRight  = 3;
                badge.style.paddingTop  = badge.style.paddingBottom = 1;
                badge.style.marginRight = badge.style.marginBottom  = 2;
                badge.style.color = new StyleColor(isMissing
                    ? new Color(1f, 0.4f, 0.4f)
                    : new Color(0.4f, 0.9f, 0.4f));
                badge.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.3f));
                badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
                badgeRow.Add(badge);

                if (isMissing && go != null)
                    allFixes.Add((go, info));
            }
            container.Add(badgeRow);
        }

        // Fix All button — only shown when anything is missing
        if (allFixes.Count > 0)
        {
            var fixBtn = new Button(() =>
            {
                foreach (var (go, info) in allFixes)
                {
                    ComponentRequirementRegistry.EnsureComponent(go, info);
                    EditorUtility.SetDirty(go);
                }
                RebuildDetails(container.parent, req);
            });

            fixBtn.text    = $"⚙ Fix All ({allFixes.Count} missing)";
            fixBtn.tooltip = "Add all missing components to all GameObjects";
            fixBtn.style.marginTop       = 3;
            fixBtn.style.height          = 20;
            fixBtn.style.fontSize        = 10;
            fixBtn.style.color           = new StyleColor(new Color(1f, 0.85f, 0.3f));
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

        return container;
    }

    // ── Simple field helpers ──────────────────────────────────────────────────

    // ── Draggable list (DropAccepted only) ───────────────────────────────────

    private VisualElement MakeDraggableListSection(RequirementData req)
    {
        var section = new VisualElement();
        section.style.paddingTop  = 3;
        section.style.paddingLeft = 4;

        // Header: "Accepts:" + add button
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems    = Align.Center;

        var headerLbl = new Label("Accepts:");
        headerLbl.style.flexGrow   = 1;
        headerLbl.style.fontSize   = 10;
        headerLbl.style.color      = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        headerLbl.tooltip          = "Draggable objects allowed into this drop zone.\nLeave empty to accept any draggable.";
        header.Add(headerLbl);

        var addBtn = new Button(() =>
        {
            Undo.RecordObject(_levelData, "Add Accepted Draggable");
            req.AcceptedDraggableIDs.Add("");
            EditorUtility.SetDirty(_levelData);
            RebuildDraggableRows(section, req);
        }) { text = "+" };
        addBtn.style.width  = 20;
        addBtn.style.height = 18;
        header.Add(addBtn);

        section.Add(header);

        // Hint when list is empty
        var emptyHint = new Label("(any draggable accepted)");
        emptyHint.name = "emptyHint";
        emptyHint.style.fontSize = 9;
        emptyHint.style.color    = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
        emptyHint.style.unityFontStyleAndWeight = FontStyle.Italic;
        emptyHint.style.paddingLeft = 2;
        section.Add(emptyHint);

        RebuildDraggableRows(section, req);
        return section;
    }

    private void RebuildDraggableRows(VisualElement section, RequirementData req)
    {
        // Remove all existing draggable rows (keep header at index 0 and emptyHint at index 1)
        while (section.childCount > 2)
            section.RemoveAt(2);

        var emptyHint = section.Q<Label>("emptyHint");

        if (req.AcceptedDraggableIDs == null || req.AcceptedDraggableIDs.Count == 0)
        {
            if (emptyHint != null) emptyHint.style.display = DisplayStyle.Flex;
            return;
        }

        if (emptyHint != null) emptyHint.style.display = DisplayStyle.None;

        for (int i = 0; i < req.AcceptedDraggableIDs.Count; i++)
        {
            int idx = i; // capture for lambda
            section.Add(MakeDraggableRow(section, req, idx));
        }
    }

    private VisualElement MakeDraggableRow(VisualElement section, RequirementData req, int index)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingTop    = 2;

        GameObject current = FindObjectByID(req.AcceptedDraggableIDs[index]);

        var objField = new ObjectField
        {
            objectType        = typeof(GameObject),
            allowSceneObjects = true,
            value             = current
        };
        objField.style.flexGrow = 1;
        objField.RegisterValueChangedCallback(evt =>
        {
            var go = evt.newValue as GameObject;
            if (go == null)
            {
                Undo.RecordObject(_levelData, "Clear Draggable");
                req.AcceptedDraggableIDs[index] = "";
                EditorUtility.SetDirty(_levelData);
                return;
            }

            // Ensure ActionTargetID + Draggable components
            var targetComp = EnsureActionTargetID(go);
            ApplyComponents(go, MechanicType.Draggable);

            Undo.RecordObject(_levelData, "Set Accepted Draggable");
            req.AcceptedDraggableIDs[index] = targetComp.ID;
            EditorUtility.SetDirty(_levelData);
        });
        row.Add(objField);

        var delBtn = new Button(() =>
        {
            Undo.RecordObject(_levelData, "Remove Accepted Draggable");
            req.AcceptedDraggableIDs.RemoveAt(index);
            EditorUtility.SetDirty(_levelData);
            RebuildDraggableRows(section, req);
        }) { text = "×" };
        delBtn.style.width = 20;
        StyleDeleteBtn(delBtn);
        row.Add(delBtn);

        return row;
    }

    private VisualElement MakeCustomIDRow(RequirementData req)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingTop    = 2;

        var lbl = new Label("Req ID:");
        lbl.style.width    = 62;
        lbl.style.fontSize = 10;
        lbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        row.Add(lbl);

        var field = new TextField { value = req.RequirementID ?? "" };
        field.style.flexGrow = 1;
        field.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(_levelData, "Change Custom Requirement ID");
            req.RequirementID = evt.newValue;
            EditorUtility.SetDirty(_levelData);
        });
        row.Add(field);
        return row;
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    private void RemoveRequirement(RequirementData req, VisualElement card, Port port)
    {
        Undo.RecordObject(_levelData, "Remove Requirement");
        Data.Requirements.Remove(req);
        _levelData.ActionConnections.RemoveAll(c => c.RequirementID == req.RequirementID);
        EditorUtility.SetDirty(_levelData);

        _requirementPorts.Remove(req.RequirementID);
        _requirementsContainer.Remove(card);
        port.DisconnectAll();
        outputContainer.Remove(port);
        RefreshPorts();
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    private static void StyleDeleteBtn(Button btn)
    {
        btn.style.color = new StyleColor(new Color(1f, 0.35f, 0.35f));
        btn.style.backgroundColor = new StyleColor(Color.clear);
        btn.style.borderLeftWidth = btn.style.borderRightWidth =
        btn.style.borderTopWidth  = btn.style.borderBottomWidth = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public Port GetRequirementPort(string requirementID)
    {
        _requirementPorts.TryGetValue(requirementID, out var port);
        return port;
    }

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
                title = IsStartStage() ? $"▶ {e.newValue}" : e.newValue;
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
