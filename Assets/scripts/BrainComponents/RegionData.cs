using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace BrainComponents
{
    public class RegionData : MonoBehaviour
    {
        public Color color;
        public BrainData brain;
        public Dictionary<string, RegionData> subRegions = new Dictionary<string, RegionData>();
        public List<NeuronData> neurons = new List<NeuronData>();
        public Bounds bounds = new Bounds();
        public Dictionary<string, List<float>> sumActivities = new Dictionary<string, List<float>>();
        public Dictionary<string, float> numActivities = new Dictionary<string, float>();
        public Dictionary<string, float> minActivities = new Dictionary<string, float>();
        public Dictionary<string, float> maxActivities = new Dictionary<string, float>();

        public void UpdateMinMax()
        {
            foreach (var fishName in sumActivities.Keys)
            {
                var activities = sumActivities[fishName];
                minActivities[fishName] = activities.Min();
                maxActivities[fishName] = activities.Max();
            }

        }
        public void AddActivity(string fishName, int timeIdx, float value)
        {
            if (!sumActivities.ContainsKey(fishName))
            {
                sumActivities[fishName] = new List<float>();
                numActivities[fishName] = 0;
            }
            if (sumActivities[fishName].Count < timeIdx+1) {
                sumActivities[fishName].Add(0f); // Initial sum value
            }
            // Debug.Log($"Adding activity to region {this.name} for fish {fishName} at timeIdx {timeIdx}. Current timeSeries length: {sumActivities[fishName].Count}");
            sumActivities[fishName][timeIdx] += value;
            numActivities[fishName] += 1;
        }
        public void AddNeuron(NeuronData neuron)
        {
            neurons.Add(neuron);
            if(neurons.Count == 1)
            {
                bounds = new Bounds(neuron.originalPosition, Vector3.zero);
            }
            bounds.Encapsulate(neuron.renderer.bounds);
        }
    }
}
