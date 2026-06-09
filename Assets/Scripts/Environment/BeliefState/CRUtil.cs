using Newtonsoft.Json;
using Supervisor;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static ObjectIn2DGridProbabilitiesUpdateJob;
using static UnityEngine.GraphicsBuffer;


[Serializable, JsonObject]
public class DisplayConfiguration
{
    public int WidthPixel;
    public int HeightPixel;
    public float DiagonalInch;
    public float DistanceToUserMeter;
    public int AspectWidth { 
        get 
        {
            return _aspectWidth == 0 ? WidthPixel / GCD(WidthPixel, HeightPixel) : _aspectWidth;
        }
        set
        {
            _aspectWidth = value;
        }
    }
    public int AspectHeight
    {
        get
        {
            return _aspectHeight == 0 ? HeightPixel / GCD(WidthPixel, HeightPixel) : _aspectHeight;
        }
        set
        {
            _aspectHeight = value;
        }
    }

    [field: SerializeField]
    private int _aspectWidth;

    [field: SerializeField]
    private int _aspectHeight;

    public DisplayConfiguration(float diagonalInch, float distanceToUserMeter, int aspectWidth, int aspectHeight)
    {
        DiagonalInch = diagonalInch;
        DistanceToUserMeter = distanceToUserMeter;
        AspectWidth = aspectWidth;
        AspectHeight = aspectHeight;
    }

    public DisplayConfiguration(){}

    public override string ToString()
    {
        return $"{WidthPixel}x{HeightPixel} - {DiagonalInch}\", Distance to User: {DistanceToUserMeter}";
    }

    private int GCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}


public static class CRUtil
{
    public static Vector3[] GetNormalDistributionForVelocity(int numberOfSamples, Vector3 velocity, double sigma, System.Random rand)
    {
        Vector3[] normal = new Vector3[numberOfSamples];

        NormalDistribution normalDistributionX = new NormalDistribution(velocity.x, sigma);
        NormalDistribution normalDistributionZ = new NormalDistribution(velocity.z, sigma);

        for (int i = 0; i < numberOfSamples; i++)
        {
            normal[i] = new Vector3((float)normalDistributionX.Sample(rand), velocity.y, (float)normalDistributionZ.Sample(rand)); //TODO: change velocity.y to the actual position of y in respect of the sample of x and z
        }
        return normal;
    }

    public static Vector2[] GetNormalDistributionForVelocity(int numberOfSamples, Vector2 velocity, double sigma, System.Random rand)
    {
        Vector2[] normal = new Vector2[numberOfSamples];

        NormalDistribution normalDistributionX = new NormalDistribution(velocity.x, sigma);
        NormalDistribution normalDistributionY = new NormalDistribution(velocity.y, sigma);

        normal[0] = velocity; // the given velocity must be part of the distribution
        for (int i = 1; i < numberOfSamples; i++)
        {
            normal[i] = new Vector2((float)normalDistributionX.Sample(rand), (float)normalDistributionY.Sample(rand));
        }
        return normal;
    }

    public static Vector2[] GetNormalDistributionForVelocity(int numberOfSamples, Vector2 velocity, Vector2 sigma, System.Random rand)
    {
        Vector2[] normal = new Vector2[numberOfSamples];

        NormalDistribution normalDistributionX = new NormalDistribution(velocity.x, sigma.x);
        NormalDistribution normalDistributionY = new NormalDistribution(velocity.y, sigma.y);

        normal[0] = velocity; // the given velocity must be part of the distribution
        for (int i = 1; i < numberOfSamples; i++)
        {
            normal[i] = new Vector2((float)normalDistributionX.Sample(rand), (float)normalDistributionY.Sample(rand));
        }
        return normal;
    }

    public static Vector2 GetSampleFromNormalDistributionForVelocity(Vector2 velocity, double sigma, System.Random rand)
    {
        NormalDistribution normalDistributionX = new NormalDistribution(velocity.x, sigma);
        NormalDistribution normalDistributionY = new NormalDistribution(velocity.y, sigma);

        return new Vector2((float)normalDistributionX.Sample(rand), (float)normalDistributionY.Sample(rand));
    }

