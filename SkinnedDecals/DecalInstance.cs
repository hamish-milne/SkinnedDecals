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

		public abstract DecalProjector Projector { get; }

		public abstract DecalObject Object { get; }

		public abstract int Priority { get; }
	}

	public abstract class DecalCameraInstance : DecalInstanceBase, IComparable<DecalCameraInstance>
	{
		public DecalInstance Parent { get; }

		public override DecalProjector Projector => Parent.Projector;

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

		public int CompareTo(DecalCameraInstance other)
		{
			return Priority.CompareTo(other?.Priority ?? 0);
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

	public class DecalInstance : DecalInstanceBase
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

		public override DecalProjector Projector { get; }

		public override DecalObject Object { get; }

		public override int Priority { get; }

		public DecalInstance(DecalProjector projector, DecalObject obj)
		{
			if(projector == null)
				throw new ArgumentNullException(nameof(projector));
			if(obj == null)
				throw new ArgumentNullException(nameof(obj));
			Projector = projector;
			Object = obj;
			Priority = projector.Priority;
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

		public virtual void AddInstance(DecalCameraInstance instance)
		{
			if(instance == null)
				throw new ArgumentNullException(nameof(instance));
			if(instance.Parent != this)
				throw new ArgumentException("Incorrect parent reference");
			if(instances.Exists(obj => obj.Camera == instance.Camera))
				throw new ArgumentException("Camera instance already exists. Try Clear() first");
			instances.Add(instance);
		}

		public List<DecalCameraInstance> Instances { get; } = new List<DecalCameraInstance>();

		protected virtual void CameraAddRemove(DecalCamera camera, bool active)
		{
			if (active)
			{
				var newInstance = DecalManager.Current.CreateDecal(this, camera, null);
				newInstance.Color = Color;
				instances.Add(newInstance);
			}
			else
			{
				var obj = instances.FirstOrDefault(o => o.Camera == camera);
				if (obj == null) return;
				obj.Dispose();
				instances.Remove(obj);
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
	}
}
