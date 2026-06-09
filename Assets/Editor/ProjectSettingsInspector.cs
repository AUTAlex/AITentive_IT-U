using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.MLAgents;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// Inspect ReloadFields() and AddFieldToInspector() to understand how the fields are loaded, updated and how those fields kept persistent for Tasks- and Vision-Agents 
/// </summary>
[CustomEditor(typeof(ProjectSettings))]
public class ProjectSettingsInspector : Editor
{
    private static Dictionary<Type, bool> _isUnfolded = new();

    private ProjectSettings _projectSettings;

    private List<(Component, FieldInfo)> _fields;

    private Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

    public override void OnInspectorGUI()
    {
        if (!Application.isPlaying) 
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            DrawPropertiesExcluding(serializedObject, "m_Script");
            EditorGUILayout.Space();

            bool projectSettingsChanged = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            if (projectSettingsChanged)
            {
                ProjectManager.EnableComponents();
                // only reload fields if e.g. the task game objects have changed
                ReloadFields();
            }

            EditorGUILayout.LabelField("Settings for Active Components of the Project", EditorStyles.boldLabel);
            AddProjectAssignFieldsToIspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate File Name"))
            {
                _projectSettings.GenerateFilename();
            }

            if (GUILayout.Button("Load Experiment Setting"))
            {
                string path = Application.dataPath;
                path = EditorUtility.OpenFilePanel("Experiment Setting Selection", Path.Combine(Directory.GetParent(path).ToString(), "config", "experiment_config"), "json");

                if (path == "")
                {
                    return;
                }

                LoadProjectSettings(path, _projectSettings);
                ReloadFields();

                Debug.Log("Experiment settings loaded!");
            }
        }
        else
        {
            EditorGUILayout.LabelField("Project Settings cannot be changed during play mode.", EditorStyles.boldLabel);
        }

