using System;
using UnityEngine;
using System.Diagnostics;

namespace Utils
{
    public class EnumHelper
    {
        public static T GetRandomEnumValue<T>() where T : Enum
        {
            var enumValues = Enum.GetValues(typeof(T));
            int randomIndex = UnityEngine.Random.Range(0, enumValues.Length);

            return (T)enumValues.GetValue(randomIndex);
        }

    }
}