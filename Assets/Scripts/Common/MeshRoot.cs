using System.Collections.Generic;
using UnityEngine;

public class MeshRoot : MonoBehaviour
{
    internal void Init(string charactor, UIStroke tplStroke, UILine tplLine, bool isDraw = false)
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
                if (tmpStrok.nodeList.Count == 1)
                {
                    stk.UpdateCurCurveIdx(-1);
                }
                else if(tmpStrok.nodeList.Count == 2)
                {
                    stk.UpdateLinePercent(0);
                }
                else
                {
                    stk.UpdateCurCurveIdx(-1);
                }
            }
            else
            {
                stk.color = Color.green;
            }
            DrawLetterManager.Instance.SetCurvePoints(charactor, tmpStrok.index, stk.GetStrokeData());
        }
    }

    internal void DestroyGO()
    {
        UILine[] arrLine = transform.GetComponentsInChildren<UILine>();
        for (int i = arrLine.Length - 1; i >= 0; --i)
        {
            GameObject.Destroy(arrLine[i].gameObject);
        }
        UIStroke[] arr1 = transform.GetComponentsInChildren<UIStroke>();
        for (int i = arr1.Length - 1; i >= 0; --i)
        {
            GameObject.Destroy(arr1[i].gameObject);
        }
    }
}