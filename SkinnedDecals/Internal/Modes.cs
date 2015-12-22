
using System;
using UnityEngine;

namespace SkinnedDecals.Internal
{
	[DecalMode]
	public class ScreenSpaceMode : DecalMode
	{
		public override int Order => -3;

		public override DecalInstance Create(DecalProjector projector, Camera camera, Renderer renderer)
		{
			//if (camera.actualRenderingPath != RenderingPath.DeferredShading || renderer is SkinnedMeshRenderer)
				// AND allow screen space for DecalObject...
			//	return null;
			return new ScreenSpaceInstance(camera, projector, renderer, DecalManager.GetCubeMesh());
		}
	}

	[DecalMode]
	public class StaticDeferredMode : DecalMode
	{
		public override int Order => -2;

		public override DecalInstance Create(DecalProjector projector, Camera camera, Renderer renderer)
		{
			if (camera.actualRenderingPath != RenderingPath.DeferredShading || renderer is SkinnedMeshRenderer)
				return null;
			return new DeferredStaticInstance(camera, renderer, projector);
		}
	}

	[DecalMode]
	public class StaticForwardMode : DecalMode
	{
		public override int Order => -1;

		public override DecalInstance Create(DecalProjector projector, Camera camera, Renderer renderer)
		{
			if (renderer is SkinnedMeshRenderer)
				return null;
			return new ForwardStaticInstance(camera, renderer, projector);
		}
	}

	public abstract class SkinnedMode : DecalMode
	{
		

		
	}

	/*[DecalMode]
	public class SkinnedDeferredMode : SkinnedMode
	{

	}

	[DecalMode]
	public class SkinnedForwardMode : SkinnedMode
	{

	}

	[DecalMode]
	public class RenderObjectMode : DecalMode
	{

	}*/
}
