using System;
using ROOT.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Static helpers shared between RequiredNodeView and other node views.</summary>
public static class NodeViewHelper
{
    // ── ActionTargetID ────────────────────────────────────────────────────────

    public static ActionTargetID EnsureActionTargetID(GameObject go)
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

    // ── Requirement linker baking ─────────────────────────────────────────────

    /// <summary>
    /// Add + configure the correct RequirementLinker on the prefab.
    /// Called by RequiredNodeView whenever requirement data changes.
    /// All field values are baked in — no runtime wiring needed.
    /// </summary>
    public static void BakeRequirementLinker(GameObject go, RequirementData req)
    {
        if (go == null || string.IsNullOrEmpty(req.RequirementID)) return;

        var desc = RequirementTypeRegistry.Get(req.Type);
        if (desc?.LinkerType == null) return;

        var component = go.GetComponent(desc.LinkerType)
                        ?? Undo.AddComponent(go, desc.LinkerType);

        var so = new SerializedObject(component);
        so.FindProperty("_requirementID").stringValue = req.RequirementID;
        desc.BakeFields(go, so, req);
        so.ApplyModifiedProperties();
        SaveObject(go);
    }

    // ── Mechanic components ───────────────────────────────────────────────────

    public static void SaveObject(UnityEngine.Object obj)
    {
        EditorUtility.SetDirty(obj);
        var go = obj is GameObject g ? g : (obj is Component c ? c.gameObject : null);
        if (go != null
            && UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null
            && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(go))
        {
            AssetDatabase.SaveAssetIfDirty(go);
        }
    }

    private static readonly MechanicType[] ItemUpMechanics = { MechanicType.Click, MechanicType.Draggable };

    public static void ApplyComponents(GameObject go, MechanicType mechanic)
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
        if (System.Array.IndexOf(ItemUpMechanics, mechanic) >= 0)
            EnsureLayer(go, "ItemUp");
        if (anyAdded) SaveObject(go);
    }

    public static void EnsureLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogWarning($"[LevelEditor] Layer '{layerName}' not found.");
            return;
        }
        if (go.layer == layer) return;
        Undo.RecordObject(go, $"Set Layer {layerName}");
        go.layer = layer;
        EditorUtility.SetDirty(go);
    }

    public static string GenerateObjectID(GameObject go)
    {
        return $"{go.name}_{Guid.NewGuid().ToString("N")[..6]}";
    }

    public static GameObject FindObjectByID(string id, LevelData levelData)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var t in UnityEngine.Object.FindObjectsOfType<ActionTargetID>())
            if (t.ID == id) return t.gameObject;
        if (levelData?.LevelPrefab != null)
            foreach (var t in levelData.LevelPrefab.GetComponentsInChildren<ActionTargetID>(true))
                if (t.ID == id) return t.gameObject;
        return null;
    }

    public static void StyleDeleteBtn(Button btn)
    {
        btn.style.color = new StyleColor(new Color(1f, 0.35f, 0.35f));
        btn.style.backgroundColor = new StyleColor(Color.clear);
        btn.style.borderLeftWidth = btn.style.borderRightWidth =
        btn.style.borderTopWidth  = btn.style.borderBottomWidth = 0;
    }
}
