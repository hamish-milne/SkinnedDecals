using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SkinnedDecals
{
	public abstract class DecalMode
	{
		public virtual int Order => 0;

		public abstract DecalInstance Create(DecalProjector projector, Camera camera, Renderer renderer);

	}
}
