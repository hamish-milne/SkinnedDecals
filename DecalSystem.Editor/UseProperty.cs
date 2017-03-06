using System;
using System.Collections;
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
		private object obj;

		private static readonly Dictionary<Type, Func<Rect, GUIContent, object, Type, object>> guiMethods =
			new Dictionary<Type, Func<Rect, GUIContent, object, Type, object>>
			{
				{ typeof(bool),    (r, l, v, t) => EditorGUI.Toggle(r, l, (bool)v) },
				{ typeof(string),  (r, l, v, t) => EditorGUI.TextField(r, l, (string)v) },
				{ typeof(int),     (r, l, v, t) => EditorGUI.IntField(r, l, (int)v) },
				{ typeof(float),   (r, l, v, t) => EditorGUI.FloatField(r, l, (float)v) },
				{ typeof(double),  (r, l, v, t) => (double)EditorGUI.FloatField(r, l, (float)(double)v) },
				{ typeof(Vector2), (r, l, v, t) => EditorGUI.Vector2Field(r, l, (Vector2)v) },
				{ typeof(Vector3), (r, l, v, t) => EditorGUI.Vector3Field(r, l, (Vector3)v) },
				{ typeof(UnityEngine.Object), (r, l, v, t) => EditorGUI.ObjectField(r, l, (UnityEngine.Object)v, t, false) },
			};

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var target = property.serializedObject.targetObject;
			if (propInfo == null)
			{
				propInfo =
					// ReSharper disable once PossibleNullReferenceException
					fieldInfo.DeclaringType
						.GetProperties()
						.FirstOrDefault(p => p.Name.Equals(fieldInfo.Name, StringComparison.OrdinalIgnoreCase));
				// ReSharper disable once PossibleNullReferenceException
				if (propInfo != null && !propInfo.DeclaringType.IsInstanceOfType(target))
				{
					// If not a property in the object, assume it's for the instance
					var tokens = property.propertyPath.Split('.');
					var token = tokens[tokens.Length - 2]; // data[N]
					var idx = int.Parse(token.Substring("data[".Length, token.Length - "data[]".Length));
					var list = (IList) target.GetType()
						.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
						.First(f => f.Name == "instances")
						.GetValue(target);
					obj = (DecalInstance) list[idx];
				}
				else
				{
					obj = target;
				}
			}
			if (propInfo == null)
			{
				EditorGUI.HelpBox(position, "Couldn't find property", MessageType.Error);
				return;
			}
			var pt = propInfo.PropertyType;
			if (pt.IsSubclassOf(typeof(UnityEngine.Object)))
				pt = typeof(UnityEngine.Object);
			if (!guiMethods.ContainsKey(pt))
			{
				EditorGUI.HelpBox(position, "Invalid property type " + pt, MessageType.Error);
				return;
			}

			EditorGUI.BeginChangeCheck();
			var newValue = guiMethods[pt](position, label, propInfo.GetValue(obj, null), propInfo.PropertyType);
			if (EditorGUI.EndChangeCheck())
			{
				(target as DecalObject)?.UpdateBackRefs();
				propInfo.SetValue(obj, newValue, null);
				property.serializedObject.Update();
				SceneView.RepaintAll();
			}
		}
	}
}
