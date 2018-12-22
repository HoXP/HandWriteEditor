using UnityEngine;
using System.Collections.Generic;
using System;
using LitJson;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(UIStroke))]
public class CurveMeshBuilderUI : MonoBehaviour
{// 根据给定关键点动态构建Catmull-Rom曲线网格
    [SerializeField][HideInInspector]
    public List<Vector2> _nodeList = new List<Vector2>();    //结点列表
    private List<Vector2> _curvePoints = null;

    public bool drawGizmos = true;  //是否绘制Gizmos，即表示关键点的Gizmos点及线
    public float gizmosNodeSize = 0.1f; //Gizmos球尺寸

    public bool isClose = false;    //是否闭合
    public float width = 0.2f;  //mesh宽度
    public int smooth = 5;  //段数
    public float uvTiling = 1f;

    #region Apple
    public List<float> _applePercentList = null;
    private RectTransform _tplApple = null;
    #endregion

    private UIStroke _uiStroke = null;

#if UNITY_EDITOR
    [NonSerialized]
    public int selectedNodeIndex = -1;  //当前所选结点索引
#endif

    private void Awake()
    {
        _uiStroke = GetComponent<UIStroke>();
        _tplApple = transform.root.Find("tplApple").GetComponent<RectTransform>();
    }
    private void OnEnable()
    {
        _uiStroke = GetComponent<UIStroke>();
        _tplApple = transform.root.Find("tplApple").GetComponent<RectTransform>();
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
        _uiStroke.UpdatePercent(1);
        _curvePoints = JsonMapper.ToObject<List<Vector2>>(_uiStroke.GetStrokeData());
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
        //stroke.disApple = _disApple;
        //stroke.disStartApple = _disStartApple;
        stroke.applePercentList = _applePercentList;
        //曲线关键点
        stroke.nodeList = _nodeList;
        DrawLetterManager.Instance.UpdateLetterDict(charName, stroke);
    }

    #region Apple
    public void InitApple()
    {
        if (_curvePoints == null)
        {
            return;
        }
        DestroyGO();
        if(_nodeList.Count == 1)
        {

        }
        else if (_nodeList.Count == 2)
        {
            if(_applePercentList!=null && _applePercentList.Count > 0)
            {
                Vector2 sNode = _curvePoints[0];
                Vector2 eNode = _curvePoints[_curvePoints.Count - 1];
                Vector2 vSE = eNode - sNode;
                float disSE = Vector2.Distance(sNode, eNode);
                for (int i = 0; i < _applePercentList.Count; i++)
                {
                    RectTransform go = GameObject.Instantiate<RectTransform>(_tplApple, transform);
                    go.name = _applePercentList[i].ToString();
                    go.gameObject.SetActive(true);
                    go.anchoredPosition = sNode + vSE * _applePercentList[i];
                }
            }
        }
        else
        {
            if (_applePercentList != null && _applePercentList.Count > 0)
            {
                for (int i = 0; i < _applePercentList.Count; i++)
                {
                    RectTransform go = GameObject.Instantiate<RectTransform>(_tplApple, transform);
                    go.name = _applePercentList[i].ToString();
                    go.gameObject.SetActive(true);
                    int idx = GetIdxByPercent(_applePercentList[i]);
                    go.anchoredPosition = _curvePoints[idx];
                }
            }
        }
    }
    private int GetIdxByPercent(float percent)  //根据比例获取索引
    {
        if(_curvePoints == null || _curvePoints.Count <= 0)
        {
            return 0;
        }
        int len = _curvePoints.Count - 1;
        int idx = Mathf.RoundToInt(len * percent);
        idx = Mathf.Clamp(idx, 0, len);
        return idx;
    }

    internal void DestroyGO()
    {
        Image[] cld = transform.GetComponentsInChildren<Image>();
        for (int i = cld.Length - 1; i >= 0; --i)
        {
            GameObject.DestroyImmediate(cld[i].gameObject);
        }
    }
    #endregion
}