    public static float[] GetNormalDistributionForVelocity(int numberOfSamples, float velocity, double sigma, System.Random rand)
    {
        float[] normal = new float[numberOfSamples];

        NormalDistribution normalDistributionX = new NormalDistribution(velocity, sigma);

        normal[0] = velocity; // the given velocity must be part of the distribution
        for (int i = 1; i < numberOfSamples; i++)
        {
            normal[i] = (float)normalDistributionX.Sample(rand); //TODO: change velocity.y to the actual position of y in respect of the sample of x and z
        }
        return normal;
    }

    public static Vector3 GetAverageVelocity(Vector3[] normalDistributionForVelocity)
    {
        float x, y, z;
        x = y = z = 0;

        foreach (Vector3 vector in normalDistributionForVelocity)
        {
            x = x + vector.x;
            y = y + vector.y;
            z = z + vector.z;
        }

        int numberOfSamples = normalDistributionForVelocity.Length;

        return new Vector3(x / numberOfSamples, y / numberOfSamples, z / numberOfSamples);
    }

    public static float GetAverageVelocity(float[] normalDistributionForVelocity)
    {
        float x;
        x = 0;

        foreach (float vector in normalDistributionForVelocity)
        {
            x = x + vector;
        }

        int numberOfSamples = normalDistributionForVelocity.Length;

        return x / numberOfSamples;
    }

    public static List<VisualStateSpace> GetFocusableGameObjectsOfTasks(List<ITask> tasks)
    {
        List<VisualStateSpace> focusableObjects = new();

        for (int i = 0; i < tasks.Count; i++)
        {
            VisualStateSpace visualStateSpace;

            if (tasks[i].GetType().GetInterfaces().Contains(typeof(ICRTask)))
            {
                visualStateSpace = ((ICRTask)tasks[i]).FocusStateSpace;
            }
            else
            {
                visualStateSpace = new()
                {
                    VisualElements = new(),
                    Camera = tasks[i].GetGameObject().transform.parent.transform.GetChildByName("Camera").GetComponent<Camera>()
                };
            }

            focusableObjects.Insert(i, visualStateSpace);
        }

        return focusableObjects;
    }

    public static bool IsSingleView(SupervisorAgent supervisorAgent)
    {
        return supervisorAgent.VisualTaskQueue.enabled || supervisorAgent.FocusActiveTask;
    }

    public static List<VisualStateSpace> GetFocusableObjectsSingleView(ITask[] tasks)
    {
        return tasks.DistinctBy(x => x.GetType()).Where(x => x.GetType().GetInterfaces().Contains(typeof(ICRTask))).Select(x => x.FocusStateSpace).ToList();
    }

    public static ITask GetTaskForIndexOfFocusableObjectSingleView(ITask[] tasks, int index)
    {
        List<VisualStateSpace> focusableObjects = GetFocusableObjectsSingleView(tasks);

        foreach (VisualStateSpace visualStateSpace in focusableObjects)
        {
            if (index < visualStateSpace.VisualElements.Count)
            {
                return visualStateSpace.Task;
            }

            index -= visualStateSpace.VisualElements.Count;
        }

        throw new EntryPointNotFoundException($"Could not find task for index {index}.");
    }

    public static int GetNumberFocusableObjects(SupervisorAgent supervisorAgent, ITask[] tasks)
    {
        if (CRUtil.IsSingleView(supervisorAgent))
        {
            return CRUtil.GetFocusableObjectsSingleView(tasks).Select(x => x.VisualElements.Count).Sum();
        }
        else
        {
            return CRUtil.GetFocusableGameObjectsOfTasks(tasks.ToList()).Select(x => x.VisualElements.Count).Sum();
        }
    }

