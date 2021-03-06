﻿using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DecalSystem.Editor
{
	[CustomEditor(typeof(DecalMaterial), true), CanEditMultipleObjects]
	public class DecalMaterialEditor : UnityEditor.Editor
	{
		[NonSerialized] private Material[] materials;
		[NonSerialized] private MaterialEditor materialEditor;

		public override void OnInspectorGUI()
		{
			SetupMaterialEditor();
			if (materialEditor == null)
			{
				EditorGUILayout.HelpBox("Unable to get material instances", MessageType.Error);
				return;
			}
			materialEditor.serializedObject.Update();
			if (materialEditor.PropertiesGUI())
			{
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
					switch (p.type)
					{
						case MaterialProperty.PropType.Float:
						case MaterialProperty.PropType.Range:
							if (prop.propertyType == SerializedPropertyType.Integer)
								prop.intValue = (int) p.floatValue;
							else
								prop.floatValue = p.floatValue;
							break;
						case MaterialProperty.PropType.Vector:
							prop.vector4Value = p.vectorValue;
 							break;
						case MaterialProperty.PropType.Color:
							prop.colorValue = p.colorValue;
							break;
						case MaterialProperty.PropType.Texture:
							prop.objectReferenceValue = p.textureValue;
							break;
					}
				}
				serializedObject.ApplyModifiedProperties();
				// Update the keywords, which might need to be changed now
				for (int i = 0; i < targets.Length; i++)
				{
					var o = ((DecalMaterial)targets[i]);
					o.Refresh();
					o.SetKeywords(materials[i]);
				}
			}
		}

		protected virtual void OnEnable()
		{
		}

		protected virtual void OnDisable()
		{
			Clear();
		}

		protected virtual void Clear()
		{
			DestroyImmediate(materialEditor);
			if(materials != null)
				foreach (var m in materials)
					DestroyImmediate(m);
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
			try
			{
				if (materials == null)
				{
					var objs = targets.Cast<DecalMaterial>().ToArray();
					materials = objs
						.Select(m => m.GetMaterial(""))
						.ToArray();
					// In theory, GetMaterial can return null at any point
					materials = materials.Any(m => m == null)
						? null
						: materials.Select(Instantiate).ToArray();
				}
				if (materials != null && materialEditor == null)
					// ReSharper disable once CoVariantArrayConversion
					materialEditor = (MaterialEditor) CreateEditor(materials);
				if (drawPreview == null)
					drawPreview = typeof (ObjectPreview).GetMethod("DrawPreview", BindingFlags.Static | BindingFlags.NonPublic);
			}
			catch
			{
				// ignored
			}
		}

		// Make sure that multi-editing DecalMaterials draws each material separately..
		public override void DrawPreview(Rect previewArea)
		{
			SetupMaterialEditor();
			// .. but make sure the names are correct
			if (materialEditor != null)
			{
				for (int i = 0; i < materials.Length; i++)
					materials[i].name = targets[i].name;
				drawPreview.Invoke(null, new object[] {materialEditor, previewArea, materialEditor.targets});
			}
		}

		public override void OnPreviewSettings()
		{
			SetupMaterialEditor();
			materialEditor?.DefaultPreviewSettingsGUI();
		}

		public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
		{
			SetupMaterialEditor();
			return materialEditor?.RenderStaticPreview(assetPath, subAssets, width, height);
		}
	}
}
