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
				DecalObject.RefreshAll(o => dobj.Contains(o));
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
			translation = m.GetColumn(3);
			rotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
			// TODO: Negative scale
			scale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return (property.isExpanded ? 4 : 1)*(EditorGUIUtility.singleLineHeight + 1f);
		}

		private static Rect TakeLine(Rect position, ref float y)
		{
			var line = EditorGUIUtility.singleLineHeight;
			var ret = new Rect(position.x, position.y + y, position.width, line);
			y += line + 2f;
			return ret;
		}

		private static readonly KeyValuePair<string, FieldInfo>[] matrixFields =
			typeof(Matrix4x4).GetFields()
			.Select(f => new KeyValuePair<string, FieldInfo>(f.Name.Replace('m', 'e'), f))
			.ToArray();
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			float y = 0;
			if (!(property.isExpanded = EditorGUI.Foldout(TakeLine(position, ref y), property.isExpanded, label))) return;
			var m = (object)default(Matrix4x4);
			foreach(var pair in matrixFields)
				pair.Value.SetValue(m, property.FindPropertyRelative(pair.Key).floatValue);
			Vector3 mPos, mScale;
			Quaternion mRotation;
			Decompose((Matrix4x4)m, out mPos, out mRotation, out mScale);
			EditorGUI.BeginChangeCheck();
			EditorGUI.indentLevel++;
			mPos = EditorGUI.Vector3Field(TakeLine(position, ref y), "Position", mPos);
			mRotation = Quaternion.Euler(EditorGUI.Vector3Field(TakeLine(position, ref y), "Rotation", mRotation.eulerAngles));
			mScale = EditorGUI.Vector3Field(TakeLine(position, ref y), "Scale", mScale);
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
