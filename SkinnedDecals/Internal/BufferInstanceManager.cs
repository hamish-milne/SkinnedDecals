using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SkinnedDecals.Internal
{


	public class BufferInstanceManager
	{
		[Serializable]
		protected class Channel : DecalChannel
		{
			public Material decal;
			public Vector2[] buffer;
		}

		public static bool IsSupported => Shader.Find("Decal/SM4")?.isSupported == true;

		[SerializeField] protected int rendererIndex;

		[SerializeField]
		protected List<Channel> channels = new List<Channel>();

		//protected 

		public virtual DecalChannel AddToChannel(DecalInstance instance)
		{
			var data = instance.GetUvData(rendererIndex);
			Channel validChannel = null;
			foreach (var c in channels)
			{
				validChannel = c;
				for (int i = 0; i < data.Length; i++)
				{
					if (float.IsInfinity(c.buffer[i].x) || float.IsInfinity(data[i].x))
						continue;
					validChannel = null;
					break;
				}
				if (validChannel != null)
					break;
			}
			if (validChannel != null)
			{
				for (int i = 0; i < data.Length; i++)
				{
					var d = data[i];
					if (float.IsInfinity(d.x))
						continue;
					validChannel.buffer[i] = d;
				}
			}
			else
			{
				validChannel = new Channel {buffer = (Vector2[]) data.Clone()};
				channels.Add(validChannel);
			}
			return validChannel;
		}
	}
}
