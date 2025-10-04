using UnityEngine;
using System.Collections.Generic;
// using UnityEditor; // Removed to prevent runtime/build errors

namespace BrainComponents
{
    public class BrainData : MonoBehaviour
    {
        public HashSet<string> activeFeatureSets = new HashSet<string>();
        public Dictionary<string, RegionData> regions = new Dictionary<string, RegionData>();
        public List<NeuronData> neurons = new List<NeuronData>();
        public Bounds bounds = new Bounds();
        public Dictionary<string, Dictionary<int, float>> totalActivityList = new Dictionary<string, Dictionary<int, float>>();

        public void AddActivity(string fishName, int timeIdx, float value)
        {
            if (!totalActivityList.ContainsKey(fishName))
            {
                totalActivityList[fishName] = new Dictionary<int, float>();
            }
            totalActivityList[fishName][timeIdx] = value;
            // Debug.Log($"Adding Brain {this.name} activity for fish {fishName} at timeIdx {timeIdx}. Total so far: {totalActivityList[fishName].Count}");
        }

        public void AddNeuron(NeuronData neuron)
        {
            neurons.Add(neuron);
            bounds.Encapsulate(neuron.originalPosition);
        }
    }
}