        if(GUI.changed)
        {
            SynchronizeInputsWithTasksGameObjects();
            _projectSettings.UpdateSettings();

            PersistProgrammaticChanges(_projectSettings);
            //Validator.ValidateProjectSettings(_projectSettings);
            ReloadFields();
        }
    }


    private void OnEnable()
    {
        _projectSettings = (ProjectSettings)target;

        ReloadFields();
    }

    private void ReloadFields()
    {
        _fields = ProjectManager.GetAttributeFieldsForProject<ProjectAssignAttribute>();

        //Explicit call necessary since GetAttributeFieldsForProject returns the objects in the project hierarchy which are destroyed when updating
        //the project settings. In contrast to that GetProjectAssignFieldsForTasks returns the fields of the tasks of the prefab objects.
        ReplaceFields(ProjectManager.GetProjectAssignFieldsForTaskPrefabs());
        if (_projectSettings.AtLeastOneTaskUsesVisionAgent())
        {
            ReplaceFields(ProjectManager.GetProjectAssignFieldsForVisionPrefab());
        }

        _fields = _fields
            .OrderBy(x => x.Item1.GetType().Name)
            .ThenBy(x => x.Item2.MetadataToken)
            .ToList();

        InitFoldOut(_fields);
    }

    private void ReplaceFields(List<(Component, FieldInfo)> fieldsToReplace)
    {
        List<Type> types = fieldsToReplace.Select(x => x.Item1.GetType()).Distinct().ToList();


        _fields.RemoveAll(x => types.Contains(x.Item1.GetType()));
        _fields.AddRange(fieldsToReplace);
    }

    private void Awake()
    {
        _projectSettings = (ProjectSettings)target;
    
    }

    private void AddProjectAssignFieldsToIspector()
    {
        Component previousComponent = null;

        //remove all destroyed objects
        _fields.RemoveAll(x => x.Item1 == null);

        foreach ((Component, FieldInfo) entry in _fields)
        {
            if(previousComponent != entry.Item1)
            {
                AddFoldOutToInspector(entry.Item1);
                previousComponent = entry.Item1;
            }

            if (_isUnfolded[entry.Item1.GetType()])
            {
                int previousIndentLevel = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                AddHeaderToInspector(entry.Item2);

                ProjectAssignAttribute projectAssignAttribute = (ProjectAssignAttribute)Attribute.GetCustomAttribute(entry.Item2, typeof(ProjectAssignAttribute));
                HiddenAttribute hidesAttribute = (HiddenAttribute)Attribute.GetCustomAttribute(entry.Item2, typeof(HiddenAttribute));

                if (!projectAssignAttribute.Hide && !HideField(hidesAttribute))
                {
                    AddFieldToInspector(entry);
                }

                EditorGUI.indentLevel = previousIndentLevel;
            }
        }
    }

    private bool HideField(HiddenAttribute hidesAttribute)
    {
        if (hidesAttribute != null) 
        {
            (Component, FieldInfo) searchedField = _fields.Where(x => x.Item2.GetFieldName() == hidesAttribute.FieldName).FirstOrDefault();
            if (!searchedField.Equals(default)) 
            {
                bool IsBoolAndFalse = searchedField.Item2.GetUnderlyingType() == typeof(bool) && (bool)searchedField.Item2.GetValue(searchedField.Item1) == false;
                bool IsNotBoolAndHasHideAttributeValue = searchedField.Item2.GetUnderlyingType() != typeof(bool) && searchedField.Item2.GetValue(searchedField.Item1).ToString() == hidesAttribute.HiddenForValue;

                if (IsBoolAndFalse || IsNotBoolAndHasHideAttributeValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void InitFoldOut(List<(Component, FieldInfo)> fields)
    {
        List<Type> types = fields.Select(x => x.Item1.GetType()).Distinct().ToList();

        foreach (Type type in types)
        {
            if (!_isUnfolded.ContainsKey(type))
            {
                _isUnfolded[type] = false;
            }
        }
    }

    private void AddFoldOutToInspector(Component component)
    {
        _isUnfolded[component.GetType()] = EditorGUILayout.Foldout(_isUnfolded[component.GetType()], component.GetType().Name, EditorStyles.foldoutHeader);
    }

    private void AddHeaderToInspector(FieldInfo field)
    {
        ProjectAssignAttribute projectAssignAttribute = (ProjectAssignAttribute)Attribute.GetCustomAttribute(field, typeof(ProjectAssignAttribute));

        if (projectAssignAttribute.Header != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(projectAssignAttribute.Header, EditorStyles.boldLabel);
        }
    }

    private Dictionary<string, ReorderableList> reorderableLists = new Dictionary<string, ReorderableList>();

    /// <summary>
    /// Loads the specific fields into the inspector AND updates the GameObjects in the project hierarchy
    /// </summary>
    /// <param name="entry"></param>
    private void AddFieldToInspector((Component, FieldInfo) entry)
    {
        TooltipAttribute tooltipAttribute = (TooltipAttribute)Attribute.GetCustomAttribute(entry.Item2, typeof(TooltipAttribute));

        GUIContent content = new GUIContent(Util.FormatFieldName(entry.Item2.Name), tooltipAttribute != null ? tooltipAttribute.tooltip : "");

        object value = entry.Item2.FieldType switch
        {
            var type when type == typeof(int) => EditorGUILayout.IntField(label: content, entry.Item2.GetValue(entry.Item1) is int @int ? @int : 0),
            var type when type == typeof(string) => EditorGUILayout.TextField(label: content, entry.Item2.GetValue(entry.Item1) is string @str ? @str : ""),
            var type when type == typeof(float) => EditorGUILayout.FloatField(label: content, entry.Item2.GetValue(entry.Item1) is float @flt ? @flt : 0f),
            var type when type == typeof(double) => EditorGUILayout.DoubleField(label: content, entry.Item2.GetValue(entry.Item1) is double @double ? @double : 0f),
            var type when type == typeof(bool) => EditorGUILayout.Toggle(label: content, entry.Item2.GetValue(entry.Item1) is bool @bool ? @bool : false),
            var type when type.IsEnum => EditorGUILayout.EnumPopup(label: content, (Enum)entry.Item2.GetValue(entry.Item1)),
            var type when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) => DrawReorderableList(entry.Item1, entry.Item2, type),
            var type when IsCustomClass(type) => ClassFieldDropDownLayout(entry.Item1, entry.Item2, type),
            _ => EditorGUILayout.ObjectField(label: content, obj: (UnityEngine.Object)entry.Item2.GetValue(entry.Item1), entry.Item2.FieldType, allowSceneObjects: true),
        };


        if (!(entry.Item2.FieldType.IsGenericType && entry.Item2.FieldType.GetGenericTypeDefinition() == typeof(List<>)))
        {
            entry.Item2.SetValue(entry.Item1, value);
        }
    }

    private object DrawReorderableList(Component component, FieldInfo fieldInfo, Type fieldType)
    {
        string key = component.GetInstanceID() + "." + fieldInfo.Name;
        if (!reorderableLists.ContainsKey(key))
        {
            IList list = (IList)fieldInfo.GetValue(component);
            Type elementType = fieldType.GetGenericArguments()[0];

            ReorderableList reorderableList = new ReorderableList(list, elementType, true, true, true, true)
            {
                drawHeaderCallback = (Rect rect) => DrawReorderableListHeaderCallback(rect, component, fieldInfo),
                drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => DrawReorderableListElementCallback(rect, index, component, fieldInfo, fieldType),
                elementHeightCallback = (int index) => ReorderableListElementHeightCallback(index, component, fieldInfo, fieldType),
                onAddCallback = (ReorderableList l) => { list.Add(Activator.CreateInstance(elementType)); },
                onRemoveCallback = (ReorderableList l) =>{list.RemoveAt(l.index);}
            };

            reorderableLists[key] = reorderableList;
        }

        ReorderableList listToDraw = reorderableLists[key];
        Rect listRect = EditorGUILayout.GetControlRect(
            false,
            listToDraw.GetHeight()
        );
        listRect = EditorGUI.IndentedRect(listRect);
        listToDraw.DoList(listRect);

        return fieldInfo.GetValue(component);
    }

    private void DrawReorderableListHeaderCallback(Rect rect, Component component, FieldInfo fieldInfo)
    {
        IList list = (IList)fieldInfo.GetValue(component);

        rect.xMin += 10; // Adjust for the foldout arrow space
        Rect labelRect = new Rect(rect.x, rect.y, rect.width - 40, rect.height);
        EditorGUI.LabelField(labelRect, Util.FormatFieldName(fieldInfo.Name));

        Rect countRect = new Rect(rect.x + rect.width - 40, rect.y, 40, rect.height);
        EditorGUI.LabelField(countRect, list.Count.ToString(), EditorStyles.label);
    }

    private void DrawReorderableListElementCallback(Rect rect, int index, Component component, FieldInfo fieldInfo, Type fieldType)
    {
        IList list = (IList)fieldInfo.GetValue(component);
        Type elementType = fieldType.GetGenericArguments()[0];

        string elementKey = component.GetInstanceID() + "." + fieldInfo.Name + ".element" + index;

        if (!_foldouts.ContainsKey(elementKey))
            _foldouts[elementKey] = false;

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect foldoutRect = new Rect(rect.x + 8, rect.y, rect.width, lineHeight);
        _foldouts[elementKey] = EditorGUI.Foldout(foldoutRect, _foldouts[elementKey], $"Element {index}", true);

        if (_foldouts[elementKey])
        {
            object element = list[index] ?? Activator.CreateInstance(elementType);

            float yOffset = rect.y + lineHeight + 2;
            var fields = GetInspectableFields(elementType);

            foreach (var field in fields)
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                Rect labelRect = new Rect(rect.x, yOffset, labelWidth, lineHeight);
                EditorGUI.LabelField(labelRect, GetFieldGUIContent(field));

                Rect fieldRect = new Rect(rect.x + labelWidth - 20, yOffset, rect.width - labelWidth + 20, lineHeight);
                object fieldValue = field.GetValue(element);
                object newValue = DrawElementField(fieldRect, fieldValue, field.FieldType);

                if (!Equals(newValue, fieldValue))
                    field.SetValue(element, newValue);

                yOffset += lineHeight + 2;
            }

            list[index] = element;
        }
    }

    private float ReorderableListElementHeightCallback(int index, Component component, FieldInfo fieldInfo, Type fieldType)
    {
        Type elementType = fieldType.GetGenericArguments()[0];

        string elementKey = component.GetInstanceID() + "." + fieldInfo.Name + ".element" + index;

        if (!_foldouts.ContainsKey(elementKey) || !_foldouts[elementKey])
            return EditorGUIUtility.singleLineHeight + 4;

        int fieldCount = GetInspectableFields(elementType).Count;
        return (EditorGUIUtility.singleLineHeight + 2) * (fieldCount + 1); // +1 for foldout line
    }

    private object DrawElementField(Rect rect, object element, Type elementType)
    {
        if (element == null)
        {
            element = Activator.CreateInstance(elementType);
        }

        if (elementType == typeof(int))
        {
            return EditorGUI.IntField(rect, element is int @int ? @int : 0);
        }
        if (elementType == typeof(string))
        {
            return EditorGUI.TextField(rect, element as string ?? "");
        }
        if (elementType == typeof(float))
        {
            return EditorGUI.FloatField(rect, element is float @flt ? @flt : 0f);
        }
        if (elementType == typeof(double))
        {
            return EditorGUI.DoubleField(rect, element is double @double ? @double : 0f);
        }
        if (elementType == typeof(bool))
        {
            return EditorGUI.Toggle(rect, element is bool @bool ? @bool : false);
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
        {
            return EditorGUI.ObjectField(rect, element as UnityEngine.Object, elementType, allowSceneObjects: true);
        }
        if (elementType.IsClass || (elementType.IsValueType && !elementType.IsPrimitive))
        {
            return ClassField(rect, element, elementType);
        }

        EditorGUI.LabelField(rect, $"Unsupported type {elementType}");
        return element;
    }

    private object ClassFieldDropDownLayout(Component component, FieldInfo fieldInfo, Type elementType)
    {
        string key = fieldInfo.Name + "_" + elementType.Name;

        if (!_foldouts.ContainsKey(key))
            _foldouts[key] = false;

        Rect rect = EditorGUILayout.GetControlRect();
        _foldouts[key] = EditorGUI.Foldout(
            rect,
            _foldouts[key],
            ObjectNames.NicifyVariableName(fieldInfo.Name),
            true
        );

        if (_foldouts[key])
        {
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical("box");
            fieldInfo.SetValue(component, ClassFieldLayout(fieldInfo.GetValue(component), elementType));
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel = previousIndent;
        }

        return fieldInfo.GetValue(component);
    }

    private object ClassFieldLayout(object element, Type elementType)
    {
        if (element == null)
            element = Activator.CreateInstance(elementType);

        // Estimate total height based on number of fields
        var fields = GetInspectableFields(elementType);
        float totalHeight = fields.Count * (EditorGUIUtility.singleLineHeight + 2);

        Rect rect = EditorGUILayout.GetControlRect(false, totalHeight);
        return ClassField(rect, element, elementType);
    }

    private object ClassField(Rect rect, object element, Type elementType)
    {
        EditorGUI.BeginChangeCheck();

        float yOffset = rect.y;
        float fieldHeight = EditorGUIUtility.singleLineHeight + 2;

        var fields = GetInspectableFields(elementType);

        foreach (var field in fields)
        {
            Rect lineRect = EditorGUI.IndentedRect(
                new Rect(rect.x, yOffset, rect.width, EditorGUIUtility.singleLineHeight)
            );

            // Reserve space for label and get control ID
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            GUIContent label = GetFieldGUIContent(field);
            Rect fieldRect = EditorGUI.PrefixLabel(lineRect, controlID, label);

            object fieldValue = field.GetValue(element);
            object newValue = DrawElementField(fieldRect, fieldValue, field.FieldType);

            if (!Equals(newValue, fieldValue))
            {
                field.SetValue(element, newValue);
            }

            yOffset += fieldHeight;
        }

        if (EditorGUI.EndChangeCheck())
        {
            return element;
        }

        return element;
    }



    private void LoadProjectSettings(string paths, ProjectSettings projectSettings)
    {
        Dictionary<Type, ISettings> settings = SettingsLoader.LoadSettings(paths);

        //Validator.ValidateExperimentSettings(settings);

        SceneManagement.ProjectSettings = projectSettings;
        SceneManagement.ConfigScene(settings);
    }

    private void SynchronizeInputsWithTasksGameObjects()
    {
        while (_projectSettings.Inputs.Count > _projectSettings.TasksGameObjects.Length)
        {
            _projectSettings.Inputs.RemoveAt(_projectSettings.Inputs.Count - 1);
        }

        for (int i = 0; i < _projectSettings.TasksGameObjects.Length; i++)
        {
            if(_projectSettings.Inputs.Count <= i)
            {
                InputActionAsset inputActions = _projectSettings.TasksGameObjects[i].transform.GetChildByName("Agent").GetComponent<PlayerInput>().actions;

                _projectSettings.Inputs.Add(inputActions);
            }
        }
    }

    private bool IsCustomClass(Type type)
    {
        return type.IsClass && !typeof(UnityEngine.Object).IsAssignableFrom(type);
    }

    //Unity does not realize the object has been changed and does not properly re-serialize it. When an object is edited in the editor, the object
    //instance is not actually edited, but the serialized data instead. When objects are directly changed, Unity might just discard it and use its
    //old serialized data. Calling EditorUtility.SetDirty on the object let Unity know it was changed. Alternatively, the SerializedObject API could
    //be used to edit the object, which is what the inspector is using. This is in this case problematic since BehaviorParameters use getter and
    //setters for its fields which cannot be serialized.
    private void PersistProgrammaticChanges(ProjectSettings projectSettings)
    {
        Agent[] agents = projectSettings.Agents;
        agents = agents.Concat(projectSettings.VisionAgents).ToArray();

        Supervisor.SupervisorAgent supervisorAgent2 = projectSettings.SupervisorAgent;

        for (int i = 0; i < agents.Length; i++)
        {
            EditorUtility.SetDirty(agents[i]);
            EditorUtility.SetDirty(agents[i].GetComponent<Unity.MLAgents.Policies.BehaviorParameters>());
        }
        EditorUtility.SetDirty(supervisorAgent2);
    }

    private List<FieldInfo> GetInspectableFields(Type type)
    {
        return type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(IsInspectableField)
            .OrderBy(field => field.MetadataToken)
            .ToList();
    }

    private bool IsInspectableField(FieldInfo field)
    {
        if (field.IsStatic)
        {
            return false;
        }

        if (Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
        {
            return false;
        }

        if (Attribute.IsDefined(field, typeof(HideInInspector)))
        {
            return false;
        }

        if (field.IsPublic)
        {
            return true;
        }

        return Attribute.IsDefined(field, typeof(SerializeField));
    }

    private GUIContent GetFieldGUIContent(FieldInfo field)
    {
        TooltipAttribute tooltipAttribute =
            (TooltipAttribute)Attribute.GetCustomAttribute(field, typeof(TooltipAttribute));

        return new GUIContent(
            Util.FormatFieldName(GetDisplayFieldName(field)),
            tooltipAttribute != null ? tooltipAttribute.tooltip : ""
        );
    }

    private string GetDisplayFieldName(FieldInfo field)
    {
        string fieldName = field.Name;

        if (fieldName.StartsWith("<") && fieldName.Contains(">k__BackingField"))
        {
            int endIndex = fieldName.IndexOf(">");
            return fieldName.Substring(1, endIndex - 1);
        }

        return fieldName;
    }
}
