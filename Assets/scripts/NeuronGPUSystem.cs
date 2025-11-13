using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct GPUNeuron
{
    public Vector4 position;
    public Vector4 originalPosition;
    public Vector4 velocity;
    public Vector4 color;
    public Vector4 activationAndPadding; // x = activation, yzw = padding

    // Helper property for easier access
    public float activation
    {
        get { return activationAndPadding.x; }
        set { activationAndPadding.x = value; }
    }
}

public class NeuronGPUSystem : MonoBehaviour
{
    [Header("Data Files")]
    public string dataFolder = "Assets/Data";
    public string positionsFileName = "neuron_positions.csv";
    public string timeseriesFileName = ""; // e.g., "fish_seizure_data.csv"

    [Header("Rendering")]
    public ComputeShader neuronCompute;
    public Material neuronMaterial;
    public Mesh sphereMesh;
    public float neuronSize = 0.5f;

    [Header("Timeseries Playback")]
    public bool useTimeseries = false;
    public bool autoPlay = false;
    public float playbackSpeed = 60f; // Frames per second
    public bool loop = true;
    public int currentTimeStep = 0;

    [Header("Test Activation (when not using timeseries)")]
    public bool randomActivation = true;
    public float activationChangeSpeed = 2f;
    public int numActiveNeurons = 1000;

    [Header("Visual Settings")]
    public float inactiveEmission = 0.1f;
    public float activeEmission = 5.0f;
    public float glowThreshold = 0.3f;

    [Header("Movement")]
    public bool enableMovement = false;
    public float movementSpeed = 0.5f;
    public float damping = 0.8f;

    [Header("Debug")]
    public bool drawBoundsGizmo = true;
    public bool positionCameraAtStart = true;

    [Header("Interaction")]
    public bool enableMouseInteraction = true;
    public Key activateKey = Key.Space;
    public float activationOnClick = 1.0f;
    public float selectionRadius = 2f;

    [Header("Brain Rotation")]
    public bool enableRotation = true;
    public float rotationSpeed = 10f; // Degrees per second
    public Vector3 rotationAxis = Vector3.up; // Y-axis by default
    private float currentRotationAngle = 0f;

    private ComputeBuffer neuronBuffer;
    private ComputeBuffer activationBuffer;
    private ComputeBuffer argsBuffer;
    private int kernelHandle;
    private Bounds renderBounds;
    private int neuronCount;
    private int timeSteps = 0;
    private bool initialized = false;
    private bool timeseriesLoaded = false;

    private GPUNeuron[] neuronDataCache;
    private int selectedNeuronIndex = -1;
    private float playbackTime = 0f;

    // Input System references
    private Mouse mouse;
    private Keyboard keyboard;

    // Region colors mapping
    private Dictionary<string, Color> regionColors = new Dictionary<string, Color>()
    {
        { "Telencephalon", new Color(1f, 0.2f, 0.2f) },
        { "Diencephalon", new Color(0.2f, 1f, 0.2f) },
        { "Mesencephalon", new Color(0.2f, 0.2f, 1f) },
        { "Rhombencephalon", new Color(1f, 1f, 0.2f) },
        { "Medulla", new Color(1f, 0.5f, 0.2f) },
        { "None", Color.white }
    };

    void Start()
    {
        // Initialize Input System devices
        mouse = Mouse.current;
        keyboard = Keyboard.current;

        if (mouse == null || keyboard == null)
        {
            Debug.LogWarning("Mouse or Keyboard not detected by Input System");
        }

        if (!ValidateReferences()) return;

        LoadNeuronsFromCSV();

        if (neuronCount > 0)
        {
            SetupRendering();

            // Load timeseries if specified
            if (useTimeseries && !string.IsNullOrEmpty(timeseriesFileName))
            {
                LoadTimeseriesData();
            }

            initialized = true;

            if (positionCameraAtStart)
            {
                PositionCamera();
            }
        }
    }

    bool ValidateReferences()
    {
        if (neuronCompute == null)
        {
            Debug.LogError("Compute Shader not assigned!");
            return false;
        }

        if (neuronMaterial == null)
        {
            Debug.LogError("Material not assigned!");
            return false;
        }

        if (sphereMesh == null)
        {
            Debug.Log("Sphere Mesh not assigned! Using default sphere.");
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempSphere);
        }

        if (!neuronCompute.HasKernel("UpdateNeurons"))
        {
            Debug.LogError("Compute shader does not have 'UpdateNeurons' kernel!");
            return false;
        }

