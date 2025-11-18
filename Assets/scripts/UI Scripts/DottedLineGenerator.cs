using System.Linq;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI.Extensions;

[ExecuteAlways]
public class DottedLineGenerator : MonoBehaviour
{
    [Range(0.0f, 0.5f)] public float segmentLength = 0.05f;
    [Range(0.0f, 0.5f)] public float spacing = 0.05f;
    public bool horizontal = false;
    public bool recreate = false;
    [SerializeField] private int nSegments = 0;
    private float prevSegmentLength = 0.05f;
    private float prevSpacing = 0.05f;

    private UILineRenderer uiLineRenderer = null;
    private RectTransform rectTransform = null;
    public UILineRenderer timeSeriesLineRenderer = null;
    public UICircle uiCircle = null;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Init();
    }

    void Init()
    {
        prevSegmentLength = segmentLength;
        prevSpacing = spacing;

        if (uiCircle == null)
        {
            uiCircle = transform.parent.gameObject.GetComponent<UICircle>();
        }

        if (uiLineRenderer == null)
        {
            uiLineRenderer = gameObject.GetOrAddComponent<UILineRenderer>();
        }
        rectTransform = uiLineRenderer.gameObject.GetComponent<RectTransform>();
        uiLineRenderer.LineList = true;
        uiLineRenderer.RelativeSize = true;
        uiLineRenderer.Points = CreateDottedLinePoints(spacing);
        if (uiCircle != null)
        {
            uiLineRenderer.color = uiCircle.color;
        }
    }

    Vector2[] CreateDottedLinePoints(float spacing)
    {
        nSegments = (int)(1.0f / (segmentLength + spacing)) + 1;
        if (nSegments <= 0) nSegments = 1;
        var points = new Vector2[nSegments*2];
        float start = 0.0f;
        for (int i = 0; i < nSegments*2; i += 2)
        {
            points[i] = horizontal ? new Vector2(start, 0) : new Vector2(0, start);
            start += segmentLength;
            if (i + 1 < nSegments*2){
                points[i + 1] = horizontal ? new Vector2(start, 0) : new Vector2(0, start);
            }
            start += spacing;
        }
        return points.Reverse().ToArray();
    }

    void OnValidate()
    {
        if (recreate)
        {
            recreate = false;
            Init();
        }
        if (prevSegmentLength != segmentLength || prevSpacing != spacing)
        {
            uiLineRenderer.Points = CreateDottedLinePoints(spacing);
            prevSegmentLength = segmentLength;
            prevSpacing = spacing;
        }
    }
}
