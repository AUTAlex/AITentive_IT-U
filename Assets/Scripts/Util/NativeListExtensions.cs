using System.Collections.Generic;
using System;
using System.Reflection;
using Unity.Collections;
using UnityEngine;

public static class NativeListExtension
{
    public static List<T> ToList<T>(this NativeList<T> nativeList) where T : unmanaged
    {
        List<T> result = new List<T>(nativeList.Length);
        for (int i = 0; i < nativeList.Length; i++)
        {
            result.Add(nativeList[i]);
        }
        return result;
    }

    public static T[] ToArray<T>(this NativeList<T> nativeList) where T : unmanaged
    {
        T[] result = new T[nativeList.Length];
        for (int i = 0; i < nativeList.Length; i++)
        {
            result[i] = nativeList[i];
        }
        return result;
    }
}
