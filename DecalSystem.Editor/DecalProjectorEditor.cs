using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DecalSystem.Editor
{
	public static class AssetUtility
	{
		public const string Root = "Assets/IronDecal Assets/";

		public static string GetUniquePath(string path, string ext)
		{
			int i = 0;
			string fullPath;
			do
			{
				fullPath = path + (i == 0 ? "" : $" ({i})") + ext;
			} while (File.Exists(fullPath));
			return fullPath;
		}

		public static void SaveBakedRederer(string pname, DecalMaterial material, Renderer r)
		{
			var assetDir = AssetDatabase.GetAssetPath(material);
			assetDir = string.IsNullOrEmpty(assetDir) ?
				"Assets" : Path.GetDirectoryName(assetDir);
			assetDir += "/";
			foreach (var m in r.sharedMaterials)
				if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
					AssetDatabase.CreateAsset(m, assetDir + m.name + ".mat");
			// Saving the mesh is not necessary if the decal exists only in the scene, but if
			// moved to a prefab it would otherwise break
			var mesh = (r as SkinnedMeshRenderer)?.sharedMesh ?? r.GetComponent<MeshFilter>().sharedMesh;
			if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
				AssetDatabase.CreateAsset(mesh, assetDir +
					pname + "_" + r.transform.parent?.name + "_" + material.name + ".asset");
		}
	}

	[CustomEditor(typeof(DecalProjector))]
	public class DecalProjectorEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			var projector = (DecalProjector)target;

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Project", GUILayout.Width(120f), GUILayout.Height(30f)))
			{
				projector.Project();
				ModifySceneToForceUpdate();
				Resources.FindObjectsOfTypeAll<SceneView>().FirstOrDefault()?.Repaint();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Bake", GUILayout.Width(120f), GUILayout.Height(30f)))
			{
				projector.ProjectBaked(out var renderers);
				// Create assets for default materials if needed
				foreach (var r in renderers)
					AssetUtility.SaveBakedRederer(projector.name, projector.DecalMaterial, r);
				Selection.activeObject = renderers.FirstOrDefault() ?? target;
				EditorUtility.SetDirty(projector.DecalMaterial);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		
		// Unity only 'updates' the scene view when something within the scene changes
		// This would be fine, but reloading scripts will remove command buffers and drawmesh calls
		// And reloading scripts doesn't count as changing the scene.
		// Ultimately the effect is that the decals temporarily vanish when scripts are changed. Not a good look.
		// So we need to manually make some small modification and then undo it..
		// .. but not on the first frame of course. How silly. The *second* frame.

		[InitializeOnLoadMethod]
		protected static void RepaintOnRecompile()
		{
			frameCount = 0;
			EditorApplication.update += ModifySceneMethod;
		}

		private static int frameCount;

		private static void ModifySceneMethod()
		{
			if (frameCount >= 1)
			{
				ModifySceneToForceUpdate();
				// ReSharper disable once DelegateSubtraction
				EditorApplication.update -= ModifySceneMethod;
			}
			frameCount++;
		}

		public static void ModifySceneToForceUpdate()
		{
			var go = new GameObject();
			DestroyImmediate(go);
		}

		[MenuItem("GameObject/Add Decal Object", false, 0)]
		protected static void AddDecalObject()
		{
			foreach (var obj in Selection.gameObjects)
				DecalObject.GetOrCreate(obj);
		}
	}
}
