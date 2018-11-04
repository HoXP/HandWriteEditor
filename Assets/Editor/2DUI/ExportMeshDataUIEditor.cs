using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExportMeshDataUI))]
public class ExportMeshDataUIEditor : Editor
{
    private ExportMeshDataUI _script;

    void Awake()
    {
        _script = target as ExportMeshDataUI;
    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("导出网格数据"))
        {
            _script.ExportData();
        }
    }
}