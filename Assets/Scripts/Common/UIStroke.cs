using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Stroke", 13)]
public class UIStroke : MaskableGraphic
{//UI笔画类，使用插值mesh来生成笔画形状
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
    public TextAsset _jsonFile = null;   //存有笔画数据信息的json文件
    [HideInInspector]
    public string _character = string.Empty;    //要生成的字符
    [HideInInspector]
    public int _index = -1; //要生成的字符笔画索引

    private bool _isClose = false;  //是否闭合
    private float _width = 20f;    //笔画宽度
    private int _smooth = 5;        //关键点之间要生成的段数
    private float _uvTiling = 1f;   //

    private List<Vector2> _nodeList = new List<Vector2>();    //结点列表
    private List<Vector2> _curvePoints = null;               //曲线每个矩形边中点
    private List<Quaternion> _tangentQuaternions = null;    //切线列表
    private List<Vector3> _vertices = null; //顶点数组
    private List<Vector2> _uv = null;       //uv数组
    private List<int> _triangles = null;    //三角形数组

    private int _curveVertexCount = 0;  //曲线定点数

    internal void InitData(List<Vector2> nodeList, float width, int smooth, float uvTiling, bool isClose = false)
    {
        if (nodeList == null || nodeList.Count < 1)
        {
            Debug.LogError("No node in nodeList.");
            return;
        }
        _nodeList = nodeList;
        _width = width;
        _smooth = smooth;
        _uvTiling = uvTiling;
        _isClose = isClose;
        if (_nodeList.Count == 1)
        {
            BuildPointMesh();
        }
        else
        {
            if (_nodeList.Count == 2)
            {
                BuildLineMesh();
            }
            else
            {
                BuildCurveMesh();
            }
            CalTangent();
            if (!_isClose)
            {
                AddSectorMeshData(10, _curvePoints.Count - 1);
            }
        }
        SetAllDirty();
    }
    public void InitDataByJson()
    {//通过json文件初始化数据
        print(string.Format("### 生成"));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_vertices == null || _vertices.Count < 1)
        {
            return;
        }
        if (_uv == null || _uv.Count < 1)
        {
            return;
        }
        if (_vertices.Count != _uv.Count)
        {
            Debug.LogError("顶点数和UV数不一致");
            return;
        }
        if (_triangles == null || _triangles.Count < 1)
        {
            return;
        }
        for (int i = 0; i < _vertices.Count; i++)
        {
            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = _vertices[i];
            vert.uv0 = _uv[i];
            vh.AddVert(vert);
        }
        for (int i = 0; i < _triangles.Count; i += 3)
        {
            vh.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
        }
    }

    private void BuildPointMesh()
    {
        _curvePoints = _nodeList;
        CalCircleMeshData(20);
    }
    private void BuildLineMesh()
    {
        _smooth = 1;
        BuildCurveMesh();
    }
    private void BuildCurveMesh()
    {
        _curvePoints = CalculateCurve(_nodeList, _smooth, _isClose);
        _vertices = GetVertices(_curvePoints, _width * 0.5f);
        _curveVertexCount = _vertices.Count;
        _uv = GetVerticesUV(_curvePoints);
        _triangles = new List<int>();
        for (int i = 2; i < _vertices.Count; i += 2)
        {
            _triangles.Add(i - 2);
            _triangles.Add(i);
            _triangles.Add(i - 1);
            _triangles.Add(i - 1);
            _triangles.Add(i);
            _triangles.Add(i + 1);
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

    private List<Vector3> GetVertices(List<Vector2> points, float expands)
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

        List<Vector3> combinePoints = new List<Vector3>();  //points.Count * 2
        for (int i = 0; i < points.Count; i++)
        {
            combinePoints.Add(points1[i]);
            combinePoints.Add(points2[i]);
        }
        //闭环处理
        if (_isClose)
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
        totalLengthReciprocal = _uvTiling / totalLength;
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
                    uvs.Add(new Vector2(_uvTiling, 1));
                    uvs.Add(new Vector2(_uvTiling, 0));
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
    private void CalTangent()
    {//计算切线角度
        if (_vertices == null || _vertices.Count < 1)
        {
            return;
        }
        _tangentQuaternions = new List<Quaternion>();
        for (int i = 0; i < _vertices.Count; i+=2)
        {
            if (i == 0)
            {
                Vector3 v3Vertical = Vector3.Cross(_vertices[1] - _vertices[0], Vector3.forward);
                _tangentQuaternions.Add(Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, v3Vertical.normalized)));
            }
            else
            {
                Vector3 v3Vertical = Vector3.Cross(_vertices[i] - _vertices[i + 1], Vector3.forward);
                _tangentQuaternions.Add(Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, v3Vertical.normalized)));
            }
        }
    }

    #region 半圆
    /// <summary>
    /// 创建圆形mesh
    /// </summary>
    /// <param name="segments">段数</param>
    /// <param name="idx">_curvePoints索引；默认0，表示curvePoints中第0个点处生成</param>
    /// <returns></returns>
    internal void AddSectorMeshData(int segments, int idx = 0)
    {
        if(_curvePoints == null || _tangentQuaternions == null || _curvePoints.Count <= idx)
        {
            return;
        }
        //生成圆——nodeList只有一个点；生成半圆——nodeList有多个点
        float radius = _width / 2.0f;
        float angleDegree = 180;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        float angleRad = Mathf.Deg2Rad * angleDegree;
        float angledelta = angleRad / segments;
        int verticesCount = segments;
        int triangleCount = segments;
        Vector3 tmpV = Vector3.zero;
        Vector2 tmpUV = Vector2.zero;
        float angleCur = 0;
        //第一个半圆
        pos = _curvePoints[0];
        rot = _tangentQuaternions[0];
        //int curveVertexCount = _vertices.Count; //曲线顶点数
        angleCur = angleRad;
        for (int i = 0; i < verticesCount; i++)
        {
            if(i == 0)
            {
                tmpV.Set(pos.x, pos.y, 0);
            }
            else
            {
                tmpV.Set(radius * Mathf.Cos(angleCur), radius * Mathf.Sin(angleCur), 0);
                tmpV = rot * tmpV + pos;
            }
            _vertices.Add(tmpV);
            tmpUV.Set(tmpV.x / radius / 2 + 0.5f, tmpV.z / radius / 2 + 0.5f);
            _uv.Add(tmpUV);
            angleCur -= angledelta;
        }
        //triangles
        for (int i = 0; i < triangleCount; i++)
        {
            if(i == 0)
            {//首
                _triangles.Add(_curveVertexCount);
                _triangles.Add(1);
                _triangles.Add(_curveVertexCount + 1);
            }
            else if(i == triangleCount - 1)
            {//尾
                _triangles.Add(_curveVertexCount);
                _triangles.Add(_curveVertexCount + triangleCount - 1);
                _triangles.Add(0);
            }
            else
            {
                _triangles.Add(_curveVertexCount);
                _triangles.Add(_curveVertexCount + i);
                _triangles.Add(_curveVertexCount + i + 1);
            }
        }
        //-------
        int curveSemiCircleCount = _vertices.Count; //加了半圆的顶点数
        pos = _curvePoints[idx];
        rot = _tangentQuaternions[idx];
        angleCur = angleRad;
        for (int i = 0; i < verticesCount; i++)
        {
            if (i == 0)
            {
                tmpV.Set(pos.x, pos.y, 0);
            }
            else
            {
                tmpV.Set(radius * Mathf.Cos(angleCur), radius * Mathf.Sin(angleCur), 0);
                tmpV = rot * tmpV + pos;
            }
            _vertices.Add(tmpV);
            tmpUV.Set(tmpV.x / radius / 2 + 0.5f, tmpV.z / radius / 2 + 0.5f);
            _uv.Add(tmpUV);
            angleCur -= angledelta;
        }
        for (int i = 0; i < triangleCount; i++)
        {
            if (i == 0)
            {//首
                _triangles.Add(curveSemiCircleCount);
                _triangles.Add(_curveVertexCount - 2);
                _triangles.Add(curveSemiCircleCount + 1);
            }
            else if (i == triangleCount - 1)
            {//尾
                _triangles.Add(curveSemiCircleCount);
                _triangles.Add(_curveVertexCount - 1);
                _triangles.Add(curveSemiCircleCount + triangleCount - 1);
            }
            else
            {
                _triangles.Add(curveSemiCircleCount);
                _triangles.Add(curveSemiCircleCount + i);
                _triangles.Add(curveSemiCircleCount + i + 1);
            }
        }
    }
    /// <summary>
    /// 创建圆形mesh
    /// </summary>
    /// <param name="segments">段数</param>
    /// <returns></returns>
    private void CalCircleMeshData(int segments)
    {
        if (_nodeList == null || _nodeList.Count <= 0)
        {
            return;
        }
        ResetVUT();
        float radius = _width / 2.0f;
        Vector2 pos = _nodeList[0];
        //vertices/uv
        int verticesCount = segments + 1;
        float angleRad = Mathf.Deg2Rad * 360;
        float angleCur = angleRad;
        float angledelta = angleRad / segments;
        Vector3 tmpV = Vector3.zero;
        Vector2 tmpUV = Vector2.zero;
        for (int i = 0; i < verticesCount; i++)
        {
            if (i == 0)
            {
                tmpV.Set(pos.x, pos.y, 0);
            }
            else
            {
                tmpV.Set(radius * Mathf.Cos(angleCur) + pos.x, radius * Mathf.Sin(angleCur) + pos.y, 0);
            }
            _vertices.Add(tmpV);
            tmpUV.Set(tmpV.x / radius / 2 + 0.5f, tmpV.z / radius / 2 + 0.5f);
            _uv.Add(tmpUV);
            angleCur -= angledelta;
        }
        //triangles
        int triangleCount = segments;
        for (int i = 0; i < triangleCount; i++)
        {
            _triangles.Add(0);
            if(i == triangleCount - 1)
            {//尾闭合
                _triangles.Add(triangleCount);
                _triangles.Add(1);
            }
            else
            {
                _triangles.Add(i + 1);
                _triangles.Add(i + 2);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if(_vertices!=null && _vertices.Count > 0)
        {
            for (int i = 0; i < _vertices.Count; i++)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawWireSphere(transform.TransformVector(_vertices[i]), .01f);
            }
        }
    }
#endif

    private void ResetVUT()
    {
        if (_vertices == null)
        {
            _vertices = new List<Vector3>();
        }
        else
        {
            _vertices.Clear();
        }
        if (_uv == null)
        {
            _uv = new List<Vector2>();
        }
        else
        {
            _uv.Clear();
        }
        if (_triangles == null)
        {
            _triangles = new List<int>();
        }
        else
        {
            _triangles.Clear();
        }
    }
    #endregion
}