using UnityEngine;

namespace SkinnedDecals
{
	public static class Utility
	{
		public static void SetTextureKeyword(this Material material, string property, string keyword, Texture texture)
		{
			material.SetTexture(property, texture);
			if(texture)
				material.EnableKeyword(keyword);
			else
				material.DisableKeyword(keyword);
		}

		public static Mesh GetMesh(this Renderer renderer)
		{
			if (renderer is MeshRenderer)
			{
				return renderer.GetComponent<MeshFilter>()?.sharedMesh;
			}
			var smr = renderer as SkinnedMeshRenderer;
			if (smr != null)
			{
				var mesh = new Mesh();
				smr.BakeMesh(mesh);
				return mesh;
			}
			return null;
		}

		public static T GetOrAdd<T>(this Component obj) where T : Component
		{
			var ret = obj.GetComponent<T>();
			// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
			if (ret == null)
				ret = obj.gameObject.AddComponent<T>();
			return ret;
		}

		public static T GetOrAddInParent<T>(this Component obj) where T : Component
		{
			var ret = obj.GetComponentInParent<T>();
			// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
			if (ret == null)
				ret = obj.gameObject.AddComponent<T>();
			return ret;
		}
	}
}
