using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class DraggableObject : MonoBehaviour
{
    private Color originalColor;
    private Renderer renderer;
    private Vector3 offset;
    private Transform toDrag;
    private float dist;
    private bool isDragging = false;
    public bool3 dragAxis = new bool3(true, false, false); // Allow dragging in X and Y by default
    public UnityEvent onDrag;
    public UnityEvent onDragEnd;

    void Start()
    {
        renderer = GetComponent<Renderer>();
        originalColor = GetComponent<Renderer>().material.color;
    }

    public void SetColour(Color c)
    {
        renderer.material.color = c;
    }

    void Update()
    {
        Vector3 pos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(pos);
        RaycastHit hit;
        Vector3 v3;

        if (!Mouse.current.leftButton.isPressed)
        {
            if (isDragging)
                onDragEnd.Invoke();
            isDragging = false;
        }
        if (Physics.Raycast(ray, out hit))
        {

            if (hit.collider.gameObject == this.gameObject)
            {
                renderer.material.color = Color.red;
                toDrag = hit.transform;
                dist = hit.transform.position.z - Camera.main.transform.position.z;
                v3 = new Vector3(pos.x, pos.y, dist);
                v3 = Camera.main.ScreenToWorldPoint(v3);
                offset = toDrag.position - v3;
                if (Mouse.current.leftButton.isPressed)
                {
                    isDragging = true;
                }
            }
            else
            {
                renderer.material.color = originalColor;
            }
        }

        if (isDragging)
        {
            var prevPos = toDrag.position;
            // Update the object's position to follow the mouse cursor
            v3 = new Vector3(pos.x, pos.y, dist);
            v3 = Camera.main.ScreenToWorldPoint(v3);
            toDrag.position = v3 + offset;
            if (dragAxis.x == false) toDrag.position = new Vector3(prevPos.x, toDrag.position.y, toDrag.position.z);
            if (dragAxis.y == false) toDrag.position = new Vector3(toDrag.position.x, prevPos.y, toDrag.position.z);
            if (dragAxis.z == false) toDrag.position = new Vector3(toDrag.position.x, toDrag.position.y, prevPos.z);

            onDrag.Invoke();
        }
    }
}