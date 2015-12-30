using UnityEditor;
using UnityEngine;

namespace SkinnedDecals.Editor
{
	[CustomEditor(typeof(DecalObject), true)]
    public class DecalObjectEditor : UnityEditor.Editor
    {
	    public override void OnInspectorGUI()
	    {
		    base.OnInspectorGUI();
		    var obj = (DecalObject) target;

			EditorGUILayout.Space();
			
	    }
    }
}
