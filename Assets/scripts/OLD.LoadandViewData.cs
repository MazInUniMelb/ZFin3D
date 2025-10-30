using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using UnityEditor;

public class LoadandViewData : MonoBehaviour
{

    /* 
    MDHS-MDAP Collaboration showing the intricacies of the zebrafish brain
    Authors: Amanda Belton, Wei Quin and Ethan Scott
    Last updated: 30 May 2025

    This script loads neuronal data from a chosen CSV file, creates 3D representations of neurons in Unity
    This neuronal data is captured by the Scott Lab at the University of Melbourne, Australia

    Todos: 
        Aesthetics of neurons, active and inactive
        Interaction iwth timeline ticker at bottom of gui to jump to timestamp
        Add visualisation of peaks of signal data
        Read in timestamp annotations from csv file (1st update python code to add annotations)
        Address occulsion with side views to indicate depth of neurons that are ative and behind others
        Add audio to neurons and microphone to centroid of brain to listen to the signal data
        Test to load all csv files in data folder and identify bugs
        Button to export video of the signal data
    */
    public string dataFolder = "Assets/Data";
    public string postionsFile = "ZF.calcium_position_data.csv";
    public Material glowMaterial;
    public Material labelMaterial;
    public Camera mainCamera; // Reference to the main camera
    public Mesh sphereMesh; // Reference to the sphere mesh
    public Mesh cubeMesh; // Reference to the cube mesh

    public float lastTime = 0;  
    public float lastDelta = 0;

    private GameObject lastSelectedNeuron = null; // Tracks the last selected neuron
    private Transform lastSelectedRegion = null;
    private List<string> availableSignalFiles = new List<string>();
    private int selectedSignalFileIndex = 0;
    private bool showSignalFileDropdown = true;
    private bool showDropdownList = false;private string loadingMessage = null;
    private GameObject brain; // Parent object for all neurons
    private List<string> regionNames = new List<string>
    {
        "Diencephalon",  "Mesencephalon", "Rhombencephalon", "Telencephalon","Ganglia", "Spinal", "None"
    };

    // Dictionary to map region names to their transforms
    private Dictionary<string, Transform> regionTransforms = new Dictionary<string, Transform>
    {
        { "Diencephalon", null },
        { "Ganglia", null },
        { "Mesencephalon", null },
        { "Rhombencephalon", null },
        { "Spinal", null },
        { "Telencephalon", null },
        { "None", null }
    };
    
    private Dictionary<int, string> regions = new Dictionary<int, string>
    {
        { 0, "Diencephalon" },     // Focus area
        { 1, "Ganglia" },
        { 2, "Mesencephalon" },    // Focus area
        { 3, "Rhombencephalon" },  // Focus area
        { 4, "Spinal" },   
        { 5, "Telencephalon" },    // Focus area
        { 6, "None" } 
    };

Dictionary<string, Color>regionColours = new Dictionary<string, Color>
{
    { "Diencephalon", new Color(0.4f, 1f, 0.3843f) }, // RGB: (127,255,98) greenish
    { "Mesencephalon", new Color(0.4588f, 0.5529f, 0.3843f) }, // RGB: (117, 141, 98) orangish
    { "Rhombencephalon", new Color(0f, 0.6275f,1f) }, // RGB: (0,126,255) bluish
    { "Telencephalon", new Color(1f, 0.5412f, 0.7647f) }, // RGB: (255, 41, 138) pinkish
    { "Ganglia", new Color(0.4588f, 0.4392f, 0.7019f) }, // Default to dark RGB: (117,112,179)
    { "Spinal", new Color(0.4588f, 0.4392f, 0.7019f) }, // Default to dark RGB: (117,112,179)
    { "None", new Color(0.4588f, 0.4392f, 0.7019f) } // Default to dark RGB: (117,112,179)
};


    private Vector3 centerPoint; // center of  ze brain
    private Vector3 returnCameraPos ;//  initial camera position
    private float radius; // The radius of the rotation set by current camera radius from centerPoint
    public float cameraSpeed = 1f; // Speed of rotation in degrees per second

    private List<int> selNeurons = new List<int>(); // list of selected neurons
    private string neuronLabel = ""; // label for the selected neuron
    private List<string> allLabels = new List<string>();
    private List<Vector3> allLabelPos = new List<Vector3>();
    private List<GameObject> neuronObjects = new List<GameObject>();
    private int[,] signalData = null;
    private Coroutine signalStepCoroutine = null;
    private bool showLabel = false;  // Control whether to show the label

    private float FOVdefault = 60f;
    private float FOVmax =40f;


    public float NeuronSize = 5.0f;
    private bool isSignalDataLoaded = false;
    private bool isExportingVideo = false;
    private string currentFishName = "";

