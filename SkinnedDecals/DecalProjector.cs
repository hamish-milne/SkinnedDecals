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
		
		public List<Material> MaterialList => materialList;

		public DecalData Project(DecalObject obj)
		{
			var thisBounds = new Bounds(transform.position, transform.lossyScale);
			var renderers = new Renderer[obj.Renderers.Count];
			bool isValid = false;
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
			var data = ScriptableObject.CreateInstance<DecalData>();
			data.Decal = Decal;
			data.ObjectToProjector = obj.transform.localToWorldMatrix * transform.localToWorldMatrix;
			for(int i = 0; i < renderers.Length; i++)
			{
				var r = renderers[i];
				if (r == null)
					continue;
				var projector = r.transform.localToWorldMatrix * transform.worldToLocalMatrix;
				data.SetProjectionMatrix(i, projector);
				if(obj.SkinMode == SkinMode.All ||
					(obj.SkinMode == SkinMode.SkinnedRenderersOnly && r is SkinnedMeshRenderer))
				{
					var mesh = r.GetMesh();
					var verts = mesh.vertices;
					var uvData = new Vector3[verts.Length];
					int[] tris;
					if(materialListMode == ListMode.None)
					{
						tris = mesh.triangles;
						ProjectionUtility.BakeUvData(tris, verts, projector, uvData);
					} else
					{
						var mats = r.sharedMaterials;
						bool first = true;
						isValid = false;
						for(int j = 0; j < mats.Length; j++)
						{
							var m = mats[j];
							if(!materialList.CheckList(m, materialListMode))
								continue;
							ProjectionUtility.BakeUvData(mesh.GetTriangles(j), verts,
								projector, uvData, first);
							isValid = true;
							first = false;
						}
						if(!isValid)
							continue;
						data.SetUvData(i, uvData);
					}
				}
			}
			return data;
		}

		public void Project()
		{
			foreach(var obj in DecalObject.Active)
			{
				if(!objectList.CheckList(obj, objectListMode))
					continue;
				var newData = Project(obj);
				if(newData == null)
					continue;
				var instance = new DecalInstance(Priority, newData, obj);
				instance.Reload();
				obj.AddDecal(instance);
			}
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
