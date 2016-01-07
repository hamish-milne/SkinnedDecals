using System.Collections.Generic;
using UnityEngine;

namespace SkinnedDecals
{
	public enum ListMode
	{
		None,
		Whitelist,
		Blacklist
	}

	public class DecalProjector : MonoBehaviour
	{
		[SerializeField] protected DecalTextureSet decal;
		[SerializeField] protected Color color;
		[SerializeField] protected int priority;
		[SerializeField] protected ListMode materialListMode, objectListMode;
		[SerializeField] protected List<Material> materialList = new List<Material>();
		[SerializeField] protected List<DecalObject> objectList = new List<DecalObject>();

		public DecalTextureSet Decal 
		{
			get { return decal; }
			set { decal = value; }
		}

		public int Priority
		{
			get { return priority; }
			set { priority = value; }
		}

		private Mesh skinMesh;

		public List<Material> MaterialList => materialList;

		private Mesh GetMesh(Renderer renderer)
		{
			if (renderer is MeshRenderer)
			{
				return renderer.GetComponent<MeshFilter>()?.sharedMesh;
			}
			var smr = renderer as SkinnedMeshRenderer;
			if (smr != null)
			{
				if(skinMesh == null)
					skinMesh = new Mesh();
				else
					skinMesh.Clear();
				smr.BakeMesh(skinMesh);
				return skinMesh;
			}
			return null;
		}

		public DecalInstance Project(DecalObject obj)
		{
			Profiler.BeginSample("Decal projection");
			var thisBounds = new Bounds(transform.position, transform.lossyScale);
			var renderers = obj.Renderers == null ? null : new Renderer[obj.Renderers.Count];
			bool isValid = obj.AllowScreenSpace;
			if(renderers != null)
				for(int i = 0; i < renderers.Length; i++)
				{
					var r = obj.Renderers[i];
					if(r != null && r.bounds.Intersects(thisBounds))
					{
						renderers[i] = r;
						isValid = true;
					}
				}
			if(!isValid)
				return null;
			var data = new DecalInstance(Priority, Decal, obj)
			{
				Color = color,
				ActiveSelf = true,
				ObjectToProjector = transform.worldToLocalMatrix * obj.transform.localToWorldMatrix,
			};
			if (renderers != null)
			{
				for (int i = 0; i < renderers.Length; i++)
				{
					var r = renderers[i];
					if (r == null)
						continue;
					var projector = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
					data.SetProjectionMatrix(i, projector);
					if (obj.SkinMode != SkinMode.All &&
						(obj.SkinMode != SkinMode.SkinnedRenderersOnly || !(r is SkinnedMeshRenderer)))
						continue;
					Profiler.BeginSample("Baking mesh");
					var mesh = GetMesh(r);
					Profiler.EndSample();
					Profiler.BeginSample("Transforming vertices");
					var verts = mesh.vertices;
					ProjectionUtility.TransformVerts(verts, r.transform, transform);
					var uvData = new Vector3[mesh.vertexCount];
					Profiler.EndSample();
					Profiler.BeginSample("Intersecting triangles");
					if (materialListMode == ListMode.None)
					{
						ProjectionUtility.Project(mesh.triangles, verts, uvData);
					}
					else
					{
						var mats = r.sharedMaterials;
						var first = true;
						isValid = false;
						for (int j = 0; j < mats.Length; j++)
						{
							var m = mats[j];
							if (!materialList.CheckList(m, materialListMode))
								continue;
							ProjectionUtility.Project(mesh.GetTriangles(j), verts, uvData, first);
							isValid = true;
							first = false;
						}
					}
					Profiler.EndSample();
					if (!isValid)
						continue;
					data.SetUvData(i, uvData);
				}
			}
			Profiler.EndSample();
			obj.AddDecal(data);
			Profiler.BeginSample("Creating camera instances");
			data.Reload();
			Profiler.EndSample();
			return data;
		}

		public void Project()
		{
			// ReSharper disable once ForCanBeConvertedToForeach
			for (int i = 0; i < DecalObject.Active.Count; i++)
			{
				var obj = DecalObject.Active[i];
				if (!objectList.CheckList(obj, objectListMode))
					continue;
				if(obj.Renderers != null || new Bounds(transform.position, transform.lossyScale).Intersects(obj.Bounds))
					Project(obj);
			}
			//System.GC.Collect(0);
		}

		protected virtual void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.color = new Color(0, 1, 1, 0.3f);
			Gizmos.DrawCube(Vector3.zero, Vector3.one);
		}

		// TEST
		protected virtual void Update()
		{
			//foreach(var o in instances)
			//	o?.Update();
			if (Input.GetKeyDown(KeyCode.A))
				Project();
		}
	}
}
