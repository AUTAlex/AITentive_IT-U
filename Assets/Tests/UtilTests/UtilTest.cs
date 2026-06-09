using UnityEngine;

using Algorithms;
using NUnit.Framework;
using System.Linq;


public class UtilTest
{
    [Test]
    // Yes I know this is a bad test, but I don`t care
    public void GenerateRandomValuesTest()
    {
        int minValue;
        int maxValue;
        int numTasks = 10;

        System.Random rnd = new();

        for (int i = 0; i < 100; i++)
        {
            minValue = rnd.Next(1, 50);
            maxValue = rnd.Next(51, 100);

            int totalPriority = (int)((((maxValue - minValue) / 2f) + minValue) * numTasks);

            System.Collections.Generic.List<int> randomValues = Util.GenerateRandomValues(minValue, maxValue, numTasks);

            Assert.AreEqual(totalPriority, randomValues.Sum(), $"Used values: min value: {minValue}; max value: {maxValue}; i: {i}");
        }
    }
}