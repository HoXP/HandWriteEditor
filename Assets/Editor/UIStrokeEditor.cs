using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(UIStroke))]
public class UIStrokeEditor : GraphicEditor
{
    enum DataGenMode
    {//数据生成方式
        Editor, //实时编辑生成
        Json,   //使用json文件生成
    }

    //private UIStroke _script;
    public SerializedObject so;
    private DataGenMode enumPopupVal;
    public SerializedProperty spChar;
    public SerializedProperty spIdx;
    //public SerializedProperty spJsonFile;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        //_script = target as UIStroke;
        so = new SerializedObject(target);
        spChar = so.FindProperty("_character");
        spIdx = so.FindProperty("_index");
        //spJsonFile = so.FindProperty("_jsonFile");
    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        enumPopupVal = (DataGenMode)EditorGUILayout.EnumPopup("数据生成方式", enumPopupVal);
        switch (enumPopupVal)
        {
            case DataGenMode.Json:
                EditorGUILayout.PropertyField(spChar);
                EditorGUILayout.PropertyField(spIdx);
                EditorGUILayout.BeginHorizontal();
                //_script._jsonFile = EditorGUILayout.ObjectField("JsonFile", _script._jsonFile, typeof(TextAsset), false) as TextAsset;
                //if (_script._jsonFile != null)
                //{
                //    if (GUILayout.Button("Gen"))
                //    {
                //        _script.InitDataByJson();
                //    }
                //}
                EditorGUILayout.EndHorizontal();
                break;
            case DataGenMode.Editor:
                break;
            default:
                break;
        }
    }
}