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
        public Dictionary<string, Dictionary<int, float>> sumActivities = new Dictionary<string, Dictionary<int, float>>();
        public Dictionary<string, float> numActivities = new Dictionary<string, float>();
        public Dictionary<string, float> minActivities = new Dictionary<string, float>();
        public Dictionary<string, float> maxActivities = new Dictionary<string, float>();

        public void UpdateMinMax()
        {
            foreach (var fishName in sumActivities.Keys)
            {
                minActivities[fishName] = sumActivities[fishName].Values.Min();
                maxActivities[fishName] = sumActivities[fishName].Values.Max();
            }
        }
        public void AddActivity(string fishName, int timeIdx, float value)
        {
            if (!sumActivities.ContainsKey(fishName))
            {
                sumActivities[fishName] = new Dictionary<int, float>();
                numActivities[fishName] = 0;
            }
            sumActivities[fishName][timeIdx] = sumActivities[fishName].GetValueOrDefault(timeIdx, 0f) + value;
            numActivities[fishName] += 1;
            // Debug.Log($"Adding {this.name} activity for fish {fishName} at timeIdx {timeIdx}. Total so far: {numActivities[fishName]}/{sumActivities[fishName].Count}");
        }
        public void AddNeuron(NeuronData neuron)
        {
            neurons.Add(neuron);
            if (neurons.Count == 1)
            {
                bounds = new Bounds(neuron.originalPosition, Vector3.zero);
            }
            bounds.Encapsulate(neuron.renderer.bounds);
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
