using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UILine : Image
{
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

    private bool _hasArrow = false;   //是否有箭头
    private float _width = 2;    //路径宽度
    private float _sideLength = 12;    //箭头边长（箭头是正三角形）
    private float _uvTiling = 1f;
    private List<Vector2> _points = null;    //路径点
    private List<Vector3> _vertices = null; //顶点数组
    private List<Vector2> _uv = null;       //uv数组
    private List<int> _triangles = null;    //三角形数组

    private List<Vector3> _triVertices = null;  //箭头mesh顶点

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="points"></param>
    public void Init(List<Vector2> points, bool hasArrow = false)
    {
        if (points == null || points.Count < 4)
        {
            Debug.LogError("Invalid data.");
            return;
        }
        _points = points;
        _vertices = GetVertices(_points, _width * 0.5f);
        _uv = GetVerticesUV(_points);
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
        //三角形箭头
        _hasArrow = hasArrow;
        if (_hasArrow)
        {
            Vector3 v3Vertical = Vector3.Cross(_vertices[_vertices.Count - 2] - _vertices[_vertices.Count - 1], Vector3.forward);
            Quaternion rot = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, v3Vertical.normalized));
            Vector3 pos = _points[_points.Count - 1];
            _triVertices = new List<Vector3>();
            _triVertices.Add(rot * new Vector3(-_sideLength / 2.0f, 0) + pos);
            _triVertices.Add(rot * new Vector3(_sideLength / 2.0f, 0) + pos);
            _triVertices.Add(rot * new Vector3(0, _sideLength * 0.866025f) + pos);
        }
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
        //绘制箭头
        if(_triVertices!=null && _triVertices.Count > 0)
        {
            for (int i = 0; i < _triVertices.Count; i++)
            {
                UIVertex vert = UIVertex.simpleVert;
                vert.color = color;
                vert.position = _triVertices[i];
                vert.uv0 = _uv[i];
                vh.AddVert(vert);
            }
            vh.AddTriangle(vh.currentVertCount - 3, vh.currentVertCount - 2, vh.currentVertCount - 1);
        }
    }

    #region 计算顶点数据
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
        return combinePoints;
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
    #endregion
}