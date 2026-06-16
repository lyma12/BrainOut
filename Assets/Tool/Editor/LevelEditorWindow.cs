using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class LevelEditorWindow : EditorWindow
{
    private LevelData _currentLevel;
    private LevelGraphView _graphView;
    private ListView _levelList;
    private List<LevelData> _levels = new List<LevelData>();

    [MenuItem("Tools/Level Editor %`")]
    public static void Open()
    {
        var window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(800, 500);
    }

    private void CreateGUI()
    {
        var split = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
        rootVisualElement.Add(split);

        var leftPane = new VisualElement();
        leftPane.style.minWidth = 150;
        split.Add(leftPane);

        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        toolbar.style.paddingLeft = 4;
        toolbar.style.paddingRight = 4;
        toolbar.style.paddingTop = 2;
        toolbar.style.paddingBottom = 2;

        var refreshBtn = new Button(RefreshLevelList) { text = "⟳" };
        refreshBtn.tooltip = "Refresh level list";
        toolbar.Add(refreshBtn);

        var newBtn = new Button(CreateNewLevel) { text = "+ New Level" };
        newBtn.style.flexGrow = 1;
        toolbar.Add(newBtn);

        leftPane.Add(toolbar);

        _levelList = new ListView(_levels, 24, MakeListItem, BindListItem);
        _levelList.selectionType = SelectionType.Single;
        _levelList.style.flexGrow = 1;
        _levelList.selectionChanged += OnLevelSelected;
        leftPane.Add(_levelList);

        var rightPane = new VisualElement();
        rightPane.style.flexGrow = 1;
        split.Add(rightPane);

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

    private VisualElement MakeListItem()
    {
        var label = new Label();
        label.style.paddingLeft = 8;
        label.style.paddingTop = 4;
        label.style.paddingBottom = 4;
        return label;
    }

    private void BindListItem(VisualElement el, int index)
    {
        if (index < 0 || index >= _levels.Count) return;
        (el as Label).text = _levels[index] != null ? _levels[index].LevelName : "(unnamed)";
    }

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
    }

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
        if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            AssetDatabase.CreateFolder("Assets", "Levels");

        var level = CreateInstance<LevelData>();
        level.LevelName = "New Level";

        var startStage = new StageData
        {
            StageID = System.Guid.NewGuid().ToString(),
            DisplayName = "Stage 0",
            NodePosition = new UnityEngine.Vector2(200, 200)
        };
        level.Stages.Add(startStage);

        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Levels/NewLevel.asset");
        AssetDatabase.CreateAsset(level, path);
        AssetDatabase.SaveAssets();

        RefreshLevelList();
        LoadLevel(level);
    }

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
