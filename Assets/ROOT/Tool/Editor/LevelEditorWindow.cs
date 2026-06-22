using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;

public class LevelEditorWindow : EditorWindow
{
    private LevelData _currentLevel;
    private LevelGraphView _graphView;
    private ListView _levelList;
    private List<LevelData> _levels = new List<LevelData>();

    // Header bar references
    private Label _headerLevelName;
    private ObjectField _prefabField;
    private VisualElement _headerBar;

    // Track which item is in rename mode
    private int _renamingIndex = -1;

    [MenuItem("Tools/Level Editor %`")]
    public static void Open()
    {
        var window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(800, 500);
    }

    private void CreateGUI()
    {
        var split = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
        rootVisualElement.Add(split);

        // ── Left pane ──────────────────────────────────────────────────────────
        var leftPane = new VisualElement();
        leftPane.style.minWidth = 160;
        leftPane.style.flexDirection = FlexDirection.Column;
        split.Add(leftPane);

        // Toolbar
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
        toolbar.style.paddingLeft = 4;
        toolbar.style.paddingRight = 4;
        toolbar.style.paddingTop = 3;
        toolbar.style.paddingBottom = 3;
        toolbar.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
        toolbar.style.borderBottomWidth = 1;

        var refreshBtn = new Button(RefreshLevelList) { text = "⟳" };
        refreshBtn.tooltip = "Refresh list";
        refreshBtn.style.width = 26;
        toolbar.Add(refreshBtn);

        var newBtn = new Button(CreateNewLevel) { text = "+ New Level" };
        newBtn.style.flexGrow = 1;
        newBtn.tooltip = "Create a new LevelData asset";
        toolbar.Add(newBtn);

        leftPane.Add(toolbar);

        // Level list
        _levelList = new ListView(_levels, 28, MakeListItem, BindListItem);
        _levelList.selectionType = SelectionType.Single;
        _levelList.style.flexGrow = 1;
        _levelList.selectionChanged += OnLevelSelected;
        leftPane.Add(_levelList);

        // ── Right pane ─────────────────────────────────────────────────────────
        var rightPane = new VisualElement();
        rightPane.style.flexGrow = 1;
        split.Add(rightPane);

        // Header bar (level name + prefab field)
        _headerBar = new VisualElement();
        _headerBar.style.flexDirection = FlexDirection.Row;
        _headerBar.style.alignItems = Align.Center;
        _headerBar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
        _headerBar.style.paddingLeft = 10;
        _headerBar.style.paddingRight = 8;
        _headerBar.style.paddingTop = 4;
        _headerBar.style.paddingBottom = 4;
        _headerBar.style.borderBottomColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
        _headerBar.style.borderBottomWidth = 1;
        _headerBar.style.display = DisplayStyle.None;

        _headerLevelName = new Label("—");
        _headerLevelName.style.unityFontStyleAndWeight = FontStyle.Bold;
        _headerLevelName.style.fontSize = 13;
        _headerLevelName.style.marginRight = 16;
        _headerLevelName.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
        _headerBar.Add(_headerLevelName);

        var prefabLabel = new Label("Prefab:");
        prefabLabel.style.marginRight = 4;
        prefabLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        _headerBar.Add(prefabLabel);

        _prefabField = new ObjectField();
        _prefabField.objectType = typeof(GameObject);
        _prefabField.allowSceneObjects = false;
        _prefabField.style.minWidth = 180;
        _prefabField.style.flexGrow = 1;
        _prefabField.RegisterValueChangedCallback(evt =>
        {
            if (_currentLevel == null) return;
            Undo.RecordObject(_currentLevel, "Set Level Prefab");
            _currentLevel.LevelPrefab = evt.newValue as GameObject;
            EditorUtility.SetDirty(_currentLevel);
        });
        _headerBar.Add(_prefabField);

        rightPane.Add(_headerBar);

        _graphView = new LevelGraphView();
        _graphView.style.flexGrow = 1;
        rightPane.Add(_graphView);

        RefreshLevelList();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDestroy()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    // ── List item factory ─────────────────────────────────────────────────────

    private VisualElement MakeListItem()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 6;
        row.style.paddingRight = 2;

        // Label (normal display)
        var label = new Label();
        label.name = "lbl";
        label.style.flexGrow = 1;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.overflow = Overflow.Hidden;
        label.style.textOverflow = TextOverflow.Ellipsis;
        label.style.paddingTop = 4;
        label.style.paddingBottom = 4;
        row.Add(label);

        // Inline rename field (hidden by default)
        var field = new TextField();
        field.name = "fld";
        field.style.flexGrow = 1;
        field.style.display = DisplayStyle.None;
        row.Add(field);

        // Delete button (always visible so user can reach it easily)
        var delBtn = new Button();
        delBtn.name = "del";
        delBtn.text = "✕";
        delBtn.tooltip = "Delete level";
        delBtn.style.width = 22;
        delBtn.style.height = 22;
        delBtn.style.fontSize = 10;
        delBtn.style.paddingLeft = 0;
        delBtn.style.paddingRight = 0;
        delBtn.style.color = new StyleColor(new Color(0.85f, 0.35f, 0.35f));
        delBtn.style.backgroundColor = new StyleColor(Color.clear);
        delBtn.style.borderLeftWidth = 0;
        delBtn.style.borderRightWidth = 0;
        delBtn.style.borderTopWidth = 0;
        delBtn.style.borderBottomWidth = 0;
        row.Add(delBtn);

        // Right-click context menu
        row.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            int idx = (int)row.userData;
            if (idx < 0 || idx >= _levels.Count) return;
            var lvl = _levels[idx];

            evt.menu.AppendAction("Rename", _ => BeginRename(idx));
            evt.menu.AppendAction("Duplicate", _ => DuplicateLevel(lvl));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete", _ => DeleteLevel(lvl));
        }));

        // Double-click label → rename
        label.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.clickCount == 2)
            {
                int idx = (int)row.userData;
                BeginRename(idx);
                evt.StopPropagation();
            }
        });

        return row;
    }

    private void BindListItem(VisualElement el, int index)
    {
        el.userData = index;

        var label = el.Q<Label>("lbl");
        var field = el.Q<TextField>("fld");
        var delBtn = el.Q<Button>("del");

        if (index < 0 || index >= _levels.Count)
        {
            label.text = "";
            return;
        }

        var lvl = _levels[index];
        label.text = lvl != null ? lvl.LevelName : "(missing)";

        // Delete button click
        delBtn.clickable = new Clickable(() => DeleteLevel(_levels[(int)el.userData]));

        // Wire rename field for this index
        field.UnregisterCallback<FocusOutEvent>(OnFieldFocusOut);
        field.UnregisterCallback<KeyDownEvent>(OnFieldKeyDown);
        field.RegisterCallback<FocusOutEvent>(OnFieldFocusOut);
        field.RegisterCallback<KeyDownEvent>(OnFieldKeyDown);

        // If this is the row being renamed, show field
        bool renaming = _renamingIndex == index;
        label.style.display = renaming ? DisplayStyle.None : DisplayStyle.Flex;
        field.style.display = renaming ? DisplayStyle.Flex : DisplayStyle.None;
        if (renaming)
        {
            field.SetValueWithoutNotify(lvl != null ? lvl.LevelName : "");
            field.schedule.Execute(() => { field.Focus(); field.SelectAll(); }).ExecuteLater(50);
        }
    }

    // ── Rename helpers ─────────────────────────────────────────────────────────

    private void BeginRename(int index)
    {
        _renamingIndex = index;
        _levelList.RefreshItem(index);
    }

    private void CommitRename(VisualElement field)
    {
        if (_renamingIndex < 0 || _renamingIndex >= _levels.Count) return;

        var tf = field as TextField;
        var newName = tf?.value?.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            var lvl = _levels[_renamingIndex];
            Undo.RecordObject(lvl, "Rename Level");
            lvl.LevelName = newName;
            EditorUtility.SetDirty(lvl);
            AssetDatabase.SaveAssets();

            if (_currentLevel == lvl)
            {
                titleContent = new GUIContent($"Level Editor — {newName}");
                _headerLevelName.text = newName;
            }
        }

        int idx = _renamingIndex;
        _renamingIndex = -1;
        _levelList.RefreshItem(idx);
    }

    private void CancelRename(int index)
    {
        _renamingIndex = -1;
        _levelList.RefreshItem(index);
    }

    private void OnFieldFocusOut(FocusOutEvent evt)
    {
        CommitRename(evt.target as VisualElement);
    }

    private void OnFieldKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            CommitRename(evt.target as VisualElement);
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            CancelRename(_renamingIndex);
            evt.StopPropagation();
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private void OnLevelSelected(IEnumerable<object> selection)
    {
        foreach (var item in selection)
        {
            if (item is LevelData data)
                LoadLevel(data);
            break;
        }
    }

    private void LoadLevel(LevelData data)
    {
        _currentLevel = data;
        _graphView.PopulateFromLevel(data);
        titleContent = new GUIContent($"Level Editor — {data.LevelName}");
        RefreshHeaderBar();
    }

    private void RefreshHeaderBar()
    {
        if (_currentLevel == null)
        {
            _headerBar.style.display = DisplayStyle.None;
            return;
        }
        _headerBar.style.display = DisplayStyle.Flex;
        _headerLevelName.text = _currentLevel.LevelName;
        _prefabField.SetValueWithoutNotify(_currentLevel.LevelPrefab);
    }

    // ── CRUD operations ────────────────────────────────────────────────────────

    private void RefreshLevelList()
    {
        _levels.Clear();
        var guids = AssetDatabase.FindAssets("t:LevelData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level != null) _levels.Add(level);
        }
        _levelList?.Rebuild();
    }

    private void CreateNewLevel()
    {
        EnsureLevelsFolder();

        var level = CreateInstance<LevelData>();
        level.LevelName = "New Level";

        var startStage = new StageData
        {
            StageID = System.Guid.NewGuid().ToString(),
            DisplayName = "Stage 0",
            NodePosition = new Vector2(200, 200)
        };
        level.Stages.Add(startStage);

        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Levels/NewLevel.asset");
        AssetDatabase.CreateAsset(level, path);
        AssetDatabase.SaveAssets();

        RefreshLevelList();
        LoadLevel(level);

        // Select & rename immediately
        int idx = _levels.IndexOf(level);
        if (idx >= 0)
        {
            _levelList.selectedIndex = idx;
            _levelList.ScrollToItem(idx);
            BeginRename(idx);
        }
    }

    private void DuplicateLevel(LevelData source)
    {
        if (source == null) return;
        EnsureLevelsFolder();

        var srcPath = AssetDatabase.GetAssetPath(source);
        var dstPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(Path.GetDirectoryName(srcPath), Path.GetFileName(srcPath)));

        AssetDatabase.CopyAsset(srcPath, dstPath);
        AssetDatabase.SaveAssets();

        var copy = AssetDatabase.LoadAssetAtPath<LevelData>(dstPath);
        if (copy != null)
        {
            copy.LevelName = source.LevelName + " (Copy)";
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
        }

        RefreshLevelList();

        if (copy != null)
        {
            int idx = _levels.IndexOf(copy);
            if (idx >= 0)
            {
                _levelList.selectedIndex = idx;
                _levelList.ScrollToItem(idx);
                LoadLevel(copy);
                BeginRename(idx);
            }
        }
    }

    private void DeleteLevel(LevelData level)
    {
        if (level == null) return;

        bool confirm = EditorUtility.DisplayDialog(
            "Delete Level",
            $"Delete \"{level.LevelName}\"?\nThis cannot be undone.",
            "Delete", "Cancel");

        if (!confirm) return;

        var path = AssetDatabase.GetAssetPath(level);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();

        if (_currentLevel == level)
        {
            _currentLevel = null;
            _graphView.PopulateFromLevel(null);
            titleContent = new GUIContent("Level Editor");
            _headerBar.style.display = DisplayStyle.None;
        }

        RefreshLevelList();
    }

    private void EnsureLevelsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            AssetDatabase.CreateFolder("Assets", "Levels");
    }

    // ── Play mode sync ─────────────────────────────────────────────────────────

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            EditorApplication.update += PollLevelManager;
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= PollLevelManager;
            _graphView?.ClearHighlight();
        }
    }

    private void PollLevelManager()
    {
        var manager = FindAnyObjectByType<LevelManager>();
        if (manager == null) return;

        EditorApplication.update -= PollLevelManager;
        manager.OnStageChanged += stageID => _graphView?.HighlightActiveStage(stageID);
    }
}
