using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;

public class UIHandler : MonoBehaviour
{
    [Header("References")]
    public LoadFishData loadFishData;

    [Tooltip("Dropdown for selecting fish")]
    public TMP_Dropdown fishDropdown;

    [Tooltip("Dropdown for selecting region")]
    public TMP_Dropdown regionDropdown;

    [Tooltip("Button to show seizure data")]
    public Button showSeizureButton;
    [Tooltip("Button to make seizure frames")]
    public Button makeFramesButton;

    [Tooltip("Button to load all fish seizure files")]
    public Button bulkLoadButton;

    [Tooltip("Button to export all fish seizure frames")]
    public Button bulkExportButton;

    [Tooltip("Show the start time for seizure animation")]
    public TMPro.TextMeshProUGUI startTimeText;

    [Tooltip("Show the end time for seizure animation")]
    public TMPro.TextMeshProUGUI endTimeText;

    [Tooltip("Menu canvas to select fish and region")]
    public GameObject menuParentObject;

    [Tooltip("Show fish name in the scene")]
    public TMPro.TextMeshProUGUI fishNameText;

    void Start()
    {

        showSeizureButton.onClick.AddListener(OnShowSeizureButtonClicked);
        makeFramesButton.onClick.AddListener(OnMakeFramesButtonClicked);
        bulkExportButton.onClick.AddListener(OnBulkExportClicked);
        regionDropdown.onValueChanged.AddListener(OnRegionDropdownChanged);
        fishDropdown.onValueChanged.AddListener(OnFishDropdownChanged);

        DisableActionButtons();

        fishDropdown.interactable = true;

        ShowMenuPanel(); // show menu to select fish  
    }

    // Populate fish dropdown
    public void PopulateFishDropdown(List<string> fishNames)
    {
        Debug.Log("Populating fish dropdown with: " + string.Join(", ", fishNames));
        fishDropdown.ClearOptions();
        fishDropdown.AddOptions(fishNames);
  
        fishDropdown.interactable = true;

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
            ShowFishName(fishName);
            fishDropdown.gameObject.SetActive(false);
            loadFishData.SetSelectedFish(fishName);
        }
        else
        {
            Debug.Log("No fish selected");
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

        public void ShowFishName(string fishName)
        {
            fishNameText.text = fishName;
            fishNameText.gameObject.SetActive(true);
        }

    public void ShowMenuPanel()
    {
        menuParentObject.SetActive(true);
        Debug.Log("Returned to choose fish panel");
        // if fish and region are selected enable actionbutton by retriggerin fish selection
        //OnFishDropdownChanged(fishDropdown.value);
    }

    public void HideMenuPanel()
    {
        menuParentObject.SetActive(false);
        Debug.Log("Hid choose fish panel");
    }

    public void EnableActionButtons()
    {
        showSeizureButton.gameObject.SetActive(true);
        makeFramesButton.gameObject.SetActive(true);
        bulkExportButton.gameObject.SetActive(true);
        regionDropdown.gameObject.SetActive(true);
    }


    public void DisableActionButtons()
    {
        showSeizureButton.gameObject.SetActive(false);
        makeFramesButton.gameObject.SetActive(false);
        bulkExportButton.gameObject.SetActive(false);
        regionDropdown.gameObject.SetActive(false);
    }

    // on button click call loadfishdata function ShowSeizureData
    public void OnShowSeizureButtonClicked()
    {
        Debug.Log("Start showing seizure data");
        DisableActionButtons();
        loadFishData.ShowSeizureData();
    }

    public void OnMakeFramesButtonClicked()
    {
        Debug.Log("Start making frames");
        loadFishData.MakeFrames();
    }

    public void OnBulkExportClicked()
    {
        Debug.Log("Loading all fish files, this will take some time");
        loadFishData.BulkExportAllFrames();
    }
}