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

		[SerializeField] protected Mesh cubeMesh, sphereMesh;

		public Mesh CubeMesh => cubeMesh;

		public Mesh SphereMesh => sphereMesh;

		public bool AllowExpensiveModes => allowExpensiveModes;

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
			Array.Sort(list, (a, b) => b.Order.CompareTo(b.Order));
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

		private static DecalCameraInstance CreateDecal(DecalInstance parent, DecalCamera camera, int i)
		{
			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var m in modes)
			{
				var obj = m.Create(parent, camera, i);
				if (obj == null) continue;
				return obj;
			}
			return null;
		}

		public virtual DecalCameraInstance[] CreateDecal(DecalInstance parent, DecalCamera camera)
		{
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));
			if (camera == null)
				throw new ArgumentNullException(nameof(camera));

			var list = new List<DecalCameraInstance>();
			if(parent.Object.Renderers != null)
				for (int i = 0, l = parent.Object.Renderers.Count; i < l; i++)
				{
					Profiler.BeginSample("Creating decal for " + parent.Object.Renderers[i]);
					var obj = CreateDecal(parent, camera, i);
					if(obj != null) list.Add(obj);
					Profiler.EndSample();
				}
			if (parent.Object.AllowScreenSpace && list.Count == 0)
			{
				var obj = CreateDecal(parent, camera, -1);
				if (obj != null)
				{
					list.Add(obj);
					obj.ActiveSelf = true;
				}
			}
			return list.ToArray();
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
