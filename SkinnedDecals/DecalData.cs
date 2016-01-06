using UnityEngine;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

namespace SkinnedDecals
{
	public class DecalData : ScriptableObject
	{
		// TODO: OnChange event?
		
		[Serializable]
		protected class RendererData
		{
			public Matrix4x4 objectToProjector;
			public byte[] uvData;
			[NonSerialized] public Vector3[] cachedData;
			
			public Vector3[] GetUvData()
			{
				if(cachedData != null)
					return (Vector3[])cachedData.Clone();
				var list = new List<Vector3>(uvData.Length / (sizeof(float) * 3));
				using(var mem = new MemoryStream(uvData))
				using(var stream = new DeflateStream(mem, CompressionMode.Decompress))
				using(var reader = new BinaryReader(stream))
				while(stream.Position < stream.Length)
					list.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
				return list.ToArray();	
			}
			
			public virtual void SetUvData(Vector3[] uvData)
			{
				using(var mem = new MemoryStream())
				{
					using(var stream = new DeflateStream(mem, CompressionMode.Compress))
					using(var writer = new BinaryWriter(stream))
					foreach(var vector in uvData)
					{
						writer.Write(vector.x);
						writer.Write(vector.y);
						writer.Write(vector.z);
					}
					this.uvData = mem.GetBuffer();
				}
				cachedData = (Vector3[])uvData.Clone();
			}
		}
		
		[SerializeField] protected DecalTextureSet decal;
		[SerializeField] protected Matrix4x4 objectToProjector;
		[SerializeField] protected List<RendererData> rendererData = new List<RendererData>();
		
		public virtual DecalTextureSet Decal 
		{
			get { return decal; }
			set { decal = value; }
		}
		
		public virtual Matrix4x4 ObjectToProjector
		{
			get { return objectToProjector; }
			set { objectToProjector = value; }
		}
		
		RendererData GetData(int index, bool add = false)
		{
			if(index < 0)
				return null;
			if(add)
			{
				while(index >= rendererData.Count)
					rendererData.Add(null);
				return rendererData[index];
			}
			else
			{
				return index >= rendererData.Count ? null : rendererData[index];
			}
		}
		
		public virtual Matrix4x4 GetProjectionMatrix(int index)
		{
			return GetData(index)?.objectToProjector ?? objectToProjector;
		}
		
		public virtual void SetProjectionMatrix(int index, Matrix4x4 matrix)
		{
			GetData(index, true).objectToProjector = matrix;
		}
		
		public virtual Vector3[] GetUvData(int index)
		{
			return GetData(index)?.GetUvData();
		}
		
		public virtual void SetUvData(int index, Vector3[] uvData)
		{
			GetData(index, true).SetUvData(uvData);
		}
	}
}
