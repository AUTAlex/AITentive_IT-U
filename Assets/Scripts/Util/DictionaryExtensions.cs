using System.Collections.Generic;
using System.Data;
using UnityEngine;

public static class DictionaryExtensions
{
    public static void AddDistinctiveRange<T1, T2>(this Dictionary<T1, T2> dict, Dictionary<T1, T2> dictToBeAdded)
    {
        foreach (KeyValuePair<T1, T2> entry in dictToBeAdded)
        {
            if (dict.ContainsKey(entry.Key))
            {
                throw new DuplicateNameException($"The dictionary already contains the key {entry.Key}");
            }

            dict.Add(entry.Key, entry.Value);
        }
    }
}
