using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MeasurementUtil
{
    /// <summary>
    /// Facilitates saving two distinct tasks to the same position in a CSV file. Without this consideration, the structure could be altered, 
    /// potentially resulting in an error.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="tasks"></param>
    /// <returns>Returns an ordered tuple in case tasks[a] and tasks[b] are not equal, a tuple with the lowest number of the given types otherwise.</returns>
    public static Tuple<int, int> GetOrderedTaskTupleFileLevel(int a, int b, ITask[] tasks)
    {
        if (a == -1 || tasks[a].GetType() == tasks[b].GetType())
        {
            int i = GetLowestIndexOfType(tasks[b].GetType(), tasks);
            return new Tuple<int, int>(i, i);
        }

        return GetOrderedTaskTupleSwitchLevel(a, b, tasks);
    }

    /// <summary>
    /// Facilitates saving two distinct tasks to the same position in a CSV file. Without this consideration, the structure could be altered, 
    /// potentially resulting in an error.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="tasks"></param>
    /// <returns>Returns an ordered tuple.</returns>
    public static Tuple<int, int> GetOrderedTaskTupleSwitchLevel(int a, int b, ITask[] tasks)
    {
        return new Tuple<int, int>(Math.Min(a, b), Math.Max(a, b));
    }

    public static string GetTupleName(Tuple<int, int> tuple, ITask[] tasks)
    {
        string n1 = Util.ShortenString(tasks[tuple.Item1].GetType().Name);
        string n2 = Util.ShortenString(tasks[tuple.Item2].GetType().Name);

        if (tasks[tuple.Item1].GetType().Equals(tasks[tuple.Item2].GetType()))
        {
            return n1;
        }

        return string.Format("{0}_{1}", n1, n2);
    }

    public static string GetMeasurementName(Type t1, Type t2)
    {
        string n1 = Util.ShortenString(t1.Name);
        string n2 = Util.ShortenString(t2.Name);

        if (t1.Equals(t2))
        {
            return n1;
        }

        return n1[0] < n2[0] ? $"{n1}_{n2}" : $"{n2}_{n1}";
    }

    public static (Type, Type) GetMeasurementTuple(Type t1, Type t2)
    {
        return t1.Name[0] < t2.Name[0] ? (t1, t2) : (t2, t1);
    }

    private static int GetLowestIndexOfType(Type type, ITask[] tasks)
    {
        return tasks.ToList().FindIndex(x => x.GetType() == type);
    }
}
