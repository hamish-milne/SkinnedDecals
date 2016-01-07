using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkinnedDecals
{
	public enum SkinMode
	{
		None,
		SkinnedRenderersOnly,
		All,
	}

	public abstract class DecalObject : MonoBehaviour
	{
		[SerializeField, Tooltip("Increase this for larger objects. Set negative for no limit")]
		protected int maxDecalCount = 8;

		[Serializable]
		protected class DecalList : SortedList<DecalInstance>
		{
			public DecalList() : base((x, y) => x.Priority.CompareTo(y?.Priority ?? 0))
			{
			}
		}

		[SerializeField]
		protected DecalList instances = new DecalList();
		
		private static readonly List<DecalObject> active = new List<DecalObject>();
		
		public static IList<DecalObject> Active { get; }
		
		static DecalObject()
		{
			Active = active.AsReadOnly();
		}

		public virtual bool AllowScreenSpace => false;

		public virtual SkinMode SkinMode => SkinMode.None;

		public virtual List<Renderer> Renderers => null;

		public virtual Bounds Bounds => default(Bounds);

		public virtual void AddDecal(DecalInstance decal)
		{
			if(decal == null)
				throw new ArgumentNullException(nameof(decal));
			instances.Remove(decal);
			instances.Add(decal);
		}

		public virtual bool RemoveDecal(DecalInstance decal)
		{
			return instances.Remove(decal);
		}

		public int DecalCount => instances.Count;

		public virtual void GetDecalList(IList<DecalInstance> list)
		{
			foreach(var item in instances)
				list.Add(item);
		}

		public DecalInstance[] GetDecalList()
		{
			var list = new List<DecalInstance>(DecalCount);
			GetDecalList(list);
			return list.ToArray();
		}

		public virtual void CullDecals()
		{
			if (maxDecalCount < 0) return;
			while(instances.Count > maxDecalCount)
				instances.RemoveAt(instances.Count - 1);
		}
		
		public virtual void Reload()
		{
			foreach(var obj in instances)
				obj.Reload();
		}

		protected virtual void OnEnable()
		{
			active.Add(this);
		}

		protected virtual void OnDisable()
		{
			while(active.Contains(this))
				active.Remove(this);
		}

		protected virtual void OnDestroy()
		{
			foreach (var obj in instances)
			{
				obj?.Dispose();
			}
		}
	}

	public class DecalObjectRendered : DecalObject
	{
		[SerializeField,
			Tooltip("The renderers on which decals will be drawn")]
		protected List<Renderer> renderers = new List<Renderer>();

		[SerializeField, Tooltip("When to use skinned decals")]
		protected SkinMode skinMode = SkinMode.SkinnedRenderersOnly;

		public override List<Renderer> Renderers => renderers;

		public override SkinMode SkinMode => skinMode;

		protected virtual void OnWillRenderObject()
		{
			DecalManager.Current.GetDecalCamera(Camera.current).Render(this);
		}
	}

	public class DecalObjectScreenSpace : DecalObject
	{
		[SerializeField] protected Bounds bounds;

		public override Bounds Bounds => bounds;

		public override bool AllowScreenSpace => true;
	}
}
