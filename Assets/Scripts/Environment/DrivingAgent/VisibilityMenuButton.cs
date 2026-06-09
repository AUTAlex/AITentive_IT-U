using TMPro;
using UnityEngine;

public class VisibilityMenuButton : MonoBehaviour
{
    [field: SerializeField, ProjectAssign]
    public bool UseVisibilityButton { get; set; } = false;

    [field: SerializeField]
    public DrivingAgentHumanCognitionBase<float> drivingAgentHumanCognition { get; set; }

    [field: SerializeField]
    public BeliefUpdater BeliefUpdater { get; set; }

    [field: SerializeField]
    public TMP_Text Text { get; set; }


    public void OnVisibilityButtonClicked()
    {
        ChangeVisibilityState();
    }


    private void OnEnable()
    {
        gameObject.SetActive(UseVisibilityButton);
        BeliefUpdater.ObjectAdded += UpdateVisibilityState;
    }

    private void OnDisable()
    {
        BeliefUpdater.ObjectAdded -= UpdateVisibilityState;
    }

    private void Start()
    {
        if (UseVisibilityButton)
        {
            ChangeVisibilityState(true);
        }
    }

    private void ChangeVisibilityState(bool? isVisible = null)
    {
        drivingAgentHumanCognition.IsVisible = isVisible ?? !drivingAgentHumanCognition.IsVisible;

        foreach (IBelievableObject believableObject in BeliefUpdater.BelievableObjects)
        {
            believableObject.IsVisible = drivingAgentHumanCognition.IsVisible;
        }

        Text.text = drivingAgentHumanCognition.IsVisible ? "Hide View" : "Provide View";
    }

    private void UpdateVisibilityState(IBelievableObject believableObject)
    {
        believableObject.IsVisible = drivingAgentHumanCognition.IsVisible;
    }
}