    private int currentSignalTimestamp = -1;
    private bool isPaused = false; // Tracks whether the step-through is paused

    public ComputeShader neuronColourComputeShader; // Reference to the compute shader
    private ComputeBuffer binaryDataBuffer; // Buffer for binary data
    private ComputeBuffer neuronColoursBuffer; // Buffer for neuron colours


    private Color baseColor;   // this is now set from datafile depending on region
    private Color mutatedColor; // changing this to private since its set for each neuron
    public ColorMutator colorMutator; // The cool thing is that the ColorMutator class has its own inspector in the Unity Editor, so you can see the changes in real-time!
    
    // Because this script is set to "ExecuteAlways", and Awake() gets called every time the script is loaded
    // We need to check if we already retreived the material from the object
    // GetComponent() is a very expensive operation in Unity, so we want to avoid it if we can!
    // Always cache your component after the first time you get it in Start() or Awake()!
    void Awake()
    {
            glowMaterial.EnableKeyword("_EMISSION"); // Apparently very important: https://discussions.unity.com/t/setting-emission-color-programatically/152813/2
            baseColor = glowMaterial.GetColor("_EmissionColor");
            mutatedColor = glowMaterial.GetColor("_EmissionColor");
            Application.runInBackground = true;
        
    }
    // end of Gabriele's most excellent code ========================

        void Start()
    {
            // Load the neuron positions and colours from the CSV files
            DirectoryInfo dir = new DirectoryInfo(dataFolder);

            brain = new GameObject("Brain"); // Create a parent object for all neurons
            // Create a parent object for all regions
            foreach (string regionName in regionNames)
            {
                GameObject region = new GameObject(regionName);
                Debug.Log(regionName + " created");
                // add dictionary entry for region
                regionTransforms[regionName] = region.transform;
                // Set the parent of the region to the brain object
                region.transform.SetParent(brain.transform, false);
            };

            availableSignalFiles = Directory.GetFiles(dataFolder, "ZF.FishSignalData*.csv")
                                .Select(Path.GetFileName)
                                .ToList();

            List<Color> sColours = new List<Color>();
            List<string> sParents = new List<string>();
    
            // Load the neuron positions and colors from the CSV file
            List<Vector3> spherePositions = LoadNeuronalData(postionsFile, out sColours, out sParents, out allLabels, out allLabelPos);
            // create neurons in 3d space
            CreateNeurons(spherePositions, sColours, sParents);

            // Calculate the center of all spheres
            Vector3 center = CalculateCenter(spherePositions);
            centerPoint = center;

            // Calculate the bounding box that encompasses all neurons
            Bounds bounds = CalculateBounds(spherePositions);
            float labelRadius = bounds.extents.magnitude;

            // Position the camera to look at the center and adjust its distance to encompass all neurons
            returnCameraPos = PositionCamera(center, bounds);
            radius = Vector3.Distance(returnCameraPos, centerPoint);
            mainCamera.fieldOfView = FOVdefault;
            
            // Start loading signal data
            StartCoroutine(LoadSignalDataAsync(postionsFile, (signalData) =>
            {

                isSignalDataLoaded = true;
                // Initialize compute buffers
                int numNeurons = signalData.GetLength(0);
                int numTimestamps = signalData.GetLength(1);

                binaryDataBuffer = new ComputeBuffer(numNeurons*numTimestamps, sizeof(int));
                neuronColoursBuffer = new ComputeBuffer(numNeurons, sizeof(float) * 4);

                Debug.Log($"Compute buffers initialized for {numNeurons} neurons.");
            }));

    }

void CreateNeurons(List<Vector3> spherePositions, List<Color> sColours, List<string> sParents)
    {
        Debug.Log("Creating neurons in 3D space...");
        neuronObjects.Clear(); // Ensure the list is empty before populating
        // Create spheres at the specified positions
        for (int i = 0; i < spherePositions.Count; i++)
        {

            Vector3 position = spherePositions[i];
            Color colour = sColours[i];
            string parent = sParents[i];

            // Create a new GameObject for each neuron
            GameObject neuron = new GameObject($"Neuron_{i}");
            //var mat = new Material(glowMaterial); // delete this is just for troubleshooting
            neuron.AddComponent<MeshFilter>().mesh = sphereMesh; // Assign the sphere mesh
            neuron.AddComponent<MeshRenderer>().material = glowMaterial; // Assign the material
            neuron.AddComponent<SphereCollider>(); // Add a sphere collider for interaction
            neuron.transform.position = position;
            neuron.transform.localScale = new Vector3(NeuronSize, NeuronSize, NeuronSize); // Scale the sphere to an initial size
            neuron.GetComponent<Renderer>().material.SetColor("_EmissionColor", colour);
            neuron.GetComponent<Renderer>().material.SetColor("_BaseColor", colour);

            // Set the parent of the neuron to the region transform
            if (regionTransforms.TryGetValue(parent, out Transform regionTransform))
            {
                neuron.transform.SetParent(regionTransform, false);
            }
            else
            {
                // Set the parent to the "None" region if the specified region is not found
                neuron.transform.SetParent(regionTransforms["None"], false);
            }
            neuronObjects.Add(neuron); // Add to the list
        
        }
        Debug.Log("Finsihed creating neurons in 3D space...");
    }

