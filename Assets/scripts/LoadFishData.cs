using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEditor;
using UnityEngine.UI;

using BrainComponents;
using Unity.Mathematics;

public class LoadFishData : MonoBehaviour
{

    /* 
    MDHS-MDAP Collaboration showing the intricacies of the zebrafish brain
    Authors: Amanda Belton, Wei Quin and Ethan Scott
    Last updated: 30 Oct 2025

    This script loads neuronal data from a chosen CSV file, creates 3D representations of neurons in Unity
    This neuronal data is captured by the Scott Lab at the University of Melbourne, Australia

    Todos: 
        Load new files from Wei eg fish61_signal.csv
        Convert inspector settings for framerate and nbrFrames to show into menupanel
        - show start + end timestamps as marker is moved
        Set views to show feature
        View name shown in viewport ui overlay
        Set colour of endMarker to different to start marker (gray?)
    Later us:
        Set view to show feature set or all neurons from IDE
        Split filtered and all (two brains and 6 cameras) ShowAllExcitedNeurons();
        Signal data isn't binary anymore
        Show region and subregion in seizure details
        forward and back buttons to 2 secs before start of peak
        Pause button
    Consider:
        Do each seizure for the same fish, same region have similar patterns
         - python glue together 6 seizures to compare
         - ptyho glue together seizure timestamps csv config 
        Select all regions to default back to show entire brain
        2 sec delay between views of the same region?
        3/4 view from main camera
        Pause, rewind, slow motion and fast forward buttons
        Show time stamp in seizure details
        Show tail movement video synced to seizure timestamp
    Done:

        Rotating brain (brain 2) (whole brain all regions) in maincamera (start with dorsal view)
        Show featureset only (Brain1)
        Generate stills for video
        Navigate to peaks or  Navigate using slider
        * Bug Telencephalon fish14
        * Bug Rhombencephalon fish05
        -- Create duplicate brains for feature sets 
        -- davinci resolve for video
        Generate frames for each seizure timstamps 10 sec before and after peak
        * Add ability to select multiple regions
        3 views
        Revamped csv position data file
        Show views
        select region
        Show peaks, 
        show line graph for nbr neurons activated
        Set initial camera positions and viewports
        Show regions and subregions
        Show fish seizure data from a chosen file
    */

    // variables for panel in editor
    [Header("Fish Data")]
    [Header("Data Sources")]
    [Tooltip("The folder path containing data files")]
    public string dataFolder = "Assets/Data";

    [Tooltip("The filename of the CSV file containing position data")]
    public string postionsFile = "ZF.calcium_position_data.csv";

    [Tooltip("The selected fish to load")]
    public string selectedFish = "";

    [Tooltip("The selected region to load")]
    public string selectedRegion = "";
    public string wholeBrain = "Brain0"; // default brain for all neurons

    [Header("Animation")]
    [Tooltip("The time interval between each step in the seizure data animation")]
    public float animationStepInterval = 0.001f;

    [Tooltip("Number of frames to export from marker timestamp")]
    public int nbrFrames = 300;

    [Tooltip("Rotation speed of brain during seizure data animation")]
    public float rotationSpeed = 20f;

    [Tooltip("Start position of line graph showing total signal for timestamp")]
    public Vector3 szLeftPos;
    [Tooltip("End position of line graph showing total signal for timestamp")]
    public Vector3 szRightPos;
    [Header("Export Settings")]
    [Tooltip("Width for exported frames")]
    public int exportWidth = 1920;

    [Tooltip("Height for exported frames")]
    public int exportHeight = 1080;

    [Header("Neuron Aesthetics")]
    [Tooltip("Material used for the neuronal glowing effect from seizure data")]
    public Material glowMaterial;

    [Tooltip("Material used for inactive neurons")]
    public Material dullMaterial;
    [Tooltip("Size of inactive neurons")]
    [Range(1.0f, 10.0f)]
    public float inactiveNeuronSize = 3.0f;

    [Tooltip("Size of active neurons")]
    [Range(1.0f, 10.0f)]
    public float activeNeuronSize = 4.0f;

    [Header("Scene References")]
    [Tooltip("Titles and video viz elements")]
    public GameObject videoTextElements;

    [Tooltip("Reference to the main camera in the scene")]
    public CameraHandler cameraHandler;

    [Tooltip("Reference to the sphere mesh used for visualisation")]
    public Mesh sphereMesh;


    [Tooltip("Parent objects for all neurons ie Brain 1")]
    public Vector3 brainPos = new Vector3(200, 200, 500); // Brain position in the scene (default for all neurons)

    [Tooltip("Distance between brains when showing featureset brains")]
    public int distBtwnBrains = 1000; // Distance between brains when showing featureset brains

    [Tooltip("Reference to the UIHandler (ChooseFish) obect")]
    public UIHandler uiHandler; // Assign in Inspector

    [Tooltip("Show status message in the scene")]
    public TMPro.TextMeshProUGUI statusMessage;

    [Tooltip("Show seizure details")]
    public TMPro.TextMeshProUGUI seizureDetails;
    public GameObject progressBar;
    public Image progressBarFill;
    public TMPro.TextMeshProUGUI progressBarText;

    private GameObject brainParent; // Parent object for all brains

    private List<BrainData> brains = new List<BrainData>();

    private List<string> availableSignalFiles = new List<string>();

    Dictionary<string, Color> regionColours = new Dictionary<string, Color>
    {
        { "Diencephalon", new Color(0.4f, 1f, 0.3843f) }, // RGB: (127,255,98) greenish
        { "Mesencephalon", new Color(0.4588f, 0.5529f, 0.3843f) }, // RGB: (117, 141, 98) orangish
        { "Rhombencephalon", new Color(0f, 0.6275f,1f) }, // RGB: (0,126,255) bluish
        { "Telencephalon", new Color(1f, 0.5412f, 0.7647f) }, // RGB: (255, 41, 138) pinkish
        { "Ganglia", new Color(0.4588f, 0.4392f, 0.7019f) }, // Default to dark RGB: (117,112,179)
        { "Spinal", new Color(0.4588f, 0.4392f, 0.7019f) }, // Default to dark RGB: (117,112,179)
        { "None", new Color(0.4588f, 0.4392f, 0.7019f) } // Default to dark RGB: (117,112,179)
    };

    private Dictionary<string, Color> featureColoursDict = new Dictionary<string, Color>();
    private Dictionary<int, Color> featuresetColourList = new Dictionary<int, Color>
    {
        { 1, new Color32(0xfc, 0xfc, 0x56, 0xff) }, // Yellow (#fcfc56)
        { 2, new Color32(0xa0, 0xff, 0x8e, 0xff) }, // Light Green (#a0ff8e)
        { 3, new Color32(0xfc, 0x88, 0x56, 0xff) }, // Orange (#fc8856)
        { 4, new Color32(0xfa, 0x7f, 0xff, 0xff) }  // Light Purple (#fa7fff)
    };

    private Dictionary<string, string> fishFileDict = new Dictionary<string, string>();

    private List<string> allLabels = new List<string>();
    //private List<GameObject> neuronObjects = new List<GameObject>();

