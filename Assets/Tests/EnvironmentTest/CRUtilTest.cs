using UnityEngine;

using Algorithms;
using NUnit.Framework;
using System.Linq;
using System.Collections.Generic;


public class CRUtilTest
{
    private List<DisplayConfiguration> _configs;

    [SetUp]
    public void Setup()
    {
        // Create dummy 1920x1080 displays with 24-inch diagonals
        _configs = new List<DisplayConfiguration>
        {
            new DisplayConfiguration { WidthPixel = 1920, HeightPixel = 1080, DiagonalInch = 24 },
            new DisplayConfiguration { WidthPixel = 1920, HeightPixel = 1080, DiagonalInch = 24 },
            new DisplayConfiguration { WidthPixel = 1920, HeightPixel = 1080, DiagonalInch = 24 }
        };
    }

    [Test]
    public void PixelCMConversionTest()
    {
        DisplayConfiguration displayConfiguration = new()
        {
            WidthPixel = 1920,
            HeightPixel = 1080,
            DiagonalInch = 27,
            DistanceToUserMeter = 0.3f
        };
        float cm = 10;

        float pixel = CRUtil.CMToPixel(cm, displayConfiguration);
        float cmConvertedBack = CRUtil.PixelToCM(pixel, displayConfiguration);

        Assert.AreEqual(cm, cmConvertedBack, 0.0001);

        cm = 8.3f;

        pixel = CRUtil.CMToPixel(cm, displayConfiguration);
        cmConvertedBack = CRUtil.PixelToCM(pixel, displayConfiguration);

        Assert.AreEqual(cm, cmConvertedBack, 0.0001);
    }

    [Test]
    public void CalculateDistanceBetweenPositionsCM_SameDisplay()
    {
        Vector2Int source = new Vector2Int(0, 0);
        Vector2Int target = new Vector2Int(960, 540); // center
        float expectedCM = CRUtil.PixelToCM(Vector2Int.Distance(source, target), _configs[0]);

        float actual = CRUtil.CalculateDistanceBetweenPositionsCM(
            sourceDisplayIndex: 0,
            targetDisplayIndex: 0,
            distanceBetweenTasksDisplays: 0,
            sourcePosition: source,
            targetPosition: target,
            displayAlignment: DisplayAlignment.Horizontal,
            displayConfigurations: _configs
        );

        Assert.AreEqual(expectedCM, actual, 0.01f);
    }

    [Test]
    public void CalculateDistanceBetweenPositionsCM_AdjacentDisplays_Horizontal()
    {
        int sourceIndex = 0;
        int targetIndex = 1;
        Vector2Int source = new Vector2Int(1800, 500); // near right edge
        Vector2Int target = new Vector2Int(100, 500);  // near left edge
        float gapCM = 2.0f;

        float expectedWidthCM = CRUtil.PixelToCM(1920, _configs[sourceIndex]);

        float sourceToEdge = expectedWidthCM - CRUtil.PixelToDisplayLocalCM(source, _configs[sourceIndex]).x;
        float edgeToTarget = CRUtil.PixelToDisplayLocalCM(target, _configs[targetIndex]).x;

        float expectedTotal = sourceToEdge + gapCM + edgeToTarget;

        float actual = CRUtil.CalculateDistanceBetweenPositionsCM(
            sourceIndex, targetIndex, gapCM, source, target, DisplayAlignment.Horizontal, _configs
        );

        Assert.AreEqual(expectedTotal, actual, 0.01f);
    }

    [Test]
    public void CalculateDistanceBetweenPositionsCM_MultipleDisplays_Vertical()
    {
        int sourceIndex = 0;
        int targetIndex = 2;
        Vector2Int source = new Vector2Int(960, 1000); // near bottom of display 0
        Vector2Int target = new Vector2Int(960, 100);  // near top of display 2
        float gapCM = 3.5f;

        float heightCM = CRUtil.PixelToCM(1080, _configs[sourceIndex]);

        float sourceToEdge = heightCM - CRUtil.PixelToDisplayLocalCM(source, _configs[sourceIndex]).y;
        float middleDisplayCM = CRUtil.PixelToCM(1080, _configs[1]);
        float edgeToTarget = CRUtil.PixelToDisplayLocalCM(target, _configs[targetIndex]).y;

        float expected = sourceToEdge + gapCM + middleDisplayCM + gapCM + edgeToTarget;

        float actual = CRUtil.CalculateDistanceBetweenPositionsCM(
            sourceIndex, targetIndex, gapCM, source, target, DisplayAlignment.Vertical, _configs
        );

        Assert.AreEqual(expected, actual, 0.01f);
    }

    [Test]
    public void CalculateDistanceBetweenPositionsCM_SourceAfterTarget()
    {
        int sourceIndex = 2;
        int targetIndex = 1;
        Vector2Int source = new Vector2Int(100, 500);
        Vector2Int target = new Vector2Int(1800, 500);
        float gapCM = 1.0f;

        float expectedWidthCM = CRUtil.PixelToCM(1920, _configs[targetIndex]);

        float sourceToEdge = CRUtil.PixelToDisplayLocalCM(source, _configs[sourceIndex]).x;
        float edgeToTarget = expectedWidthCM - CRUtil.PixelToDisplayLocalCM(target, _configs[targetIndex]).x;

        float expectedTotal = sourceToEdge + gapCM + edgeToTarget;

        float actual = CRUtil.CalculateDistanceBetweenPositionsCM(
            sourceIndex, targetIndex, gapCM, source, target, DisplayAlignment.Horizontal, _configs
        );

        Assert.AreEqual(expectedTotal, actual, 0.01f);
    }
}