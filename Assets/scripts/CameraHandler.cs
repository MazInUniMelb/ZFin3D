using UnityEngine;
using System.Collections.Generic;

public class CameraHandler : MonoBehaviour
{

    [Header("Camera References")]
    public Camera mainCamera;
    public Camera lineGraphCamera;
    public Camera dorsalCamera;
    public Camera ventralCamera;
    public Camera sagittalCamera;

    //[Header("Troubleshooting")]

    private float FOVdefault = 45f;
    private float FOVzoomedout = 55f;

    private Vector3 centerPoint; // center of  ze brain

    private Vector3 returnCameraPos;//  initial camera position
    private float radius; // The radius of the rotation set by current camera radius from centerPoint
    public float cameraSpeed = 1f; // Speed of rotation in degrees per second

    public void Start()
    {
        Debug.Log("CameraHandler Start called");
        mainCamera.enabled = true;
        lineGraphCamera.enabled = false;
        dorsalCamera.enabled = false;
        ventralCamera.enabled = false;
        sagittalCamera.enabled = false;
        // Optionally, set mainCamera to full screen at start
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }
    public void SetupViewports()
    {
        Debug.Log("Setup viewports");

        mainCamera.depth = 0;
        lineGraphCamera.depth = 1;
        dorsalCamera.depth = 2;
        ventralCamera.depth = 3;
        sagittalCamera.depth = 4;

        lineGraphCamera.enabled = true;
        dorsalCamera.enabled = true;
        ventralCamera.enabled = true;
        sagittalCamera.enabled = true;

        // Main camera: left 50% of screen
        mainCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        mainCamera.clearFlags = CameraClearFlags.Skybox;

        // Linegraph camera: left 50% of screen, top 20%
        lineGraphCamera.rect = new Rect(0f, 0.8f, 0.5f, 0.2f);
        lineGraphCamera.clearFlags = CameraClearFlags.Depth;

        // Dorsal: top third of right 50%
        dorsalCamera.rect = new Rect(0.5f, 2f/3f, 0.5f, 1f/3f);
        dorsalCamera.clearFlags = CameraClearFlags.Depth;

        // Ventral: middle third of right 50%
        ventralCamera.rect = new Rect(0.5f, 1f/3f, 0.5f, 1f/3f);
        ventralCamera.clearFlags = CameraClearFlags.Depth;

        // Sagittal: bottom third of right 50%
        sagittalCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f/3f);
        sagittalCamera.clearFlags = CameraClearFlags.Depth;

    }


    public Vector3 SetupMainCameraView(Vector3 centerPos, float extent)
    {
        if (centerPos == null)
        {
            Debug.LogWarning("No centroid position provided for camera positioning.");
            return Vector3.zero;
        }

        Debug.Log($"Positioning cameras to center: {centerPos}, extent: {extent}");

        // Calculate the direction from which you want to view (e.g., Z for main, X for lateral, Y for ventral)
        Vector3 mainViewDir = Vector3.back;      // -Z

        // Use the extent to set the distance
        float minDistance = 100f; // Set based on your scene scale
        float maxDistance = 1500f;
        float distance = Mathf.Clamp(extent * 2f, minDistance, maxDistance);

        // Main camera
        // todo move main camera slowly to new position
        Vector3 newCameraPos = centerPos + mainViewDir * distance;
        //mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, newCameraPos, 0.1f);
        mainCamera.transform.position = newCameraPos;
        mainCamera.transform.LookAt(centerPos);
        mainCamera.fieldOfView = FOVzoomedout;

        return centerPoint;
    }


    public Vector3 PositionCameras(Vector3 centerPos, float extent)
    {
        if (centerPos == null)
        {
            Debug.LogWarning("No centroid position provided for camera positioning.");
            return Vector3.zero;
        }

        Debug.Log($"Positioning cameras to center: {centerPos}, extent: {extent}");

        // Calculate the direction from which you want to view (e.g., Z for main, X for lateral, Y for ventral)
        Vector3 mainViewDir = Vector3.back;      // -Z

        Vector3 dorsalViewDir = Vector3.back;      // -Z
        Vector3 ventralDir = Vector3.down;       // -Y
        Vector3 sagittalLeftDir = Vector3.left;   // -X
        // Use the extent to set the distance
        float minDistance = 100f; // Set based on your scene scale
        float maxDistance = 1200f;
        float distance = Mathf.Clamp(extent * 2f, minDistance, maxDistance);

        // Main camera
        // todo move main camera slowly to new position
        Vector3 newCameraPos = centerPos + mainViewDir * distance;
        //mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, newCameraPos, 0.1f);
        mainCamera.transform.position = newCameraPos;
        mainCamera.transform.LookAt(centerPos);
        mainCamera.fieldOfView = FOVzoomedout;

        // Dorsal camera
        dorsalCamera.transform.position = centerPos + mainViewDir * distance;
        dorsalCamera.transform.LookAt(centerPos);
        dorsalCamera.fieldOfView = FOVdefault;

        // Move ventral camera below the region, but keep X and Z at center
        ventralCamera.transform.position = new Vector3(centerPos.x, centerPos.y - distance, centerPos.z);
        ventralCamera.transform.LookAt(centerPos);
        ventralCamera.fieldOfView = FOVdefault;

        // Lateral ie sagittal camera
        sagittalCamera.transform.position = centerPos + sagittalLeftDir * distance;
        sagittalCamera.transform.LookAt(centerPos);
        sagittalCamera.fieldOfView = FOVdefault;

        return centerPoint;
    }


    void DrawViewportBorder(Rect rect, Color color)
    {
        float w = Screen.width;
        float h = Screen.height;
        Rect pixelRect = new Rect(rect.x * w, (1 - rect.y - rect.height) * h, rect.width * w, rect.height * h);

        Color oldColor = GUI.color;
        GUI.color = color;

        // Top
        GUI.DrawTexture(new Rect(pixelRect.x, pixelRect.y, pixelRect.width, 2), Texture2D.whiteTexture);
        // Bottom
        GUI.DrawTexture(new Rect(pixelRect.x, pixelRect.y + pixelRect.height - 2, pixelRect.width, 2), Texture2D.whiteTexture);
        // Left
        GUI.DrawTexture(new Rect(pixelRect.x, pixelRect.y, 2, pixelRect.height), Texture2D.whiteTexture);
        // Right
        GUI.DrawTexture(new Rect(pixelRect.x + pixelRect.width - 2, pixelRect.y, 2, pixelRect.height), Texture2D.whiteTexture);

        GUI.color = oldColor;
    }

}
