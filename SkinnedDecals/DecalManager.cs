using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkinnedDecals
{
	public class DecalManager : MonoBehaviour
	{
		private static DecalMode[] modes;

		[SerializeField]
		protected bool allowExpensiveModes;

		[SerializeField]
		protected List<Camera> cameras = new List<Camera>();

		[SerializeField] protected Mesh cubeMesh, sphereMesh;

		public Mesh CubeMesh => cubeMesh;

		public Mesh SphereMesh => sphereMesh;

		public bool AllowExpensiveModes => allowExpensiveModes;

		public List<Camera> Cameras => cameras;

		public static DecalManager Current { get; private set; }

		protected virtual void OnEnable()
		{
			Current = this;
		}

		protected virtual void OnDisable()
		{
			if (Current == this)
				Current = null;
		}
		
		protected static void RebuildModeList()
		{
			var list = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => a.GetTypes())
				.Where(t => t.IsSubclassOf(typeof(DecalMode)))
				.Where(t => Attribute.IsDefined(t, typeof(DecalModeAttribute)))
				.Select(t => (DecalMode)Activator.CreateInstance(t))
				.ToArray();
			Array.Sort(list, (a, b) => a.Order.CompareTo(b.Order));
			modes = list;
		}

		static DecalManager()
		{
			try
			{
				RebuildModeList();
			} catch(Exception e)
			{
				Debug.LogException(e);
				Debug.LogError("Error when building decal mode list.");
			}
		}

		public virtual DecalCameraInstance[] CreateDecal(DecalInstance parent, DecalCamera camera)
		{
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));
			if (camera == null)
				throw new ArgumentNullException(nameof(camera));
			var arr = parent.Object.Renderers
				.Select(r => modes
					.Select(m => m.Create(parent, camera, r))
					.FirstOrDefault(i => i != null)
				).Where(i => i != null).ToArray();
			return arr;
		}

		public virtual DecalObject GetDecalObject(Renderer renderer)
		{
			var obj = renderer.GetOrAddInParent<DecalObject>();
			return obj.Renderers.Contains(renderer) ? obj : null;
		}

		public virtual DecalCamera GetDecalCamera(Camera camera)
		{
			return camera.GetOrAdd<DecalCamera>();
		}
	}
}
