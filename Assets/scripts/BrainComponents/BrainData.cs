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
        public Dictionary<string, List<float>> totalActivityList = new Dictionary<string, List<float>>();

        public void AddActivity(string fishName, int timeIdx, float value)
        {
            if (!totalActivityList.ContainsKey(fishName))
            {
                totalActivityList[fishName] = new List<float>();
            }
            var activityList = totalActivityList[fishName];
            while (activityList.Count <= timeIdx)
            {
                activityList.Add(0f);
            }
            activityList[timeIdx] += value;
        }

        public void AddNeuron(NeuronData neuron)
        {
            neurons.Add(neuron);
            bounds.Encapsulate(neuron.originalPosition);
        }
    }
}