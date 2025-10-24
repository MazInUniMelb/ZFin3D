using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIHandler : MonoBehaviour
{
    [Header("References")]
    public LoadFishData loadFishData;


    [Tooltip("Dropdown for selecting region")]
    public TMP_Dropdown regionDropdown;

    [Tooltip("Dropdown for selecting fish")]
    public TMP_Dropdown fishDropdown;

    [Tooltip("Button to show seizure data")]
    public Button showSeizureButton;
    [Tooltip("Button to make seizure frames")]
    public Button makeFramesButton;

    [Tooltip("Button to load all fish seizure files")]
    public Button bulkLoadButton;

    [Tooltip("Button to export all fish seizure frames")]
    public Button bulkExportButton;

    [Tooltip("Input field for start time")]
    public TMP_InputField startTimeInput;

    [Tooltip("Input field for duration")]
    public TMP_InputField durationInput;

    [Tooltip("Menu canvas to select fish and region")]
    public GameObject menuParentObject;

    void Start()
    {

        showSeizureButton.onClick.AddListener(OnShowSeizureButtonClicked);
        makeFramesButton.onClick.AddListener(OnMakeFramesButtonClicked);
        bulkLoadButton.onClick.AddListener(OnBulkLoadClicked);
        bulkExportButton.onClick.AddListener(OnBulkExportClicked);

        DisableActionButtons();

        regionDropdown.onValueChanged.AddListener(OnRegionDropdownChanged);
        fishDropdown.onValueChanged.AddListener(OnFishDropdownChanged);
    }

    // Populate fish dropdown
    public void PopulateFishDropdown(List<string> fishNames)
    {
        Debug.Log("Populating fish dropdown with: " + string.Join(", ", fishNames));
        fishDropdown.ClearOptions();
        fishDropdown.AddOptions(fishNames);
    }

    // Populate region dropdown
    public void PopulateRegionDropdown(List<string> regionNames)
    {
        Debug.Log("Populating region dropdown with: " + string.Join(", ", regionNames));
        regionDropdown.ClearOptions();
        regionDropdown.AddOptions(regionNames);
    }

    // Fish selection changed
    public void OnFishDropdownChanged(int index)
    {
        if (index > 0)
        {
            string fishName = fishDropdown.options[index].text;
            loadFishData.selectedFish = fishName;
            loadFishData.statusMessage.text = "Selected Fish: " + fishName;
            loadFishData.SetSelectedFish(fishName);
        }
    }
    // Region selection changed
    public void OnRegionDropdownChanged(int index)
    {
        string regionName = regionDropdown.options[index].text;
        loadFishData.selectedRegion = regionName;
        loadFishData.statusMessage.text = "Selected Region: " + regionName;
        loadFishData.SetSelectedRegion(regionName);
        Debug.Log($"Selected region set to: {regionName}");
    }

    public void ShowMenuPanel()
    {
        menuParentObject.SetActive(true);
        Debug.Log("Returned to choose fish panel");
        // if fish and region are selected enable actionbutton by retriggerin fish selection
        OnFishDropdownChanged(fishDropdown.value);
    }

    public void HideMenuPanel()
    {
        menuParentObject.SetActive(false);
        Debug.Log("Hid choose fish panel");
    }

    public void EnableActionButtons()
    {
        showSeizureButton.interactable = true;
        makeFramesButton.interactable = true;
        bulkLoadButton.interactable = true;
        bulkExportButton.interactable = true;
    }


    public void DisableActionButtons()
    {
        showSeizureButton.interactable = false;
        makeFramesButton.interactable = false;
        bulkLoadButton.interactable = true;
        bulkExportButton.interactable = false;
    }

    // on button click call loadfishdata function ShowSeizureData
    public void OnShowSeizureButtonClicked()
    {
        Debug.Log("Start showing seizure data");
        loadFishData.ShowSeizureData();
    }

    public void OnMakeFramesButtonClicked()
    {
        Debug.Log("Start making frames");
        loadFishData.MakeFrames();
    }

    public void OnBulkLoadClicked()
    {
        Debug.Log("Loading all fish files, this will take some time");
        loadFishData.BulkLoadAllFish();
    }
    public void OnBulkExportClicked()
    {
        Debug.Log("Loading all fish files, this will take some time");
        loadFishData.BulkExportAllFrames();
    }
}