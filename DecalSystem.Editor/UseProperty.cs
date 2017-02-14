using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DecalSystem.Editor
{
	[CustomPropertyDrawer(typeof(UsePropertyAttribute))]
	public class UsePropertyDrawer : PropertyDrawer
	{
		private PropertyInfo propInfo;

		private static readonly Dictionary<Type, Func<Rect, GUIContent, object, object>> guiMethods =
			new Dictionary<Type, Func<Rect, GUIContent, object, object>>
			{
				{ typeof(bool),    (r, l, v) => EditorGUI.Toggle(r, l, (bool)v) },
				{ typeof(string),  (r, l, v) => EditorGUI.TextField(r, l, (string)v) },
				{ typeof(int),     (r, l, v) => EditorGUI.IntField(r, l, (int)v) },
				{ typeof(float),   (r, l, v) => EditorGUI.FloatField(r, l, (float)v) },
				{ typeof(double),  (r, l, v) => (double)EditorGUI.FloatField(r, l, (float)(double)v) },
				{ typeof(Vector2), (r, l, v) => EditorGUI.Vector2Field(r, l, (Vector2)v) },
				{ typeof(Vector3), (r, l, v) => EditorGUI.Vector3Field(r, l, (Vector3)v) },
			};

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var target = property.serializedObject.targetObject;
			if (propInfo == null)
			{
				propInfo =
					target.GetType()
						.GetProperties()
						.FirstOrDefault(p => p.Name.Equals(fieldInfo.Name, StringComparison.OrdinalIgnoreCase));
			}
			if (propInfo == null)
			{
				EditorGUI.HelpBox(position, "Couldn't find property", MessageType.Error);
				return;
			}
			if (!guiMethods.ContainsKey(propInfo.PropertyType))
			{
				EditorGUI.HelpBox(position, "Invalid property type", MessageType.Error);
				return;
			}
			propInfo.SetValue(target, guiMethods[propInfo.PropertyType](position, label, propInfo.GetValue(target, null)), null);
		}
	}
}