    private int currentSignalTimestamp = -1;
    private bool isPaused = false; // Tracks whether the step-through is paused

    public GameObject timelineMarkerPrefab;
    private GameObject timelineMarker;
    private GameObject endTimelineMarker;
    private List<Vector3> timelinePoints = new List<Vector3>();

    private GameObject parentLabel; // Parent object for all labels
    private GameObject szLine = null; // Seizure line
    private GameObject mkLine = null; // Seizure line
    private bool isSeizureDataRunning = false;
    private ComputeBuffer binaryDataBuffer = null; // Buffer for binary data
    private ComputeBuffer neuronColoursBuffer = null; // Buffer for neuron colours  

    private Color baseColor;   // this is now set from datafile depending on region
    private Color mutatedColor; // changing this to private since its set for each neuron
    private int startTimestamp = 0;
    private int endTimestamp = -1;
    private List<NeuronData> activeNeurons = new List<NeuronData>();


    void Awake()
    {
        glowMaterial.EnableKeyword("_EMISSION"); // Apparently very important: https://discussions.unity.com/t/setting-emission-color-programatically/152813/2
        baseColor = glowMaterial.GetColor("_EmissionColor");
        mutatedColor = glowMaterial.GetColor("_EmissionColor");
        Application.runInBackground = true;

        // set video viz text elements to inactive
        videoTextElements.SetActive(false);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DirectoryInfo dir = new DirectoryInfo(dataFolder);

        statusMessage.text = "Loading initial position data ...";
        selectedFish = "";
        selectedRegion = "";
        uiHandler.DisableActionButtons();

        BrainData thisBrain = LoadAllNeuronData(postionsFile, "Brain0");


        List<string> regionNames = thisBrain.regions.Keys.ToList();
        // insert "Whole Brain" at start of list
        regionNames.Insert(0, "Whole Brain");

        // set regiondropdown text to 'select region'
        regionNames.Insert(0, "Select Region"); // Add prompt as first item
        uiHandler.fishDropdown.value = 0; // Select the prompt by default
        uiHandler.PopulateRegionDropdown(regionNames);

        // Populate fishFileDict with fish name as key and filename as value
        fishFileDict = getFishNamesAndFiles(dataFolder);
        List<string> fishNames = fishFileDict.Keys.ToList();
        // set fishdropdown text to 'select fish'
        fishNames.Insert(0, "Select Fish"); // Add prompt as first item
        uiHandler.fishDropdown.value = 0; // Select the prompt by default
        uiHandler.PopulateFishDropdown(fishNames);
        // make the fishdrop down inactive until a region is selected

        cameraHandler.SetupMainCameraView(thisBrain.bounds.center, thisBrain.bounds.extents.magnitude);
        // build up the view of brain, region by region
        StartCoroutine(ShowRegionsStepByStep(animationStepInterval, thisBrain)); // Enables each region every .5 seconds
        statusMessage.text = "Select a region to see neurons";

    }


    // Update is called once per frame
    void Update()
    {

    }

    private Dictionary<string, string> getFishNamesAndFiles(string dataFolder)
    {
        // Populate fishFileDict with fish name as key and filename as value
        Dictionary<string, string> fishFiles = Directory.GetFiles(dataFolder, "fish*_signal.csv")
            .Select(Path.GetFileName)
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(f, @"fish(\d+)_signal"))
            .ToDictionary(
                f =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(f, @"fish(\d+)_signal");
                    return match.Success ? "Fish" + match.Groups[1].Value : Path.GetFileNameWithoutExtension(f);
                },
                f => f
            );

        return fishFiles;
    }

    public void SetSelectedFish(string fishName)
    {
        Debug.Log("Has selected fish: " + fishName);
        selectedFish = fishName;
        if (!string.IsNullOrEmpty(selectedFish) && !string.IsNullOrEmpty(selectedRegion))
        {
            GetSelectedSeizureData(selectedRegion, selectedFish);
            statusMessage.text = $"Fish selected: {selectedFish}, Region selected: {selectedRegion}, now loading seizure file";
            // uiHandler.EnableActionButton();
        }
    }

    public void SetSelectedRegion(string regionName)
    {

        selectedRegion = regionName;
        statusMessage.text = $"Region selected: {regionName}";

        // Make seizure line if seizure data is loaded for this fish and region
        if (!string.IsNullOrEmpty(selectedFish) && !string.IsNullOrEmpty(selectedRegion))
        {
            GetSelectedSeizureData(selectedRegion, selectedFish);
            statusMessage.text = $"Fish selected: {selectedFish}, Region selected: {selectedRegion}, now loading seizure file";

            if (regionName == "Whole Brain")
            {
                ShowWholeBrain();
            }
            else
            {
                ShowOneRegion(regionName);
            }
        }
    }
    void ShowWholeBrain()
    {
        Debug.Log("Show whole brain");

        // setup cameras for whole brain
        cameraHandler.PositionMainCamera(brains[0].bounds.center, brains[0].bounds.extents.magnitude);

        int nbrBrains = brains.Count;
        cameraHandler.SetupViewports(nbrBrains);

        int brainix = 1;
        foreach (Camera fscamera in cameraHandler.fsCameras)
        {
            BrainData brain = brains[brainix];
            cameraHandler.PositionFeatureSetCamera(fscamera, brain.bounds.center, brain.bounds.extents.magnitude);
            // set all regions to active
            foreach (RegionData brainRegion in brain.regions.Values)
            {
                brainRegion.gameObject.SetActive(true);
            }
            brainix++;
        }

        // update activity line
        StartCoroutine(MakeSeizureLine("Whole Brain"));
        
    }
    
    void ShowOneRegion(string regionName)
    {
        Color regionColor = regionColours.TryGetValue(regionName, out Color c) ? c : Color.white;

        // Hide all regions except the selected one
        foreach (BrainData brain in brains)
        {
            foreach (RegionData brainRegion in brain.regions.Values)
            {
                // set this region active, all others inactive
                brainRegion.gameObject.SetActive(brainRegion.name == regionName);
                // reposition cameras to focus on this region
                foreach (Camera fscamera in cameraHandler.fsCameras)
                {
                    cameraHandler.PositionFeatureSetCamera(fscamera, brainRegion.bounds.center, brainRegion.bounds.extents.magnitude);
                }
            }
            
        }
        
        // update activity line
        StartCoroutine(MakeSeizureLine(regionName));
    }

    IEnumerator ShowRegionsStepByStep(float delaySeconds, BrainData brain)
    {

        // Calculate the label position offset from center of region
        //float labelOffsetX = 500f;

        //parentLabel = new GameObject("BrainLabels");
        //parentLabel.transform.SetParent(brainParent.transform, false);

        Debug.Log("Starting to show regions step by step");
        List<RegionData> sortedRegions = brain.regions.Values.ToList();
        sortedRegions = sortedRegions.OrderBy(r => r.transform.position.y).ToList();
        // for each region in regions dictionary
        foreach (RegionData region in sortedRegions)
        {
            Debug.Log("Now show region: " + region.name);
            // Enable the region
            region.gameObject.SetActive(true);

            // Label position: fixed distance to the right of region center, aligned on y
            //Vector3 labelPos = new Vector3(center.x + labelOffsetX, center.y, center.z);

            // Use AddLabel with line to region center
            //AddLabel(parentLabel, regionName, labelPos, true, center);

            yield return new WaitForSeconds(delaySeconds);
        }
        uiHandler.ShowMenuPanel();
    }
