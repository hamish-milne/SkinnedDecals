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

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Project", GUILayout.Width(120f), GUILayout.Height(30f)))
			{
				((DecalProjector)target).Project();
			}
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Bake", GUILayout.Width(120f), GUILayout.Height(30f)))
			{
				var projector = (DecalProjector) target;
				Renderer[] renderers;
				projector.ProjectBaked(out renderers);
				// Create assets for default materials if needed
				var assetDir = AssetDatabase.GetAssetPath(projector.DecalMaterial);
				assetDir = string.IsNullOrEmpty(assetDir) ?
					"Assets/" : Path.GetDirectoryName(assetDir);
				foreach (var m in renderers
					.SelectMany(r => r.sharedMaterials)
					.Where(m => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))))
					AssetDatabase.CreateAsset(m, assetDir + "/" + m.name + ".mat");
				Selection.activeObject = renderers.FirstOrDefault() ?? target;
				EditorUtility.SetDirty(projector.DecalMaterial);
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}
	}
}
