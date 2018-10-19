using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DrawMesh : MonoBehaviour
{
    //Data
    [SerializeField]
    private string charactor = "B";
    [SerializeField]
    private byte curIdx = 3; //当前可画的笔画索引，其他索引的笔画不响应;1基
    [SerializeField]
    private float BeginDragDistanceThreshold = 100;
    private Mesh _curMesh = null;
    private int _curCurvePointIdx = 0;    //当前曲线上点的索引

    private bool _canDrag = false;
    private Stroke _curStroke = null;
    private bool _isTween = false;
    [SerializeField]
    private float countDownInternal = 0.02f;    //Tween频率
    [SerializeField]
    private float TouchDistanceThreshold = 30;    //触控点离最近的点的阈值，超过就表示过远
    private Vector2 _pointTouch = Vector2.zero;
    //UI
    private RectTransform tranMeshRoot = null;
    private RectTransform tranDrawMeshRoot = null;
    private Button btnDraw = null;
    private Image imgTouchPad = null;
    private MeshFilter tplMesh = null;
    private MeshFilter tplDrawMesh = null;

    private UICircle tplCircleMesh = null;
    private UICircle tplDrawCircleMesh = null;

    private MeshFilter curDrawMesh = null;
    private UICircle[] curDrawMeshCircle = null;  //当前mesh的首尾半圆

    private Image imgCurPoint = null;

    private float _rate = 0;
    private Vector2 _pointProection = Vector2.zero;

    private void Awake()
    {
        tranMeshRoot = transform.Find("MeshRoot").GetComponent<RectTransform>();
        tranDrawMeshRoot = transform.Find("DrawMeshRoot").GetComponent<RectTransform>();
        imgCurPoint = transform.Find("imgCurPoint").GetComponent<Image>();

        tplMesh = transform.Find("tplMesh").GetComponent<MeshFilter>();
        tplMesh.gameObject.SetActive(false);
        tplDrawMesh = transform.Find("tplDrawMesh").GetComponent<MeshFilter>();
        tplDrawMesh.gameObject.SetActive(false);
        tplCircleMesh = transform.Find("tplCircleMesh").GetComponent<UICircle>();
        tplCircleMesh.gameObject.SetActive(false);
        tplDrawCircleMesh = transform.Find("tplDrawCircleMesh").GetComponent<UICircle>();
        tplDrawCircleMesh.gameObject.SetActive(false);
        
        btnDraw = transform.Find("btnDraw").GetComponent<Button>();
        btnDraw.onClick.AddListener(OnClickDraw);
        imgTouchPad = transform.Find("imgTouchPad").GetComponent<Image>();
        EventTrigger et = imgTouchPad.gameObject.AddComponent<EventTrigger>();
        //
        EventTrigger.Entry entry = null;
        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.BeginDrag;
        entry.callback.AddListener(OnBeginDrag);
        et.triggers.Add(entry);

        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        entry.callback.AddListener(OnDrag);
        et.triggers.Add(entry);

        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.EndDrag;
        entry.callback.AddListener(OnEndDrag);
        et.triggers.Add(entry);

        entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener(OnPointerClick);
        et.triggers.Add(entry);
    }

    private void Start()
    {
        FileStream fs = new FileStream(Application.dataPath + DrawLetterManager.ExportPath, FileMode.OpenOrCreate, FileAccess.Read);    //FileMode.Create——创建文件，如果文件名存在则覆盖重新创建
        StringBuilder sb = new StringBuilder();
        byte[] buffer = new byte[1024 * 1024 * 5];    //声明一个5M大小的字节数组
        while (true)
        {
            int r = fs.Read(buffer, 0, buffer.Length);    //返回本次实际读取到的字节数
            if (r == 0)
            {//如果返回一个0时，也就意味着什么都没有读到，读取完了
                break;
            }
            string s = Encoding.Default.GetString(buffer, 0, r);    //将字节数组中每一个元素按照指定的编码格式解码成字符串
            sb.Append(s);
        }
        fs.Close();
        //
        DrawLetterManager.Instance.UpdateLetterDictFromJson(sb.ToString());

        DrawMeshByData();
    }

    private Dictionary<int, Mesh> semiCircleDict = null;
    /// <summary>
    /// 自动绘制全部mesh
    /// </summary>
    private void DrawMeshByData()
    {
        MeshFilter[] arrFilter1 = tranMeshRoot.GetComponentsInChildren<MeshFilter>();
        for (int i = arrFilter1.Length - 1; i >= 0; --i)
        {
            GameObject.Destroy(arrFilter1[i].gameObject);
        }
        MeshFilter[] arrFilter2 = tranDrawMeshRoot.GetComponentsInChildren<MeshFilter>();
        for (int i = arrFilter2.Length - 1; i >= 0; --i)
        {
            GameObject.Destroy(arrFilter2[i].gameObject);
        }
        Dictionary<string, Letter> tmpDict = DrawLetterManager.Instance.LetterDict;
        if (tmpDict == null)
        {
            Debug.LogError(string.Format("tmpDict null"));
            return;
        }
        if(!tmpDict.ContainsKey(charactor))
        {
            Debug.LogError(string.Format("不包含字符[{0}]", charactor));
            return;
        }
        if (semiCircleDict == null)
        {
            semiCircleDict = new Dictionary<int, Mesh>();
        }
        else
        {
            semiCircleDict.Clear();
        }
        Letter tmpLetter = tmpDict[charactor];
        if(tmpLetter == null)
        {
            Debug.LogError(string.Format("tmpLetter null"));
            return;
        }
        //Dictionary<byte, Stroke> tmpStrokDict = tmpLetter.StrokeDict;
        List<Stroke> tmpStrokList = tmpLetter.StrokeList;
        //foreach (var item in tmpStrokDict)
        for (int i = 0;i < tmpStrokList.Count;i++)
        {
            //Stroke tmpStrok = item.Value;
            Stroke tmpStrok = tmpStrokList[i];
            if (tmpStrok == null)
            {
                continue;
            }
            //
            MeshFilter tmpMeshFilter1 = GameObject.Instantiate<MeshFilter>(tplMesh, tranMeshRoot);
            tmpMeshFilter1.gameObject.SetActive(true);
            Mesh tmpMesh = new Mesh();
            tmpMesh.name = string.Format("{0}{1}", charactor, tmpStrok.index);
            tmpMesh.vertices = tmpStrok.vertices;
            tmpMesh.triangles = tmpStrok.triangles;
            tmpMesh.uv = tmpStrok.uv;
            tmpMesh.RecalculateNormals();
            tmpMeshFilter1.mesh = tmpMesh;
            //
            //tmpMeshFilter.transform.localPosition = tmpMeshInfoList[i].goLclPos;
            //tmpMeshFilter.transform.localRotation = tmpMeshInfoList[i].goLclRot;
            tmpMeshFilter1.gameObject.name = tmpStrok.index.ToString();
            int count = Math.Min(2, tmpStrok.curvePoints.Count);
            for (int j = 0; j < count; ++j)
            {
                UICircle tmpCircle = GameObject.Instantiate<UICircle>(tplCircleMesh, tmpMeshFilter1.transform);
                tmpCircle.gameObject.SetActive(true);
                bool isStart = j % 2 == 0;
                int idx = isStart ? 0 : tmpStrok.curvePoints.Count - 1;
                //Vector3 pos = tmpStrok.goLclRot * tmpStrok.curvePoints[idx] + (Vector3)tmpStrok.goLclPos;
                Vector3 pos = tmpStrok.curvePoints[idx];
                Quaternion rot = Quaternion.identity;
                if (tmpStrok.tangentQuaternions != null && tmpStrok.tangentQuaternions.Count > 1)
                {
                    rot = tmpStrok.tangentQuaternions[idx];
                }
                tmpCircle.Flush(isStart, count > 1, tmpStrok.width, pos, rot);
            }

            //
            MeshFilter tmpMeshFilter2 = GameObject.Instantiate<MeshFilter>(tplDrawMesh, tranDrawMeshRoot);
            tmpMeshFilter2.gameObject.SetActive(true);
            Mesh tmpMesh2 = new Mesh();
            tmpMesh2.name = string.Format("Draw{0}{1}", charactor, tmpStrok.index);
            tmpMesh2.vertices = null;
            tmpMesh2.triangles = null;
            tmpMesh2.uv = null;
            tmpMesh2.RecalculateNormals();
            tmpMeshFilter2.mesh = tmpMesh2;
            //
            //tmpMeshFilter2.transform.localPosition = tmpMeshInfoList[i].goLclPos;
            //tmpMeshFilter2.transform.localRotation = tmpMeshInfoList[i].goLclRot;
            tmpMeshFilter2.gameObject.name = tmpStrok.index.ToString();
            //
            for (int j = 0; j < count; ++j)
            {
                UICircle tmpCircle = GameObject.Instantiate<UICircle>(tplDrawCircleMesh, tmpMeshFilter2.transform);
                tmpCircle.gameObject.SetActive(false);
                bool isStart = j % 2 == 0;
                int idx = isStart ? 0 : tmpStrok.curvePoints.Count - 1;
                //Vector3 pos = tmpStrok.goLclRot * tmpStrok.curvePoints[idx] + (Vector3)tmpStrok.goLclPos;
                Vector3 pos = tmpStrok.curvePoints[idx];
                Quaternion rot = Quaternion.identity;
                if (tmpStrok.tangentQuaternions != null && tmpStrok.tangentQuaternions.Count > 1)
                {
                    rot = tmpStrok.tangentQuaternions[idx];
                }
                tmpCircle.Flush(isStart, count > 1, tmpStrok.width, pos, rot);
            }
        }
    }

    private void OnClickDraw()
    {
        DrawMeshByData();
    }
    
    private void OnBeginDrag(BaseEventData arg0)
    {
        SetTween(false);
        _canDrag = true;
        PointerEventData ped = arg0 as PointerEventData;
        if (ped == null)
        {
            Debug.LogError(string.Format("ped == null"));
            _canDrag = false;
            return;
        }
        if (ped.pointerId < -1 || ped.pointerId > 1)
        {//不允许鼠标中键和右键响应，不允许移动端多点触控
            Debug.LogError(string.Format("ped.pointerId not valid.ped.pointerId={0}", ped.pointerId));
            _canDrag = false;
            return;
        }
        _curStroke = DrawLetterManager.Instance.GetStroke(charactor, curIdx);
        if(_curStroke == null)
        {
            Debug.LogError(string.Format("_curMeshInfo == null"));
            _canDrag = false;
            return;
        }
        if (_curStroke.curvePoints.Count <= 1)
        {
            return;
        }
        _curCurvePointIdx = 0;
        Vector2 localPoint = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), ped.position, ped.pressEventCamera, out localPoint);
        float distance = Vector2.Distance(localPoint, _curStroke.curvePoints[_curCurvePointIdx]);
        if (distance > BeginDragDistanceThreshold)
        {
            Debug.LogWarning(string.Format("T={0};CurvePoint={1};distance={2}", localPoint, _curStroke.curvePoints[_curCurvePointIdx], distance));
            _canDrag = false;
            return;
        }
        Debug.LogWarning(string.Format("curvePoints={0};vertices={1};triangles={2};uv={3};", _curStroke.curvePoints.Count, _curStroke.vertices.Length, _curStroke.triangles.Length, _curStroke.uv.Length));

        MeshFilter[] arrMeshFilter = tranDrawMeshRoot.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < arrMeshFilter.Length; ++i)
        {
            if(arrMeshFilter[i].gameObject.name.Trim() == curIdx.ToString())
            {
                curDrawMesh = arrMeshFilter[i];
                break;
            }
        }
        _curMesh = new Mesh();
        _curMesh.name = string.Format("Draw{0}{1}", charactor, curIdx);
        curDrawMesh.mesh = _curMesh;
        //
        if(curDrawMeshCircle == null)
        {
            curDrawMeshCircle = new UICircle[2];
        }
        curDrawMeshCircle[0] = curDrawMesh.transform.Find("s").GetComponent<UICircle>();
        curDrawMeshCircle[0].gameObject.SetActive(true);
        curDrawMeshCircle[1] = curDrawMesh.transform.Find("e").GetComponent<UICircle>();
        curDrawMeshCircle[1].gameObject.SetActive(true);
    }
    private void OnDrag(BaseEventData arg0)
    {
        if (!_canDrag)
        {
            Debug.LogError(string.Format("OnDrag _canDrag false"));
            return;
        }
        PointerEventData ped = arg0 as PointerEventData;
        if(ped == null)
        {
            Debug.LogError(string.Format("OnDrag ped == null"));
            return;
        }
        if(ped.pointerId < -1 || ped.pointerId > 1)
        {//不允许鼠标中键和右键响应，不允许移动端多点触控
            Debug.LogError(string.Format("OnDrag ped.pointerId not valid.ped.pointerId={0}", ped.pointerId));
            return;
        }
        if (_curStroke == null)
        {
            Debug.LogError(string.Format("OnDrag _curMeshInfo == null"));
            return;
        }
        if (_curStroke.curvePoints.Count <= 1)
        {
            return;
        }
        if(_curStroke.curvePoints.Count == 2)
        {//直线
            _pointTouch = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), ped.position, ped.pressEventCamera, out _pointTouch);
            //求投影点
            //Vector2 pointRotS = _curStroke.goLclRot * _curStroke.curvePoints[0] + (Vector3)_curStroke.goLclPos;
            Vector2 pointRotS = _curStroke.curvePoints[0];
            //Vector2 pointRotE = _curStroke.goLclRot * _curStroke.curvePoints[_curStroke.curvePoints.Count - 1] + (Vector3)_curStroke.goLclPos;
            Vector2 pointRotE = _curStroke.curvePoints[_curStroke.curvePoints.Count - 1];
            Vector3 vProection = Vector3.Project(_pointTouch - pointRotS, pointRotE - pointRotS);
            _pointProection = pointRotS + (Vector2)vProection;

            _curMesh.Clear();
            //
            Vector3[] subVertices = new Vector3[_curStroke.vertices.Length];
            Array.ConstrainedCopy(_curStroke.vertices, 0, subVertices, 0, 2);
            subVertices[2] = (Vector2)_curStroke.vertices[0] + (Vector2)vProection;
            subVertices[3] = (Vector2)_curStroke.vertices[1] + (Vector2)vProection;
            _curMesh.vertices = subVertices;
            _curMesh.uv = _curStroke.uv;
            _curMesh.triangles = _curStroke.triangles;
            curDrawMesh.mesh = _curMesh;

            curDrawMeshCircle[1].FlushTransform(_pointProection, _curStroke.tangentQuaternions[1]);

            if (Vector2.Distance(_pointProection, _pointTouch) > TouchDistanceThreshold)
            {
                _rate = Vector2.Distance(_pointProection, pointRotS) / Vector2.Distance(pointRotE, pointRotS);
                _canDrag = false;
                SetTween(true);
            }
        }
        else
        {
            _curCurvePointIdx = GetNearestPointIdx(ped.position, ped.pressEventCamera);
            UpdateMesh();
            //curDrawMeshCircle[1].FlushTransform(_curStroke.goLclRot * _curStroke.curvePoints[_curCurvePointIdx] + (Vector3)_curStroke.goLclPos, _curStroke.tangentQuaternions[_curCurvePointIdx]);
            curDrawMeshCircle[1].FlushTransform(_curStroke.curvePoints[_curCurvePointIdx], _curStroke.tangentQuaternions[_curCurvePointIdx]);
            //imgCurPoint.transform.localPosition = _curStroke.goLclRot * _curStroke.curvePoints[_curCurvePointIdx] + (Vector3)_curStroke.goLclPos;  //TODO
            imgCurPoint.transform.localPosition = _curStroke.curvePoints[_curCurvePointIdx];  //TODO
            if (Vector2.Distance(imgCurPoint.transform.localPosition, _pointTouch) > TouchDistanceThreshold)
            {
                _canDrag = false;
                SetTween(true);
            }
        }
    }
    private void OnEndDrag(BaseEventData arg0)
    {
        if(!_canDrag)
        {
            return;
        }
        PointerEventData ped = arg0 as PointerEventData;
        if (ped == null)
        {
            return;
        }
        if (ped.pointerId < -1 || ped.pointerId > 1)
        {//不允许鼠标中键和右键响应，不允许移动端多点触控
            return;
        }
        if (_curStroke == null)
        {
            return;
        }
        if (_curStroke.curvePoints.Count <= 1)
        {
            return;
        }
        if (_curStroke.curvePoints.Count == 2)
        {//直线
            //Vector2 pointRotS = _curStroke.goLclRot * _curStroke.curvePoints[0] + (Vector3)_curStroke.goLclPos;
            Vector2 pointRotS = _curStroke.curvePoints[0];
            //Vector2 pointRotE = _curStroke.goLclRot * _curStroke.curvePoints[_curStroke.curvePoints.Count - 1] + (Vector3)_curStroke.goLclPos;
            Vector2 pointRotE = _curStroke.curvePoints[_curStroke.curvePoints.Count - 1];
            _rate = Vector2.Distance(_pointProection, pointRotS) / Vector2.Distance(pointRotE, pointRotS);
        }
        else
        {

        }
        _canDrag = false;
        SetTween(true);
    }
    private void OnPointerClick(BaseEventData arg0)
    {
        PointerEventData ped = arg0 as PointerEventData;
        if (ped == null)
        {
            Debug.LogError(string.Format("ped == null"));
            _canDrag = false;
            return;
        }
        if (ped.pointerId < -1 || ped.pointerId > 1)
        {//不允许鼠标中键和右键响应，不允许移动端多点触控
            Debug.LogError(string.Format("ped.pointerId not valid.ped.pointerId={0}", ped.pointerId));
            _canDrag = false;
            return;
        }
        _curStroke = DrawLetterManager.Instance.GetStroke(charactor, curIdx);
        if (_curStroke == null)
        {
            Debug.LogError(string.Format("_curMeshInfo == null"));
            _canDrag = false;
            return;
        }
        if(_curStroke.curvePoints.Count != 1)
        {//如果不为点
            return;
        }
        _curCurvePointIdx = 0;
        Vector2 localPoint = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), ped.position, ped.pressEventCamera, out localPoint);
        //float distance = Vector2.Distance(localPoint, _curStroke.goLclRot * _curStroke.curvePoints[_curCurvePointIdx] + (Vector3)_curStroke.goLclPos);
        float distance = Vector2.Distance(localPoint, _curStroke.curvePoints[_curCurvePointIdx]);
        if (distance > BeginDragDistanceThreshold)
        {
            Debug.LogWarning(string.Format("T={0};CurvePoint={1};distance={2}", localPoint, _curStroke.curvePoints[_curCurvePointIdx], distance));
            return;
        }
        //
        MeshFilter[] arrMeshFilter = tranDrawMeshRoot.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < arrMeshFilter.Length; ++i)
        {
            if (arrMeshFilter[i].gameObject.name.Trim() == curIdx.ToString())
            {
                curDrawMesh = arrMeshFilter[i];
                break;
            }
        }
        curDrawMesh.mesh = null;
        //
        if (curDrawMeshCircle == null)
        {
            curDrawMeshCircle = new UICircle[1];
        }
        curDrawMeshCircle[0] = curDrawMesh.transform.Find("s").GetComponent<UICircle>();
        curDrawMeshCircle[0].gameObject.SetActive(true);
    }

    private void UpdateMesh()
    {
        //用以使错误[Mesh.vertices is too small. The supplied vertex array has less vertices than are referenced by the t]不显示
        _curMesh.Clear();
        //
        int curVerticeCount = (_curCurvePointIdx + 1) * 2;
        Vector3[] subVertices = new Vector3[curVerticeCount];
        Array.ConstrainedCopy(_curStroke.vertices, 0, subVertices, 0, curVerticeCount);
        _curMesh.vertices = subVertices;
        Vector2[] subUV = new Vector2[curVerticeCount];
        Array.ConstrainedCopy(_curStroke.uv, 0, subUV, 0, curVerticeCount);
        _curMesh.uv = subUV;
        //
        int curTriangleCount = curVerticeCount < 2 ? 0 : (curVerticeCount - 2) * 3;
        int[] subTriangles = new int[curTriangleCount];
        Array.ConstrainedCopy(_curStroke.triangles, 0, subTriangles, 0, curTriangleCount);
        _curMesh.triangles = subTriangles;
        print(string.Format("{0}/{1}/{2} - {3}", subVertices.Length, subUV.Length, subTriangles.Length, _curCurvePointIdx));
        curDrawMesh.mesh = _curMesh;
    }

    private void SetTween(bool isTween)
    {
        _isTween = isTween;
        if (_isTween)
        {
            if (!IsInvoking("DrawMeshCountDown"))
            {
                InvokeRepeating("DrawMeshCountDown", 0, countDownInternal);
            }
        }
        else
        {
            if(IsInvoking("DrawMeshCountDown"))
            {
                CancelInvoke("DrawMeshCountDown");
            }
        }
    }
    //Invoking Method
    private void DrawMeshCountDown()
    {
        if(_curStroke.curvePoints.Count == 2)
        {
            if (_rate > 0)
            {
                _rate -= 0.05f;
                if(_rate < 0)
                {
                    _rate = 0;
                }
                else if(_rate > 1)
                {
                    _rate = 1;
                }
                //求投影点
                //Vector2 pointRotS = _curStroke.goLclRot * _curStroke.curvePoints[0] + (Vector3)_curStroke.goLclPos;
                Vector2 pointRotS = _curStroke.curvePoints[0];
                //Vector2 pointRotE = _curStroke.goLclRot * _curStroke.curvePoints[_curStroke.curvePoints.Count - 1] + (Vector3)_curStroke.goLclPos;
                Vector2 pointRotE = _curStroke.curvePoints[_curStroke.curvePoints.Count - 1];
                Vector2 pointProection = Vector2.Lerp(pointRotS, pointRotE, _rate);
                Vector2 vProection = pointProection - pointRotS;

                _curMesh.Clear();
                //
                Vector3[] subVertices = new Vector3[_curStroke.vertices.Length];
                Array.ConstrainedCopy(_curStroke.vertices, 0, subVertices, 0, 2);
                subVertices[2] = (Vector2)_curStroke.vertices[0] + vProection;
                subVertices[3] = (Vector2)_curStroke.vertices[1] + vProection;
                _curMesh.vertices = subVertices;
                _curMesh.uv = _curStroke.uv;
                _curMesh.triangles = _curStroke.triangles;
                curDrawMesh.mesh = _curMesh;

                curDrawMeshCircle[1].FlushTransform(pointProection, _curStroke.tangentQuaternions[1]);
            }
            else
            {
                SetTween(false);
                curDrawMeshCircle[0].gameObject.SetActive(false);
                curDrawMeshCircle[1].gameObject.SetActive(false);
            }
        }
        else if(_curStroke.curvePoints.Count > 2)
        {
            if (_curCurvePointIdx-- > 0)
            {
                UpdateMesh();
                //curDrawMeshCircle[1].FlushTransform(_curStroke.goLclRot * _curStroke.curvePoints[_curCurvePointIdx] + (Vector3)_curStroke.goLclPos, _curStroke.tangentQuaternions[_curCurvePointIdx]);
                curDrawMeshCircle[1].FlushTransform(_curStroke.curvePoints[_curCurvePointIdx], _curStroke.tangentQuaternions[_curCurvePointIdx]);
            }
            else
            {
                SetTween(false);
                curDrawMeshCircle[0].gameObject.SetActive(false);
                curDrawMeshCircle[1].gameObject.SetActive(false);
            }
        }
    }

    private int GetNearestPointIdx(Vector2 screenPos, Camera cam)
    {
        _pointTouch = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), screenPos, cam, out _pointTouch);  //tranMeshRoot
        if (_curStroke == null || _curStroke.curvePoints == null)
        {
            Debug.LogError(string.Format("GetNearestPointIdx _curMeshInfo == null || _curMeshInfo.curvePoints == null"));
            return 0;
        }
        //找距离当前touch点最近的曲线点
        int nearestPointIdx = 0;
        //Vector2 curvePointWithLclPos = _curStroke.goLclRot * _curStroke.curvePoints[nearestPointIdx] + (Vector3)_curStroke.goLclPos;  //先R后T
        Vector2 curvePointWithLclPos = _curStroke.curvePoints[nearestPointIdx];  //先R后T
        float nearestDistance = Vector2.Distance(_pointTouch, curvePointWithLclPos);
        for (int i = 1; i < _curStroke.curvePoints.Count; i++)
        {
            //curvePointWithLclPos = _curStroke.goLclRot * _curStroke.curvePoints[i] + (Vector3)_curStroke.goLclPos;
            curvePointWithLclPos = _curStroke.curvePoints[i];
            float curDistance = Vector2.Distance(_pointTouch, curvePointWithLclPos);
            if (curDistance < nearestDistance)
            {
                nearestDistance = curDistance;
                nearestPointIdx = i;
            }
        }
        return nearestPointIdx;
    }
}