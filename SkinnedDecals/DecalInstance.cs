using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SkinnedDecals
{
	public abstract class DecalInstanceBase : IDisposable
	{
		private bool enabled;

		public virtual bool ActiveSelf { get; set; }

		public virtual bool ActiveInScene
		{
			get { return enabled; }
			protected set
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
		
		public abstract DecalTextureSet Decal { get; }

		public abstract DecalObject Object { get; }

		public abstract int Priority { get; }
	}

	public abstract class DecalCameraInstance : DecalInstanceBase
	{
		public DecalInstance Parent { get; }
		
		public override DecalTextureSet Decal => Parent.Decal;

		public override DecalObject Object => Parent.Object;

		public override int Priority => Parent.Priority;

		public DecalCamera Camera { get; }

		protected DecalCameraInstance(DecalInstance parent, DecalCamera camera)
		{
			if(parent == null)
				throw new ArgumentNullException(nameof(parent));
			if(camera == null)
				throw new ArgumentNullException(nameof(camera));
			Parent = parent;
			Camera = camera;
		}

		public override bool ActiveInScene
		{
			get { return base.ActiveInScene && Parent.ActiveInScene; }
			protected set { base.ActiveInScene = value; }
		}

		public override bool ActiveSelf
		{
			get { return base.ActiveSelf; }
			set
			{
				base.ActiveSelf = value;
				ActiveInScene = value && Parent.ActiveInScene;
			}
		}

		public virtual void OnPreRender() { }
	}

	[Serializable]
	public class DecalInstance : DecalInstanceBase
	{
		[Serializable]
		protected class RendererData
		{
			public Matrix4x4 objectToProjector;
			public Vector3[] uvData;
		}

		[SerializeField]
		protected DecalTextureSet decal;
		[SerializeField]
		protected Matrix4x4 objectToProjector;
		[SerializeField]
		protected List<RendererData> rendererData = new List<RendererData>();

		public override DecalTextureSet Decal => decal;

		public virtual Matrix4x4 ObjectToProjector
		{
			get { return objectToProjector; }
			set { objectToProjector = value; }
		}

		RendererData GetData(int index, bool add = false)
		{
			if (index < 0)
				return null;
			if (add)
			{
				while (index >= rendererData.Count)
					rendererData.Add(null);
				return (rendererData[index] ?? (rendererData[index] = new RendererData()));
			}
			else
			{
				return index >= rendererData.Count ? null : rendererData[index];
			}
		}

		public virtual Matrix4x4 GetProjectionMatrix(int index)
		{
			return GetData(index)?.objectToProjector ?? objectToProjector;
		}

		public virtual void SetProjectionMatrix(int index, Matrix4x4 matrix)
		{
			GetData(index, true).objectToProjector = matrix;
		}

		public virtual Vector3[] GetUvData(int index)
		{
			return GetData(index)?.uvData;
		}

		public virtual void SetUvData(int index, Vector3[] uvData)
		{
			GetData(index, true).uvData = uvData;
		}

		public override bool ActiveSelf
		{
			get
			{
				return base.ActiveSelf;
			}
			set
			{
				base.ActiveSelf = value;
				ActiveInScene = value;
			}
		}

		private void RefreshChildren()
		{
			foreach (var c in instances)
				c.ActiveSelf = c.ActiveSelf;
		}

		public override DecalObject Object { get; }

		public override int Priority { get; }

		public DecalInstance()
		{
			Instances = instances.AsReadOnly();
		}

		public DecalInstance(int priority, DecalTextureSet decal, DecalObject obj) : this()
		{
			if(decal == null)
				throw new ArgumentNullException(nameof(decal));
			if(obj == null)
				throw new ArgumentNullException(nameof(obj));
			this.decal = decal;
			Object = obj;
			Priority = priority;
		}

		private readonly List<DecalCameraInstance> instances
			= new List<DecalCameraInstance>();

		public virtual DecalCameraInstance GetInstance(DecalCamera camera)
		{
			if(camera == null)
				throw new ArgumentNullException(nameof(camera));
			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach(var o in instances)
				if (o.Camera == camera)
					return o;
			return null;
		}

		public IList<DecalCameraInstance> Instances { get; }

		protected virtual void CameraAddRemove(DecalCamera camera, bool active)
		{
			if (active)
			{
				foreach(var obj in DecalManager.Current.CreateDecal(this, camera))
				{
					obj.Color = Color;
					instances.Add(obj);
					camera.Instances.Add(obj);
				}
			}
			else
			{
				foreach(var obj in instances.Where(o => o.Camera == camera).ToArray())
				{
					obj.Dispose();
					instances.Remove(obj);
					camera.Instances.Remove(obj);
				}
			}
		}

		protected override void Enable()
		{
			DecalCamera.AddRemove += CameraAddRemove;
			RefreshChildren();
		}

		protected override void Disable()
		{
			DecalCamera.AddRemove -= CameraAddRemove;
			RefreshChildren();
		}

		public void Clear()
		{
			foreach (var o in instances)
			{
				o?.Dispose();
			}
			instances.Clear();
		}

		public override void Dispose()
		{
			Clear();
		}
		
		public virtual void Reload()
		{
			Clear();
			// ReSharper disable once ForCanBeConvertedToForeach
			for (int i = 0; i < DecalCamera.ActiveCameras.Count; i++)
			{
				CameraAddRemove(DecalCamera.ActiveCameras[i], true);
			}
		}

		protected Color color;

		public override Color Color
		{
			get { return color; }
			set
			{
				color = value;
				foreach (var o in instances)
					o.Color = value;
			}
		}
	}
	
	public interface IDecalRendererList<in T> where T : Renderer
	{
		void AddRenderer(T renderer);
	}
}
