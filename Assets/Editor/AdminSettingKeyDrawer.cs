using UnityEditor;
using UnityEngine;

// AdminSettingField의 "항목"을 글자로 적지 않고 목록(탭 / 이름)에서 고르게 해줌
[CustomPropertyDrawer(typeof(AdminSettingKeyAttribute))]
public class AdminSettingKeyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var defs = AdminSettings.Defs;
        var names = new GUIContent[defs.Count + 1];
        names[0] = new GUIContent("(고르세요)");
        int current = 0;
        for (int i = 0; i < defs.Count; i++)
        {
            names[i + 1] = new GUIContent($"{defs[i].tab}/{defs[i].label}");
            if (defs[i].key == property.stringValue) current = i + 1;
        }

        EditorGUI.BeginProperty(position, label, property);
        int picked = EditorGUI.Popup(position, label, current, names);
        if (picked != current) property.stringValue = picked == 0 ? "" : defs[picked - 1].key;
        EditorGUI.EndProperty();
    }
}