    // Update is called once per frame
    void Update()
    {    
            // Arrow key camera movement (pan and rotate)
        float panSpeed = 10f; 
        float rotDegrees = 10f; 

        // Check for input and rotate accordingly
        if (Input.GetKey(KeyCode.W)) // Rotate up
        {
            JumpCameraAroundCentroid(Vector3.right, rotDegrees); // Rotate around the X-axis
        }
        if (Input.GetKey(KeyCode.S)) // Rotate down
        {
            JumpCameraAroundCentroid(Vector3.left, rotDegrees);  // Rotate 45' around the X-axis (opposite direction)
        }
        if (Input.GetKey(KeyCode.A)) // Rotate 45' left
        {
            JumpCameraAroundCentroid(Vector3.up, rotDegrees); // Rotate 45' around the Y-axis
        }
        if (Input.GetKey(KeyCode.D)) // Rotate 45'right
        {
            JumpCameraAroundCentroid(Vector3.down, rotDegrees); // Rotate around the Y-axis (opposite direction)
        }

        // Check for input and return camera to initial position
        if (Input.GetKey(KeyCode.R)) // Return to initial position
        {
            transform.position = returnCameraPos;
            transform.LookAt(centerPoint);
            mainCamera.fieldOfView = FOVdefault;
            UnselectAllNeurons();
        }
        

    if (Input.GetKey(KeyCode.LeftArrow))
    {
        // Move camera left relative to current view
        transform.position += -mainCamera.transform.right * panSpeed * Time.deltaTime;
        centerPoint += -mainCamera.transform.right * panSpeed * Time.deltaTime;
        transform.LookAt(centerPoint);
    }
    if (Input.GetKey(KeyCode.RightArrow))
    {
        // Move camera right relative to current view
        transform.position += mainCamera.transform.right * panSpeed * Time.deltaTime;
        centerPoint += mainCamera.transform.right * panSpeed * Time.deltaTime;
        transform.LookAt(centerPoint);
    }
    if (Input.GetKey(KeyCode.UpArrow))
    {
        // Move camera up relative to current view
        transform.position += mainCamera.transform.up * panSpeed * Time.deltaTime;
        centerPoint += mainCamera.transform.up * panSpeed * Time.deltaTime;
        transform.LookAt(centerPoint);
    }
    if (Input.GetKey(KeyCode.DownArrow))
    {
        // Move camera down relative to current view
        transform.position += -mainCamera.transform.up * panSpeed * Time.deltaTime;
        centerPoint += -mainCamera.transform.up * panSpeed * Time.deltaTime;
        transform.LookAt(centerPoint);
    }

                // zoom in
        if (Input.GetKey(KeyCode.Z)) // Zoom in towards centre
        {
            transform.position = Vector3.MoveTowards(transform.position, centerPoint, cameraSpeed);
            transform.LookAt(centerPoint);
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, FOVmax, cameraSpeed * .01f);

        }

        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            SelectNeuron();
        }
 
    } // end of update

    List<Vector3> LoadNeuronalData(string fname,
                out List<Color> sColours, out List<string> sParents,
                out List<string> sLabels, out List<Vector3> sLabelPos)
    {
        Debug.Log("Loading neuron positions and colors from: " + fname);
        List<Vector3> sPositions = new List<Vector3>();
        sColours = new List<Color>();
        sParents = new List<string>();
        sLabels = new List<string>();
        sLabelPos = new List<Vector3>();


        // Combine the directory path with the filename
        string fullPath = Path.Combine(dataFolder, fname);

        // Check if the file exists
        if (!File.Exists(fullPath))
        {
            Debug.LogError("File not found: " + fullPath);
            return sPositions;
        }

        Debug.Log("Loading file: " + fullPath);
        StreamReader reader = new StreamReader(fullPath);

        // Read the header line and ensure it matches the expected format
        string headerLine = reader.ReadLine();
        string[] headers = headerLine.Split(','); // Assuming comma-separated values
        if (headers.Length < 10 ||
            headers[0].Trim() != "# SWC Index" ||
            headers[1].Trim() != "SWC Type" ||
            headers[2].Trim() != "xpos" ||
            headers[3].Trim() != "ypos" ||
            headers[4].Trim() != "zpos" ||
            headers[5].Trim() != "radius" ||
            headers[6].Trim() != "SWC Parent" ||
            headers[7].Trim() != "Region" ||
            headers[8].Trim() != "Subregion" ||
            headers[9].Trim() != "Label")
        {
            Debug.LogError("Unexpected CSV format: " + headerLine);
            return sPositions;
        }

        string line;
        // Read the rest of the file line by line
        while ((line = reader.ReadLine()) != null)
        {
            string[] values = line.Split(','); // Assuming comma-separated values

            // Skip rows with fewer than 10 columns
            if (values.Length < 10)
            {
                Debug.LogWarning("Skipping malformed row: " + line);
                continue;
            }

            // Parse position data
            if (float.TryParse(values[2], out float xPosition) &&
                float.TryParse(values[3], out float yPosition) &&
                float.TryParse(values[4], out float zPosition))
            {
                Vector3 position = new Vector3(xPosition, yPosition, zPosition);
                sPositions.Add(position);
            }
            else
            {
                Debug.LogWarning("Invalid position data: " + line);
                continue;
            }

            // Read in parent gameobject aka brain region
            string parent = values[6].Trim();
            sParents.Add(parent);
            // Read in region and subregion
            string regionList = values[7].Trim();
            string subregionList = values[8].Trim();
            // Use the regionColours dictionary to parse the color
            if (regionColours.TryGetValue(parent, out Color colour))
            {
                sColours.Add(colour);
            }
            // Construct the label text
            string labelTxt = $"Regions: {cleanList(regionList)}\nSubregions: {cleanList(subregionList)}";
            sLabels.Add(labelTxt);
            // Calculate label position
            Vector3 labelPos = SetLabelPos(new Vector3(xPosition, yPosition, zPosition), 1.0f); // Adjust label radius as needed
            sLabelPos.Add(labelPos);
        } //end of while looping line by line

        Debug.Log("Found " + sPositions.Count + " neurons");
        reader.Close();
        return sPositions;
    }