private void LoadSeizureDataSync(string fishName, string regionName, bool isBulkLoading = false, bool hideProgressBar = true)
{
    Debug.Log($"Loading seizure data for fish: {fishName}, region: {regionName}");

    if (!fishFileDict.TryGetValue(fishName, out string fishFile))
    {
        string errorMsg = $"Error: Fish name {fishName} not found in available files.";
        statusMessage.text = errorMsg;
        Debug.LogError(errorMsg);
        return;
    }

    string fullPath = Path.Combine(dataFolder, fishFile);
    if (!File.Exists(fullPath))
    {
        Debug.LogError("File not found: " + fullPath);
        return;
    }

    // Only update status message if not bulk loading (bulk manages its own messages)
    if (!isBulkLoading)
    {
        statusMessage.text = $"Loading seizure data for fish: {fishFile} and region: {selectedRegion}";
    }

    int numRows = 0;
    int numCols = 0;

    // Fast file analysis
    using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072))
    using (var reader = new StreamReader(fileStream))
    {
        reader.ReadLine(); // skip header
        string firstDataLine = reader.ReadLine();
        if (firstDataLine == null)
        {
            string errorMsg = $"Error: Empty file: {fullPath}";
            statusMessage.text = errorMsg;
            Debug.LogError(errorMsg);
            return;
        }
        
        string[] firstData = firstDataLine.Split(',');
        numCols = firstData.Length - 10;
        numRows = 1;
        
        while (reader.ReadLine() != null)
            numRows++;
    }

    Debug.Log($"Found {numCols} signal columns for {numRows} neuron/rows");

    // Pre-allocate arrays for performance
    char[] separators = { ',' };
    string[] reusableStringArray = new string[numCols + 20];
    float[] reusableFloatArray = new float[numCols];
    int[] reusableBinaryArray = new int[numCols];
    
    var culture = System.Globalization.CultureInfo.InvariantCulture;
    var numberStyles = System.Globalization.NumberStyles.Float;
    
    int firstActivityColIdx = 10;
    int rowIdx = 0;

        // Main processing loop
        using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 262144))
        using (var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true, 262144))
        {
            reader.ReadLine(); // Skip header
            string line;

            while ((line = reader.ReadLine()) != null && rowIdx < numRows)
            {
                int splitCount = SplitStringIntoArray(line, separators[0], reusableStringArray);

                if (splitCount < firstActivityColIdx)
                {
                    rowIdx++;
                    continue;
                }

                int validCount = BatchParseFloats(reusableStringArray, firstActivityColIdx,
                                                splitCount, reusableFloatArray, reusableBinaryArray,
                                                culture, numberStyles);

                if (validCount > 0)
                {
                    foreach (BrainData brain in brains)
                        BatchUpdateNeurons(rowIdx, reusableBinaryArray, validCount, fishName, brain);
                }

                rowIdx++;
            }
        }


        
    Debug.Log("clone activity data for featureset neurons");
    //CopyActivityDataToFeatureSetBrains(fishName);

    // Update min/max values
    foreach (BrainData brain in brains)
    {
        foreach (RegionData region in brain.regions.Values)
        {
            region.UpdateMinMax();
        }
        brain.UpdateMinMax();
    }

    // Only handle UI for single file loading
    if (!isBulkLoading)
    {
        uiHandler.EnableActionButtons();
        statusMessage.text = $"Seizure data loaded for fish: {selectedFish}, region: {selectedRegion} with {numRows} rows and {numCols} signal columns.";
        
        // Call MakeSeizureLine only for single file loading
        StartCoroutine(MakeSeizureLine(regionName));
    }
    else
    {
        // For bulk loading, just log completion
        Debug.Log($"Seizure data loaded for fish: {fishName} with {numRows} rows and {numCols} signal columns.");
    }
}

    // **HELPER METHODS FOR PRE-ALLOCATED PROCESSING**

    // Custom string split that uses pre-allocated array
    private int SplitStringIntoArray(string input, char separator, string[] outputArray)
    {
        int count = 0;
        int startIndex = 0;

        for (int i = 0; i <= input.Length; i++)
        {
            if (i == input.Length || input[i] == separator)
            {
                if (count < outputArray.Length)
                {
                    outputArray[count] = input.Substring(startIndex, i - startIndex);
                    count++;
                }
                startIndex = i + 1;
            }
        }
        return count;
    }

// Batch parse floats into pre-allocated arrays
private int BatchParseFloats(string[] stringArray, int startIndex, int count, 
                           float[] floatArray, int[] binaryArray,
                           System.Globalization.CultureInfo culture,
                           System.Globalization.NumberStyles numberStyles)
{
    int validCount = 0;
    int maxIndex = Mathf.Min(count, startIndex + floatArray.Length);
    
    for (int i = startIndex; i < maxIndex; i++)
    {
        if (float.TryParse(stringArray[i], numberStyles, culture, out float value))
        {
            floatArray[validCount] = value;
            binaryArray[validCount] = value > 0 ? 1 : 0;
            validCount++;
        }
    }
    return validCount;
}

