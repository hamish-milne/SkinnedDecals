using UnityEngine;

namespace SkinnedDecals
{
	public class DecalTextureSet : ScriptableObject
	{
		[SerializeField] public Texture2D albedo, normal, roughness;

		public virtual void GetTextures(out Texture2D albedo, out Texture2D normal, out Texture2D roughness)
		{
			albedo = this.albedo;
			normal = this.normal;
			roughness = this.roughness;
		}
	}
}
