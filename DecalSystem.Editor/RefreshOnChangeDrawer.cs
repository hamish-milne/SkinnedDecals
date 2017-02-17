using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DecalSystem.Editor
{
	[CustomPropertyDrawer(typeof(RefreshOnChangeAttribute))]
	public class RefreshOnChangeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (EditorGUI.PropertyField(position, property, label))
			{
				var dobj = property.serializedObject.targetObjects.OfType<DecalObject>().ToList();
				DecalObject.RefreshAll(((RefreshOnChangeAttribute)attribute).RefreshAction, o => dobj.Contains(o));
			}
		}
	}

	/// <summary>
	/// This lets us use GUILayout methods in a PropertyDrawer. I have no idea how or why this works.
	/// </summary>
	[CustomEditor(typeof(MonoBehaviour), true)]
	public class DummyEditor : UnityEditor.Editor { }

	[CustomPropertyDrawer(typeof(Matrix4x4))]
	public class MatrixDrawer : PropertyDrawer
	{
		private static void Decompose(Matrix4x4 m, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
		{
			translation = m * new Vector4(0, 0, 0, 1);
			rotation = Quaternion.LookRotation(m * Vector3.forward, m * Vector3.up);
			scale = new Vector3((m*Vector3.right).magnitude, (m*Vector3.up).magnitude, (m*Vector3.forward).magnitude);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return 0f;
		}
		
		private static readonly KeyValuePair<string, FieldInfo>[] matrixFields =
			typeof(Matrix4x4).GetFields()
			.Select(f => new KeyValuePair<string, FieldInfo>(f.Name.Replace('m', 'e'), f))
			.ToArray();
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (!(property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label))) return;
			var m = (object)default(Matrix4x4);
			foreach(var pair in matrixFields)
				pair.Value.SetValue(m, property.FindPropertyRelative(pair.Key).floatValue);
			Vector3 mPos, mScale;
			Quaternion mRotation;
			Decompose((Matrix4x4)m, out mPos, out mRotation, out mScale);
			EditorGUI.BeginChangeCheck();
			EditorGUI.indentLevel++;
			mPos = EditorGUILayout.Vector3Field("Position", mPos);
			mRotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Rotation", mRotation.eulerAngles));
			mScale = EditorGUILayout.Vector3Field("Scale", mScale);
			EditorGUI.indentLevel--;
			if (EditorGUI.EndChangeCheck())
			{
				m = Matrix4x4.TRS(mPos, mRotation, mScale);
				foreach (var pair in matrixFields)
					property.FindPropertyRelative(pair.Key).floatValue = (float) pair.Value.GetValue(m);
			}
		}
	}
}
