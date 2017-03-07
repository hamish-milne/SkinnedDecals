using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DecalSystem.Editor
{
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
				Renderer[] renderers;
				projector.ProjectBaked(out renderers);
				// Create assets for default materials if needed
				// TODO: Put these in a specific folder: IronDecal Assets?
				var assetDir = AssetDatabase.GetAssetPath(projector.DecalMaterial);
				assetDir = string.IsNullOrEmpty(assetDir) ?
					"Assets" : Path.GetDirectoryName(assetDir);
				assetDir += "/";
				foreach (var r in renderers)
				{
					foreach(var m in r.sharedMaterials)
						if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
							AssetDatabase.CreateAsset(m, assetDir + m.name + ".mat");
					var mesh = (r as SkinnedMeshRenderer)?.sharedMesh ?? r.GetComponent<MeshFilter>().sharedMesh;
					if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
						AssetDatabase.CreateAsset(mesh, assetDir +
							projector.name + "_" + r.name + "_" + projector.DecalMaterial.name + ".asset");
				}
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
