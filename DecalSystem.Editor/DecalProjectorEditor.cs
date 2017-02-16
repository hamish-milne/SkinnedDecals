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
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Bake", GUILayout.Width(120f), GUILayout.Height(30f)))
			{
				Renderer[] renderers;
				projector.ProjectBaked(out renderers);
				// Create assets for default materials if needed
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

		[InitializeOnLoadMethod]
		protected static void AddEditorUpdate()
		{
			int repaintOnDisable = 0;
			EditorApplication.update += () =>
			{
				if (DecalManager.Current == null)
				{
					// Repaint 2 frames after decals are disabled
					if (repaintOnDisable > 0)
					{
						SceneView.RepaintAll();
						repaintOnDisable--;
					}
				}
				else
				{
					DecalManager.Current.RepaintIfRequired();
					repaintOnDisable = 2;
				}
			};
		}
	}
}
