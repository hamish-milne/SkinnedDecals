using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DecalSystem
{
	[RendererType(typeof(Terrain))]
	public class DecalScreenSpaceObject : DecalObjectBase
	{
		private static readonly string[] modes = {ShaderKeywords.ScreenSpace};

		public override string[] RequiredModes => modes;

		[Serializable]
		protected class Instance : DecalInstance
		{
			[SerializeField, HideInInspector] protected DecalScreenSpaceObject obj;
			public Matrix4x4 matrix;

			public override DecalObject DecalObject => obj;

			public Instance(DecalScreenSpaceObject obj, DecalMaterial decal, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decalMaterial = decal;
				this.matrix = matrix;
			}
		}

		[SerializeField]
		protected List<Instance> instances = new List<Instance>();

		[SerializeField]
		protected Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

		public override bool ScreenSpace => true;

		public override Bounds? Bounds => bounds;

		public override Renderer Renderer => null;

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			// Multiply projector matrix by local transform; exact one used doesn't matter because we reverse this transformation
			// when building the command buffers. This keeps the decal local to the space of the object
			var ret = new Instance(this, decal, projector.localToWorldMatrix * transform.worldToLocalMatrix);
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

		private static Mesh cubeMesh;

		public static Mesh CubeMesh
		{
			get
			{
				if (cubeMesh != null) return cubeMesh;
				cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
				if(cubeMesh == null)
					throw new Exception("Cube mesh not found!");
				return cubeMesh;
			}
		}

		public override Mesh Mesh => CubeMesh;

		private void Cleanup()
		{
			instances.RemoveAll(obj => obj.DecalMaterial == null);
		}

		protected virtual MeshData[] GetMeshData()
		{
			Cleanup();
			return instances
				.Where(obj => obj.Enabled)
				.Select(obj => new MeshData
			{
				instance = obj,
				material = obj.DecalMaterial.GetMaterial(ShaderKeywords.ScreenSpace),
				matrix = obj.matrix * transform.localToWorldMatrix, // Reverse transformation in AddDecal
				mesh = Mesh
			}).ToArray();
		}


		// TODO: Merge these somehow?
		protected override MeshData[] GetDeferredData()
		{
			return GetMeshData();
		}

		protected override MeshData[] GetForwardData()
		{
			return GetMeshData();
		}

		private Vector3 lastPosition;
		private Quaternion lastRotation;
		private Vector3 lastScale;
		public override MeshData[] GetRenderPathData(RenderingPath path)
		{
			if (lastPosition != transform.position || lastRotation != transform.rotation || lastScale != transform.lossyScale)
			{
				ClearData();
				lastPosition = transform.position;
				lastRotation = transform.rotation;
				lastScale = transform.lossyScale;
			}
			return base.GetRenderPathData(path);
		}
	}
}
