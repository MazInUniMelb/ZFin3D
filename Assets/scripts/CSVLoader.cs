using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;

namespace BrainComponents
{
    public static class CSVLoader
    {
        // Data structures for returning results
        public class PositionDataResult
        {
            public List<NeuronPositionData> neurons = new List<NeuronPositionData>();
            public HashSet<string> featureSets = new HashSet<string>();
            public Dictionary<string, List<NeuronPositionData>> neuronsByRegion = new Dictionary<string, List<NeuronPositionData>>();
        }

        public class NeuronPositionData
        {
            public int neuronIdx;
            public Vector3 position;
            public string region;
            public string subregion;
            public string label;
            public HashSet<string> features = new HashSet<string>();
        }

        public class SeizureDataResult
        {
            public int numRows;
            public int numCols;
            public Dictionary<int, Dictionary<string, List<int>>> neuronActivityData = new Dictionary<int, Dictionary<string, List<int>>>();
        }

        // Delegates for callbacks
        public delegate void OnProgressUpdate(float progress, string message);
        public delegate void OnPositionDataLoaded(PositionDataResult result);
        public delegate void OnSeizureDataLoaded(SeizureDataResult result);
        public delegate void OnError(string error);

        // Static coroutine runner for non-MonoBehaviour access
        private static CSVLoaderRunner runner;
        private static CSVLoaderRunner GetRunner()
        {
            if (runner == null)
            {
                GameObject runnerObj = new GameObject("CSVLoaderRunner");
                runner = runnerObj.AddComponent<CSVLoaderRunner>();
                UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            }
            return runner;
        }

        // Main loading methods
        public static void LoadPositionData(string dataFolder, string filename,
            OnPositionDataLoaded onComplete, OnError onError = null, OnProgressUpdate onProgress = null)
        {
            GetRunner().StartCoroutine(LoadPositionDataCoroutine(dataFolder, filename, onComplete, onError, onProgress));
        }

        public static void LoadSeizureData(string dataFolder, string filename, string fishName,
            OnSeizureDataLoaded onComplete, OnError onError = null, OnProgressUpdate onProgress = null)
        {
            GetRunner().StartCoroutine(LoadSeizureDataCoroutine(dataFolder, filename, fishName, onComplete, onError, onProgress));
        }

