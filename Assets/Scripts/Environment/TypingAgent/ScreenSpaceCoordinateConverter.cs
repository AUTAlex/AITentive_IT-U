using UnityEngine;

public class ScreenSpaceCoordinateConverter : ICoordinateConverter
{
    public float CalculateDistanceCM(Vector2 fingerVelocity, DisplayConfiguration DisplayConfiguration, VisualStateSpace visualStateSpace, int numberSplits = 1)
    {
        return CRUtil.PixelToCM(fingerVelocity, DisplayConfiguration).magnitude * visualStateSpace.Canvas.scaleFactor;
    }

    public Vector2 CalculateMaxDistanceBetweenButtons(VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetMaxScreenDistanceBetweenVisualElementsScreenCoordinates() * 1.2f;
    }

    public Vector2 GetCoordinatesForGameObjectIndex(int index, VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetScreenCoordinatesForGameObjectIndex(index);
    }

    public Vector2 GetCoordinatesForGameObject(GameObject gameObject, VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetScreenCoordinatesForGameObject(gameObject);
    }

    public GameObject GetGameObjectForCoordinates(Vector2 coordinates, VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetGameObjectForScreenCoordinates(coordinates);
    }

    public Vector2 ImageToKeyboardCanvasSpace(Vector3 screenPosition, RectTransform rectTransform, VisualStateSpace visualStateSpace)
    {
        Vector2 position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPosition,
            visualStateSpace.Camera,
            out position
        );

        return position;
    }

    public Vector2 KeyboardCanvasToImageSpace(Vector3 keyboardCanvasPosition, RectTransform rectTransform, VisualStateSpace visualStateSpace)
    {
        Vector3 worldPosition = rectTransform.TransformPoint(keyboardCanvasPosition);

        Vector2 position = RectTransformUtility.WorldToScreenPoint(
            visualStateSpace.Camera,
            worldPosition
        );

        return position;
    }

    public bool IsActiveElementAtPosition(Vector2 position, VisualStateSpace visualStateSpace, float margin = 0)
    {
        return visualStateSpace.IsActiveElementAtScreenPosition(position, 10);
    }

    public Vector2 GetGameObjectSize(GameObject gameObject, VisualStateSpace visualStateSpace)
    {
        RectTransform rt = gameObject.GetComponent<RectTransform>();

        Vector3[] worldCorners = new Vector3[4];
        rt.GetWorldCorners(worldCorners);

        Vector3 min = worldCorners[0]; // Bottom-left
        Vector3 max = worldCorners[2]; // Top-right

        Vector3 minScreen = visualStateSpace.Camera.WorldToScreenPoint(min);
        Vector3 maxScreen = visualStateSpace.Camera.WorldToScreenPoint(max);

        float widthScreen = Mathf.Abs(maxScreen.x - minScreen.x);
        float heightScreen = Mathf.Abs(maxScreen.y - minScreen.y);

        return new Vector2(widthScreen, heightScreen);
    }
}
