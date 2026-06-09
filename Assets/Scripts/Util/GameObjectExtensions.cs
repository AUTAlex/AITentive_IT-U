using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class GameObjectExtensions
{
    public static T GetBaseComponent<T>(this GameObject gameObject)
    {
        return (T)(object)gameObject.GetBaseComponent(typeof(T));
    }

    public static Component GetBaseComponent(this GameObject gameObject, Type t)
    {
        Component[] components = gameObject.GetComponents(t);

        foreach (Component component in components)
        {
            if (!component.GetType().IsSubclassOf(t))
            {
                return component;
            }
        }

        return null;
    }

    public static List<Component> GetComponentsInHierarchy<T>(this GameObject gameObject)
    {
        return GetComponentsInHierarchy(gameObject, typeof(T));
    }

    public static List<Component> GetComponentsInHierarchy(this GameObject gameObject, Type t)
    {
        List<Component> components = gameObject.GetComponents(t).ToList();

        foreach (Transform child in gameObject.transform)
        {
            GetComponentsInHierarchy(child.gameObject, t, components);
        }

        return components;
    }

    public static Component FindFirstExactOrDerived(Type type)
    {
        if (!typeof(Component).IsAssignableFrom(type))
        {
            Debug.LogError($"Type {type} is not a Component.");
            return null;
        }

        // Find all objects of the given type
        Component[] allObjects = GameObject.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Component firstDerived = null;

        foreach (var obj in allObjects)
        {
            if (obj.GetType() == type)
                return obj; // Return exact match immediately

            if (firstDerived == null && type.IsAssignableFrom(obj.GetType()))
                firstDerived = obj; // Store the first derived match
        }

        return firstDerived; // Return first found derived type if no exact match was found
    }

    public static List<GameObject> GetAllParents(this GameObject obj, string stopCondition = "")
    {
        List<GameObject> parents = new List<GameObject>();
        Transform currentParent = obj.transform.parent;

        while (currentParent != null && currentParent.name != stopCondition)
        {
            parents.Add(currentParent.gameObject);
            currentParent = currentParent.parent;
        }

        return parents;
    }


    private static void GetComponentsInHierarchy(this GameObject gameObject, Type t, List<Component> components)
    {
        components.AddRange(gameObject.GetComponents(t));

        foreach (Transform child in gameObject.transform)
        {
            GetComponentsInHierarchy(child.gameObject, t, components);
        }
    }

    public static T GetFirstParentWithEnabledComponent<T>(this GameObject obj) where T : Behaviour
    {
        for (Transform current = obj.transform.parent; current != null; current = current.parent)
        {
            // Get all T on this parent and return the first enabled one.
            var components = current.GetComponents<T>();
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c != null && c.enabled)
                    return c;
            }
        }

        return null;
    }
}
