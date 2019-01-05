using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class CatmullRomDemo : MonoBehaviour
{
    private Image[] _imgs = null;
    private List<Vector2> points = null;
    [SerializeField]
    private bool _isClose = false;
    [SerializeField]
    private int _smooth = 10;

    private void OnEnable()
    {
        _imgs = GetComponentsInChildren<Image>();
    }

    void Update()
    {
        if (_imgs == null)
        {
            return;
        }
        Vector2[] pos = new Vector2[_imgs.Length];
        for (int i = 0; i < _imgs.Length; i++)
        {
            pos[i] = _imgs[i].rectTransform.position;
        }
        _smooth = Mathf.Clamp(_smooth, 1, _smooth);
        points = CalCatmullRomPoints(pos, _smooth, _isClose);
    }

    private void OnDrawGizmos()
    {
        if(points == null || points.Count <= 0)
        {
            return;
        }
        Gizmos.color = Color.white;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }

    private List<Vector2> CalCatmullRomPoints(Vector2[] nodeList, int smooth, bool isClose = false)
    {
        int pointCount = nodeList.Length;
        int segmentCount = isClose ? pointCount : pointCount - 1;    //闭环的段数为关键点数，非闭环段数为关键点数-1
        List<Vector2> allVertices = new List<Vector2>((smooth + 1) * segmentCount); //总顶点数
        Vector2[] tempVertices = new Vector2[smooth + 1];
        float smoothReciprocal = 1f / smooth;
        for (int i = 0; i < segmentCount; ++i)
        {
            Vector2 p0, p1, p2, p3; // 获得4个相邻的点以计算p1和p2之间的位置
            p1 = nodeList[i];
            if (isClose)
            {
                p0 = i == 0 ? nodeList[segmentCount - 1] : nodeList[i - 1];
                p2 = i + 1 < pointCount ? nodeList[i + 1] : nodeList[i + 1 - pointCount];
                p3 = i + 2 < pointCount ? nodeList[i + 2] : nodeList[i + 2 - pointCount];
            }
            else
            {
                p0 = i == 0 ? p1 : nodeList[i - 1];
                p2 = nodeList[i + 1];
                p3 = i == segmentCount - 1 ? p2 : nodeList[i + 2];
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
}