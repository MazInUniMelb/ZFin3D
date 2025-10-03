using UnityEngine;
using System.Collections.Generic;
//using UnityEditor;
using Unity.VisualScripting;

namespace BrainComponents
{
public class FeatureData : MonoBehaviour
{
    private Dictionary<string, int> featureDict = new Dictionary<string, int>();
    private HashSet<string> activeFeatures = new HashSet<string>();
    public Color color;

    public int GetFeature(string featureName)
    {
        return featureDict[featureName];
    }

    private void RefreshActiveFeatures()
    {
        activeFeatures.Clear();
        foreach (var kvp in featureDict)
        {
            if (kvp.Value > 0)
            {
                activeFeatures.Add(kvp.Key);
            }
        }
    }

    public void AddFeature(string featureName, int value = 0)
    {
        featureDict[featureName] = value;
        if(value > 0)
        {
            activeFeatures.Add(featureName);
        }
    }
}

public class NeuronData : MonoBehaviour
{
    public int neuronIdx = -1;
    public Vector3 originalPosition;
    public Color color;
    public string subregion;
    public string label;
    public MeshFilter meshFilter;
    public Renderer renderer;
    public SphereCollider collider;
    public BrainData brain;
    public RegionData region;
    public FeatureData featureData;
    public Dictionary<string, List<float>> activityList = new Dictionary<string, List<float>>();
    public float inactiveNeuronSize = 2.0f; // Set from LoadFishData
    public float activeNeuronSize = 4.0f; // Set from LoadFishData
    public void ResetPosition()
    {
        this.transform.position = originalPosition;
    }

    public void AddActivity(string fishName, float value, int timeIdx = -1)
    {
        if (!activityList.ContainsKey(fishName))
        {
            activityList[fishName] = new List<float>();
        }
        if (timeIdx >= 0)
        {
            activityList[fishName][timeIdx] = value;
        }
        else
        {
            activityList[fishName].Add(value);
            timeIdx = activityList[fishName].Count - 1;
        }

        region.AddActivity(fishName, timeIdx, value);
        brain.AddActivity(fishName, timeIdx, value);
    }

    public void SetActiveState(string fishName, int timeIdx)
    {
        if (activityList.ContainsKey(fishName) && timeIdx < activityList[fishName].Count)
        {
            float activityValue = activityList[fishName][timeIdx];
            if (activityValue > 0)
            {
                // Active state
                renderer.material.SetColor("_EmissionColor", color * 2.0f); // Bright emission
                renderer.material.SetColor("_BaseColor", color);
                transform.localScale = new Vector3(activeNeuronSize, activeNeuronSize, activeNeuronSize);
            }
            else
            {
                // Inactive state
                renderer.material.SetColor("_EmissionColor", color * 0.1f); // Dim emission
                renderer.material.SetColor("_BaseColor", color * 0.5f);
                transform.localScale = new Vector3(inactiveNeuronSize, inactiveNeuronSize, inactiveNeuronSize);
            }
        }
        else
        {
            // No data for this fish or time index, set to inactive
            renderer.material.SetColor("_EmissionColor", color * 0.1f); // Dim emission
            renderer.material.SetColor("_BaseColor", color * 0.5f);
            transform.localScale = new Vector3(inactiveNeuronSize, inactiveNeuronSize, inactiveNeuronSize);
        }
    }

    public void InitNeuron(Mesh sphereMesh, Material glowMaterial)
    {
        string neuronName = $"Neuron_{neuronIdx}";
        meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        renderer = gameObject.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();

        collider = gameObject.GetComponent<SphereCollider>();
        if (collider == null) collider = gameObject.AddComponent<SphereCollider>();

        meshFilter.mesh = sphereMesh;
        renderer.material = glowMaterial;
        transform.position = originalPosition;
        
        // Ensure featureData is initialized
            featureData = gameObject.GetComponent<FeatureData>();
        if (featureData == null) featureData = gameObject.AddComponent<FeatureData>();

        // start with them all full size (activeNeruonSize)
        transform.localScale = new Vector3(activeNeuronSize, activeNeuronSize, activeNeuronSize);
        renderer.material.SetColor("_EmissionColor", color);
        renderer.material.SetColor("_BaseColor", color);
    }
}
}