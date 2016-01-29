using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace DecalSystem.Editor
{
	[CustomEditor(typeof(DecalMaterial), true), CanEditMultipleObjects]
	public class DecalMaterialEditor : UnityEditor.Editor
	{
		public Material[] materials;
		public MaterialEditor materialEditor;

		public override void OnInspectorGUI()
		{
			SetupMaterialEditor();
			materialEditor.serializedObject.Update();
			if (materialEditor.PropertiesGUI())
			{
				// Get the value directly, to save LOC switching through each property type
				var mValue = typeof (MaterialProperty).GetField("m_Value", BindingFlags.Instance | BindingFlags.NonPublic);
				foreach (var p in MaterialEditor.GetMaterialProperties(materialEditor.targets))
				{
					// Don't update mixed-value properties
					if (p.hasMixedValue) continue;
					// Find the name of the field that matches the property..
					var fieldName = targets
						.Cast<DecalMaterial>()
						.Select(obj => obj.GetFieldForProperty(p.name))
						.FirstOrDefault(s => s != null);
					// If it doesn't match, doesn't matter.
					if(fieldName == null) continue;
					var prop = serializedObject.FindProperty(fieldName);
					if(prop == null) continue;
					// If m_Value doesn't exist, we're really boned, so..
					// ReSharper disable once PossibleNullReferenceException
					var value = mValue.GetValue(p);
					// Now switch through each property type anyway, because this is the only way to do it
					switch (prop.propertyType)
					{
						case SerializedPropertyType.Float:
							prop.floatValue = (float) value;
							break;
						case SerializedPropertyType.Vector4:
							prop.vector4Value = (Vector4) value;
 							break;
						case SerializedPropertyType.Color:
							prop.colorValue = (Color) value;
							break;
						case SerializedPropertyType.ObjectReference:
							prop.objectReferenceValue = (Object) value;
							break;
					}
				}
				serializedObject.ApplyModifiedProperties();
			}
		}

		public override bool HasPreviewGUI()
		{
			return true;
		}

		// This handily obscured function will draw a preview GUI using an editor and targets we provide.
		// This is important because we need to draw the preview using the *Material* editor, not this one.
		// Otherwise all the materials will look the same.
		private static MethodInfo drawPreview;

		// Create preview materials and an associated editor
		void SetupMaterialEditor()
		{
			if (materials == null)
			{
				var objs = targets.Cast<DecalMaterial>().ToArray();
				materials = objs
					.Select(m => Instantiate(m.GetMaterial("")))
					.ToArray();
				for (int i = 0; i < objs.Length; i++)
					objs[i].CopyTo(materials[i]);
			}
			if (materials != null && materialEditor == null)
				// ReSharper disable once CoVariantArrayConversion
				materialEditor = (MaterialEditor)CreateEditor(materials);
			if (drawPreview == null)
				drawPreview = typeof (ObjectPreview).GetMethod("DrawPreview", BindingFlags.Static | BindingFlags.NonPublic);
		}

		// Make sure that multi-editing DecalMaterials draws each material separately..
		public override void DrawPreview(Rect previewArea)
		{
			SetupMaterialEditor();
			// .. but make sure the names are correct
			for (int i = 0; i < materials.Length; i++)
				materials[i].name = targets[i].name;
			drawPreview.Invoke(null, new object[] {materialEditor, previewArea, materialEditor.targets});
		}

		public override void OnPreviewSettings()
		{
			SetupMaterialEditor();
			materialEditor?.DefaultPreviewSettingsGUI();
		}
	}
}
