using System;
using UnityEngine;

namespace SkinnedDecals
{
	[AttributeUsage(AttributeTargets.Class)]
	public class DecalModeAttribute : Attribute
	{
	}

	public abstract class DecalMode
	{
		public virtual int Order => 0;

		public abstract DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, Renderer renderer);
	}
}
