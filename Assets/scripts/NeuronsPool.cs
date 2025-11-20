using UnityEngine;
using System.Collections.Generic;
using BrainComponents;

public class NeuronObjectPool : MonoBehaviour
{
    private static NeuronObjectPool instance;
    public static NeuronObjectPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject poolObj = new GameObject("NeuronObjectPool");
                instance = poolObj.AddComponent<NeuronObjectPool>();
                DontDestroyOnLoad(poolObj);
            }
            return instance;
        }
    }

    private Queue<GameObject> pooledNeurons = new Queue<GameObject>();
    private int totalCreated = 0;

    public void PrewarmPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject neuron = CreateNewNeuronObject();
            neuron.SetActive(false);
            pooledNeurons.Enqueue(neuron);
        }
    }

    private GameObject CreateNewNeuronObject()
    {
        GameObject neuronObj = new GameObject($"PooledNeuron_{totalCreated++}");
        // Just add the component, don't initialize yet
        neuronObj.AddComponent<NeuronData>();
        neuronObj.transform.SetParent(transform);
        return neuronObj;
    }

    public GameObject GetNeuron()
    {
        GameObject neuron;

        if (pooledNeurons.Count > 0)
        {
            neuron = pooledNeurons.Dequeue();
            neuron.SetActive(true);
        }
        else
        {
            neuron = CreateNewNeuronObject();
        }

        return neuron;
    }

    public void ReturnNeuron(GameObject neuron)
    {
        if (neuron == null) return;

        neuron.SetActive(false);
        neuron.transform.SetParent(transform);
        neuron.transform.localPosition = Vector3.zero;

        // Reset neuron state
        NeuronData data = neuron.GetComponent<NeuronData>();
        if (data != null)
        {
            // data.ClearActivityData();
        }

        pooledNeurons.Enqueue(neuron);
    }



    public void ReturnAllNeurons()
    {
        // Find all active neurons and return them to pool
        NeuronData[] activeNeurons = FindObjectsByType<NeuronData>(FindObjectsSortMode.None);
        foreach (var neuron in activeNeurons)
        {
            ReturnNeuron(neuron.gameObject);
        }
    }
}