// Add this coroutine to handle loading and message update
private IEnumerator LoadAndShowMessage(string filename)
{
    // Clear and load neurons
    ClearNeurons();
    List<Color> sColours;
    List<string> sParents;
    List<string> sLabels;
    List<Vector3> sLabelPos;
    List<Vector3> spherePositions = LoadNeuronalData(filename, out sColours, out sParents, out sLabels, out sLabelPos);
    CreateNeurons(spherePositions, sColours, sParents);

    Vector3 center = CalculateCenter(spherePositions);
    centerPoint = center;
    Bounds bounds = CalculateBounds(spherePositions);
    float labelRadius = bounds.extents.magnitude;
    returnCameraPos = PositionCamera(center, bounds);
    radius = Vector3.Distance(returnCameraPos, centerPoint);
    mainCamera.fieldOfView = FOVdefault;

    // Start loading signal data
    yield return StartCoroutine(LoadSignalDataAsync(filename, (signalData) =>
    {
        isSignalDataLoaded = true;
        int numNeurons = signalData.GetLength(0);
        int numTimestamps = signalData.GetLength(1);

        binaryDataBuffer = new ComputeBuffer(numNeurons * numTimestamps, sizeof(int));
        neuronColoursBuffer = new ComputeBuffer(numNeurons, sizeof(float) * 4);

        Debug.Log($"Compute buffers initialized for {numNeurons} neurons.");
    }));

    loadingMessage = $"Loaded {filename}";
    yield return new WaitForSeconds(1.5f); // Show "Loaded" message for 1.5 seconds
    loadingMessage = null;
    showSignalFileDropdown = false; // Hide dropdown after message is done
}
    IEnumerator LoadSignalDataAsync(string fname, Action<int[,]> onComplete)
    {
        Debug.Log("Loading signal data asynchronously from: " + fname);

        string fullPath = Path.Combine(dataFolder, fname);
        if (!File.Exists(fullPath))
        {
            Debug.LogError("File not found: " + fullPath);
            onComplete?.Invoke(null);
            yield break;
        }

        int numRows = 0;
        int numCols = 0;

        // First pass: get number of columns and rows
        using (StreamReader reader = new StreamReader(fullPath))
        {
            string headerLine = reader.ReadLine(); // skip header
            string firstDataLine = reader.ReadLine();
            if (firstDataLine == null)
            {
                Debug.LogError("Empty file: " + fullPath);
                onComplete?.Invoke(null);
                yield break;
            }
            string[] firstData = firstDataLine.Split(',');
            numCols = firstData.Length - 10; // 10 metadata columns

            numRows = 1; // Count the first data line
            while (reader.ReadLine() != null)
                numRows++;
        }

        Debug.Log($"Detected {numRows} rows and {numCols} signal columns in {fname}");
            // Second pass: read the data

        signalData = new int[numRows, numCols];

        if (!File.Exists(fullPath))
        {
            Debug.LogError("File not found: " + fullPath);
            onComplete?.Invoke(signalData);
            yield break;
        }

        using (StreamReader reader = new StreamReader(fullPath))
        {
            string line;
            int rowIx = 0;
            // Skip header
            reader.ReadLine();
            // Read the rest of the file line by line
            while ((line = reader.ReadLine()) != null && rowIx < numRows)
            {
            string[] values = line.Split(',');
            for (int colIx = 10; colIx < 10 + numCols && colIx < values.Length; colIx++)
            {
                if (float.TryParse(values[colIx], out float value))
                {
                    signalData[rowIx, colIx - 10] = value > 0 ? 1 : 0;
                }
            }
            rowIx++;

            // Yield every 100 rows to keep UI responsive
            if (rowIx % 100 == 0)
                yield return null;
            }
        }

        Debug.Log("Signal data loaded.");
        onComplete?.Invoke(signalData);
    }// end of LoadSignalData
        
    int[] splitString(string mystr)
    {
        // Use a regular expression to match all sequences of digits
        MatchCollection matches = Regex.Matches(mystr, @"\d+");

        // Convert the matches to an array of integers
        return matches.Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
    }

    string cleanList(string mystr)
    {
      // strip out the enclosing brackets and quote marks
        mystr = mystr.Trim('[', ']', '"', '\'');
        return mystr;
    }

    Vector3 SetLabelPos(Vector3 neuronPos, float labelRadius)
    {
            // Calculate the direction from the center to the neuron position
            // normalize so it has magnitude of 1
            Vector3 direction = (neuronPos - centerPoint).normalized;

            // Scale the direction by the bounding box size to ensure the label is outside the neurons
            Vector3 newPos = centerPoint + (direction * labelRadius);

            return newPos;
    }

    // Calculate the center of all neurons
    Vector3 CalculateCenter(List<Vector3> positions)
    {
        Vector3 center = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            center += pos;
        }
        center /= positions.Count;
        return center;
    }

    // Calculate the bounding box that encompasses all neurons
    Bounds CalculateBounds(List<Vector3> positions)
    {

        Bounds bounds = new Bounds(positions[1], Vector3.zero);

        foreach (Vector3 pos in positions)
        {   
            bounds.Encapsulate(pos);
        }
        return bounds;
    }

    // Position the camera to look at the center and adjust its distance to encompass all neurons
    Vector3 PositionCamera(Vector3 center, Bounds bounds)
    {
        mainCamera.transform.position = center - new Vector3(0, 0, bounds.size.magnitude*.9f); // adjust by factor .9 to be a little closer
        mainCamera.transform.LookAt(center);
        //mainCamera.orthographicSize = bounds.size.magnitude / 2;

        return mainCamera.transform.position;
    }

    void RotateCamera(Vector3 axis)
    {
        // Rotate the camera around the center point
        transform.RotateAround(centerPoint, axis, cameraSpeed * Time.deltaTime);

        // Keep the camera at the specified radius from the center point
        // Vector3 offset = transform.position - centerPoint;
        //offset = offset.normalized * radius;
        //transform.position = centerPoint + offset;

        // Ensure the camera is always looking at the center point
        transform.LookAt(centerPoint);
    }
    void JumpCameraAroundCentroid(Vector3 axis, float angleDegrees)
    {
        // Vector from centroid to camera
        Vector3 offset = transform.position - centerPoint;
        // Rotate the offset
        Quaternion rotation = Quaternion.AngleAxis(angleDegrees, axis);
        Vector3 newOffset = rotation * offset;
        // Set new position
        transform.position = centerPoint + newOffset;
        // Look at centroid
        transform.LookAt(centerPoint);
    }

    void OnGUI()
{
    if (isSignalDataLoaded)
        ShowSignalTicker();
        
    if (isExportingVideo) // Hide GUI while exporting
            return; 

    // Display the camera's position on the screen
    Vector3 cameraPosition = mainCamera.transform.position;
    GUI.Label(new Rect(10, 10, 300, 20), $"Camera Position: {cameraPosition}");
    GUI.Label(new Rect(10, 30, 300, 20), $"Z to zoom in, R to reset camera, W-A-S-D to rotate");

        // Display the label if showLabel is true
    if (showLabel)
    {
        GUI.Box(new Rect(10, 80, 300, 100), neuronLabel); // Display the label in a box
    }


    if (showSignalFileDropdown && availableSignalFiles.Count > 0)
    {
        GUI.Label(new Rect(250, 10, 200, 20), "Select a signal data file:");

        float dropdownX = 250;
        float dropdownY = 35;
        float dropdownWidth = 250;
        float dropdownHeight = 25;
        float listItemHeight = 25;
        int numFiles = availableSignalFiles.Count;

        if (!string.IsNullOrEmpty(loadingMessage))
        {
            // Draw a box over the whole dropdown area (button + list)
            float boxHeight = dropdownHeight + (showDropdownList ? numFiles * listItemHeight : 0);
            GUI.Box(new Rect(dropdownX, dropdownY, dropdownWidth, boxHeight), "");
            GUI.Label(new Rect(dropdownX + 10, dropdownY + 5, dropdownWidth - 20, dropdownHeight), loadingMessage);
        }
        else
        {
            // Dropdown button
            if (GUI.Button(new Rect(dropdownX, dropdownY, dropdownWidth, dropdownHeight), availableSignalFiles[selectedSignalFileIndex]))
            {
                showDropdownList = !showDropdownList;
            }
            // Show dropdown list
            if (showDropdownList)
            {
                for (int i = 0; i < numFiles; i++)
                {
                    if (GUI.Button(new Rect(dropdownX, dropdownY + dropdownHeight + i * listItemHeight, dropdownWidth, listItemHeight), availableSignalFiles[i]))
                    {
                        selectedSignalFileIndex = i;
                        postionsFile = availableSignalFiles[i];
                        showDropdownList = false;
                        loadingMessage = $"Loading {postionsFile}...";

                        // Start loading the selected file immediately
                        StartCoroutine(LoadAndShowMessage(postionsFile));
                    }
                }
            }
        }
                // add button to export videos for all fish
        {
            if (signalStepCoroutine != null)
                StopCoroutine(signalStepCoroutine);
            signalStepCoroutine = StartCoroutine(ExportAllFishVideos());
        }
        return; // Don't draw the rest of the GUI until a file is loaded
    }
        // Add a button to start stepping through signal data
        if (isSignalDataLoaded)
        {
            ShowSignalTicker();

            if (GUI.Button(new Rect(10, 200, 200, 30), "Step Through Signal Data"))
            {
                UnselectAllNeurons();
                isPaused = false;
                if (signalStepCoroutine != null)
                    StopCoroutine(signalStepCoroutine); signalStepCoroutine = StartCoroutine(StepThroughSignalDataFrom(signalData, 0));
            }

            // Add a button to pause or resume stepping through signal data
            if (GUI.Button(new Rect(10, 300, 200, 30), isPaused ? "Resume Signal Data" : "Pause Signal Data"))
            {
                isPaused = !isPaused; // Toggle the pause state
            }
            if (GUI.Button(new Rect(10, 350, 200, 30), "Export Video of Signal Data"))
            {
                if (signalStepCoroutine != null)
                    StopCoroutine(signalStepCoroutine);
                signalStepCoroutine = StartCoroutine(ExportSignalDataVideo(signalData, currentFishName, 0));
            }
            
 }

    // show signal timestamp as text as well while stepping through
    if (currentSignalTimestamp >= 0)
    {
        GUI.Label(new Rect(10, 240, 300, 20), $"Signal Timestamp: {currentSignalTimestamp/2} s");
    }

}
void ShowSignalTicker()
{
    if (signalData == null || signalData.GetLength(0) == 0 || signalData.GetLength(1) == 0)
        return; // No signal data to display

    // Calculate dimensions for the ticker
    int numTimestamps = signalData.GetLength(1);
    float screenWidth = Screen.width;
    float tickerY = Screen.height - 40;
    float tickerHeight = 8f;
    float tickerMargin = 20f;
    float tickerWidth = screenWidth - 2 * tickerMargin;

    // Draw the ticker line
    GUI.Box(new Rect(tickerMargin, tickerY, tickerWidth, tickerHeight), GUIContent.none);

    // Handle mouse click on ticker to jump to timestamp and resume
    Event e = Event.current;
    Rect tickerRect = new Rect(tickerMargin, tickerY, tickerWidth, tickerHeight + 20);
    if (e.type == EventType.MouseDown && tickerRect.Contains(e.mousePosition))
    {
        float clickFraction = (e.mousePosition.x - tickerMargin) / tickerWidth;
        int clickedTimestamp = Mathf.Clamp(Mathf.RoundToInt(clickFraction * (numTimestamps - 1)), 0, numTimestamps - 1);
        JumpToSignalTimestamp(clickedTimestamp);

        // Resume playback from the clicked timestamp
        isPaused = false;
        if (signalStepCoroutine != null)
            StopCoroutine(signalStepCoroutine);
        signalStepCoroutine = StartCoroutine(StepThroughSignalDataFrom(signalData, clickedTimestamp));
    }

    // Draw the current timestamp marker
    if (currentSignalTimestamp >= 0)
    {
        float fraction = (float)currentSignalTimestamp / (numTimestamps - 1);
        float markerX = tickerMargin + fraction * tickerWidth;
        GUI.Box(new Rect(markerX - 2, tickerY - 10, 4, tickerHeight + 20), GUIContent.none);
        GUI.Label(new Rect(markerX - 5, tickerY - 30, 50, 20), $"{currentSignalTimestamp / 2}");
    }

    // Show first and last timestamps
    GUI.Label(new Rect(tickerMargin, tickerY + 12, 50, 50), "0 secs");
    GUI.Label(new Rect(screenWidth - tickerMargin - 50, tickerY + 12, 50, 50), $"{numTimestamps / 2} secs");
}

