#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unit.Ability;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AbilityInputRouter))]
public class AbilityInputRouterEditor : Editor
{
    static List<Type> s_cachedRtsAbilityTypes;
    static string[] s_cachedRtsAbilityNames;
    static List<Type> s_cachedActFpsAbilityTypes;
    static string[] s_cachedActFpsAbilityNames;
    static readonly string[] RtsSlotHotkeys = { "Q", "W", "E", "R", "D", "F", "C", "V" };

    SerializedProperty _rtsAbilitiesProperty;
    SerializedProperty _actFpsAbilitiesProperty;

    void OnEnable()
    {
        _rtsAbilitiesProperty = serializedObject.FindProperty("rtsAbilities");
        _actFpsAbilitiesProperty = serializedObject.FindProperty("actFpsAbilities");
        CacheAbilityTypes();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "rtsAbilities", "actFpsAbilities");
        EnsureRtsSlotCount(_rtsAbilitiesProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("RTS Abilities", EditorStyles.boldLabel);
        DrawFixedAbilityGroup(_rtsAbilitiesProperty, RtsSlotHotkeys, "Slot", s_cachedRtsAbilityTypes, s_cachedRtsAbilityNames);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ACT/FPS Abilities", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This list is independent from RTS command slots and can grow as needed.", MessageType.None);
        DrawDynamicAbilityGroup(_actFpsAbilitiesProperty, "Skill", s_cachedActFpsAbilityTypes, s_cachedActFpsAbilityNames);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawFixedAbilityGroup(
        SerializedProperty groupProperty,
        string[] slotLabels,
        string prefix,
        List<Type> abilityTypes,
        string[] abilityNames)
    {
        if (groupProperty == null)
            return;

        for (int i = 0; i < groupProperty.arraySize; i++)
        {
            string suffix = slotLabels != null && i < slotLabels.Length
                ? $" ({slotLabels[i]})"
                : string.Empty;
            DrawAbilitySlot($"{prefix} {i + 1}{suffix}", groupProperty.GetArrayElementAtIndex(i), abilityTypes, abilityNames);
        }
    }

    void DrawDynamicAbilityGroup(
        SerializedProperty groupProperty,
        string prefix,
        List<Type> abilityTypes,
        string[] abilityNames)
    {
        if (groupProperty == null)
            return;

        for (int i = 0; i < groupProperty.arraySize; i++)
        {
            SerializedProperty slotProperty = groupProperty.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{prefix} {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    {
                        slotProperty.managedReferenceValue = null;
                        groupProperty.DeleteArrayElementAtIndex(i);
                        return;
                    }
                }

                DrawAbilityPopup(slotProperty, abilityTypes, abilityNames);
                if (slotProperty.managedReferenceValue != null)
                {
                    slotProperty.isExpanded = true;
                    EditorGUILayout.PropertyField(slotProperty, new GUIContent("Settings"), true);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add ACT/FPS Ability", GUILayout.Width(160f)))
            {
                int insertIndex = groupProperty.arraySize;
                groupProperty.arraySize++;
                groupProperty.GetArrayElementAtIndex(insertIndex).managedReferenceValue = null;
            }
        }
    }

    void DrawAbilitySlot(
        string label,
        SerializedProperty slotProperty,
        List<Type> abilityTypes,
        string[] abilityNames)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            DrawAbilityPopup(slotProperty, abilityTypes, abilityNames);

            if (slotProperty.managedReferenceValue == null)
                return;

            slotProperty.isExpanded = true;
            EditorGUILayout.PropertyField(slotProperty, new GUIContent("Settings"), true);
        }
    }

    void DrawAbilityPopup(
        SerializedProperty slotProperty,
        List<Type> abilityTypes,
        string[] abilityNames)
    {
        int currentIndex = GetAbilityTypeIndex(slotProperty, abilityTypes);
        int selectedIndex = EditorGUILayout.Popup("Ability", currentIndex, abilityNames);
        if (selectedIndex == currentIndex)
            return;

        slotProperty.managedReferenceValue = selectedIndex == 0
            ? null
            : Activator.CreateInstance(abilityTypes[selectedIndex - 1]);
    }

    void EnsureRtsSlotCount(SerializedProperty abilityProperty)
    {
        if (abilityProperty == null || abilityProperty.arraySize == RtsSlotHotkeys.Length)
            return;

        abilityProperty.arraySize = RtsSlotHotkeys.Length;
    }

    static void CacheAbilityTypes()
    {
        if (s_cachedRtsAbilityTypes != null &&
            s_cachedRtsAbilityNames != null &&
            s_cachedActFpsAbilityTypes != null &&
            s_cachedActFpsAbilityNames != null)
            return;

        s_cachedRtsAbilityTypes = CollectAbilityTypes<RtsUnitAbility>();
        s_cachedRtsAbilityNames = BuildAbilityNames(s_cachedRtsAbilityTypes);
        s_cachedActFpsAbilityTypes = CollectAbilityTypes<ActFpsUnitAbility>();
        s_cachedActFpsAbilityNames = BuildAbilityNames(s_cachedActFpsAbilityTypes);
    }

    static List<Type> CollectAbilityTypes<TAbilityBase>() where TAbilityBase : UnitAbility
    {
        List<Type> types = new List<Type>();
        foreach (Type type in TypeCache.GetTypesDerivedFrom<TAbilityBase>())
        {
            if (type.IsAbstract || type.IsGenericType)
                continue;

            types.Add(type);
        }

        types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return types;
    }

    static string[] BuildAbilityNames(List<Type> abilityTypes)
    {
        string[] names = new string[abilityTypes.Count + 1];
        names[0] = "None";
        for (int i = 0; i < abilityTypes.Count; i++)
            names[i + 1] = ObjectNames.NicifyVariableName(abilityTypes[i].Name);

        return names;
    }

    static int GetAbilityTypeIndex(SerializedProperty slotProperty, List<Type> abilityTypes)
    {
        Type currentType = GetManagedReferenceType(slotProperty);
        if (currentType == null)
            return 0;

        for (int i = 0; i < abilityTypes.Count; i++)
        {
            if (abilityTypes[i] == currentType)
                return i + 1;
        }

        return 0;
    }

    static Type GetManagedReferenceType(SerializedProperty property)
    {
        if (property == null || string.IsNullOrWhiteSpace(property.managedReferenceFullTypename))
            return null;

        string[] parts = property.managedReferenceFullTypename.Split(' ');
        if (parts.Length != 2)
            return null;

        return Type.GetType($"{parts[1]}, {parts[0]}");
    }
}
#endif
