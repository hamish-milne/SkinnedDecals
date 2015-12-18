using System;
using UnityEngine;

namespace SkinnedDecals
{
	public abstract class DecalManager : MonoBehaviour
	{
		
	}

	public class DefaultDecalManager : MonoBehaviour
	{
		[Serializable]
		protected class ShaderReplacement
		{
			public Shader existingShader;
			public string requiredKeywords;
			public Shader decalShader;
			public string channelKeyword;
			public string channelTexture;
			public int uvChannel;
		}

		[SerializeField] protected ShaderReplacement[] shaderReplacements;


	}
}
