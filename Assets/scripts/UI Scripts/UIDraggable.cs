using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

[RequireComponent(typeof(RectTransform))]
public class UIDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool dragOnSurfaces = true;
    private RectTransform m_DraggingPlane;

    private UICircle uiCircle = null;
    private Color circleOriginalColor = Color.white;
    private UILineRenderer uiLineRenderer = null;
    private Color lineOriginalColor = Color.white;

    public UnityEvent<Vector3> markerDraggedEvent;

    void Awake()
    {
        markerDraggedEvent ??= new();
    }

    public void Start()
    {
        uiCircle = GetComponent<UICircle>();
        if (uiCircle == null)
        {
            uiCircle = GetComponentInChildren<UICircle>();
        }
        if (uiCircle != null)
        {
            circleOriginalColor = uiCircle.color;
        }
        uiLineRenderer = GetComponent<UILineRenderer>();
        if(uiLineRenderer == null)
        {
            uiLineRenderer = GetComponentInChildren<UILineRenderer>();
        }
        if(uiLineRenderer != null)
        {
            lineOriginalColor = uiLineRenderer.color;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var canvas = FindInParents<Canvas>(gameObject);
        if (canvas == null)
            return;

        if (dragOnSurfaces)
            m_DraggingPlane = transform as RectTransform;
        else
            m_DraggingPlane = canvas.transform as RectTransform;

        SetDraggedPosition(eventData);
    }

    public void OnDrag(PointerEventData data)
    {
        SetDraggedPosition(data);
    }

    private void SetDraggedPosition(PointerEventData data)
    {
        if (dragOnSurfaces && data.pointerEnter != null && data.pointerEnter.transform as RectTransform != null)
            m_DraggingPlane = data.pointerEnter.transform as RectTransform;

        var rt = GetComponent<RectTransform>();
        Vector3 globalMousePos;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(m_DraggingPlane, data.position, data.pressEventCamera, out globalMousePos))
        {
            rt.position = new Vector3(globalMousePos.x, rt.position.y, rt.position.z);
            markerDraggedEvent.Invoke(GetComponent<RectTransform>().localPosition);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    static public T FindInParents<T>(GameObject go) where T : Component
    {
        if (go == null) return null;
        var comp = go.GetComponent<T>();

        if (comp != null)
            return comp;

        Transform t = go.transform.parent;
        while (t != null && comp == null)
        {
            comp = t.gameObject.GetComponent<T>();
            t = t.parent;
        }
        return comp;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (uiCircle != null)
        {
            uiCircle.color = Color.red;
        }
        if (uiLineRenderer != null)
        {
            uiLineRenderer.color = Color.red;
        }

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (uiCircle != null)
        {
            uiCircle.color = circleOriginalColor;
        }
        if (uiLineRenderer != null)
        {
            uiLineRenderer.color = lineOriginalColor;
        }
    }
}