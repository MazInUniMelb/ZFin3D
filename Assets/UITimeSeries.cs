using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI.Extensions;

[ExecuteAlways]
public class UITimeSeries : MonoBehaviour
{
    public bool generateTimeSeries = true;
    public int nPoints = 500;
    private Dictionary<int, Vector2> pointsMap = new();
    public GameObject startMarker;
    public GameObject currentMarker;
    public GameObject endMarker;
    private UILineRenderer uiLineRenderer;
    private RectTransform rectTransform;
    public UnityEvent<int> timeseriesMarkerMovedEvent;

    void Awake()
    {
        uiLineRenderer = GetComponent<UILineRenderer>();
        rectTransform = GetComponent<RectTransform>();
        timeseriesMarkerMovedEvent ??= new();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (generateTimeSeries)
        {
            if(uiLineRenderer.Points != null && uiLineRenderer.Points.Length == nPoints){
                return;
            }
            uiLineRenderer.Points = CreateTimeSeriesPoints(nPoints);
        }
        startMarker.transform.localPosition = TransformPoint(uiLineRenderer.Points[0]);
        currentMarker.transform.localPosition = TransformPoint(uiLineRenderer.Points[nPoints / 2]);
    }

    public void SetData()
    {
        
    }

    Vector2 TransformPoint(Vector2 p)
    {
        var scaledX = rectTransform.rect.position.x + p.x * rectTransform.rect.width;
        var scaledY = rectTransform.rect.position.y + p.y * rectTransform.rect.height;
        return new Vector2(scaledX, scaledY);
    }

    public void TimeseriesFromData(int[] data)
    {
        var maxTotalActivations = data.Max();
        var points = new Vector2[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            var x = (float)i / (data.Length - 1);
            var y = (float)data[i] / maxTotalActivations;
            points[i] = new Vector2(x, y);
        }
        uiLineRenderer.Points = points;
    }

    Vector2[] CreateTimeSeriesPoints(int nPoints = 500, float amplitude = 0.5f, float frequency = 0.1f)
    {
        var points = new Vector2[nPoints];
        for (int i = 0; i < nPoints; i++)
        {
            var x = (float)i / (nPoints - 1);
            var y = math.sin(i * frequency) * amplitude;
            y += amplitude; // shift up to be all positive
            var p = new Vector2(x, y);
            points[i] = p;
        }
        return points;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    int FindClosestPointIndex(Vector3 markerPos)
    {  
        int closestIndex = -1;
        float closestDistance = Mathf.Infinity;
        for (int i = 0; i < uiLineRenderer.Points.Length; i++)
        {
            float distance = math.abs(TransformPoint(uiLineRenderer.Points[i]).x - markerPos.x);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    public void SetMarkerPosition(int index)
    {
        index = Mathf.Clamp(index, 0, uiLineRenderer.Points.Length - 1);
        currentMarker.transform.localPosition = TransformPoint(uiLineRenderer.Points[index]);
    }

    public void OnStartMarkerDragged(Vector3 startMarkerPos)
    {
        int closestIndex = FindClosestPointIndex(startMarkerPos);
        SetMarkerPosition(closestIndex);
        timeseriesMarkerMovedEvent.Invoke(closestIndex);
    }
}
