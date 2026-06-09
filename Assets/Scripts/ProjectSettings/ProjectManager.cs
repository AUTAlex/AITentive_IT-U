using Supervisor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectManager
{
    public static IProjectSettings ProjectSettings { 
        get 
        {
            return LoadProjectSettings();
        }
    }

    private static IProjectSettings _projectSettings;


    public static void EnableComponents()
    {
        List<(Component, FieldInfo)> values = GetAttributeFieldsForProject<EnablesAttribute>();

        foreach ((Component component, FieldInfo field) in values)
        {
            Attribute[] enabledAttributes = Attribute.GetCustomAttributes(field, typeof(EnablesAttribute));

            foreach (Attribute attribute in enabledAttributes)
            {
                EnablesAttribute enabledAttribute = (EnablesAttribute)attribute;
                Behaviour behaviour = FindFirstBehaviourByName(enabledAttribute.ComponentType);

                if (field.FieldType == typeof(bool))
                {
                    behaviour.enabled = (bool)field.GetValue(component);
                }
                else if (field.GetValue(component).ToString() == enabledAttribute.EnabledForValue)
                {
                    behaviour.enabled = true;
                }
                else
                {
                    behaviour.enabled = false;
                }
            }
        }
    }

    public static List<(Component, FieldInfo)> GetAttributeFieldsForProject<T>() where T : Attribute
    {
        List<GameObject> allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None).ToList();

        return ProjectManager.GetAttributeFieldsFor<T>(allObjects);
    }

    public static List<(Component, FieldInfo)> GetProjectAssignFieldsForTaskPrefabs()
    {
        List<GameObject> allObjects = GetGameObjectHierarchyOf(ProjectSettings.TasksGameObjects);

        return ProjectManager.GetAttributeFieldsFor<ProjectAssignAttribute>(allObjects);
    }

    public static List<(Component, FieldInfo)> GetProjectAssignFieldsForVisionPrefab()
    {
        List<GameObject> allObjects = GetGameObjectHierarchyOf(new GameObject[] { ProjectSettings.VisionAgentPrefab });
        List<Component> allComponents = allObjects
            .SelectMany(x => x.GetComponents<VisionAgent>())
            .Where(c => c.GetType().Name == ProjectSettings.VisionAgentChoice.ToString())
            .Cast<Component>()
            .ToList();

        return ProjectManager.GetAttributeFieldsFor<ProjectAssignAttribute>(allComponents);
    }

    public static List<(Component, FieldInfo)> GetProjectAssignFieldsForSupervisor()
    {
        List<(Component, FieldInfo)> result = GetAttributeFieldsFor<ProjectAssignAttribute>(new List<GameObject>() { ProjectSettings.SupervisorAgent.gameObject });
        result.RemoveAll(x => x.Item1.GetType().IsSubclassOf(typeof(SupervisorAgent)) || x.Item1.GetType() == typeof(SupervisorAgent));

        return ProjectManager.GetAttributeFieldsFor<ProjectAssignAttribute>(ProjectSettings.GetSupervisorAgentForSupervisorChoice(), result);
    }

    /**
    public static List<(Component, FieldInfo)> GetProjectAssignFieldsForFocusAgent()
    {
        return GetAttributeFieldsFor<ProjectAssignAttribute>(new List<GameObject>() { ProjectSettings.VisionAgent.gameObject });
    }
    **/

    public static void ProjectAssignValuesToFields()
    {
        List<string> missingVariableWarnings = new List<string>();

        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            foreach (Component component in go.GetComponents(typeof(Component)))
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

                List<Type> types = new();

                if (component != null)
                {
                    types = component.GetType().GetParentTypes().ToList();
                    types.Add(component.GetType());
                }

                foreach (Type t in types)
                {
                    foreach (var field in t.GetFields(flags))
                    {
                        if (Attribute.IsDefined(field, typeof(ProjectAssignAttribute)))
                        {
                            Component projectAssignComponent = ProjectSettings.GetManagedComponentFor(t);

                            // If the field exists, set its value
                            if (projectAssignComponent != null)
                            {
                                field.SetValue(component, field.GetValue(projectAssignComponent));
                            }
                            else if (!ProjectSettings.TasksGameObjects.IsNullOrEmpty() && ProjectSettings.Tasks.FirstOrDefault(x => x != null && x.GetType() == t) != null)
                            {
                                missingVariableWarnings.Add(string.Format("Could not find any corresponding variable in the DataStorage for variable {0} of class {1} with assigned ProjectAssign attribute.", field.GetFieldName(), t.Name));
                            }
                        }
                    }
                }
            }
        }

        missingVariableWarnings = missingVariableWarnings.Distinct().ToList();

        foreach (string missingVariableWarning in missingVariableWarnings)
        {
            Debug.LogWarning(missingVariableWarning);
        }
    }


    private static List<(Component, FieldInfo)> GetAttributeFieldsFor<T>(Component component, List<(Component, FieldInfo)> fieldsWithAttribute = null) where T : Attribute
    {
        if (fieldsWithAttribute == null)
        {
            fieldsWithAttribute = new();
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        List<Type> types = new();
        Behaviour behaviour = component as Behaviour;

        if (component != null && behaviour != null && behaviour.enabled && behaviour.gameObject.activeSelf)
        {
            types = component.GetType().GetParentTypes().ToList();
            types.Add(component.GetType());
        }

        foreach (Type t in types)
        {
            foreach (var field in t.GetFields(flags))
            {
                if (Attribute.IsDefined(field, typeof(T)))
                {
                    //The following if condition prevents that the same field of the same type is added multiple times to the list
                    if (!fieldsWithAttribute.ConvertAll(x => (x.Item1.GetType().GetLowestBaseTypeInHierarchyOf(typeof(Task)), x.Item2)).ToList().Contains((component.GetType().GetLowestBaseTypeInHierarchyOf(typeof(Task)), field)))
                    {
                        fieldsWithAttribute.Add((component, field));
                    }
                }
            }
        }

        return fieldsWithAttribute;
    }

    private static List<GameObject> GetGameObjectHierarchyOf(GameObject[] gameObjects)
    {
        List<GameObject> allObjects = new();

        foreach (GameObject gameObject in gameObjects)
        {
            if (gameObject != null)
            {
                allObjects.Add(gameObject);

                foreach (Transform transform in gameObject.transform.GetChildren())
                {
                    allObjects.Add(transform.gameObject);
                }
            }
        }

        return allObjects;
    }

    private static List<(Component, FieldInfo)> GetAttributeFieldsFor<T>(List<GameObject> allObjects) where T : Attribute
    {
        List<(Component, FieldInfo)> projectAssignFields = new();

        foreach (GameObject go in allObjects)
        {
            foreach (Component component in go.GetComponents(typeof(Component)))
            {
                projectAssignFields = GetAttributeFieldsFor<T>(component, projectAssignFields);
            }
        }

        return projectAssignFields;
    }

    private static List<(Component, FieldInfo)> GetAttributeFieldsFor<T>(List<Component> components) where T : Attribute
    {
        List<(Component, FieldInfo)> projectAssignFields = new();

        foreach (Component component in components)
        {
            projectAssignFields = GetAttributeFieldsFor<T>(component, projectAssignFields);
        }

        return projectAssignFields;
    }

    private static IProjectSettings LoadProjectSettings()
    {
        Scene currentScene = GetProjectScene();

        //"projectSettings is null" leads to problems (e.g. after BuildScriptTests.TrainingEnvironmentExecutionTest), see here:
        //https://stackoverflow.com/a/72072517/11986067 or https://forum.unity.com/threads/different-types-of-null.559879/
        if (_projectSettings == null || object.ReferenceEquals(_projectSettings, null) || (object)_projectSettings == null || _projectSettings is null || _projectSettings.Equals(null))
        {
            _projectSettings = GetProjectSettings(currentScene);
            return _projectSettings;
        }
        else
        {
            return _projectSettings;
        }
    }

    private static IProjectSettings GetProjectSettings(Scene scene)
    {
        GameObject[] allObjects = scene.GetRootGameObjects();
        IProjectSettings projectSettings = null;

        for (int i = 0; i < allObjects.Length; i++)
        {
            if (allObjects[i].name == "ProjectSettings")
            {
                projectSettings = allObjects[i].GetComponent<ProjectSettings>();
            }
        }

        return projectSettings;
    }

    private static Scene GetProjectScene()
    {
#if UNITY_EDITOR
        return EditorSceneManager.GetActiveScene();
#else
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "SupervisorML")
        {
            if (Application.CanStreamedLevelBeLoaded("SupervisorML"))
            {
                // Load the scene synchronously and switch to it
                SceneManager.LoadScene("SupervisorML");
                activeScene = SceneManager.GetActiveScene(); // Update the reference after loading
            }
            else
            {
                Debug.LogError("Scene 'SupervisorML' could not be loaded. Ensure it is added to the build settings.");
            }
        }
        return activeScene;
#endif
    }

    private static Behaviour FindFirstBehaviourByName(Type type)
    {
        // Get all components in the scene (this will search every GameObject)
        Behaviour[] allComponents = GameObject.FindObjectsByType<Behaviour>(FindObjectsSortMode.None);

        // Iterate through the components to find the first one with the specified name
        foreach (Behaviour component in allComponents)
        {
            if (component.GetType() == type)
            {
                return component;
            }
        }

        // Return null if no component with the specified name is found
        return null;
    }
}