        return true;
    }

    void LoadNeuronsFromCSV()
    {
        string fullPath = Path.Combine(dataFolder, positionsFileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"CSV file not found: {fullPath}");
            return;
        }

        Debug.Log($"Loading neuron data from: {fullPath}");

        StreamReader reader = new StreamReader(fullPath);

        // Read header
        string headerLine = reader.ReadLine();
        string[] headers = headerLine.Split(',');

        if (headers.Length < 7)
        {
            Debug.LogError($"Unexpected CSV format. Header: {headerLine}");
            reader.Close();
            return;
        }

        List<GPUNeuron> neuronList = new List<GPUNeuron>();

        // Read neuron data
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            string[] values = line.Split(',');

            if (values.Length < 7) continue;

            // Parse position
            if (float.TryParse(values[1], out float x) &&
                float.TryParse(values[2], out float y) &&
                float.TryParse(values[3], out float z))
            {
                // Parse region
                string regionList = values[4].Trim();
                string firstRegion = CleanAndExtractFirstRegion(regionList);

                // Get color for this region
                Color regionColor = regionColors.ContainsKey(firstRegion)
                    ? regionColors[firstRegion]
                    : Color.white;

                Vector3 pos = new Vector3(x, y, z * 3f);

                // Create GPU neuron with proper alignment
                GPUNeuron neuron = new GPUNeuron
                {
                    position = new Vector4(pos.x, pos.y, pos.z, 0),
                    originalPosition = new Vector4(pos.x, pos.y, pos.z, 0),
                    velocity = Vector4.zero,
                    color = new Vector4(regionColor.r, regionColor.g, regionColor.b, 1),
                    activationAndPadding = new Vector4(0, 0, 0, 0) // activation in x component
                };

                neuronList.Add(neuron);
            }
        }

        reader.Close();

        neuronCount = neuronList.Count;
        neuronDataCache = neuronList.ToArray();

        Debug.Log($"Loaded {neuronCount} neurons from CSV");

        // Calculate bounds
        CalculateBounds(neuronList);

        // Create compute buffer - 5 float4s = 80 bytes, but aligned to 96
        // Let Unity calculate the stride automatically
        neuronBuffer = new ComputeBuffer(neuronCount, 80); // Explicit 96 bytes
        neuronBuffer.SetData(neuronDataCache);

        // Verify the buffer stride
        Debug.Log($"Buffer stride: {neuronBuffer.stride} bytes, struct size: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUNeuron))} bytes");

        // Setup compute shader
        kernelHandle = neuronCompute.FindKernel("UpdateNeurons");
        neuronCompute.SetBuffer(kernelHandle, "_Neurons", neuronBuffer);
        neuronCompute.SetInt("_NeuronCount", neuronCount);
    }

    void LoadTimeseriesData()
    {
        string fullPath = Path.Combine(dataFolder, timeseriesFileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Timeseries file not found: {fullPath}");
            return;
        }

        Debug.Log($"Loading timeseries data from: {fullPath}");

        // First pass: determine dimensions
        int numRows = 0;
        int numCols = 0;
        int firstActivityColIdx = 10;

        using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072))
        using (var reader = new StreamReader(fileStream))
        {
            reader.ReadLine(); // skip header
            string firstDataLine = reader.ReadLine();

            if (firstDataLine == null)
            {
                Debug.LogError($"Empty timeseries file: {fullPath}");
                return;
            }

            string[] firstData = firstDataLine.Split(',');
            numCols = firstData.Length - firstActivityColIdx;
            numRows = 1;

            while (reader.ReadLine() != null)
                numRows++;
        }

        timeSteps = numCols;

        Debug.Log($"Timeseries dimensions: {numRows} neurons x {numCols} timesteps");

        if (numRows != neuronCount)
        {
            Debug.LogWarning($"Timeseries neuron count ({numRows}) doesn't match loaded neurons ({neuronCount})");
        }

        // Pre-allocate flat array for all activation data
        float[] allActivations = new float[neuronCount * timeSteps];

        // Second pass: read activation data
        int rowIdx = 0;

        using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 262144))
        using (var reader = new StreamReader(fileStream))
        {
            reader.ReadLine(); // Skip header
            string line;

            while ((line = reader.ReadLine()) != null && rowIdx < neuronCount)
            {
                string[] values = line.Split(',');

                if (values.Length < firstActivityColIdx)
                {
                    rowIdx++;
                    continue;
                }

                // Read activation values for this neuron across all timesteps
                int baseIndex = rowIdx * timeSteps;

                for (int col = 0; col < timeSteps && (firstActivityColIdx + col) < values.Length; col++)
                {
                    if (float.TryParse(values[firstActivityColIdx + col], out float value))
                    {
                        allActivations[baseIndex + col] = value;
                    }
                    else
                    {
                        allActivations[baseIndex + col] = 0f;
                    }
                }

                rowIdx++;
            }
        }

        Debug.Log($"Loaded {allActivations.Length} activation values ({allActivations.Length * 4 / 1024f / 1024f:F2} MB)");

        // Create activation buffer
        activationBuffer = new ComputeBuffer(allActivations.Length, sizeof(float));
        activationBuffer.SetData(allActivations);

        // Set buffer on compute shader
        neuronCompute.SetBuffer(kernelHandle, "_Activations", activationBuffer);
        neuronCompute.SetInt("_TimeSteps", timeSteps);

        // Set buffer on material
        neuronMaterial.SetBuffer("_Activations", activationBuffer);
        neuronMaterial.SetInt("_TimeSteps", timeSteps);

        timeseriesLoaded = true;
        currentTimeStep = 0;

        Debug.Log("Timeseries data loaded successfully");
    }

    string CleanAndExtractFirstRegion(string regionList)
    {
        string cleaned = regionList.Replace("[", "").Replace("]", "")
                                   .Replace("'", "").Replace("\"", "");

        string firstRegion = cleaned.Split(new char[] { '+', '/', ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim())
                                    .FirstOrDefault(s => !string.IsNullOrEmpty(s));

        return string.IsNullOrEmpty(firstRegion) ? "None" : firstRegion;
    }

    void CalculateBounds(List<GPUNeuron> neurons)
    {
        if (neurons.Count == 0) return;

        // Convert Vector4 to Vector3 for bounds calculation
        Vector3 min = new Vector3(neurons[0].position.x, neurons[0].position.y, neurons[0].position.z);
        Vector3 max = min;

        foreach (var neuron in neurons)
        {
            Vector3 pos = new Vector3(neuron.position.x, neuron.position.y, neuron.position.z);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        Vector3 center = (min + max) / 2f;
        Vector3 size = (max - min) * 1.5f;
        renderBounds = new Bounds(center, size);

        Debug.Log($"Render bounds - Center: {center}, Size: {size}");
    }

    void SetupRendering()
    {
        if (neuronCount == 0)
        {
            Debug.LogError("No neurons loaded, cannot setup rendering");
            return;
        }

        // Setup indirect args
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5];
        args[0] = sphereMesh.GetIndexCount(0);
        args[1] = (uint)neuronCount;
        args[2] = sphereMesh.GetIndexStart(0);
        args[3] = sphereMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer.SetData(args);

        Debug.Log("GPU rendering setup complete");
    }

    void PositionCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 center = renderBounds.center;
        float distance = renderBounds.size.magnitude * 1.5f;

        cam.transform.position = center + new Vector3(distance, distance * 0.5f, distance);
        cam.transform.LookAt(center);

        Debug.Log($"Camera positioned at {cam.transform.position}");
    }

    void Update()
    {
        if (!initialized || neuronCount == 0 || neuronBuffer == null) return;

        // Update rotation
        if (enableRotation)
        {
            currentRotationAngle += rotationSpeed * Time.deltaTime;
            currentRotationAngle = currentRotationAngle % 360f; // Keep it between 0-360
        }
        // Check if input devices are available
        if (keyboard == null) keyboard = Keyboard.current;
        if (mouse == null) mouse = Mouse.current;

        // Handle keyboard controls for timeseries playback
        HandleTimeseriesControls();

        // Handle mouse interaction
        if (enableMouseInteraction && !useTimeseries && mouse != null)
        {
            HandleMouseInteraction();
        }

        // Update playback
        if (useTimeseries && timeseriesLoaded)
        {
            UpdateTimeseriesPlayback();
        }
        else if (randomActivation)
        {
            UpdateRandomActivation();
        }

        // Render
        RenderNeurons();
    }

    void HandleTimeseriesControls()
    {
        if (!useTimeseries || !timeseriesLoaded || keyboard == null) return;

        // Space to play/pause
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            autoPlay = !autoPlay;
            Debug.Log($"Playback {(autoPlay ? "playing" : "paused")} at timestep {currentTimeStep}");
        }

        // Arrow keys for manual stepping
        if (keyboard.rightArrowKey.wasPressedThisFrame)
        {
            StepForward();
        }

        if (keyboard.leftArrowKey.wasPressedThisFrame)
        {
            StepBackward();
        }

        // R to reset
        if (keyboard.rKey.wasPressedThisFrame)
        {
            currentTimeStep = 0;
            playbackTime = 0f;
            Debug.Log("Reset to timestep 0");
        }

        // +/- to adjust playback speed
        if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
        {
            playbackSpeed = Mathf.Min(playbackSpeed * 1.5f, 240f);
            Debug.Log($"Playback speed: {playbackSpeed:F1} fps");
        }

        if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
        {
            playbackSpeed = Mathf.Max(playbackSpeed / 1.5f, 1f);
            Debug.Log($"Playback speed: {playbackSpeed:F1} fps");
        }
    }

    void UpdateTimeseriesPlayback()
    {
        if (autoPlay)
        {
            playbackTime += Time.deltaTime * playbackSpeed;
            currentTimeStep = Mathf.FloorToInt(playbackTime);

            if (loop)
            {
                if (currentTimeStep >= timeSteps)
                {
                    currentTimeStep = currentTimeStep % timeSteps;
                    playbackTime = currentTimeStep;
                }
            }
            else
            {
                currentTimeStep = Mathf.Clamp(currentTimeStep, 0, timeSteps - 1);
                if (currentTimeStep >= timeSteps - 1)
                {
                    autoPlay = false;
                }
            }
        }

        // Update compute shader with current timestep
        neuronCompute.SetInt("_UseTimeseries", 1);
        neuronCompute.SetInt("_CurrentTimeStep", currentTimeStep);
        neuronCompute.SetFloat("_TimeInterpolation", playbackTime - currentTimeStep);

        // Set rotation parameters
        neuronCompute.SetFloat("_RotationAngle", currentRotationAngle * Mathf.Deg2Rad);
        neuronCompute.SetVector("_RotationAxis", rotationAxis.normalized);
        neuronCompute.SetVector("_BrainCenter", renderBounds.center);
        neuronCompute.SetInt("_ApplyRotation", enableRotation ? 1 : 0);

        // Dispatch compute shader
        neuronCompute.SetInt("_NeuronCount", neuronCount);
        neuronCompute.SetFloat("_DeltaTime", Time.deltaTime);
        neuronCompute.SetFloat("_Time", Time.time);
        neuronCompute.SetFloat("_Speed", enableMovement ? movementSpeed : 0f);
        neuronCompute.SetFloat("_Damping", damping);
        neuronCompute.SetInt("_EnableMovement", enableMovement ? 1 : 0);
        neuronCompute.SetInt("_RandomActivation", 0);

        int threadGroups = Mathf.CeilToInt(neuronCount / 64f);
        neuronCompute.Dispatch(kernelHandle, threadGroups, 1, 1);
    }

    void UpdateRandomActivation()
    {
        // Set rotation parameters
        neuronCompute.SetFloat("_RotationAngle", currentRotationAngle * Mathf.Deg2Rad);
        neuronCompute.SetVector("_RotationAxis", rotationAxis.normalized);
        neuronCompute.SetVector("_BrainCenter", renderBounds.center);
        neuronCompute.SetInt("_ApplyRotation", enableRotation ? 1 : 0);

        neuronCompute.SetInt("_NeuronCount", neuronCount);
        neuronCompute.SetFloat("_DeltaTime", Time.deltaTime);
        neuronCompute.SetFloat("_Time", Time.time);
        neuronCompute.SetFloat("_Speed", enableMovement ? movementSpeed : 0f);
        neuronCompute.SetFloat("_Damping", damping);
        neuronCompute.SetInt("_EnableMovement", enableMovement ? 1 : 0);
        neuronCompute.SetInt("_RandomActivation", 1);
        neuronCompute.SetInt("_UseTimeseries", 0);
        neuronCompute.SetFloat("_ActivationChangeSpeed", activationChangeSpeed);
        neuronCompute.SetInt("_NumActiveNeurons", numActiveNeurons);

        int threadGroups = Mathf.CeilToInt(neuronCount / 64f);
        neuronCompute.Dispatch(kernelHandle, threadGroups, 1, 1);
    }

    void RenderNeurons()
    {
        // Set buffer on material EVERY frame (required for Metal)
        neuronMaterial.SetBuffer("_Neurons", neuronBuffer);
        neuronMaterial.SetFloat("_Size", neuronSize);
        neuronMaterial.SetFloat("_InactiveEmission", inactiveEmission);
        neuronMaterial.SetFloat("_ActiveEmission", activeEmission);
        neuronMaterial.SetFloat("_GlowThreshold", glowThreshold);

        if (useTimeseries && timeseriesLoaded)
        {
            neuronMaterial.SetInt("_CurrentTimeStep", currentTimeStep);
            neuronMaterial.SetFloat("_TimeInterpolation", playbackTime - currentTimeStep);
        }

        neuronCompute.SetInt("_ApplyRotation", enableRotation ? 1 : 0);
        if (enableRotation)
        {
            neuronMaterial.SetFloat("_RotationAngle", currentRotationAngle * Mathf.Deg2Rad);
            neuronMaterial.SetVector("_RotationAxis", rotationAxis.normalized);
            neuronMaterial.SetVector("_BrainCenter", renderBounds.center);
        }

        // Render
        Graphics.DrawMeshInstancedIndirect(
            sphereMesh,
            0,
            neuronMaterial,
            renderBounds,
            argsBuffer,
            castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
        );
    }

    void HandleMouseInteraction()
    {
        if (mouse == null) return;

        // Left click to select
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            selectedNeuronIndex = FindNearestNeuronToRay(ray);

            if (selectedNeuronIndex >= 0)
            {
                Debug.Log($"Selected neuron {selectedNeuronIndex} at position {neuronDataCache[selectedNeuronIndex].position}");
            }
        }

        // Activate key to activate selected neuron
        if (keyboard != null && keyboard[activateKey].wasPressedThisFrame && selectedNeuronIndex >= 0)
        {
            ActivateNeuron(selectedNeuronIndex, activationOnClick);
        }

        // Right click to deactivate
        if (mouse.rightButton.wasPressedThisFrame && selectedNeuronIndex >= 0)
        {
            DeactivateNeuron(selectedNeuronIndex);
        }
    }

    int FindNearestNeuronToRay(Ray ray)
    {
        float closestDistance = float.MaxValue;
        int closestIndex = -1;

        for (int i = 0; i < neuronCount; i++)
        {
            // Extract Vector3 from Vector4
            Vector3 neuronPos = new Vector3(
                neuronDataCache[i].position.x,
                neuronDataCache[i].position.y,
                neuronDataCache[i].position.z
            );

            Vector3 rayToNeuron = neuronPos - ray.origin;
            float projectionLength = Vector3.Dot(rayToNeuron, ray.direction);

            if (projectionLength < 0) continue;

            Vector3 projectionPoint = ray.origin + ray.direction * projectionLength;
            float distance = Vector3.Distance(projectionPoint, neuronPos);

            if (distance < selectionRadius && distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }


    // Public control methods
    public void ActivateNeuron(int neuronIndex, float activationLevel)
    {
        if (neuronIndex < 0 || neuronIndex >= neuronCount) return;

        // This now uses the helper property
        neuronDataCache[neuronIndex].activation = Mathf.Clamp01(activationLevel);
        neuronBuffer.SetData(neuronDataCache);
    }


    public void DeactivateNeuron(int neuronIndex)
    {
        ActivateNeuron(neuronIndex, 0f);
    }

    public void StepForward()
    {
        if (!timeseriesLoaded) return;
        currentTimeStep = (currentTimeStep + 1) % timeSteps;
        playbackTime = currentTimeStep;
        Debug.Log($"Timestep: {currentTimeStep}/{timeSteps}");
    }

    public void StepBackward()
    {
        if (!timeseriesLoaded) return;
        currentTimeStep = (currentTimeStep - 1 + timeSteps) % timeSteps;
        playbackTime = currentTimeStep;
        Debug.Log($"Timestep: {currentTimeStep}/{timeSteps}");
    }

    public void SetTimeStep(int step)
    {
        if (!timeseriesLoaded) return;
        currentTimeStep = Mathf.Clamp(step, 0, timeSteps - 1);
        playbackTime = currentTimeStep;
    }

    public void SetPlaybackSpeed(float fps)
    {
        playbackSpeed = Mathf.Max(fps, 0.1f);
    }


    void OnDrawGizmos()
    {
        if (drawBoundsGizmo && initialized)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(renderBounds.center, renderBounds.size.magnitude * 0.02f);

            if (selectedNeuronIndex >= 0 && selectedNeuronIndex < neuronCount)
            {
                Gizmos.color = Color.cyan;
                Vector3 selectedPos = new Vector3(
                    neuronDataCache[selectedNeuronIndex].position.x,
                    neuronDataCache[selectedNeuronIndex].position.y,
                    neuronDataCache[selectedNeuronIndex].position.z
                );
                Gizmos.DrawWireSphere(selectedPos, neuronSize * 2f);
            }
        }
    }

    void OnDestroy()
    {
        neuronBuffer?.Release();
        activationBuffer?.Release();
        argsBuffer?.Release();
    }
}