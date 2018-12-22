using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/17zuoye/UIStroke")]
public class UIStroke : MaskableGraphic
{//UI笔画类，使用插值mesh来生成笔画形状
    public enum StrokeType
    {//笔画类型
        Point,
        Line,
        Curve
    }
    private StrokeType _strokeType = StrokeType.Curve;

    private bool _isClose = false;  //是否闭合
    private float _width = 20f;     //笔画宽度
    private int _smooth = 1;        //关键点之间要生成的段数

    private const int SemiCircleSegment = 10;   //半圆段数

    private Vector2[] _nodeList = null;     //结点列表
    private Vector2[] _curvePoints = null;  //曲线每个矩形边中点;Count永远是顶点数的一半
    private Quaternion[] _tangentQuaternions = null;    //切线列表
    private Vector3[] _vertices = null; //顶点数组

    private float _percent = 1;

    private Vector3[] _verticesCurve = null;    //备份曲线顶点数据
    private Vector3[] _verticesSCS = null;  //备份首端半圆数据
    private Vector3[] _verticesSCE = null;  //备份尾端半圆数据
    private Vector3[] _verticesSCEPatial = null;  //运动中的尾端半圆数据
    private Vector3[] _verticesCloseRingLast2 = null;   //备份闭环最后两个顶点位置
    private UIVertex[] _polys = null;   //四边形数组

    public void InitData(Vector2[] nodeList, float width, int smooth, bool isClose = false)
    {
        _polys = new UIVertex[4];
        if (nodeList == null || nodeList.Length < 1)
        {
            Debug.LogError("No node in nodeList.");
            return;
        }
        _nodeList = nodeList;
        if (_nodeList.Length == 1)
        {
            _strokeType = StrokeType.Point;
        }
        else if (_nodeList.Length == 2)
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
        _isClose = isClose;
        CalculateCurve();
        CalTangent();
        InitVT();
        if (_isClose && _verticesCurve != null && _verticesCurve.Length > 1)
        {
            _verticesCloseRingLast2 = new Vector3[]
                {
                    _verticesCurve[_verticesCurve.Length - 2],
                    _verticesCurve[_verticesCurve.Length - 1]
                };
            HandelClosedRing();
        }
        SetAllDirty();
    }
    private void InitVT()
    {
        ResetVUT();
        if(_strokeType == StrokeType.Point)
        {
            CalCircleMeshData(SemiCircleSegment * 2);
        }
        else
        {
            _verticesCurve = MeshUtils.GetVertices(_curvePoints, _width * 0.5f);
            if (!_isClose)
            {
                AddSectorMeshData();
            }
            UpdatePercent(1);
        }
    }
    
