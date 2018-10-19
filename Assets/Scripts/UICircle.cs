using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class UICircle : MonoBehaviour
{
    private MeshFilter _mf = null;

    private void Init()
    {
        _mf = transform.GetComponent<MeshFilter>();
    }

    internal void Flush(bool isStart, bool isSemi,float width, Vector3 pos, Quaternion rot)
    {
        Init();
        gameObject.name = isStart ? "s" : "e";
        if (isSemi)
        {
            Mesh circle = DrawCircle.CreateMesh(width / 2.0f, 0, 180, 10);
            _mf.mesh = circle;
        }
        else
        {
            Mesh circle = DrawCircle.CreateMesh(width / 2.0f, 0, 360, 20);
            _mf.mesh = circle;
        }
        FlushTransform(pos, rot);
    }
    internal void FlushTransform(Vector3 pos, Quaternion rot)
    {
        _mf.transform.localPosition = pos;
        _mf.transform.rotation = rot;
    }
}