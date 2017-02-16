using System.Linq;
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

	public class MatrixDrawer : PropertyDrawer
	{
		
	}
}
