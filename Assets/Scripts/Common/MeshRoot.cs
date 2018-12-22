using LitJson;
using System.Collections.Generic;
using UnityEngine;

public class MeshRoot : MonoBehaviour
{
    internal void Init(string charactor, UIStroke tplStroke, bool isDraw = false)
    {
        Dictionary<string, Letter> tmpDict = DrawLetterManager.Instance.LetterDict;
        if (tmpDict == null)
        {
            Debug.LogError(string.Format("tmpDict null"));
            return;
        }
        if (!tmpDict.ContainsKey(charactor))
        {
            Debug.LogError(string.Format("不包含字符[{0}]", charactor));
            return;
        }
        Letter tmpLetter = tmpDict[charactor];
        if (tmpLetter == null)
        {
            Debug.LogError(string.Format("tmpLetter null"));
            return;
        }
        List<Stroke> tmpStrokList = tmpLetter.StrokeList;
        for (int i = 0; i < tmpStrokList.Count; i++)
        {
            Stroke tmpStrok = tmpStrokList[i];
            if (tmpStrok == null)
            {
                continue;
            }
            UIStroke stk = GameObject.Instantiate<UIStroke>(tplStroke, transform);
            stk.gameObject.SetActive(true);
            stk.gameObject.name = tmpStrok.index.ToString();
            stk.InitData(tmpStrok.nodeList.ToArray(), tmpStrok.width, tmpStrok.smooth, tmpStrok.isClose);
            if (isDraw)
            {
                stk.color = new Color(1, 0.6353f, 0, 1f);
                stk.UpdatePercent(0);
            }
            else
            {
                stk.color = Color.green;
                stk.UpdatePercent(1);
#if UNITY_EDITOR
                _curvePoints = JsonMapper.ToObject<List<Vector2>>(stk.GetStrokeData());
                _vertices = JsonMapper.ToObject<List<Vector3>>(stk.GetStrokeVertices());
#endif
            }
            DrawLetterManager.Instance.SetCurvePoints(charactor, tmpStrok.index, stk.GetStrokeData());
        }
    }

    internal void DestroyGO()
    {
        UIStroke[] arr1 = transform.GetComponentsInChildren<UIStroke>();
        for (int i = arr1.Length - 1; i >= 0; --i)
        {
            GameObject.Destroy(arr1[i].gameObject);
        }
    }

#if UNITY_EDITOR
    private List<Vector2> _curvePoints = null;
    private List<Vector3> _vertices = null;
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