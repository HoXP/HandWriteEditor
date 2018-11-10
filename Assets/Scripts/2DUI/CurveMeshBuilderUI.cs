using UnityEngine;
using System.Collections.Generic;
using System;

[ExecuteInEditMode]
[RequireComponent(typeof(UIStroke))]
public class CurveMeshBuilderUI : MonoBehaviour
{// 根据给定关键点动态构建Catmull-Rom曲线网格
    [SerializeField][HideInInspector]
    public List<Vector2> _nodeList = new List<Vector2>();    //结点列表

    public bool drawGizmos = true;  //是否绘制Gizmos，即表示关键点的Gizmos点及线
    public float gizmosNodeSize = 0.1f; //Gizmos球尺寸

    public bool isClose = false;    //是否闭合
    public float width = 0.2f;  //mesh宽度
    public int smooth = 5;  //段数
    public float uvTiling = 1f;

    private UIStroke _uiStroke = null;

#if UNITY_EDITOR
    [NonSerialized]
    public int selectedNodeIndex = -1;  //当前所选结点索引
#endif

    private void Awake()
    {
        _uiStroke = GetComponent<UIStroke>();
    }
    private void OnEnable()
    {
        _uiStroke = GetComponent<UIStroke>();
    }
    private void Update()
    {
        BuildMesh();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {//在Scene视图中绘制样条曲线
        if (!drawGizmos)
        {
            return;
        }
        Vector3 prevPosition = Vector3.zero;
        for (int i = 0; i < _nodeList.Count; i++)
        {
            if (i == 0)
            {
                prevPosition = transform.TransformPoint(_nodeList[i]);
            }
            else
            {
                Vector3 curPosition = transform.TransformPoint(_nodeList[i]);
                Gizmos.DrawLine(prevPosition, curPosition);
                prevPosition = curPosition;
            }
            if (i == selectedNodeIndex)
            {
                Color c = Gizmos.color;
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(prevPosition, gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
                Gizmos.color = c;
            }
            else
            {
                Gizmos.DrawSphere(prevPosition, gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition));
            }
        }
        //if(_mesh!=null)
        //{
        //    Gizmos.DrawWireSphere(transform.TransformPoint(_mesh.vertices[0]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
        //    Gizmos.DrawSphere(transform.TransformPoint(_mesh.vertices[1]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
        //    Gizmos.DrawWireSphere(transform.TransformPoint(_mesh.vertices[_mesh.vertices.Length - 1]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
        //    Gizmos.DrawSphere(transform.TransformPoint(_mesh.vertices[_mesh.vertices.Length - 2]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
        //}
    }
#endif

    #region Node操作
    public void AddNode(Vector2 position)
    {
        _nodeList.Add(position);
    }
    public void InsertNode(int index, Vector2 position)
    {
        index = Mathf.Max(index, 0);
        if (index >= _nodeList.Count)
        {
            AddNode(position);
        }
        else
        {
            _nodeList.Insert(index, position);
        }
    }
    public void RemoveNode(int index)
    {
        if (index < 0 || index >= _nodeList.Count)
        {
            return;
        }
        _nodeList.RemoveAt(index);
    }
    public void ClearNodes()
    {
        _nodeList.Clear();
    }
    #endregion

    public bool BuildMesh()
    {
        if (_nodeList.Count <= 0)
        {
            Debug.LogError("至少得有一个结点");
            return false;
        }
        NormalizeNodeList();
        if(_uiStroke == null)
        {
            _uiStroke = GetComponent<UIStroke>();
        }
        _uiStroke.InitData(_nodeList.ToArray(), width, smooth, isClose);
        UpdateData();
        return true;
    }
    private void NormalizeNodeList()
    {//标准化nodeList，使当前Transform的RTS都归一，且保持nodeList的点世界坐标不变
        if (_nodeList == null || _nodeList.Count <= 0)
        {
            Debug.LogError(string.Format("nodeList is null"));
            return;
        }
        for (int i = 0; i < _nodeList.Count; i++)
        {
            _nodeList[i] = transform.localRotation * _nodeList[i] + transform.localPosition;
        }
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
    private void UpdateData()
    {
        string charName = transform.parent.gameObject.name.Trim();
        if (string.IsNullOrEmpty(charName))
        {
            Debug.LogError(string.Format("charName 为空字串"));
            return;
        }
        string idxStr = transform.gameObject.name.Trim();
        if (string.IsNullOrEmpty(idxStr))
        {
            Debug.LogError(string.Format("idxStr 为空字串"));
            return;
        }
        byte idx = 0;
        byte.TryParse(idxStr, out idx);
        if (idx == 0)
        {
            Debug.LogError(string.Format("idx == 0"));
            return;
        }
        Stroke stroke = new Stroke();
        stroke.index = idx;
        stroke.isClose = isClose;
        stroke.smooth = smooth;
        stroke.width = width;
        //曲线关键点
        stroke.nodeList = _nodeList;
        DrawLetterManager.Instance.UpdateLetterDict(charName, stroke);
    }
}