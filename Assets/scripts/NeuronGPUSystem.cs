using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.InputSystem;

public struct GPUNeuron
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 color;
    public float activation;
}

public class NeuronGPUSystem : MonoBehaviour
{
    private GPUNeuron[] neuronDataCache; // Keep a CPU copy for modifications
    [Header("Data File")]
    public string dataFolder = "Assets/Data";
    public string csvFileName = "neuron_positions.csv";

    [Header("Rendering")]
    public ComputeShader neuronCompute;
    public Material neuronMaterial;
    public Mesh sphereMesh;
    public float neuronSize = 0.5f;

    [Header("Test Activation")]
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
    public KeyCode activateKey = KeyCode.Space;
    public float activationOnClick = 1.0f;
    public float selectionRadius = 2f; // Radius to find nearest neuron

    private int selectedNeuronIndex = -1;

    private ComputeBuffer neuronBuffer;
    private ComputeBuffer argsBuffer;
    private int kernelHandle;
    private Bounds renderBounds;
    private int neuronCount;
    private bool initialized = false;

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
        // Validate references
        if (neuronCompute == null)
        {
            Debug.LogError("Compute Shader not assigned!");
            return;
        }

        if (neuronMaterial == null)
        {
            Debug.LogError("Material not assigned!");
            return;
        }

