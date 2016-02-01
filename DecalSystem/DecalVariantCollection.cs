using System.Collections.Generic;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Used to force the inclusion of decal material variants
	/// </summary>
	public class DecalVariantCollection : ScriptableObject
	{
		[SerializeField]
		protected List<Material> materials = new List<Material>();

		public List<Material> Materials => materials;
	}
}
