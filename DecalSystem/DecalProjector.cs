﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecalSystem
{
	public enum FilterMode
	{
		None,
		Whitelist,
		Blacklist,
	}


	public class DecalProjector : MonoBehaviour
	{
		[SerializeField] protected DecalMaterial decal;

		[SerializeField] protected FilterMode objectFilterMode;
		[SerializeField] protected List<DecalObject> objectFilter = new List<DecalObject>();
		[SerializeField] protected FilterMode materialFilterMode;
		[SerializeField] protected List<Material> materialFilter = new List<Material>();

		public virtual DecalMaterial DecalMaterial
		{
			get { return decal; }
			set { decal = value; }
		}

		public virtual FilterMode ObjectFilterMode
		{
			get { return objectFilterMode; }
			set { objectFilterMode = value; }
		}
		public virtual FilterMode MaterialFilterMode
		{
			get { return materialFilterMode; }
			set { materialFilterMode = value; }
		}

		public virtual List<DecalObject> ObjectFilter => objectFilter;
		public virtual List<Material> MaterialFilter => materialFilter;

		protected virtual Bounds GetBounds()
		{
			return Util.UnitBounds(transform);
		}

		protected static bool FilterList<T>(List<T> list, FilterMode filterMode, T obj)
		{
			switch (filterMode)
			{
				case FilterMode.Whitelist:
					return !list.Contains(obj);
				case FilterMode.Blacklist:
					return list.Contains(obj);
				default:
					return false;
			}
		}

		public void Project(out DecalInstance[] instances)
		{
			var list = new List<DecalInstance>();
			Project(list);
			instances = list.ToArray();
		}

		public virtual int Project(IList<DecalInstance> instances = null)
		{
			if (decal == null)
				throw new InvalidOperationException("No decal material");
			var allObjects = DecalObject.ActiveObjects;
			var count = 0;
			var projectorBounds = GetBounds();
			foreach (var obj in allObjects)
			{
				if (!obj.CanAddDecals) continue;
				if (!projectorBounds.Intersects(obj.Bounds) ||
					FilterList(objectFilter, objectFilterMode, obj)) continue;
				if (obj.Renderer == null)
				{
					var o = obj.AddDecal(transform, decal, -1);
					if (o == null) continue;
					Debug.Log($"Adding decal to {obj}");
					instances?.Add(o);
					count++;
				}
				else
				{
					var mats = obj.Materials;
					for (int i = 0; i < obj.Mesh.subMeshCount; i++)
					{
						var m = i < mats.Length ? mats[i] : null;
						if (FilterList(materialFilter, materialFilterMode, m)) continue;
						var o = obj.AddDecal(transform, decal, i);
						if (o == null) continue;
						Debug.Log($"Adding decal to {obj}");
						instances?.Add(o);
						count++;
					}
				}
			}
			return count;
		}

		public void ProjectBaked(out Renderer[] instances)
		{
			var list = new List<Renderer>();
			ProjectBaked(list);
			instances = list.ToArray();
		}

		public virtual int ProjectBaked(IList<Renderer> instances = null)
		{
			if(decal == null)
				throw new InvalidOperationException("No decal material");
			var allObjects = Application.isPlaying ? DecalObject.ActiveObjects
				: FindObjectsOfType<DecalObject>();
			var count = 0;
			var projectorBounds = GetBounds();
			foreach (var obj in allObjects)
			{
				if (!obj.CanAddDecals) continue;
				if (!projectorBounds.Intersects(obj.Bounds) ||
					FilterList(objectFilter, objectFilterMode, obj)) continue;
				if (obj.Renderer == null)
				{
					Debug.LogWarning("Cannot bake decal on " + obj, this);
					continue;
				}
				var mat = decal.GetMaterial("");
				if (mat == null)
					throw new InvalidOperationException("No default mode for decal material");
				decal.CopyTo(mat);

				var mesh = obj.GetCurrentMesh();
				var submeshMask = 0;
				var mats = obj.Materials;
				for (int i = 0; i < obj.Mesh.subMeshCount; i++)
				{
					var m = i < mats.Length ? mats[i] : null;
					if (FilterList(materialFilter, materialFilterMode, m))
						submeshMask |= (1 << i);
				}
				mesh = ProjectionUtility.GetMesh(mesh, obj.transform, transform, obj.Mesh, submeshMask, true);
				mesh.name = obj.name + ":" + decal.name;
				var newObj = new GameObject(decal.name);
				newObj.transform.SetParent(obj.transform, false);
				var newRenderer = obj.CreateRenderer(newObj, mesh);
				newRenderer.sharedMaterial = mat;
				instances?.Add(newRenderer);
				count++;
			}
			return count;
		}

		protected virtual void OnDrawGizmosSelected()
		{
			DrawGizmo(transform);
		}

		public static void DrawGizmo(Transform transform)
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.color = new Color(0, 1, 1, 0.5f);
			Gizmos.DrawCube(Vector3.zero, Vector3.one);
		}
	}
}
