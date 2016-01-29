using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Renders decals on a fixed <c>MeshRenderer</c>
	/// </summary>
	[RequireComponent(typeof(MeshRenderer))]
	public class DecalFixedObject : DecalObjectBase
	{
		public MeshRenderer Renderer { get; private set; }

		public override Bounds Bounds => Renderer.bounds;

		private Mesh mesh;

		public override Mesh Mesh => mesh;

		public override bool ScreenSpace => false;

		protected virtual void Awake()
		{
			Renderer = GetComponent<MeshRenderer>();
			mesh = GetComponent<MeshFilter>().sharedMesh;
		}

		[Serializable]
		protected class FixedChannel : DecalInstance
		{
			[SerializeField] protected DecalFixedObject obj;
			[SerializeField] protected Material material;
			[SerializeField] protected int submesh;
			protected MaterialPropertyBlock properties;
			[SerializeField] protected List<Matrix4x4> matrices = new List<Matrix4x4>(8);

			public override DecalObject DecalObject => obj;

			public override bool HasMultipleDecals => matrices.Count > 1;

			public bool TryAddMatrix(DecalObjectBase obj, DecalMaterial decalMaterial, int submesh, Matrix4x4 matrix)
			{
				if (DecalMaterial != decalMaterial || this.submesh != submesh || matrices.Count >= 8)
					return false;
				matrices.Add(matrix);
				return true;
			}

			public bool RefreshMatrices()
			{
				if(properties == null)
					properties = new MaterialPropertyBlock();
				else
					properties.Clear();
				var ret = UpdateMaterial();
				if (matrices.Count == 1)
				{
					properties.SetMatrix("_Projector", matrices[0]);
				}
				else
				{
					var dummyMatrix = Matrix4x4.TRS(Vector3.one*float.NegativeInfinity, Quaternion.identity, Vector3.one);
					var shaderPropertyCount = matrices.Count > 4 ? 8 : 4;
					for (int i = 0; i < shaderPropertyCount; i++)
						properties.SetMatrix($"_Projectors{i}", i < matrices.Count ? matrices[i] : dummyMatrix);
				}
				return ret;
			}

			public bool UpdateMaterial()
			{
				var newMaterial = DecalMaterial.GetMaterial(matrices.Count > 4 ? "_FIXED8" :
					(matrices.Count > 1 ? "_FIXED4" : "_FIXEDSINGLE"));
				var ret = newMaterial != material;
				material = newMaterial;
				DecalMaterial.CopyTo(properties);
				return ret;
			}

			public FixedChannel(DecalFixedObject obj, DecalMaterial decal, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decal = decal;
				matrices.Add(matrix);
				RefreshMatrices();
				UpdateMaterial();
			}

			public MeshData GetMeshData()
			{
				return new MeshData
				{
					material = material,
					materialPropertyBlock = properties,
					submesh = submesh,
					matrix = Matrix4x4.identity
				};
			}

			public FixedChannel() { }
		}

		[SerializeField]
		protected List<FixedChannel> instances = new List<FixedChannel>();

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			var matrix = projector.worldToLocalMatrix * transform.localToWorldMatrix;
			var ret = instances.FirstOrDefault(obj => obj.TryAddMatrix(this, decal, submesh, matrix));
			if (ret == null)
			{
				instances.Add(ret = new FixedChannel(this, decal, matrix));
				ClearData();
			}
			if(ret.RefreshMatrices())
				ClearData();
			return ret;
		}

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if((action & (RefreshAction.MaterialPropertiesChanged | RefreshAction.ChangeInstanceMaterial)) != 0)
				foreach (var obj in instances)
					if(obj.UpdateMaterial())
						ClearData();
		}

		protected virtual MeshData[] GetMeshData()
		{
			if (instances.Count == 0 || !enabled)
				return null;
			return instances
				.Where(obj => obj.Enabled)
				.Select(obj =>
			{
				var data = obj.GetMeshData();
				data.mesh = Mesh;
				data.transform = Renderer.transform;
				return data;
			}).ToArray();
		}

		protected override void GetDeferredData(out MeshData[] meshData, out RendererData[] rendererData)
		{
			rendererData = null;
			meshData = GetMeshData();
		}

		protected override void GetForwardData(out MeshData[] meshData, out RendererData[] rendererData)
		{
			rendererData = null;
			meshData = GetMeshData();
		}
	}
}
