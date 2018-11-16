using LitJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DrawMeshPanel : MonoBehaviour
{
    [SerializeField]
    private string charactor = "B";
    [SerializeField]
    private byte curIdx = 3; //当前可画的笔画索引，其他索引的笔画不响应;1基
    [SerializeField]
    private float BeginDragDistanceThreshold = 100;
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
    private MeshRoot meshRoot = null;
    private MeshRoot meshRootD = null;
    private Button btnDraw = null;
    private Image imgTouchPad = null;
    private UIStroke tplStroke = null;
    private UIStroke curStroke = null;
    private UILine tplLine = null;
    //data
    private float _percent = 0;
    private Vector2 _pointProection = Vector2.zero;
    private Vector2 _pointS = Vector2.zero;
    private Vector2 _pointE = Vector2.zero;

    private void Awake()
    {
        RegisterExporter();

        meshRoot = transform.Find("MeshRoot").GetComponent<MeshRoot>();
        meshRootD = transform.Find("MeshRootD").GetComponent<MeshRoot>();

        tplStroke = transform.Find("tplStroke").GetComponent<UIStroke>();
        tplStroke.gameObject.SetActive(false);
        tplLine = transform.Find("tplLine").GetComponent<UILine>();
        tplLine.gameObject.SetActive(false);

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
    private void RegisterExporter()
    {//使LitJson支持float，Vector，Quaternion
        JsonMapper.RegisterExporter<float>((obj, writer) => writer.Write(Convert.ToDouble(obj)));
        JsonMapper.RegisterImporter<double, float>((input) => { return (float)input; });
        JsonMapper.RegisterExporter<Vector2>(delegate (Vector2 obj, JsonWriter writer)
        {
            writer.WriteArrayStart();
            writer.Write(Convert.ToDouble(obj.x));
            writer.Write(Convert.ToDouble(obj.y));
            writer.WriteArrayEnd();
        });
        JsonMapper.RegisterExporter<Vector3>(delegate (Vector3 obj, JsonWriter writer)
        {
            writer.WriteArrayStart();
            writer.Write(Convert.ToDouble(obj.x));
            writer.Write(Convert.ToDouble(obj.y));
            writer.Write(Convert.ToDouble(obj.z));
            writer.WriteArrayEnd();
        });
        JsonMapper.RegisterExporter<Quaternion>(delegate (Quaternion obj, JsonWriter writer)
        {
            writer.WriteArrayStart();
            writer.Write(Convert.ToDouble(obj.x));
            writer.Write(Convert.ToDouble(obj.y));
            writer.Write(Convert.ToDouble(obj.z));
            writer.Write(Convert.ToDouble(obj.w));
            writer.WriteArrayEnd();
        });
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
    
    private void DrawMeshByData()
    {// 自动绘制全部mesh
        meshRoot.DestroyGO();
        meshRootD.DestroyGO();
        meshRoot.Init(charactor, tplStroke, tplLine);
        meshRootD.Init(charactor, tplStroke, tplLine, true);
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
        if (_curStroke.curvePoints == null || _curStroke.curvePoints.Count <= 1)
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
        UIStroke[] arr = meshRootD.GetComponentsInChildren<UIStroke>();
        for (int i = 0; i < arr.Length; ++i)
        {
            if(arr[i].gameObject.name.Trim() == curIdx.ToString())
            {
                curStroke = arr[i];
                break;
            }
        }
        _pointS = _curStroke.curvePoints[0];
        _pointE = _curStroke.curvePoints[_curStroke.curvePoints.Count - 1];
    }
    private void OnDrag(BaseEventData arg0)
    {
        if (!_canDrag)
        {
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
        if (_curStroke.curvePoints == null || _curStroke.curvePoints.Count <= 1)
        {
            return;
        }
        if(_curStroke.curvePoints.Count == 2)
        {//直线
            _pointTouch = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), ped.position, ped.pressEventCamera, out _pointTouch);
            //求投影点
            Vector3 vProection = Vector3.Project(_pointTouch - _pointS, _pointE - _pointS);
            _percent = vProection.magnitude / (_pointE - _pointS).magnitude;
            curStroke.UpdateLinePercent(_percent);
            _pointProection = _pointS + (Vector2)vProection;
            if (Vector2.Distance(_pointProection, _pointTouch) > TouchDistanceThreshold)
            {
                _canDrag = false;
                SetTween(true);
            }
        }
        else
        {
            UpdateCurCurvePointIdx(ped.position, ped.pressEventCamera);
            curStroke.UpdateCurCurveIdx(_curCurvePointIdx);
            if (Vector2.Distance(_curStroke.curvePoints[_curCurvePointIdx], _pointTouch) > TouchDistanceThreshold)
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
        if (_curStroke.curvePoints == null || _curStroke.curvePoints.Count <= 1)
        {
            return;
        }
        if (_curStroke.curvePoints.Count == 2)
        {//直线
            Vector2 pointRotS = _curStroke.curvePoints[0];
            Vector2 pointRotE = _curStroke.curvePoints[_curStroke.curvePoints.Count - 1];
            _percent = Vector2.Distance(_pointProection, pointRotS) / Vector2.Distance(pointRotE, pointRotS);
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
            //_canDrag = false;
            return;
        }
        if (ped.pointerId < -1 || ped.pointerId > 1)
        {//不允许鼠标中键和右键响应，不允许移动端多点触控
            Debug.LogError(string.Format("ped.pointerId not valid.ped.pointerId={0}", ped.pointerId));
            //_canDrag = false;
            return;
        }
        _curStroke = DrawLetterManager.Instance.GetStroke(charactor, curIdx);
        if (_curStroke == null)
        {
            Debug.LogError(string.Format("_curMeshInfo == null"));
            //_canDrag = false;
            return;
        }
        if(_curStroke.curvePoints == null || _curStroke.curvePoints.Count != 1)
        {//如果不为点
            return;
        }
        _curCurvePointIdx = 0;
        Vector2 localPoint = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), ped.position, ped.pressEventCamera, out localPoint);
        float distance = Vector2.Distance(localPoint, _curStroke.curvePoints[_curCurvePointIdx]);
        if (distance > BeginDragDistanceThreshold)
        {
            Debug.LogWarning(string.Format("T={0};CurvePoint={1};distance={2}", localPoint, _curStroke.curvePoints[_curCurvePointIdx], distance));
            return;
        }
        //
        UIStroke[] arr = meshRootD.GetComponentsInChildren<UIStroke>();
        for (int i = 0; i < arr.Length; ++i)
        {
            if (arr[i].gameObject.name.Trim() == curIdx.ToString())
            {
                curStroke = arr[i];
                curStroke.UpdateCurCurveIdx(0);
                break;
            }
        }
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
            if (_percent > 0)
            {
                _percent -= 0.05f;
                _percent = Mathf.Clamp01(_percent);
                curStroke.UpdateLinePercent(_percent);
            }
            else
            {
                SetTween(false);
            }
        }
        else if(_curStroke.curvePoints.Count > 2)
        {
            if (_curCurvePointIdx-- > -1)
            {
                curStroke.UpdateCurCurveIdx(_curCurvePointIdx);
            }
            else
            {
                SetTween(false);
            }
        }
    }
    
    private void UpdateCurCurvePointIdx(Vector2 screenPos, Camera cam)
    {//根据屏幕坐标更新最近的curvePoints元素索引
        if (_curStroke == null || _curStroke.curvePoints == null)
        {
            return;
        }
        _pointTouch = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.GetComponent<RectTransform>(), screenPos, cam, out _pointTouch);
        int lastIdx = _curStroke.curvePoints.Count - 1;
        for (int i = _curCurvePointIdx; i < Mathf.Min(_curCurvePointIdx + _curStroke.smooth, lastIdx); i++)
        {
            Vector2 pS = _curStroke.curvePoints[i];
            Vector2 pC = _curStroke.curvePoints[i >= lastIdx ? lastIdx : i + 1];
            Vector2 pE = _curStroke.curvePoints[i >= lastIdx - 1 ? lastIdx : i + 2];
            float dotS = Vector2.Dot(_pointTouch - pS, pC - pS);
            float dotE = Vector2.Dot(_pointTouch - pC, pE - pC);
            if (dotS > 0 && dotE <= 0)
            {
                _curCurvePointIdx = i + 1;
                //Debug.Log(string.Format("{0}/{1}:{2},{3}", _curCurvePointIdx, _curStroke.curvePoints.Count, dotS, dotE));
            }
        }
    }
}