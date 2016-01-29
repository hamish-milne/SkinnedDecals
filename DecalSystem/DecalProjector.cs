using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	public class DecalProjector : MonoBehaviour
	{
		[SerializeField] protected DecalMaterial decal;
		[SerializeField] protected bool bakeDecal;

		private static readonly Vector3[] vectors =
		{
			new Vector3( 1,  0,  0),
			new Vector3(-1,  0,  0),
			new Vector3( 0,  1,  0),
			new Vector3( 0, -1,  0),
			new Vector3( 0,  0,  1),
			new Vector3( 0,  0, -1),
		};

		protected virtual void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
			Gizmos.color = new Color(0, 1, 1, 0.3f);
			Gizmos.DrawCube(Vector3.zero, Vector3.one);
		}

		public virtual void Project()
		{
			var allObjects = DecalObject.ActiveObjects;
			if (allObjects.Count == 0)
			{
				Debug.LogWarning("No DecalObjects in projection box", this);
				return;
			}
			var points = vectors.Select(v => transform.TransformPoint(v)).ToArray();
			foreach (var obj in allObjects)
			{
				if (points.Any(p => obj.Bounds.Contains(p)))
				{
					if (bakeDecal)
						BakeDecal(obj);
					else
						obj.AddDecal(transform, decal, obj.ScreenSpace ? -1 : 0);
				}
			}
		}

		// TODO: Material/object filtering
		protected void BakeDecal(DecalObject obj)
		{
			var mesh = obj.ScreenSpace ? null : obj.Mesh;
			if (mesh == null)
			{
				Debug.LogWarning("Unable to bake decal for " + obj, this);
				return;
			}
			var uvData = new Vector2[mesh.vertexCount];
			var verts = mesh.vertices;
			ProjectionUtility.TransformVerts(verts, obj.transform, transform);
			ProjectionUtility.Project(mesh.triangles, verts, uvData);
			mesh = MeshUtility.GetMesh(mesh, uvData, 0);
			var newObj = new GameObject(decal.name);
			newObj.transform.SetParent(obj.transform, false);
			var newRenderer = obj.CreateRenderer(newObj, mesh);
			var mat = decal.GetMaterial("");
			decal.CopyTo(mat);
			newRenderer.sharedMaterial = mat;
		}

		void Update()
		{
			if (Input.GetKeyDown(KeyCode.A))
			{
				Project();
				DecalManager.Current.ClearData();
			}
		}
	}
}
