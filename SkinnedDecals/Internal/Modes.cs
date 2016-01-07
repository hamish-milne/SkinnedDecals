using UnityEngine;
using System.Linq;

namespace SkinnedDecals.Internal
{
	public abstract class DecalModeBase : DecalMode
	{
		public static bool AddRenderer<TDecal, TRenderer>(DecalInstance parent, TRenderer r)
			where TDecal : DecalCameraInstance, IDecalRendererList<TRenderer>
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

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, int rendererIndex)
		{
			if (!camera.IsDeferred || rendererIndex >= 0 || !parent.Object.AllowScreenSpace)
				return null;
			return new ScreenSpaceInstance(parent, camera);
		}
	}

	[DecalMode]
	public class StaticDeferredMode : DecalMode
	{
		public override int Order => -2;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, int rendererIndex)
		{
			if (!camera.IsDeferred || rendererIndex < 0 ||
				parent.Object.Renderers[rendererIndex] is SkinnedMeshRenderer)
				return null;
			return new DeferredStaticInstance(parent, camera, rendererIndex);
		}
	}

	[DecalMode]
	public class SkinnedDeferredMode : DecalMode
	{
		public override int Order => -1;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, int rendererIndex)
		{
			if (!camera.IsDeferred || rendererIndex < 0)
				return null;
			return new DeferredSkinnedInstance(parent, camera, rendererIndex);
		}
	}

	[DecalMode]
	public class RenderObjectMode : DecalMode
	{
		public override int Order => 0;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, int rendererIndex)
		{
			if (!DecalManager.Current.AllowExpensiveModes || rendererIndex < 0)
				return null;
			return new RenderObjectInstance(parent, camera, rendererIndex);
		}
	}

	[DecalMode]
	public class StaticForwardMode : DecalMode
	{
		public override int Order => 1;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, int rendererIndex)
		{
			if (camera.IsDeferred || rendererIndex < 0 ||
				parent.Object.Renderers[rendererIndex] is SkinnedMeshRenderer)
				return null;
			return new ForwardStaticInstance(parent, camera, rendererIndex);
		}
	}

	[DecalMode]
	public class SkinnedForwardMode : DecalMode
	{
		public override int Order => 2;

		public override DecalCameraInstance Create(DecalInstance parent, DecalCamera camera, int rendererIndex)
		{
			if (camera.IsDeferred || rendererIndex < 0)
				return null;
			return new ForwardSkinnedInstance(parent, camera, rendererIndex);
		}
	}
}