// Batch update all neurons for a row
private void BatchUpdateNeurons(int rowIdx, int[] binaryArray, int count, 
                               string fishName,BrainData thisBrain)
    {
        NeuronData neuron = thisBrain.neurons.FirstOrDefault(n => n.neuronIdx == rowIdx);   
        
    if (neuron != null)
    {
        // Add all activities for this neuron at once
        for (int timeIdx = 0; timeIdx < count; timeIdx++)
        {
            neuron.AddActivity(fishName, binaryArray[timeIdx], timeIdx);
        }
    }
    else
    {
        // This is normal for feature set brains - they don't have all neurons
        Debug.Log($"No neuron found with neuronIdx {rowIdx} in brain {thisBrain.name}");
    }
    }
    
    private void CopyActivityDataToFeatureSetBrains(string fishName)
    {
        if (brains.Count <= 1)
        {
            Debug.Log("No feature set brains to copy activity data to");
            return;
        }

        BrainData mainBrain = brains[0];
        Debug.Log($"Copying activity data for fish {fishName} to {brains.Count - 1} feature set brains");

        // Copy to each feature set brain (skip brains[0] as it's the main brain)
        for (int brainIndex = 1; brainIndex < brains.Count; brainIndex++)
        {
            BrainData featureBrain = brains[brainIndex];
            Debug.Log($"Copying activity data to brain: {featureBrain.name}");

            // Copy activity data from corresponding neurons in main brain
            foreach (NeuronData featureNeuron in featureBrain.neurons)
            {
                // Find the corresponding neuron in main brain by neuronIdx
                NeuronData mainNeuron = mainBrain.neurons.FirstOrDefault(n => n.neuronIdx == featureNeuron.neuronIdx);
                
                if (mainNeuron != null)
                {
                    // Copy activity data from main neuron to feature neuron
                    featureNeuron.CopyActivityData(mainNeuron);
                }
                else
                {
                    Debug.LogWarning($"Could not find corresponding neuron with index {featureNeuron.neuronIdx} in main brain");
                }
            }

            Debug.Log($"Copied activity data for {featureBrain.neurons.Count} neurons in brain {featureBrain.name}");
        }

        Debug.Log($"Activity data copying completed for fish {fishName}");
    }
    void GetSelectedSeizureData(string regionName, string fishName)
    {
        if (brains[0].totalActivityList.ContainsKey(fishName))
        {
            Debug.Log("Seizure data for fish " + fishName + " already loaded");
            return;
        }
        
        uiHandler.DisableActionButtons();
        LoadSeizureDataSync(fishName, regionName, isBulkLoading: false, hideProgressBar: true);
    }

    IEnumerator MakeSeizureLine(string regionName)
    {
        float minValue = 0f;
        float maxValue = 0f;
        int numPoints = 0;

        Dictionary<string, Dictionary<int, float>> allActivities;

        if (regionName == "Whole Brain")
        {
            Debug.Log("Show whole brain seizure line ");

            minValue = brains[0].minActivities[selectedFish];
            maxValue = brains[0].maxActivities[selectedFish];
            numPoints = (int)brains[0].totalActivityList[selectedFish].Count;

            allActivities = brains[0].totalActivityList;
        }
        else
        {

            Color regionColor = regionColours[regionName];

            minValue = brains[0].regions[regionName].minActivities[selectedFish];
            maxValue = brains[0].regions[regionName].maxActivities[selectedFish];
            numPoints = (int)brains[0].regions[regionName].sumActivities[selectedFish].Count;

            allActivities = brains[0].regions[regionName].sumActivities;
        }
        // Create or clear existing seizure line object
        if (szLine != null)
        {
            DestroyImmediate(szLine);
            szLine = null;
        }
        szLine = new GameObject("SeizureLine");
        LineRenderer lineRenderer = szLine.AddComponent<LineRenderer>();
        MeshRenderer mRenderer = szLine.AddComponent<MeshRenderer>();
        mRenderer.material = glowMaterial;

        Color lineColour = Color.yellow;
        float zPos = szRightPos.z;

        float graphHeight = 200f;

        Debug.Log($"Creating seizure line with {numPoints} points for fish {selectedFish} in region {regionName}");
        lineRenderer.positionCount = numPoints;


        float yPos = szLeftPos.y;
        float markerSpacer = 40f;

        timelinePoints.Clear();

        for (int i = 0; i < numPoints; i++)
        {
            float t = (float)i / (numPoints - 1); // Normalized position [0,1]
                                                  // show values of szleftpos and szrightpos and t
            float xPos = Mathf.Lerp(szLeftPos.x, szRightPos.x, t);

            float value = allActivities[selectedFish][i];
            float scaledValue = (maxValue > minValue)
                ? ((value - minValue) / (maxValue - minValue)) * graphHeight
                : 0f;
            Vector3 point = new Vector3(xPos, yPos + scaledValue, zPos);
            lineRenderer.SetPosition(i, point);
            // shift point down a bit for marker
            point.y = szLeftPos.y - markerSpacer;
            timelinePoints.Add(point);

            // Add axis marker every 500 timestamps
            if (i % 500 == 0)
            {
                // Axis arker position: fixed y below the line  
                Vector3 axisPos = new Vector3(xPos, yPos - (markerSpacer * 2.0f), zPos);

                // Create a timestamp label (col nbr)
                var axisTimestamp = new GameObject($"TimestampMarker_{i}");
                var tmp = axisTimestamp.AddComponent<TMPro.TextMeshPro>();
                tmp.text = i.ToString();
                tmp.fontSize = 400;
                tmp.color = Color.white;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.rectTransform.sizeDelta = new Vector2(100f, 80f);

                axisTimestamp.transform.position = axisPos;
                axisTimestamp.transform.SetParent(szLine.transform, false);
                yield return null;
            }
        } // end of loop through timestamps

        lineRenderer.startWidth = 8f;
        lineRenderer.endWidth = 8f;
        lineRenderer.material = glowMaterial;
        lineRenderer.startColor = lineColour;
        lineRenderer.endColor = lineColour;

        if (mkLine == null)
        {
            Debug.Log("Creating marker line");
            mkLine = new GameObject("MarkerLine");
            // add dark line below seizure line
            mkLine.AddComponent<LineRenderer>();
            LineRenderer darkLine = mkLine.GetComponent<LineRenderer>();
            darkLine.positionCount = 2;
            darkLine.startWidth = 8f;
            darkLine.endWidth = 8f;
            // darkLine.material = dullMaterial;
            darkLine.material = glowMaterial;
            darkLine.startColor = Color.black;
            darkLine.endColor = Color.black;
            darkLine.SetPosition(0, new Vector3(szLeftPos.x, szLeftPos.y - markerSpacer, szLeftPos.z));
            darkLine.SetPosition(1, new Vector3(szRightPos.x, szRightPos.y - markerSpacer, szRightPos.z));
        }

        // Create timeline markers
        startTimestamp = 0;
        if (timelineMarker == null)
        {
            timelineMarker = Instantiate(timelineMarkerPrefab);
            timelineMarker.GetComponent<DraggableObject>().onDrag.AddListener(OnMarkerDrag);
        }
        timelineMarker.transform.position = timelinePoints[startTimestamp];


        endTimestamp = Math.Min(startTimestamp + nbrFrames, timelinePoints.Count - 1);
        if (endTimelineMarker == null)
        {
            endTimelineMarker = Instantiate(timelineMarkerPrefab);
            Renderer tmRenderer = timelineMarker.GetComponent<Renderer>();
            if (tmRenderer != null)
            {
                tmRenderer.material.color = Color.grey;
            }
            FreezeEndMarker(endTimestamp);
        };

        uiHandler.EnableActionButtons();
        statusMessage.text = $"Move timestamp marker in seizure line graph above then click 'Show Seizure Data' to start animation.";

        yield return null;
    } // end of MakeSeizureLine


    public void ShowSeizureData()
    {
        // make the menu ui inactive
        uiHandler.HideMenuPanel();
         // Set the flag to true when starting
        isSeizureDataRunning = true;

        // Start stepping through the seizure data
        int markerTimestamp = currentSignalTimestamp >= 0 ? currentSignalTimestamp : 0;
        int numTimestamps = 0;
        if (selectedRegion == "Whole Brain")
        {
            numTimestamps = (int)brains[0].totalActivityList[selectedFish].Count;
        }
        else
        {
            numTimestamps = (int)brains[0].regions[selectedRegion].sumActivities[selectedFish].Count;
        }

        StartCoroutine(StepThroughSeizureData(markerTimestamp, numTimestamps, animationStepInterval, exportFrames: false));
        StartCoroutine(RotateBrainDuringSeizure());
    }


