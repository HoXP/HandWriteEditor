using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CurveMeshBuilderUI))]
public class CurveMeshBuilderUIEditor : Editor
{
    private CurveMeshBuilderUI _script;

    void Awake()
    {
        _script = target as CurveMeshBuilderUI;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.BeginVertical();

        if (_script._nodeList.Count <= 0)
        {
            GUILayout.Label("控制点至少需要1个！", "CN EntryWarn");
        }
        else if (_script._nodeList.Count == 1)
        {
            GUILayout.Label("1个控制点表示 <b><color=green>点</color></b>", "CN EntryInfo");
        }
        else if(_script._nodeList.Count == 2)
        {
            GUILayout.Label("2个控制点表示 <b><color=green>直线</color></b>", "CN EntryInfo");
        }
        else
        {
            GUILayout.Label("≥3个控制点表示 <b><color=green>曲线</color></b>", "CN EntryInfo");
        }
        for (int i = 0; i < _script._nodeList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button((i + 1).ToString(), GUILayout.Width(20)))
            {
                _script.selectedNodeIndex = i;
            }
            Vector2 newNodePos = EditorGUILayout.Vector2Field("", _script._nodeList[i]);
            if (_script._nodeList[i] != newNodePos)
            {
                _script._nodeList[i] = newNodePos;
            }
            if (GUILayout.Button("<", GUILayout.Width(20)))
            {
                Vector2 pos = i == 0 ? _script._nodeList[i] - Vector2.right : (_script._nodeList[i - 1] + _script._nodeList[i]) * 0.5f;
                _script.InsertNode(i, pos);
                _script.selectedNodeIndex = i;
            }
            if (GUILayout.Button("×", GUILayout.Width(20))) //✖
            {
                _script.RemoveNode(i);
                _script.selectedNodeIndex = i < _script._nodeList.Count ? i : i - 1;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add"))
        {
            Vector2 pos = _script._nodeList.Count == 0 ? Vector2.zero : _script._nodeList[_script._nodeList.Count - 1] + Vector2.right;
            _script.AddNode(pos);
            _script.selectedNodeIndex = _script._nodeList.Count - 1;
        }
        if (GUILayout.Button("Clear"))
        {
            _script.ClearNodes();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        if (GUILayout.Button("生成网格"))
        {
            _script.BuildMesh();
        }
        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }
}