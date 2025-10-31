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

    [Header("FeatureSet View")]
    public Camera featureSetCamera;
    public bool featureSetViewEnabled = true;

    public List<Camera> fsCameras = new List<Camera>();

    //[Header("Troubleshooting")]

    private GameObject cameraParentObject = null;

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
        featureSetCamera.enabled = false;
        // set mainCamera to full screen at start
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);

        // setup linegraph camera to show timeline view precisely
        lineGraphCamera.orthographic = true;
        lineGraphCamera.orthographicSize = 200f; 
    }

    public void CreateFeaturesetCameras(int nbrFeatureSets)
    {
        // disable any existing feature set cameras
        foreach (Camera cam in fsCameras)
        {
            Destroy(cam.gameObject);
        }
        fsCameras.Clear();
        cameraParentObject = mainCamera.transform.parent.gameObject;
        
        for (int i = 1; i <= nbrFeatureSets; i++)
        {
                // create duplicate feature set camera for each brain
                Camera featureSetCamera = Instantiate(this.featureSetCamera);
                featureSetCamera.name = "FeatureSetCamera" + i.ToString();
                featureSetCamera.enabled = false;
                featureSetCamera.transform.SetParent(cameraParentObject.transform, false);
                fsCameras.Add(featureSetCamera);
                featureSetCamera.depth = 4 + i;            
        }
    }

    public void SetupViewports(int nbrBrains)
    {

        mainCamera.depth = 0;

        lineGraphCamera.enabled = true;

        // Linegraph camera: full width of screen, top 20%
        lineGraphCamera.rect = new Rect(0f, 0.8f, 1f, 0.2f);
        lineGraphCamera.clearFlags = CameraClearFlags.Depth;

        float featureSetWidth = 1f / nbrBrains;

        for (int i = 0; i < (nbrBrains-1); i++) // nbr brains - 1 because first brain is main camera
        {
            if (i == 0)
            {
                mainCamera.enabled = true;
                // Main camera: left portion of screen, bottom 80%
                mainCamera.rect = new Rect(0f, 0f, featureSetWidth, .8f);
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                Debug.Log($"Enabling feature set camera {i}: {fsCameras[i].name}");
                fsCameras[i].enabled = true;
                // Feature set camera: right portion of screen, bottom 80%
                fsCameras[i].rect = new Rect(i * featureSetWidth, 0f, featureSetWidth, .8f);
                fsCameras[i].clearFlags = CameraClearFlags.Skybox;
            }
        }


        //Parameters breakdown:
        // 0f = X position (left edge) - starts at the very left of the screen (0%)
        // 0.8f = Y position (bottom edge) - starts at 80% up from the bottom of the screen
        // 1f = Width - takes up the full width of the screen (100%)
        // 0.2f = Height - takes up 20% of the screen height


    }

    public Vector3 SetupMainCameraView(Vector3 centerPos, float extent)
    {
        if (centerPos == null)
        {
            Debug.LogWarning("No centroid position provided for camera positioning.");
            return Vector3.zero;
        }

        centerPoint.x = centerPos.x+50f; // shift to right slightly

        Debug.Log($"Positioning cameras to center: {centerPos}, extent: {extent}");

        // Calculate the direction from which you want to view (e.g., Z for main, X for lateral, Y for ventral)
        Vector3 mainViewDir = Vector3.back;      // -Z

        // Use the extent to set the distance
        float minDistance = 100f; // Set based on your scene scale
        float maxDistance = 1500f;
        float distance = Mathf.Clamp(extent * 1.2f, minDistance, maxDistance);

        // Main camera
        // todo move main camera slowly to new position
        Vector3 newCameraPos = centerPos + mainViewDir * distance;
        // adjust y axis to be lower becuase whole brain is so much longer than it is wide
        newCameraPos.y = newCameraPos.y * .4f;
        //mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, newCameraPos, 0.1f);
        mainCamera.transform.position = newCameraPos;
        mainCamera.transform.LookAt(centerPos);
        mainCamera.fieldOfView = FOVzoomedout;

        // set mainCamera to full screen at start
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);

        return centerPoint;
    }

    public Vector3 PositionFeatureSetCamera(Camera fsCamera, Vector3 centerPos, float extent)
        {
            if (centerPos == null)
            {
                Debug.LogWarning("No centroid position provided for camera positioning.");
                return Vector3.zero;
            }

            Debug.Log($"Positioning feature set camera {fsCamera.name} to center: {centerPos}, extent: {extent}");

            // Calculate the direction from which you want to view (e.g., Z for main, X for lateral, Y for ventral)
            Vector3 mainViewDir = Vector3.back;      // -Z

            // Use the extent to set the distance
            float minDistance = 1000f; // Set based on your scene scale
            float maxDistance = 1500f;
            float distance = Mathf.Clamp(extent * 1.5f, minDistance, maxDistance);

            // Feature set camera
            // todo move main camera slowly to new position
            Vector3 newCameraPos = centerPos + mainViewDir * distance;

            //mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, newCameraPos, 0.1f);
            fsCamera.transform.position = newCameraPos;
            fsCamera.transform.LookAt(centerPos);
            fsCamera.fieldOfView = FOVzoomedout;

            return centerPoint;
        }
    

    public Vector3 PositionMainCamera(Vector3 centerPos, float extent)
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
        float maxDistance = 1200f;
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


    public void SetupRegionViewports()
    {
        // no longer used but not deleting yet
        Debug.Log("Setup viewports");

        mainCamera.depth = 0;
        lineGraphCamera.depth = 1;
        dorsalCamera.depth = 2;
        ventralCamera.depth = 3;
        sagittalCamera.depth = 4;
        featureSetCamera.depth = 5;

        lineGraphCamera.enabled = true;
        dorsalCamera.enabled = true;
        ventralCamera.enabled = true;
        sagittalCamera.enabled = true;
        if (featureSetViewEnabled) 
            {
            featureSetCamera.enabled = true; 
            dorsalCamera.enabled = false;
            }
        else
            {
            featureSetCamera.enabled = false;
            dorsalCamera.enabled = true;
             }

        // Main camera: left 50% of screen
        mainCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        mainCamera.clearFlags = CameraClearFlags.Skybox;

        // Linegraph camera: left 50% of screen, top 20%
        lineGraphCamera.rect = new Rect(0f, 0.8f, 0.5f, 0.2f);
        lineGraphCamera.clearFlags = CameraClearFlags.Depth;

        // Dorsal feature set view: top third of right 50%
        dorsalCamera.rect = new Rect(0.5f, 0.66f, 0.5f, 0.34f);
        dorsalCamera.clearFlags = CameraClearFlags.Depth;

        // Feature set view: top third of right 50%
        featureSetCamera.rect = new Rect(0.5f, 2f/3f, 0.5f, 1f/3f);
        featureSetCamera.clearFlags = CameraClearFlags.Depth;

        // Ventral: middle third of right 50%
        ventralCamera.rect = new Rect(0.5f, 1f/3f, 0.5f, 1f/3f);
        ventralCamera.clearFlags = CameraClearFlags.Depth;

        // Sagittal: bottom third of right 50%
        sagittalCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f/3f);
        sagittalCamera.clearFlags = CameraClearFlags.Depth;

    }

    public Vector3 PositionRegionCameras(Vector3 centerPos, float extent)
    {
        // no longer used but not deleting yet
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
        newCameraPos.y = newCameraPos.y * .9f;
        //mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, newCameraPos, 0.1f);
        mainCamera.transform.position = newCameraPos;
        mainCamera.transform.LookAt(centerPos);
        mainCamera.fieldOfView = FOVzoomedout;

        // Dorsal camera
        dorsalCamera.transform.position = centerPos + mainViewDir * distance;
        dorsalCamera.transform.LookAt(centerPos);
        dorsalCamera.fieldOfView = FOVdefault;

        // Featureset camera
        featureSetCamera.transform.position = centerPos + mainViewDir * distance;
        featureSetCamera.transform.LookAt(centerPos);
        featureSetCamera.fieldOfView = FOVdefault;

        // Move ventral camera below the region, but keep X and Z at center
    // AB to do change to above the region
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