public void BulkLoadAllFish()
{
    Debug.Log("Starting bulk load for all fish");
    
    statusMessage.text = $"Starting bulk load, please be patient ...";
    StartCoroutine(BulkLoadAllFishCoroutine());
}

private IEnumerator BulkLoadAllFishCoroutine()
{
    int totalFish = fishFileDict.Keys.Count;
    int currentFishIndex = 0;

    // Initialize bulk progress bar for loading phase
    progressBar.SetActive(true);
    progressBarFill.fillAmount = 0f;
    progressBarText.text = "0%";

    statusMessage.text = $"Starting bulk load for {totalFish} fish...";
    selectedRegion = "Whole Brain"; // For bulk operations, we use whole brain

    // Load all seizure data files
    Debug.Log("=== BULK LOADING: Loading all seizure data files ===");
    foreach (var fishName in fishFileDict.Keys)
    {
        currentFishIndex++;

        // Update bulk progress bar for loading phase
        float loadingProgress = (float)currentFishIndex / totalFish;
        progressBarFill.fillAmount = loadingProgress;
        progressBarText.text = $"Loading: {(int)(loadingProgress * 100)}%";

        statusMessage.text = $"Loading seizure data for fish {fishName}... ({currentFishIndex}/{totalFish})";
        Debug.Log($"Bulk loading: Processing fish {fishName} ({currentFishIndex}/{totalFish})");

        // Check if seizure data is already loaded for this fish
        if (!brains[0].totalActivityList.ContainsKey(fishName))
        {
            // Use the unified function for bulk loading
            LoadSeizureDataSync(fishName, selectedRegion, isBulkLoading: true, hideProgressBar: false);

            // Brief pause to allow any pending operations
            yield return new WaitForSeconds(0.1f);
        }

        // Check if data was successfully loaded
        if (!brains[0].totalActivityList.ContainsKey(fishName))
        {
            Debug.LogError($"Failed to load seizure data for fish {fishName}. Skipping this fish...");
            continue;
        }

        Debug.Log($"Successfully loaded seizure data for fish {fishName}");
    }

    // Hide progress bar after loading phase
    progressBar.SetActive(false);
    yield return new WaitForSeconds(0.5f);

    uiHandler.bulkExportButton.interactable = true;
    
    // Final status update
    statusMessage.text = $"Bulk loading completed! Loaded data for {totalFish} fish.";
    Debug.Log("Bulk loading completed for all fish");
}

public void BulkExportAllFrames()
{
    Debug.Log("Starting bulk export for all fish");
    StartCoroutine(BulkExportAllFramesCoroutine());
}