    public void UpdatePercent(float percent)
    {//直线：更新当前直线比例；percent∈[0,1]
        _percent = Mathf.Clamp01(percent);
        if (_percent == 0)
        {
            CrossFadeAlpha(0, 0, false);
            return;
        }
        else
        {
            CrossFadeAlpha(1, 0, false);
        }
        if (_strokeType == StrokeType.Point)
        {
        }
        else if (_strokeType == StrokeType.Line)
        {
            _vertices = new Vector3[4];
            Array.Copy(_verticesCurve, _vertices, _verticesCurve.Length);
            Vector3 vES = _curvePoints[1] - _curvePoints[0];
            Vector3 vESP = vES * _percent;
            _vertices[2] = _verticesCurve[0] + vESP;
            _vertices[3] = _verticesCurve[1] + vESP;
            //尾端半圆位置
            _verticesSCEPatial = new Vector3[_verticesSCE.Length];
            Vector3 vES1P = -vES * (1 - _percent);
            for (int i = 0; i < _verticesSCEPatial.Length; i++)
            {
                _verticesSCEPatial[i] = _verticesSCE[i] + vES1P;
            }
        }
        else
        {
            int len = GetCurveIdxByPercent(_percent) + 1;
            _vertices = new Vector3[len * 2];
            Array.Copy(_verticesCurve, _vertices, _vertices.Length);
            //尾端半圆位置
            AddSectorMeshDataVer(false);
            _verticesSCEPatial = new Vector3[_verticesSCE.Length];
            Array.Copy(_verticesSCE, _verticesSCEPatial, _verticesSCEPatial.Length);
        }
        SetAllDirty();
    }
    private int GetCurveIdxByPercent(float percent)
    {
        if(_curvePoints == null)
        {
            return 0;
        }
        return (int)(_percent * (_curvePoints.Length - 1));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_strokeType == StrokeType.Point)
        {
            List<UIVertex> scVertices = new List<UIVertex>();
            int segments = SemiCircleSegment * 2;
            for (int i = 0; i < segments; i++)
            {
                int idx = i * 3;
                scVertices.Add(GetUIVertexByPos(_vertices[0]));
                if (i == segments - 1)
                {//尾闭合
                    scVertices.Add(GetUIVertexByPos(_vertices[1]));
                    scVertices.Add(GetUIVertexByPos(_vertices[segments]));
                }
                else
                {
                    scVertices.Add(GetUIVertexByPos(_vertices[i + 2]));
                    scVertices.Add(GetUIVertexByPos(_vertices[i + 1]));
                }
            }
            vh.AddUIVertexTriangleStream(scVertices);
        }
        else
        {
            if (!_isClose && _verticesSCS != null && _verticesSCS.Length > 0 && _verticesSCE != null && _verticesSCE.Length > 0)
            {//半圆
                List<UIVertex> scVertices = new List<UIVertex>();
                scVertices.AddRange(GetSemiCircleVertices(true));
                scVertices.AddRange(GetSemiCircleVertices(false));
                vh.AddUIVertexTriangleStream(scVertices);
            }
            if (_vertices != null && _vertices.Length > 0)
            {//曲线
                for (int i = 0; i + 2 < _vertices.Length; i += 2)
                {//i永为偶数
                    for (int j = 0; j < _polys.Length; j++)
                    {//j∈[0,3]
                        UIVertex vert = UIVertex.simpleVert;
                        vert.color = color;
                        vert.position = _vertices[i + j];
                        vert.uv0 = Vector2.zero;
                        _polys[j] = vert;
                    }
                    UIVertex tmp = _polys[0];
                    _polys[0] = _polys[1];
                    _polys[1] = tmp;
                    vh.AddUIVertexQuad(_polys);
                }
            }
        }
    }
    private List<UIVertex> GetSemiCircleVertices(bool isStart)
    {
        if (_vertices == null || _vertices.Length <= 0)
        {
            return null;
        }
        int sIdx = 0, eIdx = 0;
        Vector3[] v3Arr = null;
        if(isStart)
        {
            sIdx = 0;
            eIdx = 1;
            v3Arr = _verticesSCS;
        }
        else
        {
            sIdx = _vertices.Length - 1;
            eIdx = _vertices.Length - 2;
            v3Arr = _verticesSCEPatial;
        }
        if(v3Arr == null || v3Arr.Length <= 0)
        {
            return null;
        }
        List<UIVertex> scVertices = new List<UIVertex>();
        int triangleCount = SemiCircleSegment;
        for (int i = 0; i < triangleCount; i++)
        {
            scVertices.Add(GetUIVertexByPos(v3Arr[0]));
            if (i == 0)
            {//首
                scVertices.Add(GetUIVertexByPos(v3Arr[triangleCount - 1]));
                scVertices.Add(GetVerticesCurvePoint(sIdx));
            }
            else if (i == triangleCount - 1)
            {//尾
                scVertices.Add(GetVerticesCurvePoint(eIdx));
                scVertices.Add(GetUIVertexByPos(v3Arr[1]));
            }
            else
            {
                scVertices.Add(GetUIVertexByPos(v3Arr[i]));
                scVertices.Add(GetUIVertexByPos(v3Arr[i + 1]));
            }
        }
        return scVertices;
    }
    private UIVertex GetVerticesCurvePoint(int idx)
    {
        if (_vertices == null || _vertices.Length <= idx)
        {
            return UIVertex.simpleVert;
        }
        return GetUIVertexByPos(_vertices[idx]);
    }
    private UIVertex GetUIVertexByPos(Vector3 pos)
    {
        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;
        vert.uv0 = Vector2.zero;
        vert.position = pos;
        return vert;
    }

    public string GetStrokeData()
    {// 获取笔画数据
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[");
        for (int i = 0; i < _curvePoints.Length; i++)
        {
            sb.Append("{");
            sb.AppendFormat("\"x\":{0},\"y\":{1}", _curvePoints[i].x, _curvePoints[i].y);
            sb.Append("}");
            if (i < _curvePoints.Length - 1)
            {
                sb.Append(",");
            }
        }
        sb.Append("]");
        return sb.ToString();
    }
    internal string GetStrokeVertices()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[");
        for (int i = 0; i < _verticesCurve.Length; i++)
        {
            sb.Append("{");
            sb.AppendFormat("\"x\":{0},\"y\":{1},\"z\":{2}", _verticesCurve[i].x, _verticesCurve[i].y, _verticesCurve[i].z);
            sb.Append("}");
            if (i < _verticesCurve.Length - 1)
            {
                sb.Append(",");
            }
        }
        sb.Append("]");
        return sb.ToString();
    }

    #region 生成mesh数据
    private void CalculateCurve()
    {//根据nodeList计算Catmul-Rom曲线，得到curvePoints
        if (_strokeType == StrokeType.Point || _strokeType == StrokeType.Line)
        {
            _curvePoints = _nodeList;
        }
        else
        {
            int pointCount = _nodeList.Length;
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
            _curvePoints = allVertices.ToArray();
        }
    }
    private void HandelClosedRing()
    {//处理闭环
        if (_isClose && _verticesCurve.Length > 3)
        {
            Vector2 v20 = (_verticesCurve[0] + _verticesCloseRingLast2[0]) / 2;
            Vector2 v21 = (_verticesCurve[1] + _verticesCloseRingLast2[1]) / 2;
            _verticesCurve[0] = v20;
            _verticesCurve[1] = v21;
            if (_percent == 1)
            {
                _verticesCurve[_verticesCurve.Length - 2] = v20;
                _verticesCurve[_verticesCurve.Length - 1] = v21;
            }
        }
    }
    private void CalTangent()
    {//计算切线角度;使用curvePoint来计算
        if(_curvePoints == null)
        {
            return;
        }
        if(_curvePoints.Length < 2)
        {
            return;
        }
        Vector2 vVertical = Vector2.zero;
        _tangentQuaternions = new Quaternion[_curvePoints.Length];
        for (int i = 0; i < _curvePoints.Length; i++)
        {
            if (i == 0)
            {
                _tangentQuaternions[i] = Quaternion.FromToRotation(Vector3.up, _curvePoints[0] - _curvePoints[1]);
            }
            else
            {
                Vector2 v = _curvePoints[i] - _curvePoints[i - 1];
                _tangentQuaternions[i] = Quaternion.FromToRotation(Vector3.up, v);
                if (_tangentQuaternions[i].eulerAngles.x != 0 || _tangentQuaternions[i].eulerAngles.y != 0)
                {//用于修正Quaternion.FromToRotation()参数为平行向量时会使得尾端半圆翻面的问题
                    _tangentQuaternions[i] = Quaternion.Euler(0, 0, _tangentQuaternions[i].eulerAngles.z);
                }
            }
        }
    }

    #region 圆
    private void AddSectorMeshData()
    {//创建扇形mesh
        if (_verticesCurve == null || _verticesCurve.Length <= 0)
        {//没有顶点就不生成半圆;
            return;
        }
        if (_curvePoints == null || _tangentQuaternions == null)
        {
            return;
        }
        AddSectorMeshDataVer(true);
        AddSectorMeshDataVer(false);
    }
    private void AddSectorMeshDataVer(bool isStart)
    {
        Vector3 tmpV = Vector3.zero;
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        float radius = _width / 2.0f;
        float angleRad = Mathf.Deg2Rad * 180;
        float angleDelta = angleRad / SemiCircleSegment;
        float angleCur = angleRad;
        Vector3[] lst = null;
        if (isStart)
        {
            lst = _verticesSCS;
            pos = _curvePoints[0];
            rot = _tangentQuaternions[0];
        }
        else
        {
            lst = _verticesSCE;
            int curCurveIdx = GetCurveIdxByPercent(_percent);
            pos = _curvePoints[curCurveIdx];
            rot = _tangentQuaternions[curCurveIdx];
        }
        for (int i = 0; i < lst.Length; i++)
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
            lst[i] = tmpV;
            angleCur -= angleDelta;
        }
    }
    private void CalCircleMeshData(int segments)
    {// 创建圆形mesh；segments：段数
        if (_nodeList == null || _nodeList.Length <= 0)
        {
            return;
        }
        ResetVUT();
        float radius = _width / 2.0f;
        Vector2 pos = _nodeList[0];
        //vertices/uv
        float angleRad = Mathf.Deg2Rad * 360;
        float angleCur = 0;
        float angledelta = angleRad / segments;
        Vector3 tmpV = Vector3.zero;
        Vector2 tmpUV = Vector2.zero;
        for (int i = 0; i <= segments; ++i)
        {//顶点数为段数+1
            if (i == 0)
            {
                tmpV.Set(pos.x, pos.y, 0);
            }
            else
            {
                tmpV.Set(radius * Mathf.Cos(angleCur) + pos.x, radius * Mathf.Sin(angleCur) + pos.y, 0);
                angleCur += angledelta;
            }
            _vertices[i] = tmpV;
            tmpUV.Set(tmpV.x / radius / 2 + 0.5f, tmpV.z / radius / 2 + 0.5f);
        }
    }
    #endregion

    private void ResetVUT()
    {
        //vt
        int vLen = -1;
        if (_strokeType == StrokeType.Point)
        {
            int circleSegment = SemiCircleSegment * 2;
            vLen = circleSegment + 1;
        }
        else
        {
            int totalSemiCircleSegment = _isClose ? 0 : SemiCircleSegment * 2;
            vLen = _curvePoints.Length * 2 + totalSemiCircleSegment;
        }
        if (_vertices == null || _vertices.Length != vLen)
        {
            _vertices = new Vector3[vLen];
        }
        else
        {
            Array.Clear(_vertices, 0, _vertices.Length);
        }
        //bak vt
        int cLen = _curvePoints.Length * 2;
        if (_verticesCurve == null || _verticesCurve.Length != cLen)
        {
            _verticesCurve = new Vector3[cLen];
        }
        else
        {
            Array.Clear(_verticesCurve, 0, _verticesCurve.Length);
        }
        if(_verticesSCS == null || _verticesSCS.Length != SemiCircleSegment || _verticesSCE == null || _verticesSCE.Length != SemiCircleSegment)
        {
            _verticesSCS = new Vector3[SemiCircleSegment];
            _verticesSCE = new Vector3[SemiCircleSegment];
        }
        else
        {
            Array.Clear(_verticesSCS, 0, _verticesSCS.Length);
            Array.Clear(_verticesSCE, 0, _verticesSCE.Length);
        }
    }
    #endregion
}