using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CurveMeshBuilder : MonoBehaviour
{// 根据给定关键点动态构建Catmull-Rom曲线网格
    public struct CurveSegment2D
    {//表示曲线每段的结构体
        public Vector2 point1, point2;  //该段起点及终点坐标

        public CurveSegment2D(Vector2 point1, Vector2 point2)
        {//构造方法
            this.point1 = point1;
            this.point2 = point2;
        }

        public Vector2 SegmentVector
        {//该段向量
            get
            {
                return point2 - point1;
            }
        }
    }

    [HideInInspector]
    public List<Vector2> nodeList = new List<Vector2>();    //结点列表
    private List<Vector2> curvePoints = null;   //曲线每个矩形边中点
    private Vector3[] _rotatedVertices = null;
    private List<Quaternion> _tangentQuaternions = null;    //切线列表

    public bool drawGizmos = true;  //是否绘制Gizmos，即表示关键点的Gizmos点及线
    public float gizmosNodeSize = 0.1f; //Gizmos球尺寸

    public bool isClose = false;    //是否闭合
    public int smooth = 5;  //段数
    public float uvTiling = 1f;
    public float width = 0.2f;  //mesh宽度

    private Mesh _mesh;

#if UNITY_EDITOR
    [NonSerialized]
    public int selectedNodeIndex = -1;  //当前所选结点索引
#endif

    //UI
    private Image imgCurvePoint = null;
    private RectTransform tranCurvePointRoot = null;
    private UICircle tplCircleMesh = null;

    private void Awake()
    {
        BuildMesh();
    }

    private void Init()
    {
        tplCircleMesh = transform.root.Find("tplCircleMesh").GetComponent<UICircle>();
        tplCircleMesh.gameObject.SetActive(false);
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "CurveMesh";
            GetComponent<MeshFilter>().mesh = _mesh;
        }
        _mesh.Clear();
        //UI
        ExportMeshData tmpExportMeshData = transform.GetComponentInParent<ExportMeshData>();
        imgCurvePoint = tmpExportMeshData.imgCurvePoint;
        imgCurvePoint.gameObject.SetActive(false);
        tranCurvePointRoot = tmpExportMeshData.tranCurvePointRoot;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {//在Scene视图中绘制样条曲线
        if (!drawGizmos)
        {
            return;
        }
        Vector3 prevPosition = Vector3.zero;
        for (int i = 0; i < nodeList.Count; i++)
        {
            if (i == 0)
            {
                prevPosition = transform.TransformPoint(nodeList[i]);
            }
            else
            {
                Vector3 curPosition = transform.TransformPoint(nodeList[i]);
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
        //
        if(_mesh!=null)
        {//
            //Gizmos.DrawWireSphere(transform.TransformPoint(_mesh.vertices[0]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
            //Gizmos.DrawSphere(transform.TransformPoint(_mesh.vertices[1]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
            //Gizmos.DrawWireSphere(transform.TransformPoint(_mesh.vertices[_mesh.vertices.Length - 1]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
            //Gizmos.DrawSphere(transform.TransformPoint(_mesh.vertices[_mesh.vertices.Length - 2]), gizmosNodeSize * UnityEditor.HandleUtility.GetHandleSize(prevPosition) * 1.5f);
        }
    }
#endif

    #region Node操作
    public void AddNode(Vector2 position)
    {
        nodeList.Add(position);
    }
    public void InsertNode(int index, Vector2 position)
    {
        index = Mathf.Max(index, 0);
        if (index >= nodeList.Count)
        {
            AddNode(position);
        }
        else
        {
            nodeList.Insert(index, position);
        }
    }
    public void RemoveNode(int index)
    {
        if (index < 0 || index >= nodeList.Count)
        {
            return;
        }
        nodeList.RemoveAt(index);
    }
    public void ClearNodes()
    {
        nodeList.Clear();
    }
    #endregion

    public bool BuildMesh()
    {
        Init();
        if(nodeList.Count <= 0)
        {
            Debug.LogError("至少得有一个结点");
            return false;
        }
        if (nodeList.Count == 1)
        {
            BuildPointMesh();
        }
        else if (nodeList.Count == 2)
        {
            BuildLineMesh();
        }
        else
        {
            BuildCurveMesh();
        }
        //data
        UpdateRotatedVertices();
        UpdateTangent();
        UpdateData();
        DrawDebugPoints();
        if(!isClose)
        {
            BuildCircleMesh();
        }
        return true;
    }
    private void BuildPointMesh()
    {
        _mesh.name = "Circle";
        curvePoints = nodeList;
    }
    private void BuildLineMesh()
    {
        smooth = 1;
        _mesh.name = "Line";
        BuildCurveMesh();
    }
    private void BuildCurveMesh()
    {
        curvePoints = CalculateCurve(nodeList, smooth, isClose);
        List<Vector2> vertices = GetVertices(curvePoints, width * 0.5f);
        List<Vector2> verticesUV = GetVerticesUV(curvePoints);

        Vector3[] _vertices = new Vector3[vertices.Count];
        Vector2[] _uv = new Vector2[verticesUV.Count];
        int[] _triangles = new int[(vertices.Count - 2) * 3];
        for (int i = 0; i < vertices.Count; i++)
        {
            _vertices[i].Set(vertices[i].x, vertices[i].y, 0);
        }
        for (int i = 0; i < verticesUV.Count; i++)
        {
            _uv[i].Set(verticesUV[i].x, verticesUV[i].y);
        }
        for (int i = 2; i < vertices.Count; i += 2)
        {
            int index = (i - 2) * 3;
            _triangles[index] = i - 2;
            _triangles[index + 1] = i - 0;
            _triangles[index + 2] = i - 1;
            _triangles[index + 3] = i - 1;
            _triangles[index + 4] = i - 0;
            _triangles[index + 5] = i + 1;
        }
        _mesh.name = "Curve";
        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        _mesh.uv = _uv;
        _mesh.RecalculateNormals(); //重计算法线
    }
    private void BuildCircleMesh()
    {
        UICircle[] items = transform.GetComponentsInChildren<UICircle>();
        if (items != null && items.Length > 0)
        {
            for (int i = items.Length - 1; i >= 0; --i)
            {
                GameObject.DestroyImmediate(items[i].gameObject);
            }
        }
        int count = Math.Min(2, nodeList.Count);
        for (int j = 0; j < count; ++j)
        {
            UICircle item = GameObject.Instantiate<UICircle>(tplCircleMesh, transform);
            item.gameObject.SetActive(true);
            bool isStart = j % 2 == 0;
            Vector3 pos = isStart ? curvePoints[0] : curvePoints[curvePoints.Count - 1];
            Quaternion rot = Quaternion.identity;
            if(_tangentQuaternions!=null&& _tangentQuaternions.Count > 1)
            {
                rot = isStart? _tangentQuaternions[0] : _tangentQuaternions[_tangentQuaternions.Count - 1];
            }
            item.Flush(isStart, count > 1,width, pos, rot);
        }
    }
    private void DrawDebugPoints()
    {
        Image[] items = tranCurvePointRoot.GetComponentsInChildren<Image>(true);
        if (items != null && items.Length > 0)
        {
            for (int i = items.Length - 1; i >= 0; --i)
            {
                GameObject.DestroyImmediate(items[i].gameObject);
            }
        }
        for (int i = 0; i < curvePoints.Count; i++)
        {
            Image item = GameObject.Instantiate<Image>(imgCurvePoint, tranCurvePointRoot);
            item.gameObject.SetActive(true);
            item.transform.localPosition = transform.localRotation * curvePoints[i] + transform.localPosition;
        }
    }

    /// <summary>
    /// 计算Catmul-Rom曲线
    /// </summary>
    /// <param name="points">关键点</param>
    /// <param name="smooth">两个相邻关键点之间的段数</param>
    /// <param name="curveClose">曲线是否为一个闭环</param>
    /// <returns></returns>
    public List<Vector2> CalculateCurve(IList<Vector2> points, int smooth, bool curveClose)
    {
        int pointCount = points.Count;
        int segmentCount = curveClose ? pointCount : pointCount - 1;    //闭环的段数为关键点数，非闭环段数为关键点数-1
        List<Vector2> allVertices = new List<Vector2>((smooth + 1) * segmentCount); //总顶点数
        Vector2[] tempVertices = new Vector2[smooth + 1];
        float smoothReciprocal = 1f / smooth;
        for (int i = 0; i < segmentCount; ++i)
        {
            Vector2 p0, p1, p2, p3; // 获得4个相邻的点以计算p1和p2之间的位置
            p1 = points[i];

            if (curveClose)
            {
                p0 = i == 0 ? points[segmentCount - 1] : points[i - 1];
                p2 = i + 1 < pointCount ? points[i + 1] : points[i + 1 - pointCount];
                p3 = i + 2 < pointCount ? points[i + 2] : points[i + 2 - pointCount];
            }
            else
            {
                p0 = i == 0 ? p1 : points[i - 1];
                p2 = points[i + 1];
                p3 = i == segmentCount - 1 ? p2 : points[i + 2];
            }

            Vector2 pA = p1;
            Vector2 pB = 0.5f * (-p0 + p2);
            Vector2 pC = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
            Vector2 pD = 0.5f * (-p0 + 3f * p1 - 3f * p2 + p3);

            float t = 0;
            for (int j = 0; j <= smooth; j++)
            {
                tempVertices[j] = pA + t * (pB + t * (pC + t * pD));
                t += smoothReciprocal;
            }
            for (int j = allVertices.Count == 0 ? 0 : 1; j < tempVertices.Length; j++)
            {
                allVertices.Add(tempVertices[j]);
            }
        }
        return allVertices;
    }

    private List<CurveSegment2D> GetSegments(List<Vector2> points)
    {
        List<CurveSegment2D> segments = new List<CurveSegment2D>();
        for (int i = 1; i < points.Count; i++)
        {
            segments.Add(new CurveSegment2D(points[i - 1], points[i]));
        }
        return segments;
    }

    private List<Vector2> GetVertices(List<Vector2> points, float expands)
    {
        List<CurveSegment2D> segments = GetSegments(points);
        List<CurveSegment2D> segments1 = new List<CurveSegment2D>();
        List<CurveSegment2D> segments2 = new List<CurveSegment2D>();
        for (int i = 0; i < segments.Count; i++)
        {
            Vector2 vOffset = new Vector2(-segments[i].SegmentVector.y, segments[i].SegmentVector.x).normalized;
            segments1.Add(new CurveSegment2D(segments[i].point1 + vOffset * expands, segments[i].point2 + vOffset * expands));
            segments2.Add(new CurveSegment2D(segments[i].point1 - vOffset * expands, segments[i].point2 - vOffset * expands));
        }
        List<Vector2> points1 = new List<Vector2>(points.Count);
        List<Vector2> points2 = new List<Vector2>(points.Count);
        for (int i = 0; i < segments1.Count; i++)
        {
            if (i == 0)
            {
                points1.Add(segments1[0].point1);
            }
            else
            {
                Vector2 crossPoint;
                if (!TryCalculateLinesIntersection(segments1[i - 1], segments1[i], out crossPoint, 0.1f))
                {
                    crossPoint = segments1[i].point1;
                }
                points1.Add(crossPoint);
            }
            if (i == segments1.Count - 1)
            {
                points1.Add(segments1[i].point2);
            }
        }
        for (int i = 0; i < segments2.Count; i++)
        {
            if (i == 0)
            {
                points2.Add(segments2[0].point1);
            }
            else
            {
                Vector2 crossPoint;
                if (!TryCalculateLinesIntersection(segments2[i - 1], segments2[i], out crossPoint, 0.1f))
                {
                    crossPoint = segments2[i].point1;
                }
                points2.Add(crossPoint);
            }
            if (i == segments2.Count - 1)
            {
                points2.Add(segments2[i].point2);
            }
        }

        List<Vector2> combinePoints = new List<Vector2>(points.Count * 2);
        for (int i = 0; i < points.Count; i++)
        {
            combinePoints.Add(points1[i]);
            combinePoints.Add(points2[i]);
        }
        //闭环处理
        if (isClose)
        {
            if (combinePoints.Count > 3)
            {
                Vector2 v21 = (combinePoints[1] + combinePoints[combinePoints.Count - 1]) / 2;
                combinePoints[1] = v21;
                combinePoints[combinePoints.Count - 1] = v21;
                Vector2 v22 = (combinePoints[0] + combinePoints[combinePoints.Count - 2]) / 2;
                combinePoints[0] = v22;
                combinePoints[combinePoints.Count - 2] = v22;
            }
        }
        return combinePoints;
    }

    private List<Vector2> GetVerticesUV(List<Vector2> points)
    {
        List<Vector2> uvs = new List<Vector2>();
        float totalLength = 0;
        float totalLengthReciprocal = 0;
        float curLength = 0;
        for (int i = 1; i < points.Count; i++)
        {
            totalLength += Vector2.Distance(points[i - 1], points[i]);
        }
        totalLengthReciprocal = uvTiling / totalLength;
        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0)
            {
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(0, 0));
            }
            else
            {
                if (i == points.Count - 1)
                {
                    uvs.Add(new Vector2(uvTiling, 1));
                    uvs.Add(new Vector2(uvTiling, 0));
                }
                else
                {
                    curLength += Vector2.Distance(points[i - 1], points[i]);
                    float uvx = curLength * totalLengthReciprocal;

                    uvs.Add(new Vector2(uvx, 1));
                    uvs.Add(new Vector2(uvx, 0));
                }
            }
        }
        return uvs;
    }

    private bool TryCalculateLinesIntersection(CurveSegment2D segment1, CurveSegment2D segment2, out Vector2 intersection, float angleLimit)
    {
        intersection = new Vector2();

        Vector2 p1 = segment1.point1;
        Vector2 p2 = segment1.point2;
        Vector2 p3 = segment2.point1;
        Vector2 p4 = segment2.point2;

        float denominator = (p2.y - p1.y) * (p4.x - p3.x) - (p1.x - p2.x) * (p3.y - p4.y);
        if (denominator == 0)
        {// 如果分母为0，则表示平行
            return false;
        }

        float angle = Vector2.Angle(segment1.SegmentVector, segment2.SegmentVector);    // 检查段之间的角度
        if (angle < angleLimit || (180f - angle) < angleLimit)
        {// 如果两个段之间的角度太小，我们将它们视为平行
            return false;
        }
        float x = ((p2.x - p1.x) * (p4.x - p3.x) * (p3.y - p1.y)
                + (p2.y - p1.y) * (p4.x - p3.x) * p1.x
                - (p4.y - p3.y) * (p2.x - p1.x) * p3.x) / denominator;
        float y = -((p2.y - p1.y) * (p4.y - p3.y) * (p3.x - p1.x)
                + (p2.x - p1.x) * (p4.y - p3.y) * p1.y
                - (p4.x - p3.x) * (p2.y - p1.y) * p3.y) / denominator;
        intersection.Set(x, y);
        return true;
    }

    private void UpdateRotatedVertices()
    {
        if (_mesh == null)
        {
            Debug.LogError(string.Format("_mesh is null"));
            return;
        }
        _rotatedVertices = new Vector3[_mesh.vertices.Length];
        for (int i = 0; i < _mesh.vertices.Length; i++)
        {
            _rotatedVertices[i] = transform.localRotation * _mesh.vertices[i] + transform.localPosition;
        }
    }
    private void UpdateTangent()
    {//更新
        _tangentQuaternions = new List<Quaternion>();
        for (int i = 0; i < _rotatedVertices.Length; i++)
        {
            if (i % 2 == 0)
            {
                if (i == 0)
                {
                    Vector3 v3Vertical = Vector3.Cross(_rotatedVertices[1] - _rotatedVertices[0], Vector3.forward);
                    _tangentQuaternions.Add(Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, v3Vertical.normalized)));
                }
                else
                {
                    Vector3 v3Vertical = Vector3.Cross(_rotatedVertices[i] - _rotatedVertices[i + 1], Vector3.forward);
                    _tangentQuaternions.Add(Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, v3Vertical.normalized)));
                }
            }
        }
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
        //stroke.goLclPos.x = transform.localPosition.x;
        //stroke.goLclPos.y = transform.localPosition.y;
        //stroke.goLclRot = transform.localRotation;
        //曲线关键点
        List<Vector2> tmpNodePoints = new List<Vector2>();
        for (int i = 0; i < nodeList.Count; i++)
        {
            Vector2 tmpV2 = transform.localRotation * nodeList[i] + transform.localPosition;
            tmpNodePoints.Add(tmpV2);
        }
        stroke.nodeList = tmpNodePoints;
        //曲线结点
        //List<Vector2> tmpCurvePoints = new List<Vector2>();
        //for (int i = 0; i < curvePoints.Count; i++)
        //{
        //    Vector2 tmpV2 = transform.localRotation * curvePoints[i] + transform.localPosition;
        //    tmpCurvePoints.Add(tmpV2);
        //}
        //stroke.curvePoints = tmpCurvePoints;
        stroke.tangentQuaternions = _tangentQuaternions;
        //stroke.vertices = _rotatedVertices;
        //stroke.triangles = _mesh.triangles;
        //stroke.uv = _mesh.uv;

        DrawLetterManager.Instance.UpdateLetterDict(charName, stroke);
    }
}