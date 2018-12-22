using LitJson;
using System;
using System.IO;
using System.Text;
using UnityEngine;

[ExecuteInEditMode]
public class ExportMeshDataUI : MonoBehaviour
{
    private void Awake()
    {
        DrawLetterManager.Instance.RegisterExporter();
    }
    private void OnEnable()
    {
        DrawLetterManager.Instance.RegisterExporter();
    }

    public void ExportData()
    {
        DrawLetterManager.Instance.LetterDict.Clear();
        BuildAllMesh();
        string jsonStr = JsonMapper.ToJson(DrawLetterManager.Instance.LetterDict);
        Debug.LogWarning(string.Format("{0}", jsonStr));
        //存储json文件
        FileStream fs = new FileStream(Application.dataPath + DrawLetterManager.ExportPath, FileMode.Create);    //FileMode.Create——创建文件，如果文件名存在则覆盖重新创建
        byte[] bytes = new UTF8Encoding().GetBytes(jsonStr);
        fs.Write(bytes, 0, bytes.Length);
        fs.Close();
    }
    private void BuildAllMesh()
    {
        CurveMeshBuilderUI[] arr = transform.GetComponentsInChildren<CurveMeshBuilderUI>(true);
        if(arr!=null&& arr.Length > 0)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i].BuildMesh();
            }
        }
    }
}