using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkinnedDecals
{
	public class DecalCamera : MonoBehaviour
	{
		public event Action<DecalCamera> PreRender, PostRender;

		public static event Action<DecalCamera, bool> AddRemove;

		private static readonly List<DecalCamera> activeCameras = new List<DecalCamera>();
		public static IList<DecalCamera> ActiveCameras { get; }

		public Camera Camera => GetComponent<Camera>();

		public virtual bool IsDeferred => Camera.actualRenderingPath == RenderingPath.DeferredShading;

		private readonly HashSet<DecalObject> renderSet = new HashSet<DecalObject>();
		private readonly List<DecalObject> renderList = new List<DecalObject>();

		public List<DecalCameraInstance> Instances { get; } = new List<DecalCameraInstance>();

		static DecalCamera()
		{
			ActiveCameras = activeCameras.AsReadOnly();
		}

		protected virtual void OnPreRender()
		{
			// ReSharper disable once LoopCanBePartlyConvertedToQuery
			foreach(var obj in Instances)
				if (obj != null)
					obj.ActiveSelf = renderSet.Contains(obj.Object);

			PreRender?.Invoke(this);
		}

		protected virtual void OnPostRender()
		{
			renderSet.Clear();
			renderList.Clear();

			PostRender?.Invoke(this);
		}

		protected virtual void OnEnable()
		{
			activeCameras.Add(this);
			AddRemove?.Invoke(this, true);
		}

		protected virtual void OnDisable()
		{
			activeCameras.Remove(this);
			AddRemove?.Invoke(this, false);
		}

		public void Render(DecalObject obj)
		{
			if(obj == null)
				throw new ArgumentNullException(nameof(obj));
			if(renderSet.Add(obj))
				renderList.Add(obj);
		}
	}
}