    public static Vector3? GetScreenCoordinatesForActiveGameObject(List<VisualStateSpace> visualStateSpaces)
    {
        foreach (VisualStateSpace visualStateSpace in visualStateSpaces) 
        { 
            if (visualStateSpace.HasActiveElement())
            {
                return visualStateSpace.GetScreenCoordinatesForActiveGameObject();
            }
        }

        return null;
    }

    public static GameObjectPosition ConvertToGameObjectPosition(GameObject gameObject, Camera camera)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();

        if (rectTransform == null)
        {
            throw new System.Exception("GameObject does not have a RectTransform component.");
        }

        // Get the world position of the button
        Vector3 worldPosition = rectTransform.position;

        // Convert world position to screen position
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);

        // Get the size of the button in world space
        Vector2 worldSize = rectTransform.rect.size;

        // Convert world size to screen size (assuming no scaling issues)
        Vector2 screenSize = worldSize * rectTransform.lossyScale;

        return new GameObjectPosition
        {
            position = new float2(screenPosition.x, screenPosition.y),
            size = new float2(screenSize.x, screenSize.y),
        };
    }

    public static float GetEffectiveTargetWidth(Vector2 direction, Vector2 targetSize)
    {
        Vector2 dirNorm = direction.normalized;
        float w = targetSize.x;
        float h = targetSize.y;
        float weff = Mathf.Sqrt(Mathf.Pow(w * dirNorm.x, 2) + Mathf.Pow(h * dirNorm.y, 2));
        
        return weff;
    }

    public static float PixelToCM(float pixel, DisplayConfiguration displayConfiguration)
    {
        float ppi = GetPPI(displayConfiguration);

        float inches = pixel / ppi;

        return inches * 2.54f;
    }

    public static float ViewPortToCM(Vector2 velocityViewport, DisplayConfiguration displayConfiguration, int numberOfSplits = 1)
    {
        // Convert diagonal inches to centimeters
        float diagonalCm = displayConfiguration.DiagonalInch * 2.54f;

        // Calculate width and height in cm
        float aspectDiagonal = (float)Math.Sqrt(displayConfiguration.AspectWidth * displayConfiguration.AspectWidth + displayConfiguration.AspectHeight * displayConfiguration.AspectHeight);
        float widthCm = diagonalCm * displayConfiguration.AspectWidth / aspectDiagonal;
        float heightCm = diagonalCm * displayConfiguration.AspectHeight / aspectDiagonal;

        // Calculate the length of the viewport diagonal
        float lengthCm = (float)Math.Sqrt(Math.Pow(velocityViewport.x/numberOfSplits * widthCm, 2) + Math.Pow(velocityViewport.y * heightCm, 2));

        return lengthCm;
    }

    public static float SigmaToCM(float sigma, DisplayConfiguration displayConfiguration)
    {
        float diagonal_cm = displayConfiguration.DiagonalInch * 2.54f;

        return sigma * diagonal_cm;
    }

    public static Vector2 PixelToCM(Vector2 pixel, DisplayConfiguration displayConfiguration)
    {
        return new Vector2(PixelToCM(pixel.x, displayConfiguration), PixelToCM(pixel.y, displayConfiguration));
    }

    public static float CMToPixel(float cm, DisplayConfiguration displayConfiguration)
    {
        float ppi = GetPPI(displayConfiguration);

        float inches = cm / 2.54f;
        float pixels = inches * ppi;

        return pixels;
    }

    public static Vector2 PixelToDisplayLocalCM(Vector2Int pixelPos, DisplayConfiguration config)
    {
        float ppi = GetPPI(config);
        float cmPerPixel = 2.54f / ppi;

        return new Vector2(pixelPos.x * cmPerPixel, pixelPos.y * cmPerPixel);
    }

    public static float GetFovealRadiusInCM(float distanceToScreenMeters, float visualAngleDegrees = 2.5f)
    {
        // Convert viewing distance to millimeters
        float distanceInCM = distanceToScreenMeters * 100f;

        // 1 degree of visual angle = 0.01745 radians
        float thetaRadians = Mathf.Deg2Rad * visualAngleDegrees;

        // Calculate the radius in mm using trigonometry
        float fovealRadiusCM = distanceInCM * Mathf.Tan(thetaRadians);

        return fovealRadiusCM;
    }

    public static float CalculateDistanceBetweenPositionsCM(int sourceDisplayIndex, int targetDisplayIndex, float distanceBetweenTasksDisplays, Vector2Int sourcePosition, Vector2Int targetPosition, DisplayAlignment displayAlignment, List<DisplayConfiguration> displayConfigurations)
    {
        if(sourceDisplayIndex == targetDisplayIndex)
        {
            return CRUtil.PixelToCM(Vector2Int.Distance(sourcePosition, targetPosition), displayConfigurations[sourceDisplayIndex]);
        }

        float distanceBetweenDisplays = CaclulateSpaceBetweenDisplays(sourceDisplayIndex, targetDisplayIndex, distanceBetweenTasksDisplays, displayConfigurations, displayAlignment);

        var sourceConfig = displayConfigurations[sourceDisplayIndex];
        var targetConfig = displayConfigurations[targetDisplayIndex];

        Vector2 sourceCM = CRUtil.PixelToDisplayLocalCM(sourcePosition, sourceConfig);
        Vector2 targetCM = CRUtil.PixelToDisplayLocalCM(targetPosition, targetConfig);

        bool isMovingForward = sourceDisplayIndex < targetDisplayIndex;

        float sourceToEdge = displayAlignment == DisplayAlignment.Horizontal
            ? (isMovingForward ? CRUtil.PixelToCM(sourceConfig.WidthPixel, sourceConfig) - sourceCM.x : sourceCM.x)
            : (isMovingForward ? CRUtil.PixelToCM(sourceConfig.HeightPixel, sourceConfig) - sourceCM.y : sourceCM.y);

        float edgeToTarget = displayAlignment == DisplayAlignment.Horizontal
            ? (isMovingForward ? targetCM.x : CRUtil.PixelToCM(targetConfig.WidthPixel, targetConfig) - targetCM.x)
            : (isMovingForward ? targetCM.y : CRUtil.PixelToCM(targetConfig.HeightPixel, targetConfig) - targetCM.y);

        return distanceBetweenDisplays + sourceToEdge + edgeToTarget;
    }


    private static float CaclulateSpaceBetweenDisplays(int sourceDisplayIndex, int targetDisplayIndex, float distanceBetweenTasksDisplays, List<DisplayConfiguration> displayConfigurations, DisplayAlignment displayAlignment)
    {
        if(sourceDisplayIndex == targetDisplayIndex)
        {
            return 0;
        }

        int start = Mathf.Min(sourceDisplayIndex, targetDisplayIndex);
        int end = Mathf.Max(sourceDisplayIndex, targetDisplayIndex);

        float totalDistanceCM = distanceBetweenTasksDisplays; //minimum distance

        for (int i = start + 1; i < end; i++)
        {
            var config = displayConfigurations[i];
            totalDistanceCM += displayAlignment == DisplayAlignment.Horizontal
                ? CRUtil.PixelToCM(config.WidthPixel, config)
                : CRUtil.PixelToCM(config.HeightPixel, config);

            totalDistanceCM += distanceBetweenTasksDisplays;
        }

        return totalDistanceCM;
    }

    private static float GetPPI(DisplayConfiguration displayConfiguration)
    {
        //fallback value which is 96 on Windows
        float ppi = Screen.dpi;

        if (displayConfiguration.WidthPixel == 0 || displayConfiguration.HeightPixel == 0 || displayConfiguration.DiagonalInch == 0)
        {
            if (ppi == 0)
            {
                ppi = 96;
            }
        }
        else
        {
            float screenDiagonalPixel = Mathf.Sqrt(Mathf.Pow(displayConfiguration.WidthPixel, 2) + Mathf.Pow(displayConfiguration.HeightPixel, 2));
            ppi = screenDiagonalPixel / displayConfiguration.DiagonalInch;
        }

        return ppi;
    }
}
