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
	
	public class DecalObject : MonoBehaviour
	{
		[SerializeField, Tooltip("Enable this for ground objects and flat scenery")]
		protected bool allowScreenSpace;
		
		[SerializeField, Tooltip("When to use skinned decals")]
		protected SkinMode skinMode;

		[SerializeField, Tooltip("Increase this for larger objects. Set negative for no limit")]
		protected int maxDecalCount = 1;

		[SerializeField,
			Tooltip("The renderers on which decals will be drawn. Does not apply to screen space decals")]
		protected List<Renderer> renderers = new List<Renderer>();

		protected readonly SortedList<DecalInstance> instances = new SortedList<DecalInstance>();
		
		private static readonly List<DecalObject> active = new List<DecalObject>();
		
		public static IList<DecalObject> Active { get; }
		
		static DecalObject()
		{
			Active = active.AsReadOnly();
		}

		public bool AllowScreenSpace => allowScreenSpace;

		public SkinMode SkinMode => skinMode;

		public virtual bool AlwaysDraw => !HasRenderers;
		
		public virtual bool HasRenderers => renderers.Count > 0;
		
		public List<Renderer> Renderers => renderers;

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

		protected virtual void OnWillRenderObject()
		{
			DecalManager.Current.GetDecalCamera(Camera.current)?.Render(this);
		}
	}
}