private IEnumerator BulkExportAllFramesCoroutine()
{
    // Check if any fish data is loaded
    if (brains[0].totalActivityList.Count == 0)
    {
        statusMessage.text = "No fish data loaded! Please run 'Bulk Load All Fish' first.";
        Debug.LogWarning("No fish data loaded for export. Run BulkLoadAllFish first.");
        yield break;
    }

    int totalFish = brains[0].totalActivityList.Keys.Count;
    int currentFishIndex = 0;

    statusMessage.text = $"Starting bulk export for {totalFish} loaded fish...";
    selectedRegion = "Whole Brain"; // For bulk export, we use whole brain

    // Hide UI elements once before starting frame generation
    uiHandler.HideMenuPanel();

    Debug.Log("=== BULK EXPORT: Generating frames for each fish ===");
    foreach (string fishName in brains[0].totalActivityList.Keys)
    {
        currentFishIndex++;

        statusMessage.text = $"Generating frames for fish {fishName}... ({currentFishIndex}/{totalFish})";
        Debug.Log($"Frame generation: Processing fish {fishName} ({currentFishIndex}/{totalFish})");

        // Temporarily set selectedFish to current fish
        string originalSelectedFish = selectedFish;
        selectedFish = fishName;

        // Update the fish dropdown to reflect current fish (for UI consistency)
        var fishList = fishFileDict.Keys.ToList();
        int fishIndex = fishList.IndexOf(fishName);
        if (fishIndex >= 0)
        {
            uiHandler.fishDropdown.value = fishIndex + 1; // +1 because "Select Fish" is at index 0
        }

        // Create seizure line for this fish (needed for timeline points)
        yield return StartCoroutine(MakeSeizureLine(selectedRegion));

        // Brief pause to ensure seizure line is ready
        yield return new WaitForSeconds(0.2f);

        // Setup export path for this fish
        string regionID = selectedRegion.Replace(" ", "").Substring(0, Math.Min(5, selectedRegion.Replace(" ", "").Length));
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string exportPath = Path.Combine(projectRoot, "SignalDataFrames/Signals_" + selectedFish + "_" + regionID);
        if (Directory.Exists(exportPath))
            Directory.Delete(exportPath, true);
        Directory.CreateDirectory(exportPath);

        // Get number of timestamps for this fish
        int numTimestamps = (int)brains[0].totalActivityList[selectedFish].Count;

        Debug.Log($"Starting frame generation for {fishName} with {numTimestamps} timestamps");

        // Reset timeline markers for full export
        ResetTimelineMarkersForFullExport(numTimestamps);

            // Set the seizure running flag
            isSeizureDataRunning = true;

        // Start frame generation and brain rotation simultaneously
        StartCoroutine(StepThroughSeizureData(0, numTimestamps, .01f, exportFrames: true, exportPath));
        StartCoroutine(RotateBrainDuringSeizure());


        // Wait for frame export to complete
        while (isSeizureDataRunning)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Restore original selectedFish
        selectedFish = originalSelectedFish;

        // Brief pause between fish
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"Completed frame generation for fish {fishName}");
    }

    // Show UI elements again
    uiHandler.ShowMenuPanel();

    // Final status update
    statusMessage.text = $"Bulk export completed! Processed {totalFish} fish.";
    Debug.Log("Bulk export completed for all fish");
}
    public void MakeFrames()
    {
        Debug.Log("Starting seizure frame export for single fish");

        // Hide UI for single fish export
        uiHandler.HideMenuPanel();

        int numTimestamps = 0;
        if (selectedRegion == "Whole Brain")
        {
            numTimestamps = (int)brains[0].totalActivityList[selectedFish].Count;
        }
        else
        {
            numTimestamps = (int)brains[0].regions[selectedRegion].sumActivities[selectedFish].Count;
        }

        Debug.Log($"Reset timeline markers for frame export for a total nbr of timestamps: {numTimestamps}");
        // Reset timeline markers for full export
        ResetTimelineMarkersForFullExport(numTimestamps);

        Debug.Log("Setup export path for seizure frames");
        // Setup export path
        string regionID = selectedRegion.Replace(" ", "").Substring(0, Math.Min(5, selectedRegion.Replace(" ", "").Length));
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string exportPath = Path.Combine(projectRoot, "SignalDataFrames/Signals_" + selectedFish + "_" + regionID);
        if (Directory.Exists(exportPath))
            Directory.Delete(exportPath, true);
        Directory.CreateDirectory(exportPath);

        Debug.Log("Starting frame generation and brain rotation");

        StartCoroutine(StepThroughSeizureData(0, numTimestamps, .001f, exportFrames: true, exportPath));
        StartCoroutine(RotateBrainDuringSeizure());

    }
    
    private void CaptureHighResScreenshot(string filepath, bool useCustomResolution = true)
    {
        if (useCustomResolution && cameraHandler?.mainCamera != null)
        {
            // **OPTION 3: Camera-based high-res capture**
            Camera mainCam = cameraHandler.mainCamera;
            
            // Create render texture at custom resolution
            RenderTexture renderTexture = new RenderTexture(exportWidth, exportHeight, 24);
            RenderTexture previousTarget = mainCam.targetTexture;
            
            // Set camera to render to high-res texture
            mainCam.targetTexture = renderTexture;
            mainCam.Render();
            
            // Read the render texture
            RenderTexture.active = renderTexture;
            Texture2D screenshot = new Texture2D(exportWidth, exportHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, exportWidth, exportHeight), 0, 0);
            screenshot.Apply();
            
            // Save the high-res image
            byte[] pngData = screenshot.EncodeToPNG();
            File.WriteAllBytes(filepath, pngData);
            
            // Restore camera settings
            mainCam.targetTexture = previousTarget;
            RenderTexture.active = null;
            
            // Clean up
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            
            Debug.Log($"High-res screenshot saved: {filepath} ({exportWidth}x{exportHeight})");
        }
        else
        {
            // **FALLBACK: Standard screen capture**
            ScreenCapture.CaptureScreenshot(filepath);
            Debug.Log($"Standard screenshot saved: {filepath}");
        }
    }

    private void OnMarkerDrag()
    {
        Debug.Log("Marker dragged");

        // If animation is running, stop it 
        if (isSeizureDataRunning)
        {
            StopSeizureAnimation();
            return; // Exit early, don't process position updates during animation
        }

        if (timelineMarker != null && timelinePoints.Count > 0)
        {

            var markerX = timelineMarker.transform.position.x;
            var firstTimestampX = timelinePoints[0].x;
            var lastTimestampX = timelinePoints[timelinePoints.Count - 1].x;
            if (markerX < firstTimestampX || markerX > lastTimestampX)
            {
                Debug.Log($"Marker dragged out of bounds: {markerX} not in ({firstTimestampX}, {lastTimestampX})");
                // out of bounds
                return;
            }
            // find the closest point to the marker
            float closestDist = float.MaxValue;
            int closestIdx = -1;
            for (int i = 0; i < timelinePoints.Count; i++)
            {
                float dist = Math.Abs(markerX - timelinePoints[i].x);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestIdx = i;
                }
            }
            if (closestIdx != -1 && closestIdx != currentSignalTimestamp)
            {
                startTimestamp = closestIdx;
                currentSignalTimestamp = closestIdx;
                uiHandler.startTimeInput.text = closestIdx.ToString();
                foreach (NeuronData neuron in activeNeurons)
                {
                    neuron.Deactivate();
                }

                activeNeurons.Clear();
                foreach (BrainData brain in brains)
                {
                    if (selectedRegion == "Whole Brain")
                    {
                        // Activate neurons for this timestamp
                        activeNeurons.AddRange(brain.GetActiveNeurons(selectedFish, closestIdx));
                    }
                    else
                    {
                        // Activate neurons for this timestamp
                        activeNeurons.AddRange(brain.regions[selectedRegion].GetActiveNeurons(selectedFish, closestIdx));
                    }
                }
                foreach (NeuronData neuron in activeNeurons)
                {
                    neuron.SetActiveState(selectedFish, closestIdx);
                }
            }
            endTimestamp = Math.Min(startTimestamp + nbrFrames, timelinePoints.Count - 1);
            ResetEndMarker(endTimestamp);
        }
    }
    

    private void ResetTimelineMarkersForFullExport(int totalTimestamps)
    {
        // Reset start marker to timestamp 0
        startTimestamp = 0;
        currentSignalTimestamp = 0;
        if (timelineMarker != null && timelinePoints.Count > 0)
        {
            timelineMarker.transform.position = timelinePoints[0];
        }
        
        // Reset end marker to the last timestamp 
        endTimestamp =totalTimestamps - 1;
        if (endTimelineMarker != null && timelinePoints.Count > endTimestamp)
        {
            endTimelineMarker.transform.position = timelinePoints[endTimestamp];
        }
        
        // Update UI to reflect the reset
        uiHandler.startTimeInput.text = startTimestamp.ToString();
        
        Debug.Log($"Timeline markers reset: Start={startTimestamp}, End={endTimestamp}, Total={totalTimestamps}");
    }

    IEnumerator StepThroughSeizureData(int startTimestamp, int numTimestamps = -1, float stepInterval=.001f, bool exportFrames = false, string exportPath = "")
    {
    

            int endTimestamp = -1;
            if (exportFrames)
                {
                endTimestamp = numTimestamps;
                }
            else
            {
                endTimestamp = Math.Min(startTimestamp + nbrFrames, numTimestamps);
            }


        Debug.Log($"Stepping through seizure data for fish {selectedFish} in region {selectedRegion}, Startime: {startTimestamp}, endTime: {endTimestamp}");
            
            for (int col = startTimestamp; col < endTimestamp; col += 1)
            {
                currentSignalTimestamp = col;
                UpdateNeuronStates(col, selectedFish, selectedRegion);

                // Move timeline marker
                if (timelineMarker != null && timelinePoints.Count > col)
                {
                    timelineMarker.transform.position = timelinePoints[col];
                }

                // Frame export logic
                if (exportFrames && !string.IsNullOrEmpty(exportPath))
                {
                    yield return new WaitForEndOfFrame();
                    
                    string regionID = selectedRegion.Replace(" ", "").Substring(0, Math.Min(5, selectedRegion.Replace(" ", "").Length));
                    string framefile = Path.Combine(exportPath, $"{selectedFish}_{regionID}_{col:D05}.png");
                    
                    // **Use the abstracted high-res capture function**
                    CaptureHighResScreenshot(framefile);
                }

                yield return new WaitForSeconds(stepInterval);
            }

            // Set the flag to false when done
            isSeizureDataRunning = false;

            // Cleanup and UI restoration
            int finalEndTimestamp = Mathf.Min(currentSignalTimestamp + nbrFrames, numTimestamps - 1);
            ResetEndMarker(finalEndTimestamp);
            uiHandler.ShowMenuPanel();
            
            if (exportFrames)
            {
                Debug.Log($"Frames exported to {exportPath}");
            }
    }


    IEnumerator RotateBrainDuringSeizure()
    {
        
        // Wait for seizure data to start
        while (!isSeizureDataRunning)
        {
            yield return null;
        }

        // Continuously rotate while seizure data is playing
        while (isSeizureDataRunning)
        {
            // Rotate each brain around its centroid every frame
            foreach (BrainData brain in brains)
            {
                Vector3 centroid = brain.bounds.center;            
                brain.transform.RotateAround(centroid, Vector3.up, rotationSpeed * Time.deltaTime);
                }

            yield return null; // Update every frame for smooth rotation
        }

        // Reset all brains to their original transforms when rotation stops
        foreach (BrainData brain in brains)
        {
            brain.ResetToOriginalTransform();
        }

    }

    private void StopSeizureAnimation()
    {
        if (isSeizureDataRunning)
        {
            isSeizureDataRunning = false;
            Debug.Log("Seizure animation stopped by user");
            
            // Reset brains to original position
            foreach (var brain in brains)
            {
                brain.ResetToOriginalTransform();
            }
            
            // Show menu panel again
            uiHandler.ShowMenuPanel();
            
            // Update status message
            statusMessage.text = "Animation stopped. Move marker and click 'Show Seizure Data' to restart.";
        }
    }

    private void UpdateNeuronStates(int timestamp, string fishName, string regionName)
    {
        foreach (NeuronData neuron in activeNeurons)
        {
            neuron.Deactivate();
        }
        activeNeurons.Clear();

        foreach (BrainData brain in brains)
        {
            if (regionName == "Whole Brain")
            {
                // Activate neurons for this timestamp
                activeNeurons.AddRange(brain.GetActiveNeurons(fishName, timestamp));
            }
            else
            {
                // Activate neurons for this timestamp
                activeNeurons.AddRange(brain.regions[regionName].GetActiveNeurons(fishName, timestamp));
            }
        }
        foreach (NeuronData neuron in activeNeurons)
        {
            neuron.SetActiveState(fishName, timestamp);
        }
    }

    void FreezeEndMarker(int endTimestamp = -1)
    {
        if (endTimelineMarker != null)
        {
            var draggable = endTimelineMarker.GetComponent<DraggableObject>();
            if (draggable != null)
            {
                // disable dragging
                draggable.enabled = false;
            }
        }
        endTimelineMarker.transform.position = timelinePoints[endTimestamp];
    }

    void ResetEndMarker(int endTimestamp = -1)
    {
        //int numPoints = timelinePoints.Count;
        //if (endTimestamp >= numPoints || endTimestamp < 0)
        //   endTimelineMarker.transform.position = timelinePoints[0];
        
        endTimelineMarker.transform.position = timelinePoints[endTimestamp];
    }

    public BrainData LoadAllNeuronData(string postionsFile, string brainName, bool useSWCIndex = true)
    {
        Debug.Log("Loading neuronal data from: " + postionsFile);
        // Combine the directory path with the filename
        string fullPath = Path.Combine(dataFolder, postionsFile);

        // Check if the file exists
        if (!File.Exists(fullPath))
        {
            Debug.LogError("File not found: " + fullPath);
            return null;
        }

        Debug.Log("Loading file: " + fullPath);
        StreamReader reader = new StreamReader(fullPath);

        // Read the header line and ensure it matches the expected format
        string headerLine = reader.ReadLine();
        string[] headers = headerLine.Split(','); // Assuming comma-separated values
        if (headers.Length < 6 ||
            headers[0].Trim() != "x_SWCIndex" ||
            // headers[1].Trim() != "SWC Type" ||
            headers[1].Trim() != "xpos" ||
            headers[2].Trim() != "ypos" ||
            headers[3].Trim() != "zpos" ||
            //headers[5].Trim() != "radius" ||
            //headers[6].Trim() != "SWC Parent" ||
            headers[4].Trim() != "Region" ||
            headers[5].Trim() != "Subregion" ||
            headers[6].Trim() != "Label")
        // || headers[7].Trim() != "Feature1" )
        {
            Debug.LogError("Unexpected CSV format: " + headerLine);
            return null;
        }

        // check how many feature sets ie how many columns from 7 onwards, each column is a featureset

        // Feature sets start at index 7
        int featureSetStartIndex = 7;
        int featureSetCount = headers.Length - featureSetStartIndex;
        Dictionary<string, int> featureSetNeuronCounts = new Dictionary<string, int>();
        HashSet<string> featureSetNames = System.Linq.Enumerable.ToHashSet(headers.Skip(featureSetStartIndex));
        Debug.Log($"Found {featureSetCount} feature sets. Feature set names are: {string.Join(", ", featureSetNames)}");

        GameObject obj = new GameObject(brainName);
        BrainData defaultBrain = obj.AddComponent<BrainData>();
        defaultBrain.activeFeatureSets = featureSetNames;
        SetupFeatureColours(featureSetNames);

        string line;
        int lineIdx = 0;
        // Read the rest of the file line by line
        while ((line = reader.ReadLine()) != null)
        {
            string[] values = line.Split(','); // Assuming comma-separated values

            // Skip rows with fewer than 6 columns
            if (values.Length < 6)
            {
                Debug.LogWarning("Skipping malformed row: " + line);
                continue;
            }
            int.TryParse(values[0], out int swcIndex);
            if (float.TryParse(values[1], out float x) &&
                            float.TryParse(values[2], out float y) &&
                            float.TryParse(values[3], out float z))
            {
                string regionList = values[4].Trim();
                string subregionList = values[5].Trim();
                string label = values[6].Trim();

                // get first region from regionList
                string firstRegion = cleanList(regionList)
                    .Split(new char[] { '+', '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .FirstOrDefault(s => !string.IsNullOrEmpty(s));
                if (string.IsNullOrEmpty(firstRegion))
                    firstRegion = "None";

                string regionName = firstRegion;

                Color color = regionColours.TryGetValue(regionName, out Color c) ? c : Color.white;

                // Construct the label text
                string labelTxt = $"Regions: {cleanList(regionList)}\nSubregions: {cleanList(subregionList)}";


                if (!defaultBrain.regions.ContainsKey(regionName))
                {
                    // };// Create a new GameObject for the region
                    GameObject regionObj = new GameObject(regionName);
                    RegionData newRegion = regionObj.AddComponent<RegionData>();
                    newRegion.name = regionName;
                    newRegion.color = color;
                    newRegion.brain = defaultBrain;
                    defaultBrain.regions[regionName] = newRegion;
                    newRegion.transform.SetParent(defaultBrain.transform, false);
                }
                RegionData region = defaultBrain.regions[regionName];
                region.gameObject.SetActive(false); // start with all regions inactive

                // Create a new GameObject for the neuron
                GameObject neuronObj = new GameObject($"Neuron_{swcIndex}");
                // Add NeuronData as a component
                NeuronData newNeuron = neuronObj.AddComponent<NeuronData>();
                // Set properties
                newNeuron.neuronIdx = useSWCIndex ? swcIndex : lineIdx;
                newNeuron.originalPosition = new Vector3(x, y, z * 3f); // scale z for better brain shape
                newNeuron.color = color;
                newNeuron.brain = defaultBrain;
                newNeuron.region = region;
                newNeuron.subregion = subregionList;
                newNeuron.label = labelTxt;
                newNeuron.activeNeuronSize = activeNeuronSize;
                newNeuron.inactiveNeuronSize = inactiveNeuronSize;
                newNeuron.transform.SetParent(region.gameObject.transform, false);

                newNeuron.InitNeuron(sphereMesh, glowMaterial, activeNeuronSize);

                for (var fi = featureSetStartIndex; fi < values.Length; fi++)
                {
                    int.TryParse(values[fi], out int neuronFeatureValue);
                    if (neuronFeatureValue != 0)
                    {
                        newNeuron.featureData.AddFeature(headers[fi]);
                    }
                }

                defaultBrain.AddNeuron(newNeuron);
                region.AddNeuron(newNeuron);

            }
            lineIdx++;
        }//end of reading file line by line

    // add default brain as the first brain brain[0]
    defaultBrain.StoreOriginalTransform();
    brains.Add(defaultBrain);
    
    Debug.Log("===============Cloning feature set neurons==================");
    // Clone each featureset into additional brains
    HashSet<string> allFeatureSets = defaultBrain.activeFeatureSets;
    int brainix = 1;
        foreach (string featureSet in allFeatureSets)
        {
            Debug.Log($"Cloning feature set neurons for feature set: {featureSet}");
            Color ncolor = featuresetColourList[brainix];
            BrainData newBrain = CloneFeatureSetNeurons(defaultBrain, "Brain" + brainix.ToString(), featureSet, distBtwnBrains * brainix, ncolor);
            newBrain.StoreOriginalTransform();
            brains.Add(newBrain);
            brainix++;
        }

        cameraHandler.CreateFeaturesetCameras( featureSetCount);

        reader.Close();
        Debug.Log("Added " + defaultBrain.neurons.Count + " neurons to new brain gameObject: " + defaultBrain.name);

        return defaultBrain;
    } // end of LoadAllNeuronData


    private BrainData CloneFeatureSetNeurons(BrainData thisBrain, string brainName,string featureSet, int distBtwnBrains,Color featureColor)
    {

        Debug.Log("Cloning neurons with featuresets from brain: " + thisBrain.name);
        GameObject obj = new GameObject(brainName);
        BrainData newBrain = obj.AddComponent<BrainData>();

        // Add regions from thisBrain to newBrain
        foreach (var regionEntry in thisBrain.regions)
        {
            string regionName = regionEntry.Key;
            RegionData region = regionEntry.Value;
            // Create a new GameObject for the region
            GameObject regionObj = new GameObject(regionName);
            RegionData newRegion = regionObj.AddComponent<RegionData>();
            newRegion.name = regionName;
            newRegion.color = region.color;
            newRegion.brain = newBrain;
            newBrain.regions[regionName] = newRegion;
            newRegion.transform.SetParent(newBrain.transform, false);
            newRegion.gameObject.SetActive(false); // start with all regions inactive
        }


        // Add only the featureset neurons from thisBrain to newBrain
        foreach (NeuronData neuron in thisBrain.neurons)
        {
            if (neuron.featureData != null && neuron.featureData.IsFeatureActive(featureSet))
            {
                // Set new position for cloned neuron
                Vector3 newPosition = neuron.originalPosition + new Vector3(distBtwnBrains, 0, 0);
                NeuronData newNeuron = neuron.CopyNeuron(newBrain, newPosition, featureColor);
                
                // Set up hierarchy
                newNeuron.region = newBrain.regions[neuron.region.name];
                newNeuron.transform.SetParent(newBrain.regions[neuron.region.name].gameObject.transform, false);
                newNeuron.InitNeuron(sphereMesh, glowMaterial, activeNeuronSize);

                // Add to brain and region
                newBrain.AddNeuron(newNeuron);
                RegionData thisRegion = newBrain.regions[neuron.region.name];
                thisRegion.AddNeuron(newNeuron);


            }
        }
        return newBrain;
    }
    
    private void SetupFeatureColours(HashSet<string> featureSetNames)
    {
        featureColoursDict.Clear();
        int colourIx = 0;
        foreach (string feature in featureSetNames)
        {
            if (!featureColoursDict.ContainsKey(feature))
            {
                // assign a colour from the list
                Color color = featuresetColourList.ElementAt(colourIx % featuresetColourList.Count).Value;
                featureColoursDict[feature] = color;
                colourIx++;
            }
        }

        foreach (var kvp in featureColoursDict)
        {
            Debug.Log($"Feature: {kvp.Key}, Colour: {kvp.Value}");
        }
    }   

    private Color GetFeatureColour(HashSet<string> activeFeatures)
    {
        // todo debug, this isn't working 2nd time its called??
        Color fColour = Color.white;
        Debug.Log("Getting colour for features: " + string.Join(", ", activeFeatures));
        if (activeFeatures.Count == 1)
        {
            string feature = activeFeatures.First();
            Debug.Log("Getting colour for feature: " + feature);
            if (featureColoursDict.ContainsKey(feature))
            {
                fColour = featureColoursDict[feature];
            }
            else
            {
                Debug.LogWarning($"Feature '{feature}' not found in featureColoursDict!");
                fColour = Color.white;
            }
        }
        else
        {
            fColour = Color.Lerp(Color.red, Color.blue, 0.5f); // purple for multiple features
        }
        return fColour; 
    }

    private void DeleteLabels(GameObject parentLabel)
    {
        // Find all label objects and destroy them
        foreach (Transform child in parentLabel.transform)
        {
            Destroy(child.gameObject);
        }
    }
    private GameObject AddLabel(GameObject parentLabel, string labelText, Vector3 position, bool drawLine = false, Vector3 lineEndPoint = default(Vector3))
    {

        float labelTextOffset = -200f; // don't overlp with line

        var labelObj = new GameObject(labelText + "_Label");
        var tmp = labelObj.AddComponent<TMPro.TextMeshPro>();
        tmp.text = labelText;
        tmp.fontSize = 200;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;

        // Set a fixed width for all labels (adjust as needed)
        float labelWidth = 200f; // Wide enough for "Rhombencephalon"
        float labelHeight = 120f;
        tmp.rectTransform.sizeDelta = new Vector2(labelWidth, labelHeight);
        labelObj.transform.position = position - new Vector3(labelTextOffset, 0, 0); // Offset to right of line

        Vector3 direction = (Camera.main.transform.position - labelObj.transform.position).normalized;
        labelObj.transform.forward = direction; // Face the camera
        labelObj.transform.Rotate(0, 210, 0); // Rotate again to face the camera correctly
        labelObj.transform.SetParent(parentLabel.transform, false);

        if (drawLine)
        {
            var lineObj = new GameObject(labelText + "_LabelLine");
            var lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, position);
            lineRenderer.SetPosition(1, lineEndPoint);
            lineRenderer.startWidth = 2f;
            lineRenderer.endWidth = 2f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

            // Set line color from regionColours dictionary
            Color regionColor = regionColours.TryGetValue(labelText, out Color c) ? c : Color.white;
            lineRenderer.startColor = regionColor;
            lineRenderer.endColor = regionColor;

            lineObj.transform.SetParent(parentLabel.transform, false);
        }

        return labelObj;
    }

    string cleanList(string mystr)
    {
        // strip out the enclosing brackets and quote marks
        mystr = mystr.Trim('[', ']', '"', '\'');
        return mystr;
    }




    // Utility to clear existing neurons and release buffers

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

}
