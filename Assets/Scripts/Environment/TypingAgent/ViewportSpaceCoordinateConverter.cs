using UnityEngine;

public class ViewportSpaceCoordinateConverter : ICoordinateConverter
{
    public float CalculateDistanceCM(Vector2 fingerVelocity, DisplayConfiguration DisplayConfiguration, VisualStateSpace visualStateSpace, int numberSplits = 1)
    {
        return CRUtil.ViewPortToCM(fingerVelocity, DisplayConfiguration, numberSplits);
    }

    public Vector2 CalculateMaxDistanceBetweenButtons(VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetMaxScreenDistanceBetweenVisualElementsViewport();
    }

    public Vector2 GetCoordinatesForGameObjectIndex(int index, VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetViewportCoordinatesForGameObjectIndex(index);
    }

    //works
    public Vector2 GetCoordinatesForGameObject(GameObject gameObject, VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetViewportCoordinatesForGameObject(gameObject);
    }

    public  GameObject GetGameObjectForCoordinates(Vector2 coordinates, VisualStateSpace visualStateSpace)
    {
        return visualStateSpace.GetGameObjectForViewportCoordinates(coordinates);
    }

    public Vector2 ImageToKeyboardCanvasSpace(Vector3 viewportPosition, RectTransform rectTransform, VisualStateSpace visualStateSpace)
    {
        float z = Mathf.Abs(rectTransform.position.z - visualStateSpace.Camera.transform.position.z);
        Vector3 worldPosition = visualStateSpace.Camera.ViewportToWorldPoint(new Vector3(viewportPosition.x, viewportPosition.y, z));

        Vector3 localPosition = rectTransform.InverseTransformPoint(worldPosition);

        return new Vector2(localPosition.x, localPosition.y);
    }

    //works
    public Vector2 KeyboardCanvasToImageSpace(Vector3 keyboardCanvasPosition, RectTransform rectTransform, VisualStateSpace visualStateSpace)
    {
        Vector3 worldPosition = rectTransform.TransformPoint(keyboardCanvasPosition);

        Vector3 viewportPosition = visualStateSpace.Camera.WorldToViewportPoint(worldPosition);

        return new Vector2(viewportPosition.x, viewportPosition.y);
    }

    public bool IsActiveElementAtPosition(Vector2 position, VisualStateSpace visualStateSpace, float margin = 0)
    {
        return visualStateSpace.IsActiveElementAtViewportPosition(position, 0);
    }

    public Vector2 GetGameObjectSize(GameObject gameObject, VisualStateSpace visualStateSpace)
    {
        RectTransform rt = gameObject.GetComponent<RectTransform>();

        Vector3[] worldCorners = new Vector3[4];
        rt.GetWorldCorners(worldCorners);

        Vector3 min = worldCorners[0]; // Bottom-left
        Vector3 max = worldCorners[2]; // Top-right

        Vector3 minVP = visualStateSpace.Camera.WorldToViewportPoint(min);
        Vector3 maxVP = visualStateSpace.Camera.WorldToViewportPoint(max);

        float widthViewport = Mathf.Abs(maxVP.x - minVP.x);
        float heightViewport = Mathf.Abs(maxVP.y - minVP.y);

        return new Vector2(widthViewport, heightViewport);
    }
}