        // Position data loading coroutine
        private static IEnumerator LoadPositionDataCoroutine(string dataFolder, string filename,
            OnPositionDataLoaded onComplete, OnError onError, OnProgressUpdate onProgress)
        {
            string fullPath = Path.Combine(dataFolder, filename);
            if (!File.Exists(fullPath))
            {
                onError?.Invoke($"File not found: {fullPath}");
                yield break;
            }

            PositionDataResult result = new PositionDataResult();
            int lineCount = 0;
            int totalLines = File.ReadLines(fullPath).Count() - 1; // Exclude header

            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072))
            using (var reader = new StreamReader(fileStream))
            {
                // Read header
                string headerLine = reader.ReadLine();
                string[] headers = headerLine.Split(',');

                // Validate headers
                if (!ValidatePositionHeaders(headers))
                {
                    onError?.Invoke("Unexpected CSV format in position file");
                    yield break;
                }

                // Find feature set columns
                int featureSetStartIndex = 7;
                for (int i = featureSetStartIndex; i < headers.Length; i++)
                {
                    result.featureSets.Add(headers[i].Trim());
                }

                // Process data in chunks
                int chunkSize = 500;
                int processedInChunk = 0;
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    string[] values = line.Split(',');
                    if (values.Length < 6) continue;

                    if (TryParseNeuronPosition(values, headers, out NeuronPositionData neuronData))
                    {
                        result.neurons.Add(neuronData);

                        // Group by region
                        if (!result.neuronsByRegion.ContainsKey(neuronData.region))
                            result.neuronsByRegion[neuronData.region] = new List<NeuronPositionData>();
                        result.neuronsByRegion[neuronData.region].Add(neuronData);
                    }

                    lineCount++;
                    processedInChunk++;

                    if (processedInChunk >= chunkSize)
                    {
                        float progress = (float)lineCount / totalLines;
                        onProgress?.Invoke(progress, $"Loading positions: {lineCount}/{totalLines}");
                        processedInChunk = 0;
                        yield return null; // Yield control back to Unity
                    }
                }
            }

            onProgress?.Invoke(1f, "Position data loaded");
            onComplete?.Invoke(result);
        }

        // Seizure data loading coroutine
        private static IEnumerator LoadSeizureDataCoroutine(string dataFolder, string filename, string fishName,
            OnSeizureDataLoaded onComplete, OnError onError, OnProgressUpdate onProgress)
        {
            string fullPath = Path.Combine(dataFolder, filename);
            if (!File.Exists(fullPath))
            {
                onError?.Invoke($"File not found: {fullPath}");
                yield break;
            }

            SeizureDataResult result = new SeizureDataResult();

            // Fast file analysis first
            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072))
            using (var reader = new StreamReader(fileStream))
            {
                reader.ReadLine(); // Skip header
                string firstDataLine = reader.ReadLine();
                if (firstDataLine == null)
                {
                    onError?.Invoke($"Empty file: {fullPath}");
                    yield break;
                }

                string[] firstData = firstDataLine.Split(',');
                result.numCols = firstData.Length - 10;
                result.numRows = 1;

                while (reader.ReadLine() != null)
                    result.numRows++;
            }

            onProgress?.Invoke(0.1f, $"Found {result.numCols} timestamps for {result.numRows} neurons");

            // Process data in chunks
            yield return ProcessSeizureDataChunked(fullPath, fishName, result, onProgress);

            onComplete?.Invoke(result);
        }

        private static IEnumerator ProcessSeizureDataChunked(string fullPath, string fishName,
            SeizureDataResult result, OnProgressUpdate onProgress)
        {
            // Pre-allocate arrays for performance
            char[] separators = { ',' };
            string[] reusableStringArray = new string[result.numCols + 20];
            float[] reusableFloatArray = new float[result.numCols];
            int[] reusableBinaryArray = new int[result.numCols];

            var culture = CultureInfo.InvariantCulture;
            var numberStyles = NumberStyles.Float;

            int firstActivityColIdx = 10;
            int rowIdx = 0;
            int chunkSize = 100;
            int processedInChunk = 0;

            using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 262144))
            using (var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true, 262144))
            {
                reader.ReadLine(); // Skip header
                string line;

                while ((line = reader.ReadLine()) != null && rowIdx < result.numRows)
                {
                    int splitCount = SplitStringIntoArray(line, separators[0], reusableStringArray);

                    if (splitCount >= firstActivityColIdx)
                    {
                        int validCount = BatchParseFloats(reusableStringArray, firstActivityColIdx,
                                                        splitCount, reusableFloatArray, reusableBinaryArray,
                                                        culture, numberStyles);

                        if (validCount > 0)
                        {
                            // Store activity data
                            if (!result.neuronActivityData.ContainsKey(rowIdx))
                                result.neuronActivityData[rowIdx] = new Dictionary<string, List<int>>();

                            result.neuronActivityData[rowIdx][fishName] = reusableBinaryArray.Take(validCount).ToList();
                        }
                    }

                    rowIdx++;
                    processedInChunk++;

                    if (processedInChunk >= chunkSize)
                    {
                        float progress = 0.1f + (0.9f * rowIdx / result.numRows);
                        onProgress?.Invoke(progress, $"Processing seizure data: {rowIdx}/{result.numRows}");
                        processedInChunk = 0;
                        yield return null;
                    }
                }
            }

            onProgress?.Invoke(1f, "Seizure data processed");
        }

        // Helper methods
        private static bool ValidatePositionHeaders(string[] headers)
        {
            return headers.Length >= 7 &&
                   headers[0].Trim() == "x_SWCIndex" &&
                   headers[1].Trim() == "xpos" &&
                   headers[2].Trim() == "ypos" &&
                   headers[3].Trim() == "zpos" &&
                   headers[4].Trim() == "Region" &&
                   headers[5].Trim() == "Subregion" &&
                   headers[6].Trim() == "Label";
        }

        private static bool TryParseNeuronPosition(string[] values, string[] headers, out NeuronPositionData data)
        {
            data = new NeuronPositionData();

            if (!int.TryParse(values[0], out data.neuronIdx))
                return false;

            if (!float.TryParse(values[1], out float x) ||
                !float.TryParse(values[2], out float y) ||
                !float.TryParse(values[3], out float z))
                return false;

            data.position = new Vector3(x, y, z * 3f); // Scale z

            string regionList = values[4].Trim();
            data.region = CleanList(regionList)
                .Split(new char[] { '+', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "None";

            data.subregion = values[5].Trim();
            data.label = $"Regions: {CleanList(regionList)}\nSubregions: {CleanList(values[5])}";

            // Parse features
            for (int i = 7; i < values.Length && i < headers.Length; i++)
            {
                if (int.TryParse(values[i], out int featureValue) && featureValue != 0)
                {
                    data.features.Add(headers[i]);
                }
            }

            return true;
        }

        private static string CleanList(string mystr)
        {
            return mystr.Trim('[', ']', '"', '\'');
        }

        private static int SplitStringIntoArray(string input, char separator, string[] outputArray)
        {
            int count = 0;
            int startIndex = 0;

            for (int i = 0; i <= input.Length; i++)
            {
                if (i == input.Length || input[i] == separator)
                {
                    if (count < outputArray.Length)
                    {
                        outputArray[count] = input.Substring(startIndex, i - startIndex);
                        count++;
                    }
                    startIndex = i + 1;
                }
            }
            return count;
        }

        private static int BatchParseFloats(string[] stringArray, int startIndex, int count,
                                   float[] floatArray, int[] binaryArray,
                                   CultureInfo culture, NumberStyles numberStyles)
        {
            int validCount = 0;
            int maxIndex = Mathf.Min(count, startIndex + floatArray.Length);

            for (int i = startIndex; i < maxIndex; i++)
            {
                if (float.TryParse(stringArray[i], numberStyles, culture, out float value))
                {
                    floatArray[validCount] = value;
                    binaryArray[validCount] = value > 0 ? 1 : 0;
                    validCount++;
                }
            }
            return validCount;
        }

        // MonoBehaviour helper for running coroutines
        private class CSVLoaderRunner : MonoBehaviour { }
    }
}