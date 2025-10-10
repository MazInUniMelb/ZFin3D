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
    Last updated: 30 May 2025

    This script loads neuronal data from a chosen CSV file, creates 3D representations of neurons in Unity
    This neuronal data is captured by the Scott Lab at the University of Melbourne, Australia

    Todos: 
        Show featureset only (Brain1)
        Load new files from Wei
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

    [Tooltip("Start position of line graph showing total signal for timestamp")]
    public Vector3 szLeftPos;
    [Tooltip("End position of line graph showing total signal for timestamp")]
    public Vector3 szRightPos;

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

    [Tooltip("Reference to the main camera in the scene")]
    public CameraHandler cameraHandler;

    [Tooltip("Reference to the sphere mesh used for visualisation")]
    public Mesh sphereMesh;


    [Tooltip("Parent objects for all neurons ie Brain 1")]
    public Vector3 brainPos = new Vector3(200, 200, 500); // Brain position in the scene (default for all neurons)

    [Tooltip("Distance between brains when showing featureset brains")]
    public int distBtwnBrains = 2000; // Distance between brains when showing featureset brains

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
        { 1, Color.magenta }, // Pink
        { 2, Color.blue },    // blue
        { 3, Color.green },   // Green
        { 4, Color.red }      // Red
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
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DirectoryInfo dir = new DirectoryInfo(dataFolder);

        statusMessage.text = "Loading initial position data ...";
        selectedFish = "";
        selectedRegion = "";

        BrainData thisBrain = LoadAllNeuronData(postionsFile, "Brain0");
        brains.Add(thisBrain);

        if (cameraHandler.featureSetViewEnabled)
        {
            BrainData newBrain = CloneFeatureSetNeurons(thisBrain, "Brain1");
            brains.Add(newBrain);
        }

        List<string> regionNames = thisBrain.regions.Keys.ToList();

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
        Dictionary<string, string> fishFiles = Directory.GetFiles(dataFolder, "ZF.FishSignalData*.csv")
            .Select(Path.GetFileName)
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(f, @"FishSignalData(\d+)"))
            .ToDictionary(
                f =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(f, @"FishSignalData(\d+)");
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
        // DeleteLabels(parentLabel);
        // Set line color from regionColours dictionary
        Color regionColor = regionColours.TryGetValue(regionName, out Color c) ? c : Color.white;


        foreach (var brain in brains)
        {
            foreach (var brainRegion in brain.regions.Values)
            {
                brainRegion.gameObject.SetActive(brainRegion.name == regionName);
            }
        }

        // Update the camera position based on the new region for Brain0
        RegionData region = brains[0].regions[regionName];
        cameraHandler.PositionCameras(region.bounds.center, region.bounds.extents.magnitude);
        // if featureSetViewEnabled, show Brain1 (featureset) in viewport top right (instead of dorsal)
        if (cameraHandler.featureSetViewEnabled)
        {
            RegionData region1 = brains[1].regions[regionName];
            cameraHandler.PositionFeatureSetCamera(region1.bounds.center, region1.bounds.extents.magnitude);
        }
        cameraHandler.SetupViewports();

        selectedRegion = regionName;
        statusMessage.text = $"Region selected: {regionName}";

        // Make seizure line if seizure data is loaded for this fish and region
        if (!string.IsNullOrEmpty(selectedFish) && !string.IsNullOrEmpty(selectedRegion))
        {
            GetSelectedSeizureData(selectedRegion, selectedFish);
            statusMessage.text = $"Fish selected: {selectedFish}, Region selected: {selectedRegion}, now loading seizure file";
            // uiHandler.EnableActionButton();
        }
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


    private IEnumerator LoadSeizureData(string fishName, string regionName, int batchSize = 1000)
    {
        if (!fishFileDict.TryGetValue(fishName, out string fishFile))
        {
            statusMessage.text = $"Error: Fish name {fishName} not found in available files.";
            Debug.LogError(statusMessage.text);
            yield break;
        }
        statusMessage.text = $"Loading seizure data for fish: {fishFile} and region: {selectedRegion}";
        int numRows = 0;
        int numCols = 0;

        string fullPath = Path.Combine(dataFolder, fishFile);
        if (!File.Exists(fullPath))
        {
            Debug.LogError("File not found: " + fullPath);
            yield break;
        }

        // First pass: get number of columns and rows
        using (StreamReader reader = new StreamReader(fullPath))
        {
            string headerLine = reader.ReadLine(); // skip header
            string firstDataLine = reader.ReadLine();
            if (firstDataLine == null)
            {
                statusMessage.text = $"Error: Empty file: {fullPath}";
            }
            string[] firstData = firstDataLine.Split(',');
            numCols = firstData.Length - 10; // 10 metadata columns
            numRows = 1; // Count the first data line
            while (reader.ReadLine() != null)
                numRows++;
        }
        Debug.Log($"Found {numCols} signal columns for {numRows} neuron/rows, in {fishFile}");

        statusMessage.text = $"Detected {numRows} rows and {numCols} signal columns in {fishFile}";
        int rowIdx = 0;
        int firstActivityColIdx = 10; // Skip the first 10 columns of neuron data
        int progress = 0;
        int maxProgress = numCols * numRows;
        using (StreamReader reader = new StreamReader(fullPath))
        {
            string line;

            // Skip header
            reader.ReadLine();
            // Read the rest of the seizure file line by line
            Debug.Log($"Starting while loop {rowIdx} < {numRows}");
            // int maxNeurons = brains[0].neurons.Count;
            while (rowIdx < numRows)
            {
                line = reader.ReadLine();
                if (line == null) break; // End of file
                string[] values = line.Split(',');
                int colIdx = firstActivityColIdx;
                int forIterations = 0;
                while (colIdx < values.Length)
                {
                    if (float.TryParse(values[colIdx], out float activationValue))
                    {
                        progress += 1;
                        foreach (var brain in brains)
                        {
                            if (rowIdx >= brain.neurons.Count)
                            {
                                // Debug.LogWarning($"Skipping row {rowIdx} for brain {brain.name} with only {brain.neurons.Count} neurons.");
                                continue;
                            }
                            var neuron = brain.neurons[rowIdx];
                            neuron.AddActivity(fishName, activationValue > 0 ? 1 : 0, colIdx - firstActivityColIdx);
                        }
                    }
                    colIdx++;
                    forIterations++;
                    if (forIterations > values.Length)
                    {
                        // Debug.Log($"Breaking out of the for loop {forIterations}. Total columns {values.Length}");
                        break;
                    }
                }

                // move to next line in file
                rowIdx += 1;
                if (rowIdx % batchSize == 0)
                {
                    progressBarFill.fillAmount = (float)progress / maxProgress;
                    progressBarText.text = $"{(int)((float)progress / maxProgress * 100)}%";
                    // Debug.Log($"Processed {rowIdx}/{numRows} rows, progress: {progress}/{maxProgress}");
                    yield return null; // Yield to avoid freezing
                }
            }
        }

        // for testing only, get activated nodes for fish05 at timestamp 2751
        // activeNeurons = (brains[0].regions[selectedRegion].GetActiveNeurons(selectedFish, 2751));
        // // get list of indexes of active neurons
        // List<int> activeNeuronIndexes = new List<int>();
        // string ixList = "[";
        // foreach (var neuron in activeNeurons)
        // {
        //     ixList += neuron.neuronIdx.ToString();
        //     ixList += ", ";
        // }
        // ixList += "]";
        // Debug.Log($"At timestamp 2751, found {activeNeurons.Count} active neurons");
        // Debug.Log(ixList);
        // end of testing only
        

        Debug.Log("Signal data loaded.");
        foreach (var brain in brains)
        {
            foreach (var region in brain.regions.Values)
            {
                region.UpdateMinMax();
            }
        }
        progressBarFill.fillAmount = (float)progress / maxProgress;
        progressBarText.text = $"{(int)((float)progress / maxProgress * 100)}%";
        progressBar.SetActive(false);
        uiHandler.EnableActionButtons();
        // Debug.Log($"Processed {rowIdx}/{numRows} rows, progress: {progress}/{maxProgress}");
        statusMessage.text = $"Seizure data loaded for fish: {selectedFish}, region: {selectedRegion} with {numRows} rows and {numCols} signal columns. Now creating seizure line."; 
        yield return StartCoroutine(MakeSeizureLine(regionName));
        yield return null;
    }


    void GetSelectedSeizureData(string regionName, string fishName)
    {
        if (brains[0].totalActivityList.ContainsKey(fishName))
        {
            Debug.Log("Seizure data for fish " + fishName + " already loaded");
            return;
        }
        
        uiHandler.DisableActionButtons();

        RegionData region = brains[0].regions[regionName];
        Debug.Log($"Brain[0] has {brains[0].neurons.Count} neurons");
        Debug.Log($"Region {region.name} has {region.neurons.Count} neurons");
        Debug.Log("about to load seizure data for fish " + fishName);
        progressBar.SetActive(true);
        progressBarFill.fillAmount = 0f;
        progressBarText.text = "0%";
        StartCoroutine(LoadSeizureData(fishName, regionName));
    }

    IEnumerator MakeSeizureLine(string regionName)
    {
        Color regionColor = regionColours[regionName];
        // Create or clear existing seizure line object
        if (szLine != null)
        {
            Destroy(szLine);
        }
        szLine = new GameObject("SeizureLine");

        MeshRenderer mRenderer = szLine.AddComponent<MeshRenderer>();
        mRenderer.material = glowMaterial;

        LineRenderer lineRenderer = szLine.GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = szLine.AddComponent<LineRenderer>();

        Color lineColour = Color.yellow;
        float zPos = szRightPos.z;

        float graphHeight = 200f;

        int numPoints = (int)brains[0].regions[regionName].sumActivities[selectedFish].Count;
        Debug.Log($"Creating seizure line with {numPoints} points for fish {selectedFish} in region {regionName}");
        lineRenderer.positionCount = numPoints;

        float minValue = brains[0].regions[regionName].minActivities[selectedFish];
        float maxValue = brains[0].regions[regionName].maxActivities[selectedFish];

        float yPos = szLeftPos.y;
        float markerSpacer = 40f;

        timelinePoints.Clear();

        for (int i = 0; i < numPoints; i++)
        {
            float t = (float)i / (numPoints - 1); // Normalized position [0,1]
                                                  // show values of szleftpos and szrightpos and t
            float xPos = Mathf.Lerp(szLeftPos.x, szRightPos.x, t);

            float value = brains[0].regions[regionName].sumActivities[selectedFish][i];
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
        if (brains[0].regions[selectedRegion].sumActivities[selectedFish].Count < 1f)
        {
            statusMessage.text = "No seizure data loaded. Please load a fish file first.";
            return;
        }

        // make the menu ui inactive
        uiHandler.HideMenuPanel();

        // Start stepping through the seizure data

        int markerTimestamp = currentSignalTimestamp >= 0 ? currentSignalTimestamp : 0;
        StartCoroutine(StepThroughSeizureData(markerTimestamp));
    }

    public void MakeSeizureFrames()
    {
        if (brains[0].regions[selectedRegion].sumActivities[selectedFish].Count < 1f)
        {
            statusMessage.text = "No seizure data loaded. Please load a fish file first.";
            return;
        }
        // make the menu ui inactive
        uiHandler.HideMenuPanel();

        int markerTimestamp = currentSignalTimestamp >= 0 ? currentSignalTimestamp : 0;
        StartCoroutine(ExportSignalDataFrames(selectedFish, markerTimestamp));
    }

    public void OnMarkerDrag()
    {
        Debug.Log("Marker dragged");
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
                Debug.Log($"Marker dragged to timestamp {closestIdx}");

                
                // if (startTimestamp != math.min(endTimestamp - nbrFrames, closestIdx))
                // {
                //     // deactivate all active neurons first
                //     foreach (NeuronData neuron in activeNeurons)
                //     {
                //         neuron.Deactivate();
                //     }

                // }


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
                    activeNeurons.AddRange(brain.regions[selectedRegion].GetActiveNeurons(selectedFish, closestIdx));
                }
                foreach (NeuronData neuron in activeNeurons)
                {
                    neuron.SetActiveState(selectedFish, closestIdx);
                }

                endTimestamp = Math.Min(startTimestamp + nbrFrames, timelinePoints.Count - 1);
                ResetEndMarker(endTimestamp);
            }
        }
    }

    IEnumerator StepThroughSeizureData(int startTimestamp, int skipSize = 1, int batchSize = 100)
    {
        int numNeurons = brains[0].regions[selectedRegion].neurons.Count;
        int numTimestamps = (int)brains[0].regions[selectedRegion].sumActivities[selectedFish].Count;

        Debug.Log($"Stepping through seizure data for fish {selectedFish} in region {selectedRegion}. Num Neurons: {numNeurons}, Num Timestamps: {numTimestamps}");

        int endTimestamp = Math.Min(startTimestamp + nbrFrames, numTimestamps);

        for (int col = startTimestamp; col < endTimestamp; col += skipSize)
        {
            // todo add pause button
            //while (isPaused)
            //    yield return null;

            // statusMessage.text = $"Stepping through timestamp: {col + 1}/{numTimestamps}";

            currentSignalTimestamp = col;
            //int neuronsProcessed = 0;
            UpdateNeuronStates(col, selectedFish, selectedRegion);

            // Move timeline marker
            if (timelineMarker != null && timelinePoints.Count > col)
            {
                timelineMarker.transform.position = timelinePoints[col];
            }

            // yield return new WaitForSeconds(animationStepInterval);
            yield return null;
        }
        endTimestamp = Mathf.Min(currentSignalTimestamp + nbrFrames, numTimestamps-1);
        ResetEndMarker(endTimestamp);
        uiHandler.ShowMenuPanel();
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
            // Activate neurons for this timestamp
            activeNeurons.AddRange(brain.regions[regionName].GetActiveNeurons(fishName, timestamp));

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
        if (endTimelineMarker != null)
        {
            var draggable = endTimelineMarker.GetComponent<DraggableObject>();
            if (draggable != null)
            {
                // enable dragging
                //draggable.enabled = true;
            }
        }
        endTimelineMarker.transform.position = timelinePoints[endTimestamp];
    }

    IEnumerator ExportSignalDataFrames(string fishID, int markerTimestamp)
    {
        int numNeurons = brains[0].regions[selectedRegion].neurons.Count;
        int numTimestamps = (int)brains[0].regions[selectedRegion].sumActivities[selectedFish].Count;

        int startFrame = Mathf.Clamp(markerTimestamp, 0, numTimestamps - 1);
        int endFrame = Mathf.Min(startFrame + nbrFrames, numTimestamps);
        float lastTime;

        //FreezeEndMarker();

        string regionID = selectedRegion.Substring(0, Math.Min(5, selectedRegion.Length)); // first 5 letters of region name
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "SignalDataFrames/Signals_" + fishID + "_" + regionID);
        if (Directory.Exists(folder))
            // delete existing folder and contents if previosuly created
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);


        for (int col = startFrame; col < endFrame; col++)
        {
            lastTime = Time.time;
            currentSignalTimestamp = col;

            // Move timeline marker
            if (timelineMarker != null && timelinePoints.Count > col)
            {
                timelineMarker.transform.position = timelinePoints[col];
            }

            UpdateNeuronStates(col, fishID, selectedRegion);
            yield return new WaitForEndOfFrame();
            string framefile = Path.Combine(folder, $"{fishID}_{regionID}_{col:D05}.png");
            ScreenCapture.CaptureScreenshot(framefile);
            yield return new WaitForSeconds(0.01f);
        }

        Debug.Log($"Frames exported to {folder}");


        int endTimestamp = Mathf.Min(currentSignalTimestamp + nbrFrames, numTimestamps-1);
        ResetEndMarker(endTimestamp);
        uiHandler.ShowMenuPanel();
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

        // delete this, for testing only
        List<int> xtraIdx = new List<int> {6418, 6903, 6992, 7008, 7090, 7350, 7361, 7398, 7411, 7465, 7954, 8260, 8308, 8392, 8942, 8947, 10623, 11918, 12484, 12595, 12767, 12841, 12846, 13534, 13790, 13825, 14009, 15754, 16829, 16938 };

        Debug.Log("Loading file: " + fullPath);
        StreamReader reader = new StreamReader(fullPath);

        // Read the header line and ensure it matches the expected format
        string headerLine = reader.ReadLine();
        string[] headers = headerLine.Split(','); // Assuming comma-separated values
        if (headers.Length < 6 ||
            headers[0].Trim() != "# SWC Index" ||
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
                newNeuron.originalPosition = new Vector3(x, y, z);
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
                    int.TryParse(values[1], out int neuronFeatureValue);
                    if (neuronFeatureValue != 0)
                        newNeuron.featureData.AddFeature(headers[fi]);
                    
                    // delete this, for testing only
                    if (xtraIdx.Contains(swcIndex))
                    {
                        newNeuron.featureData.AddFeature(headers[fi]);
                    }
                    
                    // set colour from tobecreated feature colour dictionary
                }

                defaultBrain.AddNeuron(newNeuron);
                region.AddNeuron(newNeuron);

            }
            lineIdx++;
        }//end of reading file line by line

        reader.Close();

        Debug.Log("Added " + defaultBrain.neurons.Count + " neurons to new brain gameObject: " + defaultBrain.name);

        return defaultBrain;
    } // end of LoadAllNeuronData


    private BrainData CloneFeatureSetNeurons(BrainData thisBrain, string brainName)
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

        HashSet<string> allFeatureSets = thisBrain.activeFeatureSets;
        Debug.Log($"The feature sets are: {string.Join(", ", allFeatureSets)}");
        Color nColour;

        // Add only the featureset neurons from thisBrain to newBrain
        foreach (NeuronData neuron in thisBrain.neurons)
        {
            if (neuron.featureData != null)
            {
                //Debug.Log($"Neuron {neuron.neuronIdx} has featureset data {neuron.featureData}");
                if (neuron.featureData.activeFeatures.Count > 0)
                {
                    Debug.Log($"Neuron {neuron.neuronIdx} has featureset data {string.Join(", ", neuron.featureData.activeFeatures)}");

                    // nColour = GetFeatureColour(neuron.featureData.activeFeatures);
                    nColour = Color.magenta;

                    // Clone the neuron gameobject
                    GameObject original = neuron.gameObject;
                    GameObject neuronObj = Instantiate(original);
                    NeuronData newNeuron = neuronObj.GetComponent<NeuronData>();
                    newNeuron.color = nColour;
                    newNeuron.brain = newBrain;
                    neuronObj.name = original.name; // remove (Clone) from name
                    Vector3 newPos = neuron.originalPosition;
                    newPos.x += distBtwnBrains;
                    newNeuron.originalPosition = newPos;

                    newNeuron.transform.SetParent(newBrain.regions[neuron.region.name].gameObject.transform, false);
                    newNeuron.InitNeuron(sphereMesh, glowMaterial, activeNeuronSize);

                    newBrain.AddNeuron(newNeuron);
                    RegionData thisRegion = newBrain.regions[neuron.region.name];
                    thisRegion.AddNeuron(newNeuron);

                    // change colour of neuron in Brain0 as well
                    Renderer nRenderer = neuron.renderer;
                    nRenderer.material.SetColor("_EmissionColor", nColour);
                    nRenderer.material.SetColor("_BaseColor", nColour);
                }
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
