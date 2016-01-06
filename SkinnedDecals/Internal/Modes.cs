
using UnityEngine;
using System.Linq;

namespace SkinnedDecals.Internal
{
	public abstract class DecalModeBase : DecalMode
	{
		public static bool AddRenderer<TDecal, TRenderer>(DecalInstance parent, TRenderer r)
			where TDecal : DecalCameraInstance, DecalRendererList<TRenderer>
			where TRenderer : Renderer
		{
			var obj = parent.Instances.OfType<TDecal>().FirstOrDefault();
			if(obj == null)
				return false;
			obj.AddRenderer(r);
			return true;
		}
	}
	
	[DecalMode]
	public class ScreenSpaceMode : DecalMode
	{
		public override int Order => -3;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, Renderer renderer)
		{
			if (!camera.IsDeferred || renderer is SkinnedMeshRenderer || !parent.Object.AllowScreenSpace)
				return null;
			return new ScreenSpaceInstance(parent, camera, renderer, DecalManager.Current.CubeMesh);
		}
	}

	[DecalMode]
	public class StaticDeferredMode : DecalMode
	{
		public override int Order => -2;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, Renderer renderer)
		{
			if (!camera.IsDeferred || renderer is SkinnedMeshRenderer)
				return null;
			return new DeferredStaticInstance(parent, camera, renderer);
		}
	}

	[DecalMode]
	public class SkinnedDeferredMode : DecalMode
	{
		public override int Order => -1;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, Renderer renderer)
		{
			var smr = renderer as SkinnedMeshRenderer;
			if (!camera.IsDeferred || smr == null)
				return null;
			return new DeferredSkinnedInstance(parent, camera, smr);
		}
	}

	/*[DecalMode]
	public class RenderObjectMode : DecalMode
	{

	}*/

	[DecalMode]
	public class StaticForwardMode : DecalMode
	{
		public override int Order => 1;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, Renderer renderer)
		{
			if (camera.IsDeferred || renderer is SkinnedMeshRenderer)
				return null;
			return new ForwardStaticInstance(parent, camera, renderer);
		}
	}

	[DecalMode]
	public class SkinnedForwardMode : DecalMode
	{
		public override int Order => 2;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, Renderer renderer)
		{
			var smr = renderer as SkinnedMeshRenderer;
			if (camera.IsDeferred || smr == null)
				return null;
			return new ForwardSkinnedInstance(parent, camera, smr);
		}
	}
}
