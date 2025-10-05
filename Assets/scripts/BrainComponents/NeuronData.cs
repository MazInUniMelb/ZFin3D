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
            if (value > 0)
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
        private HighlightSphere highlightSphere;
        public MeshFilter meshFilter;
        public Renderer renderer;
        public SphereCollider collider;
        public BrainData brain;
        public RegionData region;
        public FeatureData featureData;
        public Dictionary<string, Dictionary<int, float>> activityList = new Dictionary<string, Dictionary<int, float>>();
        public bool isActive = false;
        private bool wasActive = false;
        public float inactiveNeuronSize = 2.0f; // Set from LoadFishData
        public float activeNeuronSize = 4.0f; // Set from LoadFishData


        public void Awake()
        {
            if (highlightSphere == null) highlightSphere = gameObject.GetOrAddComponent<HighlightSphere>();
        }
        public void ResetPosition()
        {
            this.transform.position = originalPosition;
        }

        private void OnValidate()
        {
            if (isActive != wasActive)
            {
                if (isActive) Activate();
                else Deactivate();
                wasActive = isActive;
            }
        }

        public void AddActivity(string fishName, float value, int timeIdx = -1)
        {
            // Debug.Log($"Adding neuron activity for fish {fishName} at timeIdx {timeIdx} with value {value}");
            if (!activityList.ContainsKey(fishName))
            {
                activityList[fishName] = new Dictionary<int, float>();
            }
            activityList[fishName][timeIdx] = value;
            region.AddActivity(fishName, timeIdx, value);
            brain.AddActivity(fishName, timeIdx, value);
        }

        // Active State
        public void Activate()
        {
            isActive = true;
            // renderer.material.SetColor("_EmissionColor", color * 2.0f); // Bright emission
            // renderer.material.SetColor("_BaseColor", color);
            highlightSphere.TurnOn();
        }

        // Inactive State
        public void Deactivate()
        {
            isActive = false;
            // renderer.material.SetColor("_EmissionColor", color * 0.1f); // Dim emission
            // renderer.material.SetColor("_BaseColor", color * 0.5f);
            highlightSphere.TurnOff();
        }

        public void SetActiveState(string fishName, int timeIdx)
        {
            if (activityList.ContainsKey(fishName) && timeIdx < activityList[fishName].Count)
            {
                float activityValue = activityList[fishName][timeIdx];
                if (activityValue > 0)
                {
                    Activate();
                    return;
                }
            }
            Deactivate();
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
            transform.localScale = new Vector3(inactiveNeuronSize, inactiveNeuronSize, inactiveNeuronSize);
            renderer.material.SetColor("_EmissionColor", color);
            renderer.material.SetColor("_BaseColor", color);
        }

    }
}