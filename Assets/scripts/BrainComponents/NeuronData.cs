using UnityEngine;
using System.Collections.Generic;
//using UnityEditor;
using Unity.VisualScripting;

namespace BrainComponents
{
    public class FeatureData : MonoBehaviour
    {
        //private Dictionary<string, int> featureDict = new Dictionary<string, int>();
        public HashSet<string> activeFeatures = new HashSet<string>();

        public bool IsFeatureActive(string featureName)
        {
            return activeFeatures.Contains(featureName);
        }

        public void AddFeature(string featureName)
        {
            activeFeatures.Add(featureName);
        }
    }

    public class NeuronData : MonoBehaviour
    {
        public int neuronIdx = -1;
        public Vector3 originalPosition;
        public Color color;
        public string subregion;
        public string label;
        public HighlightSphere highlightSphere;
        public MeshFilter meshFilter;
        public Renderer renderer;
        public SphereCollider collider;
        public BrainData brain;
        public RegionData region;
        public FeatureData featureData;
        public Dictionary<string, Dictionary<int, float>> activityList = new Dictionary<string, Dictionary<int, float>>();
        public bool isActive = false;
        private bool wasActive = false;
        public float inactiveNeuronSize; // Set from LoadFishData
        public float activeNeuronSize; // Set from LoadFishData


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

        public void InitNeuron(Mesh sphereMesh, Material glowMaterial, float activeNeuronSize)
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

            highlightSphere = GetComponent<HighlightSphere>();
            if (highlightSphere == null)
                    {
                        highlightSphere = gameObject.AddComponent<HighlightSphere>();
                    }
            // Pass sizes to HighlightSphere
            highlightSphere.SetSizes(inactiveNeuronSize, activeNeuronSize);
        }



        public NeuronData CopyNeuron(BrainData newBrain, Vector3 newPosition, Color? newColor = null)
        {
            // Clone the GameObject
            NeuronData clonedNeuron = Instantiate(this);
            
            // Copy basic properties
            clonedNeuron.neuronIdx = this.neuronIdx;
            clonedNeuron.originalPosition = newPosition;
            clonedNeuron.color = newColor ?? this.color;
       
            clonedNeuron.subregion = this.subregion;
            clonedNeuron.label = this.label;
            clonedNeuron.brain = newBrain;

            clonedNeuron.inactiveNeuronSize = this.inactiveNeuronSize;
            clonedNeuron.activeNeuronSize = this.activeNeuronSize;
            clonedNeuron.highlightSphere = this.highlightSphere;

            // Update position
            clonedNeuron.transform.position = newPosition;

            // Initialize empty activity list and populate afterwards
            clonedNeuron.activityList = new Dictionary<string, Dictionary<int, float>>();

            // Update material colors
            if (clonedNeuron.renderer != null)
            {
                clonedNeuron.renderer.material.SetColor("_EmissionColor", newColor ?? this.color);
                clonedNeuron.renderer.material.SetColor("_BaseColor", newColor ?? this.color);
                clonedNeuron.renderer.material.EnableKeyword("_EMISSION");
            }
            else {
                Debug.LogWarning("Cloned neuron has no renderer component.");
            }

            return clonedNeuron;
        }

        public void CopyActivityData(NeuronData originalNeuron)
        {
            this.activityList = new Dictionary<string, Dictionary<int, float>>();
            // this.activityList = originalNeuron.activityList;
            
            foreach (KeyValuePair<string, Dictionary<int, float>> fishEntry in originalNeuron.activityList)
            {
                string fishName = fishEntry.Key;
                Dictionary<int, float> timestampData = fishEntry.Value;

                foreach (KeyValuePair<int, float> timestampEntry in timestampData)
                {
                    int timestamp = timestampEntry.Key;
                    float activityValue = timestampEntry.Value;
                    this.AddActivity(fishName, activityValue, timestamp);
                }
            }
        }

    }
}