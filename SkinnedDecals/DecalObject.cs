using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkinnedDecals
{
	public class DecalObject : MonoBehaviour
	{
		[SerializeField, Tooltip("Enable this for ground objects and flat scenery")]
		protected bool allowScreenSpace;

		[SerializeField, Tooltip("Increase this for larger objects. Set negative for no limit")]
		protected int maxDecalCount = 1;

		protected readonly SortedList<DecalCameraInstance> instances = new SortedList<DecalCameraInstance>();

		public bool AllowScreenSpace => allowScreenSpace;

		public virtual bool IgnoreCulling { get; protected set; }

		public virtual void AddDecal(DecalCameraInstance decal)
		{
			if(decal == null)
				throw new ArgumentNullException(nameof(decal));
			instances.Remove(decal);
			instances.Add(decal);
		}

		public virtual bool RemoveDecal(DecalCameraInstance decal)
		{
			return instances.Remove(decal);
		}

		public int DecalCount => instances.Count;

		public virtual void GetDecalList(IList<DecalCameraInstance> list)
		{
			foreach(var item in instances)
				list.Add(item);
		}

		public DecalCameraInstance[] GetDecalList()
		{
			var list = new List<DecalCameraInstance>(DecalCount);
			GetDecalList(list);
			return list.ToArray();
		}

		public virtual void CullDecals()
		{
			if (maxDecalCount < 0) return;
			while(instances.Count > maxDecalCount)
				instances.RemoveAt(instances.Count - 1);
		}

		public static DecalObject GetDecalParent(Renderer r)
		{
			if (r == null)
				return null;
			var ret = r.GetComponent<DecalObject>();
			// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
			if (ret == null)
				ret = r.GetComponentInParent<DecalObject>();
			return ret;
		}

		protected virtual void OnWillRenderObject()
		{
			DecalManager.Current.GetDecalCamera(Camera.current)?.Render(this);
		}
	}
}
