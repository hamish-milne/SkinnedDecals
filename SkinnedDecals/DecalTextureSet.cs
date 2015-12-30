using UnityEngine;

namespace SkinnedDecals
{
	public class DecalTextureSet : ScriptableObject
	{
		[SerializeField] protected Texture2D albedo, normal, roughness, height, emission;

		public virtual Texture2D Albedo => albedo;
		public virtual Texture2D Normal => normal;
		public virtual Texture2D Roughness => roughness;
		public virtual Texture2D Height => height;
		public virtual Texture2D Emission => emission;
	}
}
