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
		
		public abstract DecalData Decal { get; }

		public abstract DecalObject Object { get; }

		public abstract int Priority { get; }
	}

	public abstract class DecalCameraInstance : DecalInstanceBase
	{
		public DecalInstance Parent { get; }
		
		public override DecalData Decal => Parent.Decal;

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
	}

	public class DecalInstance : DecalInstanceBase, IComparable<DecalCameraInstance>
	{
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

		public override DecalData Decal { get; }

		public override DecalObject Object { get; }

		public override int Priority { get; }

		public DecalInstance(int priority, DecalData decal, DecalObject obj)
		{
			if(decal == null)
				throw new ArgumentNullException(nameof(decal));
			if(obj == null)
				throw new ArgumentNullException(nameof(obj));
			Decal = decal;
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

		public List<DecalCameraInstance> Instances { get; } = new List<DecalCameraInstance>();

		protected virtual void CameraAddRemove(DecalCamera camera, bool active)
		{
			if (active)
			{
				foreach(var obj in DecalManager.Current.CreateDecal(this, camera))
				{
					obj.Color = Color;
					instances.Add(obj);
				}
			}
			else
			{
				foreach(var obj in instances.Where(o => o.Camera == camera).ToArray())
				{
					obj.Dispose();
					instances.Remove(obj);
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
			foreach (var o in Instances)
				o?.Dispose();
			Instances.Clear();
		}

		public override void Dispose()
		{
			Clear();
		}
		
		public virtual void Reload()
		{
			Clear();
			foreach(var camera in DecalCamera.ActiveCameras)
				CameraAddRemove(camera, true);
		}

		protected Color color;

		public override Color Color
		{
			get { return color; }
			set
			{
				color = value;
				foreach (var o in Instances)
					o.Color = value;
			}
		}
		
		public int CompareTo(DecalCameraInstance other)
		{
			return Priority.CompareTo(other?.Priority ?? 0);
		}
	}
	
	public interface DecalRendererList<T> where T : Renderer
	{
		void AddRenderer(T renderer);
	}
}
