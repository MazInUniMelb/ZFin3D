using UnityEngine;
using System.Linq;
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

        public Dictionary<string, float> numActivities = new Dictionary<string, float>();
        public Dictionary<string, float> minActivities = new Dictionary<string, float>();
        public Dictionary<string, float> maxActivities = new Dictionary<string, float>();

        public void AddActivity(string fishName, int timeIdx, float value)
        {
            if (!totalActivityList.ContainsKey(fishName))
            {
                totalActivityList[fishName] = new Dictionary<int, float>();
            }
            totalActivityList[fishName][timeIdx] = value;
            // Debug.Log($"Adding Brain {this.name} activity for fish {fishName} at timeIdx {timeIdx}. Total so far: {totalActivityList[fishName].Count}");
        }

        public void UpdateMinMax()
        {
            foreach (var fishName in totalActivityList.Keys)
            {
                minActivities[fishName] = totalActivityList[fishName].Values.Min();
                maxActivities[fishName] = totalActivityList[fishName].Values.Max();
            }
        }

        public void AddNeuron(NeuronData neuron)
        {
            neurons.Add(neuron);
            bounds.Encapsulate(neuron.originalPosition);
        }


        public List<NeuronData> GetActiveNeurons(string fishName, int timeIdx, float threshold = 0.5f)
        {
            List<NeuronData> activeNeurons = new List<NeuronData>();
            foreach (var neuron in neurons)
            {
                if (neuron.activityList.ContainsKey(fishName) &&
                    neuron.activityList[fishName].ContainsKey(timeIdx) &&
                    neuron.activityList[fishName][timeIdx] >= threshold)
                {
                    activeNeurons.Add(neuron);
                }
            }
            return activeNeurons;
        }
    }
}