        if (sphereMesh == null)
        {
            Debug.LogError("Sphere Mesh not assigned! Using default sphere.");
            // Try to create a default sphere
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempSphere);
        }

        // Check if compute shader has the kernel
        if (!neuronCompute.HasKernel("UpdateNeurons"))
        {
            Debug.LogError("Compute shader does not have 'UpdateNeurons' kernel!");
            return;
        }

        LoadNeuronsFromCSV();

        if (neuronCount > 0)
        {
            SetupRendering();
            initialized = true;

            if (positionCameraAtStart)
            {
                PositionCamera();
            }
        }
    }

    void LoadNeuronsFromCSV()
    {
        string fullPath = Path.Combine(dataFolder, csvFileName);

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

        // Validate header format
        if (headers.Length < 7 ||
            headers[0].Trim() != "x_SWCIndex" ||
            headers[1].Trim() != "xpos" ||
            headers[2].Trim() != "ypos" ||
            headers[3].Trim() != "zpos" ||
            headers[4].Trim() != "Region" ||
            headers[5].Trim() != "Subregion" ||
            headers[6].Trim() != "Label")
        {
            Debug.LogError($"Unexpected CSV format. Header: {headerLine}");
            reader.Close();
            return;
        }

        List<GPUNeuron> neuronList = new List<GPUNeuron>();

        // Read neuron data
        string line;
        int lineIdx = 0;
        while ((line = reader.ReadLine()) != null)
        {
            string[] values = line.Split(',');

            if (values.Length < 7)
            {
                Debug.LogWarning($"Skipping malformed row: {line}");
                continue;
            }

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

                // Create GPU neuron
                GPUNeuron neuron = new GPUNeuron
                {
                    position = new Vector3(x, y, z * 3f), // Scale z for better brain shape
                    velocity = Vector3.zero,
                    color = new Vector3(regionColor.r, regionColor.g, regionColor.b),
                    activation = 0f
                };

                neuronList.Add(neuron);
            }

            lineIdx++;
        }

        reader.Close();

        neuronCount = neuronList.Count;
        neuronDataCache = neuronList.ToArray(); // CACHE THE DATA
        Debug.Log($"Loaded {neuronCount} neurons from CSV");

        if (neuronCount == 0)
        {
            Debug.LogError("No neurons loaded from CSV!");
            return;
        }

        // Log first few neurons for debugging
        for (int i = 0; i < Mathf.Min(5, neuronCount); i++)
        {
            Debug.Log($"Neuron {i}: pos={neuronList[i].position}, color={neuronList[i].color}");
        }

        // Calculate bounds
        CalculateBounds(neuronList);

        // Create compute buffer
        neuronBuffer = new ComputeBuffer(neuronCount, sizeof(float) * 10);
        neuronBuffer.SetData(neuronDataCache); // Use cached data

        Debug.Log($"Created neuron buffer with {neuronCount} neurons");

        // Setup compute shader
        kernelHandle = neuronCompute.FindKernel("UpdateNeurons");
        neuronCompute.SetBuffer(kernelHandle, "_Neurons", neuronBuffer);
        neuronCompute.SetInt("_NeuronCount", neuronCount);

        Debug.Log($"Compute shader kernel handle: {kernelHandle}");
    }

    string CleanAndExtractFirstRegion(string regionList)
    {
        // Remove brackets and quotes
        string cleaned = regionList.Replace("[", "").Replace("]", "")
                                   .Replace("'", "").Replace("\"", "");

        // Split by common separators and get first non-empty
        string firstRegion = cleaned.Split(new char[] { '+', '/', ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim())
                                    .FirstOrDefault(s => !string.IsNullOrEmpty(s));

        return string.IsNullOrEmpty(firstRegion) ? "None" : firstRegion;
    }

    void CalculateBounds(List<GPUNeuron> neurons)
    {
        if (neurons.Count == 0) return;

        Vector3 min = neurons[0].position;
        Vector3 max = neurons[0].position;

        foreach (var neuron in neurons)
        {
            min = Vector3.Min(min, neuron.position);
            max = Vector3.Max(max, neuron.position);
        }

        Vector3 center = (min + max) / 2f;
        Vector3 size = (max - min) * 1.5f; // Add padding
        renderBounds = new Bounds(center, size);

        Debug.Log($"Render bounds - Center: {center}, Size: {size}, Min: {min}, Max: {max}");
    }

    void SetupRendering()
    {
        if (neuronCount == 0)
        {
            Debug.LogError("No neurons loaded, cannot setup rendering");
            return;
        }

        // Verify mesh
        if (sphereMesh == null)
        {
            Debug.LogError("Sphere mesh is null!");
            return;
        }

        Debug.Log($"Sphere mesh: vertices={sphereMesh.vertexCount}, triangles={sphereMesh.triangles.Length / 3}");

        // Setup indirect args for instanced rendering
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5];
        args[0] = sphereMesh.GetIndexCount(0);  // Index count per instance
        args[1] = (uint)neuronCount;             // Instance count
        args[2] = sphereMesh.GetIndexStart(0);   // Start index location
        args[3] = sphereMesh.GetBaseVertex(0);   // Base vertex location
        args[4] = 0;                              // Start instance location

        Debug.Log($"Args buffer: indexCount={args[0]}, instanceCount={args[1]}, startIndex={args[2]}, baseVertex={args[3]}");

        argsBuffer.SetData(args);

        // Verify material and shader
        if (neuronMaterial.shader == null)
        {
            Debug.LogError("Material has no shader assigned!");
            return;
        }

        Debug.Log($"Material shader: {neuronMaterial.shader.name}");

        // Note: Buffer binding moved to Update() for Metal compatibility

        Debug.Log("GPU rendering setup complete");
    }
    void PositionCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("No main camera found");
            return;
        }

        // Position camera to look at the center of all neurons
        Vector3 center = renderBounds.center;
        float distance = renderBounds.size.magnitude * 1.5f;

        cam.transform.position = center + new Vector3(distance, distance * 0.5f, distance);
        cam.transform.LookAt(center);

        Debug.Log($"Camera positioned at {cam.transform.position} looking at {center}");
    }

    void Update()
    {
        if (!initialized || neuronCount == 0 || neuronBuffer == null) return;

        // Handle mouse interaction
        if (enableMouseInteraction)
        {
            HandleMouseInteraction();
        }

        // Only run compute shader for random activation if enabled
        // Skip compute shader when manually controlling neurons
        if (randomActivation)
        {
            neuronCompute.SetInt("_NeuronCount", neuronCount);
            neuronCompute.SetFloat("_DeltaTime", Time.deltaTime);
            neuronCompute.SetFloat("_Time", Time.time);
            neuronCompute.SetFloat("_Speed", enableMovement ? movementSpeed : 0f);
            neuronCompute.SetFloat("_Damping", damping);
            neuronCompute.SetInt("_EnableMovement", enableMovement ? 1 : 0);
            neuronCompute.SetInt("_RandomActivation", 1);
            neuronCompute.SetFloat("_ActivationChangeSpeed", activationChangeSpeed);
            neuronCompute.SetInt("_NumActiveNeurons", numActiveNeurons);
            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt(neuronCount / 64f);
            neuronCompute.Dispatch(kernelHandle, threadGroups, 1, 1);
        }

        // IMPORTANT: Set buffer on material EVERY frame (required for Metal)
        neuronMaterial.SetBuffer("_Neurons", neuronBuffer);
        neuronMaterial.SetFloat("_Size", neuronSize);
        neuronMaterial.SetFloat("_InactiveEmission", inactiveEmission);
        neuronMaterial.SetFloat("_ActiveEmission", activeEmission);
        neuronMaterial.SetFloat("_GlowThreshold", glowThreshold);

        // Render all neurons in a single draw call
        Graphics.DrawMeshInstancedIndirect(
            sphereMesh,
            0,
            neuronMaterial,
            renderBounds,
            argsBuffer,
            castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
        );
    }

    void OnDrawGizmos()
    {
        if (drawBoundsGizmo && initialized)
        {
            // Draw the render bounds
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);

            // Draw center point
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(renderBounds.center, renderBounds.size.magnitude * 0.02f);

            // Highlight selected neuron
            if (selectedNeuronIndex >= 0 && selectedNeuronIndex < neuronCount)
            {
                Gizmos.color = Color.cyan;
                Vector3 selectedPos = neuronDataCache[selectedNeuronIndex].position;
                Gizmos.DrawWireSphere(selectedPos, neuronSize * 2f);
            }
        }
    }

    void OnDestroy()
    {
        neuronBuffer?.Release();
        argsBuffer?.Release();
    }

    // Public control methods
    public void SetRandomActivation(bool enabled)
    {
        randomActivation = enabled;
    }

    public void SetNumActiveNeurons(int count)
    {
        numActiveNeurons = Mathf.Clamp(count, 0, neuronCount);
    }

    // Public methods to control individual neurons
    public void ActivateNeuron(int neuronIndex, float activationLevel)
    {
        if (neuronIndex < 0 || neuronIndex >= neuronCount)
        {
            Debug.LogWarning($"Invalid neuron index: {neuronIndex}");
            return;
        }

        neuronDataCache[neuronIndex].activation = Mathf.Clamp01(activationLevel);

        // Update GPU buffer with modified data
        neuronBuffer.SetData(neuronDataCache);

        Debug.Log($"Activated neuron {neuronIndex} with level {activationLevel}");
    }

    public void DeactivateNeuron(int neuronIndex)
    {
        ActivateNeuron(neuronIndex, 0f);
    }

    public void ActivateNeuronRange(int startIndex, int endIndex, float activationLevel)
    {
        for (int i = startIndex; i <= endIndex && i < neuronCount; i++)
        {
            neuronDataCache[i].activation = Mathf.Clamp01(activationLevel);
        }

        neuronBuffer.SetData(neuronDataCache);
        Debug.Log($"Activated neurons {startIndex} to {endIndex}");
    }

    public void DeactivateAllNeurons()
    {
        for (int i = 0; i < neuronCount; i++)
        {
            neuronDataCache[i].activation = 0f;
        }

        neuronBuffer.SetData(neuronDataCache);
        Debug.Log("Deactivated all neurons");
    }

    public float GetNeuronActivation(int neuronIndex)
    {
        if (neuronIndex < 0 || neuronIndex >= neuronCount)
            return 0f;

        return neuronDataCache[neuronIndex].activation;
    }


    void HandleMouseInteraction()
    {
        // Click to select nearest neuron
        if (Mouse.current.leftButton.isPressed)
        {
            Vector3 pos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(pos);
            selectedNeuronIndex = FindNearestNeuronToRay(ray);

            if (selectedNeuronIndex >= 0)
            {
                Debug.Log($"Selected neuron {selectedNeuronIndex} at position {neuronDataCache[selectedNeuronIndex].position}");
                ActivateNeuron(selectedNeuronIndex, activationOnClick);
            }
        }

        // Press key to activate selected neuron
        if (Keyboard.current.spaceKey.isPressed && selectedNeuronIndex >= 0)
        {
            ActivateNeuron(selectedNeuronIndex, activationOnClick);
        }

        // Right click to deactivate
        if (Mouse.current.rightButton.isPressed && selectedNeuronIndex >= 0)
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
            Vector3 neuronPos = neuronDataCache[i].position;

            // Calculate distance from ray to neuron position
            Vector3 rayToNeuron = neuronPos - ray.origin;
            float projectionLength = Vector3.Dot(rayToNeuron, ray.direction);

            // Skip if behind camera
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
}