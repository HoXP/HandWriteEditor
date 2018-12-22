using System.Collections.Generic;
using UnityEngine;

public class MeshUtils
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
    /// <summary>
    /// 根据一段折线生成一组顶点
    /// </summary>
    /// <param name="curvePoints">折线数组</param>
    /// <param name="expands">顶点相对于折线点的偏移</param>
    /// <returns></returns>
    public static Vector3[] GetVertices(Vector2[] curvePoints, float expands)
    {
        if (curvePoints == null || curvePoints.Length <= 1)
        {
            return null;
        }
        List<Vector3> combinePoints = new List<Vector3>();
        List<CurveSegment2D> segments = new List<CurveSegment2D>();
        for (int i = 1; i < curvePoints.Length; i++)
        {
            segments.Add(new CurveSegment2D(curvePoints[i - 1], curvePoints[i]));
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
        for (int i = 0; i < curvePoints.Length; i++)
        {
            combinePoints.Add(points1[i]);
            combinePoints.Add(points2[i]);
        }
        return combinePoints.ToArray();
    }
    private static bool CalcLinesIntersection(CurveSegment2D segment1, CurveSegment2D segment2, out Vector2 intersection, float angleLimit)
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
}