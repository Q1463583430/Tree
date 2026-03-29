using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EmployeeRepository))]
public class EmployeeRepositoryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EmployeeRepository repo = (EmployeeRepository)target;
        int count = repo != null ? repo.Count : 0;
        int capacity = repo != null ? repo.Capacity : 0;
        int baseCapacity = repo != null ? Mathf.Max(0, repo.baseCapacity) : 0;
        int bonusCapacity = Mathf.Max(0, capacity - baseCapacity);
        int remaining = repo != null ? repo.RemainingCapacity : 0;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("容量监控", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField("当前员工数", count);
            EditorGUILayout.IntField("基础容量", baseCapacity);
            EditorGUILayout.IntField("容量加成", bonusCapacity);
            EditorGUILayout.IntField("当前总容量", capacity);
            EditorGUILayout.IntField("剩余容量", remaining);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("运行中时会显示实时容量（含特殊房间加成）。", MessageType.Info);
        }

        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
