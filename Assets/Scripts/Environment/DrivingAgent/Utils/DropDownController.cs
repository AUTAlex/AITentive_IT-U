using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class DropDownController : MonoBehaviour {
    public TextMeshProUGUI buttonText;
    public GameObject itemPrefab;
    public GameObject dropDownField;

    public GameManager GameManager;

    private void Start() {
        if (GameManager.ActiveScenarioSettings) {
            buttonText.text = GameManager.ActiveScenarioSettings.name;
        } else {
            GetComponent<Button>().interactable = false;
        }
    }
    
    public void CreateItems() {
        if (dropDownField.activeSelf) {
            dropDownField.SetActive(false);
            return;
        }

        foreach (Transform child in dropDownField.transform) {
            Destroy(child.gameObject);
        }

        foreach (ScenarioSettings currentSettings in GameManager.Scenarios) {
            GameObject newObject = Instantiate(itemPrefab, Vector3.zero, Quaternion.identity, dropDownField.transform);
            DropDownItem item = newObject.GetComponent<DropDownItem>();
            item.textMeshProText.text = currentSettings.name;
            item.Init(this, currentSettings);

            if (currentSettings == GameManager.ActiveScenarioSettings) {
                item.textMeshProText.color = Color.green;
                item.GetComponent<Toggle>().interactable = false;
            }
        }

        dropDownField.SetActive(true);
    }


    public void CloseDropDown(ScenarioSettings settings) {
        dropDownField.SetActive(false);
        GameManager.ActiveScenarioSettings = settings;
        buttonText.text = GameManager.ActiveScenarioSettings.name;
        GameManager.LoadScenarioSettings();
        GameManager.RestartSimulation();
    }
}