// Update neuron colors to selected timestamp
void JumpToSignalTimestamp(int timestamp)
{
    if (signalData == null) return;
    int numNeurons = signalData.GetLength(0);
    int numTimestamps = signalData.GetLength(1);
    if (timestamp < 0 || timestamp >= numTimestamps) return;

    currentSignalTimestamp = timestamp;
    for (int neuron = 0; neuron < numNeurons; neuron++)
    {
        float value = signalData[neuron, timestamp];
        GameObject neuronObj = GameObject.Find($"Neuron_{neuron}");
        if (neuronObj != null)
        {
            Color colorToSet = value > 0 ? Color.red : Color.blue;
            neuronObj.GetComponent<Renderer>().material.SetColor("_EmissionColor", colorToSet);
            neuronObj.GetComponent<Renderer>().material.SetColor("_BaseColor", colorToSet);
        }
    }
}

void HighlightNeuron(GameObject neuron)
{
    // Get the parent transform of the neuron if needing to highlight the whole region which we're not doing right now
    Transform neuronsRegion = neuron.transform.parent;
    Debug.Log("region is "+neuronsRegion.name+" for "+neuron.name);

    // Reset the last selected neuron if its different
    if (lastSelectedNeuron != null && lastSelectedNeuron != neuron)
    {
        // Reset the scale for the last selected neuron
        lastSelectedNeuron.transform.localScale = new Vector3(NeuronSize, NeuronSize, NeuronSize); // Reset scale for each child
        // reset intensity of glow
        glowMaterial.SetColor("_EmissionColor", Color.yellow * 1.0f);
    }
    // scale up size and glow of selected neuron
    // neuron.localScale = new Vector3(NeuronSize*2, NeuronSize*2, NeuronSize*2); // Reset scale for each child
    // reset intensity of glow
    glowMaterial.SetColor("_EmissionColor", Color.yellow * 2.0f);

    // Update the last selected neuron
    lastSelectedNeuron = neuron;
    lastSelectedRegion = neuronsRegion;

    } // end of HighlightNeuron

    void SelectNeuron()
    {
        // Create a ray from the camera to the mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Perform the raycast
        if (Physics.Raycast(ray, out hit))
        {
            GameObject selectedNeuron = hit.collider.gameObject;
            // Pop a label for the selected neuron
            PopUpNeuronLabel(selectedNeuron);
            HighlightNeuron(selectedNeuron);
            Debug.Log($"Selected Neuron: {selectedNeuron.name}");
        }
    }

    void UnselectAllNeurons()
        {
            // Iterate through regions  
            foreach (Transform region in brain.transform)
            {
                // reset scale of all neuroneach neuron (child of region)
                foreach (Transform neuron in region)
                {
                    neuron.localScale = new Vector3(NeuronSize, NeuronSize, NeuronSize); // Reset scale for each child
                    // reset intensity of glow
                    glowMaterial.SetColor("_EmissionColor", Color.yellow * 1.0f);
                }
            }
        }

    // function to pop up neuron label for the selected neuron which is not yet in use
    void PopUpNeuronLabel(GameObject neuron)
    {

        // Get the neuron index from the name
        int neuronIndex = int.Parse(neuron.name.Split('_')[1]);

        neuronLabel = $"Neuron: {neuron.name}\nPosition: {neuron.transform.position}\n";
        neuronLabel +=allLabels[neuronIndex];
        neuronLabel += $"\nColour: {neuron.GetComponent<Renderer>().material.GetColor("_BaseColor")}";
        
        showLabel = true; // Enable the label display
    }

    IEnumerator StepThroughSignalDataFrom(int[,] signalData, int startTimestamp)
    {
        int numNeurons = signalData.GetLength(0);
        int numTimestamps = signalData.GetLength(1);

        for (int col = startTimestamp; col < numTimestamps; col++)
        {
            while (isPaused)
                yield return null;

            lastTime = Time.time;
            currentSignalTimestamp = col;
            for (int neuron = 0; neuron < numNeurons; neuron++)
            {
                int binaryValue = signalData[neuron, col];
                if (neuron < neuronObjects.Count && neuronObjects[neuron] != null)
                {
                    Color colorToSet = binaryValue == 1 ? Color.yellow : Color.blue;
                    var renderer = neuronObjects[neuron].GetComponent<Renderer>();
                    renderer.material.SetColor("_EmissionColor", colorToSet);
                    renderer.material.SetColor("_BaseColor", colorToSet);
                }
            }
            lastDelta = Time.time - lastTime;
            yield return new WaitForSeconds(0.01f);
        }
        currentSignalTimestamp = -1;
        signalStepCoroutine = null;
    }

    // Method to extract fish number
    private int GetFishNumber(string filename)
    {
        Match match = Regex.Match(filename, @"FishSignalData(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
        {
            return number;
        }
        return -1; // Return -1 or throw an exception if no valid number found
    }

    private IEnumerator ExportAllFishVideos()
    {
        isExportingVideo = true; // Hide GUI during batch export

        foreach (string csvFile in availableSignalFiles)
        {
            // if fishnumber is < 38 then skip it
            int fishNumber = GetFishNumber(csvFile);
            Debug.Log("Processing file: " + csvFile + " with fish number: " + fishNumber);
            if (fishNumber > 37)
            {
                // Extract fish name/id from filename (e.g., ZF.FishSignalData07_20250530_141502.csv -> Fish07)
                var match = System.Text.RegularExpressions.Regex.Match(csvFile, @"FishSignalData(\d+)");
                if (match.Success)
                    currentFishName = "Fish" + match.Groups[1].Value;
                else
                    currentFishName = Path.GetFileNameWithoutExtension(csvFile);
                loadingMessage = $"Loading {csvFile} for fish {currentFishName} ...";
                yield return StartCoroutine(LoadAndShowMessage(csvFile));

                // Wait for signal data to be loaded
                while (!isSignalDataLoaded)
                    yield return null;

                loadingMessage = $"Exporting video for {currentFishName}...";
                yield return StartCoroutine(ExportSignalDataVideo(signalData, currentFishName, 0));
            }
            else
            {
                Debug.Log($"Skipping fish {fishNumber} as it is below the threshold.");
            }
        }

        loadingMessage = "All videos exported!";
        yield return new WaitForSeconds(2f);
        loadingMessage = null;
        isExportingVideo = false;
    } // end of ExportAllFishVideos


    IEnumerator ExportSignalDataVideo(int[,] signalData,string fishID, int startTimestamp)
    {
        isExportingVideo = true; // Hide GUI

        int numNeurons = signalData.GetLength(0);
        int numTimestamps = signalData.GetLength(1);

        string folder = Path.Combine(Application.dataPath, "SignalDataFrames/Signals_"+fishID, Path.GetFileNameWithoutExtension(postionsFile) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(folder);

        for (int col = startTimestamp; col < numTimestamps; col++)
        {
            lastTime = Time.time;
            currentSignalTimestamp = col;
            for (int neuron = 0; neuron < numNeurons; neuron++)
            {
                int binaryValue = signalData[neuron, col];
                if (neuron < neuronObjects.Count && neuronObjects[neuron] != null)
                {
                    Color colorToSet = binaryValue == 1 ? Color.yellow : Color.blue;
                    var renderer = neuronObjects[neuron].GetComponent<Renderer>();
                    renderer.material.SetColor("_EmissionColor", colorToSet);
                    renderer.material.SetColor("_BaseColor", colorToSet);
                }
            }

            // Wait for rendering to finish
            yield return new WaitForEndOfFrame();

            // Capture the frame
            string filename = Path.Combine(folder, $"{fishID}_{col:D05}.png");
            ScreenCapture.CaptureScreenshot(filename);

            // Optionally, wait a bit to ensure file is written (not strictly necessary)
            yield return new WaitForSeconds(0.01f);
        }

        Debug.Log($"Frames exported to {folder}. Use ffmpeg or similar to create a video.");
        currentSignalTimestamp = -1;
        signalStepCoroutine = null;
        isExportingVideo = false;
    }

// Utility to clear existing neurons and release buffers
    void ClearNeurons()
    {

        if (binaryDataBuffer != null)
        {
            binaryDataBuffer.Release();
            binaryDataBuffer = null;
        }
        if (neuronColoursBuffer != null)
        {
            neuronColoursBuffer.Release();
            neuronColoursBuffer = null;
        }

        foreach (var neuron in neuronObjects)
        {
            if (neuron != null)
                Destroy(neuron);
        }
        neuronObjects.Clear();
        if (brain != null)
        {
            foreach (Transform region in brain.transform)
            {
                foreach (Transform child in region)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    } // end of ClearNeurons

    void OnDestroy()
    {
        if (binaryDataBuffer != null)
        {
            binaryDataBuffer.Release();
            binaryDataBuffer = null;
        }
        if (neuronColoursBuffer != null)
        {
            neuronColoursBuffer.Release();
            neuronColoursBuffer = null;
        }
    }
} // end of class. class dismissed.

