using LitJson;
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class ExportMeshData : MonoBehaviour
{
    public Image imgCurvePoint = null;
    public RectTransform tranCurvePointRoot = null;

    private void Start()
    {
        RegisterExporter();
    }

    public void ExportData()
    {
        BuildAllMesh();
        string jsonStr = JsonMapper.ToJson(DrawLetterManager.Instance.LetterDict);
        Debug.LogWarning(string.Format("{0}", jsonStr));
        SaveJsonFile(jsonStr);
    }

    private void BuildAllMesh()
    {
        CurveMeshBuilder[] arrCurveMeshBuilder = transform.GetComponentsInChildren<CurveMeshBuilder>(true);
        for (int i = 0; i < arrCurveMeshBuilder.Length; i++)
        {
            arrCurveMeshBuilder[i].BuildMesh();
        }
    }

    private void SaveJsonFile(string json)
    {
        FileStream fs = new FileStream(Application.dataPath + DrawLetterManager.ExportPath, FileMode.Create);    //FileMode.Create——创建文件，如果文件名存在则覆盖重新创建
        byte[] bytes = new UTF8Encoding().GetBytes(json);
        fs.Write(bytes, 0, bytes.Length);
        fs.Close();
    }

    private void RegisterExporter()
    {//使LitJson支持float，Vector，Quaternion
        //JsonMapper.RegisterExporter<byte>((obj, writer) => writer.Write(Convert.ToInt32(obj)));
        JsonMapper.RegisterExporter<float>((obj, writer) => writer.Write(Convert.ToDouble(obj)));
        JsonMapper.RegisterImporter<string, float>((input) => { return float.Parse(input); });
        JsonMapper.RegisterExporter<Vector2>(delegate (Vector2 obj, JsonWriter writer)
        {
            writer.WriteArrayStart();
            writer.Write(Convert.ToDouble(obj.x));
            writer.Write(Convert.ToDouble(obj.y));
            writer.WriteArrayEnd();
        });
        //JsonMapper.RegisterExporter<Vector2>((obj, writer) => writer.Write(string.Format("x:{0},y:{1}", Convert.ToDouble(obj.x), Convert.ToDouble(obj.y))));
        //JsonMapper.RegisterExporter<Vector2>((obj, writer) => writer.Write(string.Format("x:{0},y:{1}", obj.x, obj.y)));
        JsonMapper.RegisterExporter<Vector3>(delegate (Vector3 obj, JsonWriter writer)
        {
            writer.WriteArrayStart();
            writer.Write(Convert.ToDouble(obj.x));
            writer.Write(Convert.ToDouble(obj.y));
            writer.Write(Convert.ToDouble(obj.z));
            writer.WriteArrayEnd();
        });
        JsonMapper.RegisterExporter<Quaternion>(delegate (Quaternion obj, JsonWriter writer)
        {
            writer.WriteArrayStart();
            writer.Write(Convert.ToDouble(obj.x));
            writer.Write(Convert.ToDouble(obj.y));
            writer.Write(Convert.ToDouble(obj.z));
            writer.Write(Convert.ToDouble(obj.w));
            writer.WriteArrayEnd();
        });
    }
}