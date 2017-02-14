using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace DecalSystem.Editor
{
	public static class DecalVariantCollector
	{
		private class Variant : IEquatable<Variant>
		{
			private readonly Shader shader;
			private readonly string[] keywords;

			public Variant(Shader shader, List<string> keywords)
			{
				if(shader == null)
					throw new ArgumentNullException(nameof(shader));
				if(keywords == null)
					throw new ArgumentNullException(nameof(keywords));
				this.shader = shader;
				this.keywords = keywords.ToArray();
				Array.Sort(this.keywords);
			}

			public override int GetHashCode()
			{
				var hc = keywords.Length;
				hc ^= shader.GetHashCode();
				for(int i = 0; i < keywords.Length; i++)
				{
					var kw = keywords[i];
					hc ^= i*kw?.GetHashCode() ?? (1 << i);
				}
				return hc;
			}

			public bool Equals(Variant other)
			{
				if (other == null ||
					other.shader != shader ||
					other.keywords.Length != keywords.Length)
					return false;
				// ReSharper disable once LoopCanBeConvertedToQuery
				for(int i = 0; i < keywords.Length; i++)
					if (other.keywords[i] != keywords[i])
						return false;
				return true;
			}

			public Material GetMaterial()
			{
				var mat = new Material(shader);
				foreach(var kw in keywords)
					mat.EnableKeyword(kw);
				return mat;
			}

			public override bool Equals(object obj)
			{
				return ReferenceEquals(this, obj) || Equals(obj as Variant);
			}
		}

		[MenuItem("Assets/Collect DecalSystem variants")]
		public static void CreateAndCollect()
		{
			var obj = UnityEngine.Object.FindObjectOfType<DecalVariantCollection>();
			if(obj == null)
				obj = ScriptableObject.CreateInstance<DecalVariantCollection>();
			AssetDatabase.CreateAsset(obj, "Assets/Collector.asset");
			Collect(obj);
			Selection.activeObject = obj;
		}

		public static void Collect(DecalVariantCollection collection)
		{
			// Get the scenes included in the build
			var scenes =
				EditorBuildSettings.scenes
				.Where(s => s.enabled)
				.Select(s => AssetDatabase.LoadAssetAtPath(s.path, typeof(SceneAsset)))
				.ToArray();

			// Get the 'preloaded assets' as set in the player settings
			var preloadedAssetsProp = new SerializedObject(
				Resources.FindObjectsOfTypeAll<PlayerSettings>().First())
				.FindProperty("preloadedAssets");
			var preloadedAssets = Enumerable.Range(0, preloadedAssetsProp.arraySize)
				.Select(i => preloadedAssetsProp.GetArrayElementAtIndex(i).objectReferenceValue)
				.ToArray();

			// Concatenate them together, and get everything they depend on.
			// This is a list of *all* assets that will be included in a build.
			var rootAssets = scenes
				.Concat(preloadedAssets)
				.Where(o => o != null && o != collection)
				.ToArray();
			var deps = EditorUtility.CollectDependencies(rootAssets);	

			var decalMaterials = deps.OfType<DecalMaterial>().ToArray();
			Debug.Log(decalMaterials.Length);
			// Get the list of mode keywords based on the DecalObjects included
			var includedModes = deps.OfType<DecalObject>().Concat(
				EditorBuildSettings.scenes
					.Where(s => s.enabled)
					.Select(s => SceneManager.GetSceneByPath(s.path))
					.SelectMany(s => s.GetRootGameObjects())
					.SelectMany(go => go.GetComponentsInChildren<DecalObject>(true))
				).SelectMany(obj => obj.RequiredModes)
				.Distinct()
				.ToArray();
			Debug.Log(includedModes.Length);

			// Get all the required materials and modes to get the collection of shader variants.
			// Create 'dummy' materials to force Unity to include them, then
			var kwList = new List<string>();
			var variants =
				decalMaterials
					.SelectMany(dm => includedModes
						.Select(mode =>
						{
							kwList.Clear();
							kwList.Add(mode);
							dm.SetKeywords(kwList.Add, kw => kwList.Remove(kw));
							return new Variant(dm.GetShaderForMode(mode), kwList);
						})
					)
				.Distinct()
				.Select(v => v.GetMaterial())
				.ToArray();
			foreach(var m in variants)
				AssetDatabase.AddObjectToAsset(m, collection);
			collection.Materials.Clear();
			foreach(var mat in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(collection)))
				if(mat != collection)
					UnityEngine.Object.DestroyImmediate(mat, true);
			collection.Materials.AddRange(variants);
			EditorUtility.SetDirty(collection);
			if (!preloadedAssets.Contains(collection))
			{
				preloadedAssetsProp.arraySize++;
				preloadedAssetsProp.GetArrayElementAtIndex
					(preloadedAssetsProp.arraySize - 1).objectReferenceValue = collection;
			}
		}
	}
}
