using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class VKeyCallback : MonoBehaviour
{
    private InputAction vKeyAction;
    private RLDrivingExerciseCR _rlDrivingExerciseCR;

    void Awake()
    {
        // Create the action in code
        vKeyAction = new InputAction(
            name: "VKey",
            type: InputActionType.Button,
            binding: "<Keyboard>/v"
        );

        // Register callback
        vKeyAction.performed += OnVKeyPressed;
        _rlDrivingExerciseCR = GetComponent<RLDrivingExerciseCR>();
    }

    void OnEnable()
    {
        vKeyAction.Enable();
    }

    void OnDisable()
    {
        vKeyAction.Disable();
    }

    private void OnVKeyPressed(InputAction.CallbackContext ctx)
    {
        _rlDrivingExerciseCR.IsVisible = !_rlDrivingExerciseCR.IsVisible;
    }
}
