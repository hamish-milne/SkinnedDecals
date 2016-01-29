using UnityEngine;

namespace SkinnedDecals
{
	[CreateAssetMenu]
	public class DecalTextureSet : ScriptableObject
	{
		[SerializeField] protected Texture2D albedo, normal, roughness, height, emission;
		[SerializeField] protected Color mainColor, emissionColor;

		public virtual Texture2D Albedo => albedo;
		public virtual Texture2D Normal => normal;
		public virtual Texture2D Roughness => roughness;
		public virtual Texture2D Height => height;
		public virtual Texture2D Emission => emission;

		public virtual Color MainColor => mainColor;
		public virtual Color EmissionColor => emissionColor;
	}
}
