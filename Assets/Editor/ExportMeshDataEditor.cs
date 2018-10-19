using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExportMeshData))]
public class ExportMeshDataEditor : Editor
{
    private ExportMeshData _script;

    void Awake()
    {
        _script = target as ExportMeshData;
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