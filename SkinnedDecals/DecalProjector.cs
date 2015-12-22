using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkinnedDecals
{
	public abstract class DecalInstance : IDisposable
	{
		private bool enabled;

		public bool Enabled
		{
			get { return enabled; }
			set
			{
				if (value && !enabled)
					Enable();
				else if (!value && enabled)
					Disable();
				enabled = value;
			}
		}


		protected abstract void Enable();

		protected abstract void Disable();

		public abstract void Dispose();

		public abstract Color Color { get; set; }
	}

	public class DecalProjector : MonoBehaviour
	{
		[SerializeField] protected Texture2D decalTex;
		[SerializeField] protected DecalTextureSet decal;
		[SerializeField] protected new Camera camera;
		[SerializeField] protected Transform testPoint;
		[SerializeField] protected DecalManager manager;
		private Renderer current;

		private readonly List<DecalInstance> instances = new List<DecalInstance>();

		public DecalTextureSet Decal => decal;

		public void Project()
		{
			decal = ScriptableObject.CreateInstance<DecalTextureSet>();
			decal.albedo = decalTex;

			var thisBounds = new Bounds(transform.position, transform.lossyScale);
			var renderers = FindObjectsOfType<Renderer>().Where(r => r.bounds.Intersects(thisBounds));
			current = renderers.FirstOrDefault();

			if (current != null)
			{
				var instance = manager.CreateDecal(camera, this, current);
				Debug.Log(instance);
				if (instance != null)
				{
					instance.Enabled = true;
					instances.Add(instance);
				}
			}
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
			if (Input.GetKeyDown(KeyCode.A))
				Project();
		}
	}
}
