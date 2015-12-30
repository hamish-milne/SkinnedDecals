using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkinnedDecals
{


	public class DecalProjector : MonoBehaviour
	{
		[SerializeField] protected Texture2D decalTex;
		[SerializeField] protected DecalTextureSet decal;
		[SerializeField] protected new Camera camera;
		[SerializeField] protected Transform testPoint;
		[SerializeField] protected DecalManager manager;
		[SerializeField] protected int priority;
		private Renderer current;

		private readonly List<DecalCameraInstance> instances = new List<DecalCameraInstance>();

		public DecalTextureSet Decal => decal;

		public int Priority => priority;

		public void Project()
		{
			decal = ScriptableObject.CreateInstance<DecalTextureSet>();
			//decal.albedo = decalTex;

			var thisBounds = new Bounds(transform.position, transform.lossyScale);
			var renderers = FindObjectsOfType<Renderer>().Where(r => r.bounds.Intersects(thisBounds));
			current = renderers.FirstOrDefault();

			/*if (current != null)
			{
				var instance = manager.CreateDecal(camera, this, current);
				Debug.Log(instance);
				if (instance != null)
				{
					instance.Enabled = true;
					instances.Add(instance);
				}
			}*/
		}

		protected virtual void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.color = new Color(0, 1, 1, 0.3f);
			Gizmos.DrawCube(Vector3.zero, Vector3.one);
		}

		protected virtual void OnDestroy()
		{
			foreach(var o in instances)
				o?.Dispose();
		}

		// TEST
		protected virtual void Update()
		{
			//foreach(var o in instances)
			//	o?.Update();
			if (Input.GetKeyDown(KeyCode.A))
				Project();
		}
	}
}
