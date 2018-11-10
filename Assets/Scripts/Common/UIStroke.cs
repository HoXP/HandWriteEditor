using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/17zuoye/Stroke")]
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

    public enum StrokeType
    {//笔画类型
        Point,
        Line,
        Curve
    }
    private StrokeType _strokeType = StrokeType.Curve;

    private bool _isClose = false;  //是否闭合
    private float _width = 20f;     //笔画宽度
    private int _smooth = 5;        //关键点之间要生成的段数
    private float _uvTiling = 1f;

    private const int SemiCircleSegment = 10;   //半圆段数

    private List<Vector2> _nodeList = null;     //结点列表
    private List<Vector2> _curvePoints = null;  //曲线每个矩形边中点;Count永远是顶点数的一半
    private List<Vector2> _curvePointsPartial = null;
    private List<Quaternion> _tangentQuaternions = null;    //切线列表
    private List<Vector3> _vertices = null; //顶点数组
    private List<Vector2> _uv = null;       //uv数组
    private List<int> _triangles = null;    //三角形数组

    private int _curveVertexCount = 0;  //曲线定点数
    private int _curCurveIdx = 0;
    private float _percent = 0;
    private Vector3[] _bakLineVertices = new Vector3[4];    //备份直线顶点数据
    private Vector3[] _bakESemiCircleVertices = new Vector3[SemiCircleSegment];    //备份尾端半圆数据

    public void InitData(Vector2[] nodeList, float width, int smooth, bool isClose = false)
    {
        if (nodeList == null || nodeList.Length < 1)
        {
            Debug.LogError("No node in nodeList.");
            return;
        }
        _nodeList = new List<Vector2>(nodeList);
        if (_nodeList.Count == 1)
        {
            _strokeType = StrokeType.Point;
        }
        else if (_nodeList.Count == 2)
        {
            _strokeType = StrokeType.Line;
        }
        else
        {
            _strokeType = StrokeType.Curve;
        }
        _width = width;
        if (_strokeType == StrokeType.Line)
        {
            _smooth = 1;
        }
        else
        {
            _smooth = smooth;
        }
        //_uvTiling = uvTiling;
        _isClose = isClose;
        CalculateCurve();
        BuildCurveMesh();
        if (_strokeType == StrokeType.Line)
        {
            _bakLineVertices = _vertices.GetRange(0, 4).ToArray();
        }
        SetAllDirty();
    }

    public void UpdateLinePercent(float percent)
    {//直线：更新当前直线比例；percent∈[0,1]
        percent = Mathf.Clamp01(percent);
        _percent = percent;
        if (_percent == 0)
        {
            CrossFadeAlpha(0, 0, false);
        }
        else
        {
            CrossFadeAlpha(1, 0, false);
        }
        Vector3 vES = _curvePoints[1] - _curvePoints[0];
        Vector3 vESP = vES * _percent;
        _vertices[2] = _vertices[0] + vESP;
        _vertices[3] = _vertices[1] + vESP;
        //平移尾端半圆
        for (int i = 4 + SemiCircleSegment, j = 0; i < _vertices.Count && j < _bakESemiCircleVertices.Length; i++, j++)
        {
            _vertices[i] = _bakESemiCircleVertices[j] - vES + vESP;
        }
        SetAllDirty();
    }
    public void UpdateCurCurveIdx(int curCurveIdx)
    {//曲线：更新_curCurveIdx，以确定当前渲染曲线mesh的部分
        _curCurveIdx = curCurveIdx;
        if (_curCurveIdx < 0)
        {
            CrossFadeAlpha(0, 0, false);
            return;
        }
        else
        {
            CrossFadeAlpha(1, 0, false);
        }
        if (_curCurveIdx >= _curvePoints.Count)
        {
            _curCurveIdx = _curvePoints.Count - 1;
        }
        BuildCurveMesh(true);
        SetAllDirty();
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
        if (_curCurveIdx < 0 || _curCurveIdx >= _curvePoints.Count)
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

    private int GetCurCurveCount()
    {//获取当前要画的曲线的点个数
        return _curCurveIdx + 1;
    }

    /// <summary>
    /// 获取笔画数据
    /// </summary>
    public string GetStrokeData()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[");
        for (int i = 0; i < _curvePoints.Count; i++)
        {
            sb.Append("{");
            sb.AppendFormat("\"x\":{0},\"y\":{1}", _curvePoints[i].x, _curvePoints[i].y);
            sb.Append("}");
            if (i < _curvePoints.Count - 1)
            {
                sb.Append(",");
            }
        }
        sb.Append("]");
        return sb.ToString();
    }

    #region 生成mesh数据
    private void BuildCurveMesh(bool isPartial = false)
    {//isPartial:是否是部分的
        ResetVUT();
        if (_strokeType == StrokeType.Point)
        {
            _curCurveIdx = 0;
            CalCircleMeshData(SemiCircleSegment * 2);
        }
        else
        {
            if (!isPartial)
            {
                _curCurveIdx = _curvePoints.Count - 1;
            }
            GetPartialCurvePoints();
            _vertices = GetVertices(_curvePointsPartial, _width * 0.5f);
            if (_vertices == null)
            {
                _curveVertexCount = 0;
                return;
            }
            _curveVertexCount = _vertices.Count;
            _uv = GetVerticesUV(_curvePointsPartial);
            for (int i = 2; i < _vertices.Count; i += 2)
            {
                _triangles.Add(i - 2);
                _triangles.Add(i);
                _triangles.Add(i - 1);
                _triangles.Add(i - 1);
                _triangles.Add(i);
                _triangles.Add(i + 1);
            }
            //
            CalTangent();
            if (!_isClose)
            {
                AddSectorMeshData();
            }
        }
    }

    private void GetPartialCurvePoints()
    {//获取部分curvePoints
        int count = GetCurCurveCount();
        if (count >= 0 && count <= _curvePoints.Count)
        {
            _curvePointsPartial = _curvePoints.GetRange(0, count);
        }
        else
        {
            _curvePointsPartial = null;
        }
    }
    private void CalculateCurve()
    {//根据nodeList计算Catmul-Rom曲线，得到curvePoints
        if (_strokeType == StrokeType.Point)
        {
            _curvePoints = _nodeList;
        }
        else
        {
            int pointCount = _nodeList.Count;
            int segmentCount = _isClose ? pointCount : pointCount - 1;    //闭环的段数为关键点数，非闭环段数为关键点数-1
            List<Vector2> allVertices = new List<Vector2>((_smooth + 1) * segmentCount); //总顶点数
            Vector2[] tempVertices = new Vector2[_smooth + 1];
            float smoothReciprocal = 1f / _smooth;
            for (int i = 0; i < segmentCount; ++i)
            {
                Vector2 p0, p1, p2, p3; // 获得4个相邻的点以计算p1和p2之间的位置
                p1 = _nodeList[i];
                if (_isClose)
                {
                    p0 = i == 0 ? _nodeList[segmentCount - 1] : _nodeList[i - 1];
                    p2 = i + 1 < pointCount ? _nodeList[i + 1] : _nodeList[i + 1 - pointCount];
                    p3 = i + 2 < pointCount ? _nodeList[i + 2] : _nodeList[i + 2 - pointCount];
                }
                else
                {
                    p0 = i == 0 ? p1 : _nodeList[i - 1];
                    p2 = _nodeList[i + 1];
                    p3 = i == segmentCount - 1 ? p2 : _nodeList[i + 2];
                }
                Vector2 pA = p1;
                Vector2 pB = 0.5f * (-p0 + p2);
                Vector2 pC = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
                Vector2 pD = 0.5f * (-p0 + 3f * p1 - 3f * p2 + p3);
                float t = 0;
                for (int j = 0; j <= _smooth; j++)
                {
                    tempVertices[j] = pA + t * (pB + t * (pC + t * pD));
                    t += smoothReciprocal;
                }
                for (int j = allVertices.Count == 0 ? 0 : 1; j < tempVertices.Length; j++)
                {
                    allVertices.Add(tempVertices[j]);
                }
            }
            _curvePoints = allVertices;
        }
    }
    private List<Vector3> GetVertices(List<Vector2> points, float expands)
    {
        if (points == null || points.Count <= 0)
        {
            return null;
        }
        List<Vector3> combinePoints = new List<Vector3>();
        if (points.Count == 1)
        {//如果只有一个点，则需要根据切线方向求其两边的顶点
            Vector2 point = points[0];
            Quaternion q = _tangentQuaternions[_curCurveIdx];
            Vector2 tanV2 = q * Vector2.up;
            tanV2.Set(tanV2.y, -tanV2.x);
            combinePoints.Add(point + tanV2 * expands);
            combinePoints.Add(point - tanV2 * expands);
            return combinePoints;
        }
        List<CurveSegment2D> segments = new List<CurveSegment2D>();
        for (int i = 1; i < points.Count; i++)
        {
            segments.Add(new CurveSegment2D(points[i - 1], points[i]));
        }
        List<CurveSegment2D> segments1 = new List<CurveSegment2D>();
        List<CurveSegment2D> segments2 = new List<CurveSegment2D>();
        for (int i = 0; i < segments.Count; i++)
        {
            Vector2 vOffset = new Vector2(-segments[i].SegmentVector.y, segments[i].SegmentVector.x).normalized;
            segments1.Add(new CurveSegment2D(segments[i].point1 + vOffset * expands, segments[i].point2 + vOffset * expands));
            segments2.Add(new CurveSegment2D(segments[i].point1 - vOffset * expands, segments[i].point2 - vOffset * expands));
        }
        List<Vector2> points1 = new List<Vector2>();
        List<Vector2> points2 = new List<Vector2>();
        if (segments1.Count != segments2.Count)
        {
            Debug.LogError("segments1.Count != segments2.Count");
            return null;
        }
        for (int i = 0; i < segments1.Count; i++)
        {
            if (i == 0)
            {
                points1.Add(segments1[0].point1);
                points2.Add(segments2[0].point1);
            }
            else
            {
                Vector2 crossPoint;
                if (!CalcLinesIntersection(segments1[i - 1], segments1[i], out crossPoint, 0.1f))
                {
                    crossPoint = segments1[i].point1;
                }
                points1.Add(crossPoint);
                if (!CalcLinesIntersection(segments2[i - 1], segments2[i], out crossPoint, 0.1f))
                {
                    crossPoint = segments2[i].point1;
                }
                points2.Add(crossPoint);
            }
            if (i == segments1.Count - 1)
            {
                points1.Add(segments1[i].point2);
                points2.Add(segments2[i].point2);
            }
        }
        if (points.Count != points1.Count || points.Count != points2.Count)
        {
            Debug.LogWarning(string.Format("{0}-{1}-{2}", points.Count, points1.Count, points2.Count));
        }
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
    private bool CalcLinesIntersection(CurveSegment2D segment1, CurveSegment2D segment2, out Vector2 intersection, float angleLimit)
    {//计算两线交点
        intersection = Vector2.zero;
        Vector2 p1 = segment1.point1;
        Vector2 p2 = segment1.point2;
        Vector2 p3 = segment2.point1;
        Vector2 p4 = segment2.point2;
        float denominator = (p2.y - p1.y) * (p4.x - p3.x) - (p1.x - p2.x) * (p3.y - p4.y);
        if (denominator == 0)
        {//如果分母为0，则表示平行
            return false;
        }
        float angle = Vector2.Angle(segment1.SegmentVector, segment2.SegmentVector);    // 检查段之间的角度
        if (angle < angleLimit || (180f - angle) < angleLimit)
        {//如果两个段之间的角度太小，我们将它们视为平行
            return false;
        }
        float x = ((p2.x - p1.x) * (p4.x - p3.x) * (p3.y - p1.y) + (p2.y - p1.y) * (p4.x - p3.x) * p1.x - (p4.y - p3.y) * (p2.x - p1.x) * p3.x) / denominator;
        float y = -((p2.y - p1.y) * (p4.y - p3.y) * (p3.x - p1.x) + (p2.x - p1.x) * (p4.y - p3.y) * p1.y - (p4.x - p3.x) * (p2.y - p1.y) * p3.y) / denominator;
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
        for (int i = 0; i < _vertices.Count; i += 2)
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
    /// <returns></returns>
    private void AddSectorMeshData()
    {
        if (_vertices == null || _vertices.Count <= 0)
        {//没有顶点就不生成半圆;
            return;
        }
        if (_curvePoints == null || _tangentQuaternions == null || _curvePoints.Count <= _curCurveIdx)
        {
            return;
        }
        //生成圆——nodeList只有一个点；生成半圆——nodeList有多个点
        float radius = _width / 2.0f;
        float angleDegree = 180;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        float angleRad = Mathf.Deg2Rad * angleDegree;
        float angledelta = angleRad / SemiCircleSegment;
        int verticesCount = SemiCircleSegment;
        int triangleCount = SemiCircleSegment;
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
        //triangles
        for (int i = 0; i < triangleCount; i++)
        {
            if (i == 0)
            {//首
                _triangles.Add(_curveVertexCount);
                _triangles.Add(1);
                _triangles.Add(_curveVertexCount + 1);
            }
            else if (i == triangleCount - 1)
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
        int curveVertexCountWithSemiCircle = _vertices.Count; //加了半圆的顶点数
        pos = _curvePoints[_curCurveIdx];
        rot = _tangentQuaternions[_curCurveIdx];
        if (_curCurveIdx == 0)
        {
            rot = rot * Quaternion.Euler(0, 0, 180);
        }
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
            //bak
            if (_strokeType == StrokeType.Line)
            {
                _bakESemiCircleVertices[i].Set(tmpV.x, tmpV.y, tmpV.z);
            }
        }
        for (int i = 0; i < triangleCount; i++)
        {
            if (i == 0)
            {//首
                _triangles.Add(curveVertexCountWithSemiCircle);
                _triangles.Add(_curveVertexCount - 2);
                _triangles.Add(curveVertexCountWithSemiCircle + 1);
            }
            else if (i == triangleCount - 1)
            {//尾
                _triangles.Add(curveVertexCountWithSemiCircle);
                _triangles.Add(_curveVertexCount - 1);
                _triangles.Add(curveVertexCountWithSemiCircle + triangleCount - 1);
            }
            else
            {
                _triangles.Add(curveVertexCountWithSemiCircle);
                _triangles.Add(curveVertexCountWithSemiCircle + i);
                _triangles.Add(curveVertexCountWithSemiCircle + i + 1);
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
            if (i == triangleCount - 1)
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
    #endregion

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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_curvePoints != null && _curvePoints.Count > 1)
        {
            Gizmos.DrawLine(transform.TransformVector(_curvePoints[0]), transform.TransformVector(_curvePoints[1]));
        }
        if (_vertices != null && _vertices.Count > 0)
        {
            for (int i = 0; i < _vertices.Count; i++)
            {
                if (i < 4)
                {
                    if (i % 2 == 0)
                    {
                        Gizmos.color = Color.cyan;
                    }
                    else
                    {
                        Gizmos.color = Color.green;
                    }
                    Gizmos.DrawWireSphere(transform.TransformVector(_vertices[i]), .04f);
                }
                else
                {
                    if (i % 2 == 0)
                    {
                        Gizmos.color = Color.red;
                    }
                    else
                    {
                        Gizmos.color = Color.blue;
                    }
                    Gizmos.DrawWireSphere(transform.TransformVector(_vertices[i]), .03f);
                }
            }
        }
    }
#endif
}