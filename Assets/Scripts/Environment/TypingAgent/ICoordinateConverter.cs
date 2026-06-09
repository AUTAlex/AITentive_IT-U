using UnityEngine;

public interface ICoordinateConverter
{
    float CalculateDistanceCM(Vector2 fingerVelocity, DisplayConfiguration DisplayConfiguration, VisualStateSpace visualStateSpace, int numberSplits = 1);

    Vector2 CalculateMaxDistanceBetweenButtons(VisualStateSpace visualStateSpace);

    Vector2 GetCoordinatesForGameObjectIndex(int index, VisualStateSpace visualStateSpace);

    Vector2 GetCoordinatesForGameObject(GameObject gameObject, VisualStateSpace visualStateSpace);

    GameObject GetGameObjectForCoordinates(Vector2 coordinates, VisualStateSpace visualStateSpace);

    Vector2 GetGameObjectSize(GameObject gameObject, VisualStateSpace visualStateSpace);

    Vector2 ImageToKeyboardCanvasSpace(Vector3 screenPosition, RectTransform rectTransform, VisualStateSpace visualStateSpace);

    Vector2 KeyboardCanvasToImageSpace(Vector3 keyboardCanvasPosition, RectTransform rectTransform, VisualStateSpace visualStateSpace);

    bool IsActiveElementAtPosition(Vector2 position, VisualStateSpace visualStateSpace,  float margin = 0);
}