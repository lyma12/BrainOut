using System;
using System.Collections.Generic;
using ROOT.Scripts;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public sealed class DropAcceptedDescriptor : IRequirementDescriptor
{
    static DropAcceptedDescriptor() =>
        RequirementTypeRegistry.Register(RequirementType.DropAccepted, new DropAcceptedDescriptor());

    // ── Linker ────────────────────────────────────────────────────────────────

    public Type LinkerType => typeof(DropRequirementLinker);

    public void BakeFields(GameObject go, SerializedObject linkerSO, RequirementData req)
    {
        var ids = CollectIDs(req);

        // DropRequirementLinker
        var arr = linkerSO.FindProperty("_acceptedIDs");
        arr.ClearArray();
        for (int i = 0; i < ids.Count; i++)
        {
            arr.InsertArrayElementAtIndex(i);
            arr.GetArrayElementAtIndex(i).stringValue = ids[i];
        }
        linkerSO.FindProperty("_lockOnAccept").boolValue = req.DropLockOnAccept;

        // DropZone — acceptOnce, acceptedItemIDs, snapPoint
        var dropZone = go.GetComponent<DropZone>();
        if (dropZone != null)
        {
            var zoneSO = new SerializedObject(dropZone);
            zoneSO.FindProperty("_acceptOnce").boolValue = req.DropAcceptOnce;

            var zoneArr = zoneSO.FindProperty("_acceptedItemIDs");
            zoneArr.ClearArray();
            for (int i = 0; i < ids.Count; i++)
            {
                zoneArr.InsertArrayElementAtIndex(i);
                zoneArr.GetArrayElementAtIndex(i).stringValue = ids[i];
            }

            var snapProp = zoneSO.FindProperty("_snapPoint");
            snapProp.objectReferenceValue = null;
            if (!string.IsNullOrEmpty(req.DropSnapPointID))
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    var atid = t.GetComponent<ActionTargetID>();
                    if (atid != null && atid.ID == req.DropSnapPointID)
                    {
                        snapProp.objectReferenceValue = t;
                        break;
                    }
                }
            }
            zoneSO.ApplyModifiedProperties();
        }

        // Per-Snappable config
        foreach (var entry in req.AcceptedSnappables)
        {
            if (string.IsNullOrEmpty(entry.SnappableID)) continue;
            var snappableGO = NodeViewHelper.FindObjectByID(entry.SnappableID, null);
            if (snappableGO == null) continue;
            var snappable = snappableGO.GetComponent<Snappable>();
            if (snappable == null) continue;
            var so = new SerializedObject(snappable);
            so.FindProperty("_snapDistance").floatValue        = entry.SnapDistance;
            so.FindProperty("_returnOnInvalidDrop").boolValue  = entry.ReturnOnInvalidDrop;
            so.ApplyModifiedProperties();
            NodeViewHelper.SaveObject(snappableGO);
        }
    }

    private static List<string> CollectIDs(RequirementData req)
    {
        var ids = new List<string>();
        foreach (var e in req.AcceptedSnappables)
            if (!string.IsNullOrEmpty(e.SnappableID))
                ids.Add(e.SnappableID);
        return ids;
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    public string SourcePickerLabel => "Drop Zone";
    public MechanicType SourceMechanic => MechanicType.DropTarget;
    public bool ShowStatus => true;

    public VisualElement BuildExtraUI(RequirementData req, LevelData levelData,
        Action onBakeNeeded, Action onRebuildUI)
    {
        var root = new VisualElement();
        root.Add(BuildZoneConfigSection(req, levelData, onBakeNeeded));
        root.Add(BuildSnappableListSection(req, levelData, onBakeNeeded));
        return root;
    }

    // ── Zone config ───────────────────────────────────────────────────────────

    private VisualElement BuildZoneConfigSection(RequirementData req, LevelData levelData, Action onBakeNeeded)
    {
        var section = new VisualElement();
        section.style.paddingTop    = 3;
        section.style.paddingBottom = 3;
        section.style.borderBottomWidth = 1;
        section.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));

        section.Add(BuildSnapPointRow(req, levelData, onBakeNeeded));
        section.Add(BuildToggleRow("Accept Once:", req.DropAcceptOnce,
            "Zone chỉ nhận một lần rồi đóng lại",
            v => { req.DropAcceptOnce = v; onBakeNeeded(); },
            levelData, "Toggle Accept Once"));
        section.Add(BuildToggleRow("Lock:", req.DropLockOnAccept,
            "Item bị khóa sau khi drop đúng, không kéo ra được nữa",
            v => { req.DropLockOnAccept = v; onBakeNeeded(); },
            levelData, "Toggle Lock On Accept"));
        return section;
    }

    private VisualElement BuildSnapPointRow(RequirementData req, LevelData levelData, Action onBakeNeeded)
    {
        var row = MakeRow();
        row.Add(MakeLabel("Snap:"));

        Transform currentSnap = null;
        var sourceGO = NodeViewHelper.FindObjectByID(req.SourceObjectID, levelData);
        if (sourceGO != null)
        {
            var dz = sourceGO.GetComponent<DropZone>();
            if (dz != null)
            {
                var so = new SerializedObject(dz);
                currentSnap = so.FindProperty("_snapPoint").objectReferenceValue as Transform;
            }
        }

        var objField = new ObjectField { objectType = typeof(Transform), allowSceneObjects = true, value = currentSnap };
        objField.style.flexGrow = 1;
        objField.RegisterValueChangedCallback(evt =>
        {
            var t = evt.newValue as Transform;
            if (t == null) { req.DropSnapPointID = ""; EditorUtility.SetDirty(levelData); onBakeNeeded(); return; }
            var atid = t.GetComponent<ActionTargetID>();
            if (atid == null) { atid = Undo.AddComponent<ActionTargetID>(t.gameObject); atid.ID = NodeViewHelper.GenerateObjectID(t.gameObject); NodeViewHelper.SaveObject(t.gameObject); }
            Undo.RecordObject(levelData, "Set Snap Point");
            req.DropSnapPointID = atid.ID;
            EditorUtility.SetDirty(levelData);
            onBakeNeeded();
        });
        row.Add(objField);
        return row;
    }

    private VisualElement BuildToggleRow(string labelText, bool current, string tooltip,
        Action<bool> onChange, LevelData levelData, string undoName)
    {
        var row = MakeRow();
        var lbl = MakeLabel(labelText);
        lbl.tooltip = tooltip;
        row.Add(lbl);
        var toggle = new Toggle { value = current };
        toggle.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(levelData, undoName);
            onChange(evt.newValue);
            EditorUtility.SetDirty(levelData);
        });
        row.Add(toggle);
        return row;
    }

    // ── Snappable list ────────────────────────────────────────────────────────

    private VisualElement BuildSnappableListSection(RequirementData req, LevelData levelData, Action onBakeNeeded)
    {
        var section = new VisualElement();
        section.style.paddingTop = 4;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems    = Align.Center;

        var headerLbl = new Label("Snappables:");
        headerLbl.style.flexGrow = 1;
        headerLbl.style.fontSize = 10;
        headerLbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        headerLbl.tooltip        = "Danh sách Snappable được chấp nhận. Empty = chấp nhận tất cả.";
        header.Add(headerLbl);

        var addBtn = new Button(() =>
        {
            Undo.RecordObject(levelData, "Add Snappable Entry");
            req.AcceptedSnappables.Add(new SnappableEntryData());
            EditorUtility.SetDirty(levelData);
            RebuildEntries(section, req, levelData, onBakeNeeded);
        }) { text = "+" };
        addBtn.style.width = 20; addBtn.style.height = 18;
        header.Add(addBtn);
        section.Add(header);

        var emptyHint = new Label("(any snappable accepted)");
        emptyHint.name = "emptyHint";
        emptyHint.style.fontSize = 9;
        emptyHint.style.color    = new StyleColor(new Color(0.45f, 0.45f, 0.45f));
        emptyHint.style.unityFontStyleAndWeight = FontStyle.Italic;
        section.Add(emptyHint);

        RebuildEntries(section, req, levelData, onBakeNeeded);
        return section;
    }

    private void RebuildEntries(VisualElement section, RequirementData req,
        LevelData levelData, Action onBakeNeeded)
    {
        while (section.childCount > 2) section.RemoveAt(2);
        var hint = section.Q<Label>("emptyHint");
        bool empty = req.AcceptedSnappables == null || req.AcceptedSnappables.Count == 0;
        if (hint != null) hint.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
        if (empty) return;
        for (int i = 0; i < req.AcceptedSnappables.Count; i++)
            section.Add(BuildEntryCard(section, i, req, levelData, onBakeNeeded));
    }

    private VisualElement BuildEntryCard(VisualElement section, int index, RequirementData req,
        LevelData levelData, Action onBakeNeeded)
    {
        var entry = req.AcceptedSnappables[index];

        var card = new VisualElement();
        card.style.borderLeftWidth = 2;
        card.style.borderLeftColor = new StyleColor(new Color(0.4f, 0.4f, 0.5f));
        card.style.paddingLeft  = 4;
        card.style.marginTop    = 3;
        card.style.marginBottom = 1;

        // Row 1: Object picker + delete
        var topRow = MakeRow();
        var objField = new ObjectField { objectType = typeof(GameObject), allowSceneObjects = true,
            value = NodeViewHelper.FindObjectByID(entry.SnappableID, levelData) };
        objField.style.flexGrow = 1;
        objField.RegisterValueChangedCallback(evt =>
        {
            var go = evt.newValue as GameObject;
            if (go == null)
            {
                Undo.RecordObject(levelData, "Clear Snappable");
                entry.SnappableID = "";
                EditorUtility.SetDirty(levelData);
                return;
            }
            var atid = NodeViewHelper.EnsureActionTargetID(go);
            NodeViewHelper.ApplyComponents(go, MechanicType.Draggable);
            Undo.RecordObject(levelData, "Set Snappable");
            entry.SnappableID = atid.ID;
            EditorUtility.SetDirty(levelData);
            onBakeNeeded();
        });
        topRow.Add(objField);

        var delBtn = new Button(() =>
        {
            Undo.RecordObject(levelData, "Remove Snappable Entry");
            req.AcceptedSnappables.RemoveAt(index);
            EditorUtility.SetDirty(levelData);
            onBakeNeeded();
            RebuildEntries(section, req, levelData, onBakeNeeded);
        }) { text = "×" };
        delBtn.style.width = 20;
        NodeViewHelper.StyleDeleteBtn(delBtn);
        topRow.Add(delBtn);
        card.Add(topRow);

        // Row 2: SnapDistance + ReturnOnInvalidDrop
        var configRow = new VisualElement();
        configRow.style.flexDirection = FlexDirection.Row;
        configRow.style.alignItems    = Align.Center;
        configRow.style.paddingTop    = 2;

        var snapLbl = MakeLabel("Snap Dist:", 58f);
        configRow.Add(snapLbl);

        var snapField = new FloatField { value = entry.SnapDistance };
        snapField.style.width = 40;
        snapField.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(levelData, "Change Snap Distance");
            entry.SnapDistance = Mathf.Max(0.01f, evt.newValue);
            snapField.SetValueWithoutNotify(entry.SnapDistance);
            EditorUtility.SetDirty(levelData);
            onBakeNeeded();
        });
        configRow.Add(snapField);

        var spacer = new VisualElement(); spacer.style.flexGrow = 1;
        configRow.Add(spacer);

        var retLbl = MakeLabel("Return:", 42f);
        retLbl.tooltip = "Trả item về vị trí gốc khi drop không hợp lệ";
        configRow.Add(retLbl);

        var retToggle = new Toggle { value = entry.ReturnOnInvalidDrop };
        retToggle.RegisterValueChangedCallback(evt =>
        {
            Undo.RecordObject(levelData, "Toggle Return On Invalid Drop");
            entry.ReturnOnInvalidDrop = evt.newValue;
            EditorUtility.SetDirty(levelData);
            onBakeNeeded();
        });
        configRow.Add(retToggle);

        card.Add(configRow);
        return card;
    }

    // ── Status targets ────────────────────────────────────────────────────────

    public IEnumerable<(string id, string label, MechanicType mechanic)> GetExtraStatusTargets(RequirementData req)
    {
        if (req.AcceptedSnappables == null) yield break;
        foreach (var entry in req.AcceptedSnappables)
            if (!string.IsNullOrEmpty(entry.SnappableID))
                yield return (entry.SnappableID, entry.SnappableID, MechanicType.Draggable);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingBottom = 2;
        return row;
    }

    private static Label MakeLabel(string text, float width = 62f)
    {
        var lbl = new Label(text);
        lbl.style.width    = width;
        lbl.style.fontSize = 10;
        lbl.style.color    = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
        return lbl;
    }
}
