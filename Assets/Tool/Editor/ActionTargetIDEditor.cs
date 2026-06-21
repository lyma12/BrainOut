using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionTargetID))]
public class ActionTargetIDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (ActionTargetID)target;

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        string newID = EditorGUILayout.TextField("ID", t.ID);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t, "Change ActionTargetID");
            t.ID = newID;
            EditorUtility.SetDirty(t);
        }
        if (GUILayout.Button("Gen", GUILayout.Width(40)))
        {
            Undo.RecordObject(t, "Generate ActionTargetID");
            t.ID = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            EditorUtility.SetDirty(t);
        }
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(t.ID))
        {
            EditorGUILayout.HelpBox("ID đang trống. Nhấn Gen để tạo tự động.", MessageType.Warning);
            return;
        }

        // Kiểm tra trùng ID trong scene
        bool hasDuplicate = false;
        foreach (var other in FindObjectsOfType<ActionTargetID>())
        {
            if (other != t && other.ID == t.ID)
            {
                hasDuplicate = true;
                break;
            }
        }

        if (hasDuplicate)
            EditorGUILayout.HelpBox($"ID \"{t.ID}\" đã tồn tại trên object khác trong scene!", MessageType.Error);
    }
}
