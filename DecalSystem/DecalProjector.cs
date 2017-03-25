using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// The method for filtering a set by another set
	/// </summary>
	public enum FilterMode
	{
		/// <summary>
		/// None; all objects are accepted
		/// </summary>
		None,

		/// <summary>
		/// Only objects in the list are valid
		/// </summary>
		Whitelist,

		/// <summary>
		/// All objects valid, except those in the list
		/// </summary>
		Blacklist,
	}

	/// <summary>
	/// Creates dynamic and baked decal instances
	/// </summary>
	public class DecalProjector : MonoBehaviour
	{
		[SerializeField] protected DecalMaterial decal;

		[SerializeField] protected FilterMode objectFilterMode;
		[SerializeField] protected List<DecalObject> objectFilter = new List<DecalObject>();
		[SerializeField] protected FilterMode materialFilterMode;
		[SerializeField] protected List<Material> materialFilter = new List<Material>();

		[SerializeField] protected float maxNormal;
		[SerializeField] protected float bakedNormalInterpolate = 0.5f;

		/// <summary>
		/// The decal material to paint
		/// </summary>
		public virtual DecalMaterial DecalMaterial
		{
			get { return decal; }
			set { decal = value; }
		}

		/// <summary>
		/// Decides how to filter by object
		/// </summary>
		public virtual FilterMode ObjectFilterMode
		{
			get { return objectFilterMode; }
			set { objectFilterMode = value; }
		}

		/// <summary>
		/// Decides how to filter by material
		/// </summary>
		public virtual FilterMode MaterialFilterMode
		{
			get { return materialFilterMode; }
			set { materialFilterMode = value; }
		}

		/// <summary>
		/// Filters the target mesh vertices by the dot product of the normal and the projection axis.
		/// More simply, <c>0</c> means the decal only draws on front-facing polygons, and <c>1</c> will
		/// draw on all vertices.
		/// </summary>
		public virtual float MaxNormal
		{
			get { return maxNormal; }
			set { maxNormal = value; }
		}

		/// <summary>
		/// Mixes the normals of the target with the projection direction. <c>0</c> means the decal normals will
		/// match the geometry, <c>1</c> means they point towards the projector. Setting to less than <c>0</c> will
		/// exactly preserve normals and tangents, which may break the parallax effect.
		/// </summary>
		public virtual float? NormalInterpolate
		{
			get { return bakedNormalInterpolate < 0 ? default(float?) : bakedNormalInterpolate; }
			set { bakedNormalInterpolate = value ?? -1f; }
		}

		/// <summary>
		/// Filters objects to project on based on <c>ObjectFilterMode</c>
		/// </summary>
		public virtual List<DecalObject> ObjectFilter => objectFilter;

		/// <summary>
		/// Filters submeshes by material based on <c>MaterialFilterMode</c>
		/// </summary>
		public virtual List<Material> MaterialFilter => materialFilter;

		/// <summary>
		/// Gets the bounds to project on
		/// </summary>
		/// <returns></returns>
		protected virtual Bounds GetBounds()
		{
			return Util.UnitBounds(transform);
		}

		/// <summary>
		/// Checks whether an object should be included based on a given list and filter mode
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list"></param>
		/// <param name="filterMode"></param>
		/// <param name="obj"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Projects dynamic decals
		/// </summary>
		/// <param name="instances"></param>
		public void Project(out DecalInstance[] instances)
		{
			var list = new List<DecalInstance>();
			Project(list);
			instances = list.ToArray();
		}

		/// <summary>
		/// Projects dynamic decals, adding the created instances to the list
		/// </summary>
		/// <param name="instances"></param>
		/// <returns>The number of instances created</returns>
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
					FilterList(ObjectFilter, ObjectFilterMode, obj)) continue;
				if (obj.Renderer == null)
				{
					var o = obj.AddDecal(transform, decal, -1, MaxNormal);
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
						if (FilterList(MaterialFilter, MaterialFilterMode, m)) continue;
						var o = obj.AddDecal(transform, decal, i, MaxNormal);
						if (o == null) continue;
						Debug.Log($"Adding decal to {obj}");
						instances?.Add(o);
						count++;
					}
				}
			}
			return count;
		}

		/// <summary>
		/// Projects static decals
		/// </summary>
		/// <param name="instances"></param>
		public void ProjectBaked(out Renderer[] instances)
		{
			var list = new List<Renderer>();
			ProjectBaked(list);
			instances = list.ToArray();
		}

		/// <summary>
		/// Projects static decals, adding the created renderers to the given list
		/// </summary>
		/// <param name="instances"></param>
		/// <returns>The number of instances created</returns>
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
					FilterList(ObjectFilter, ObjectFilterMode, obj)) continue;
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
					if (FilterList(MaterialFilter, MaterialFilterMode, m))
						submeshMask |= (1 << i);
				}
				mesh = ProjectionUtility.GetMesh(mesh, obj.transform, transform, obj.Mesh, submeshMask, NormalInterpolate, MaxNormal);
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

		/// <summary>
		/// Draws a unit cube gizmo around the given transform
		/// </summary>
		/// <param name="transform"></param>
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
