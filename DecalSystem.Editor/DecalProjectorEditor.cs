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
			if (GUILayout.Button("Bake", GUILayout.Height(30f)))
			{
				Renderer[] renderers;
				((DecalProjector)target).ProjectBaked(out renderers);
				// TODO: Use decal cached materials
				foreach (var m in renderers.SelectMany(r => r.sharedMaterials))
					AssetDatabase.CreateAsset(m, "Assets/Decals/" + m.name.Replace(':', '_') + ".mat");
			}
		}
	}
}
