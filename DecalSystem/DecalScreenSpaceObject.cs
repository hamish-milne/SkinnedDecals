using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	public class DecalScreenSpaceObject : DecalObjectBase
	{
		private static readonly string[] modes = {"_SCREENSPACE"};

		public override string[] RequiredModes => modes;

		[Serializable]
		protected class Instance : DecalInstance
		{
			[SerializeField] protected DecalScreenSpaceObject obj;
			public Matrix4x4 matrix;

			public override DecalObject DecalObject => obj;

			public Instance(DecalScreenSpaceObject obj, DecalMaterial decal, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decal = decal;
				this.matrix = matrix;
			}
		}

		[SerializeField]
		protected List<Instance> instances = new List<Instance>();

		[SerializeField]
		protected Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

		public override bool ScreenSpace => true;

		public override Bounds Bounds => bounds;

		public override Renderer Renderer => null;

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			var ret = new Instance(this, decal, projector.localToWorldMatrix);
			instances.Add(ret);
			ClearData();
			return ret;
		}

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if ((action & (RefreshAction.ChangeInstanceMaterial
				| RefreshAction.MaterialPropertiesChanged)) != 0)
				ClearData();
		}

		public override Mesh Mesh => DecalManager.Current.CubeMesh;

		protected override bool RequireDepthTexture => true;

		protected virtual MeshData[] GetMeshData()
		{
			var cube = DecalManager.Current.CubeMesh;
			return instances
				.Where(obj => obj.Enabled)
				.Select(obj => new MeshData
			{
				material = obj.DecalMaterial.GetMaterial("_SCREENSPACE"),
				matrix = obj.matrix,
				mesh = cube
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
