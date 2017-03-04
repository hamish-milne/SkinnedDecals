using System;
using System.Collections.Generic;
using UnityEngine;

namespace DecalSystem
{
	[RendererType(typeof(Terrain))]
	public class DecalScreenSpaceObject : DecalObjectBase
	{
		private static readonly string[] modes = {ShaderKeywords.ScreenSpace};

		public override string[] RequiredModes => modes;

		[Serializable]
		protected class ScreenSpaceInstance : DecalInstanceBase
		{
			[NonSerialized]
			public DecalScreenSpaceObject obj;
			public Matrix4x4 matrix;

			public override DecalObject DecalObject => obj;

			public override Matrix4x4? LocalMatrix => matrix;

			public override string ModeString => ShaderKeywords.ScreenSpace;

			public override void GetDrawCommand(RenderingPath renderPath, ref Mesh mesh,
				ref Renderer renderer, ref int submesh, ref Material material,
				ref MaterialPropertyBlock propertyBlock, ref Matrix4x4 matrix)
			{
				base.GetDrawCommand(renderPath, ref mesh, ref renderer, ref submesh,
					ref material, ref propertyBlock, ref matrix);
				mesh = CubeMesh;
			}

			public ScreenSpaceInstance(DecalScreenSpaceObject obj, DecalMaterial decalMaterial, Matrix4x4 matrix)
			{
				this.obj = obj;
				this.decalMaterial = decalMaterial;
				this.matrix = matrix;
			}
		}

		[SerializeField]
		protected List<ScreenSpaceInstance> instances = new List<ScreenSpaceInstance>();

		[SerializeField]
		protected Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

		public override bool ScreenSpace => true;

		public override Bounds? Bounds => bounds;

		public override Renderer Renderer => null;

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			// Multiply projector matrix by local transform; This keeps the decal local to the space of the object
			var ret = new ScreenSpaceInstance(this, decal,
				transform.worldToLocalMatrix * projector.localToWorldMatrix);
			instances.Add(ret);
			return ret;
		}

		public override int Count => instances.Count;

		public override DecalInstance GetDecal(int index)
		{
			var ret = instances[index];
			ret.obj = this;
			return ret;
		}

		public override bool RemoveDecal(DecalInstance instance)
		{
			return instances.Remove((ScreenSpaceInstance) instance);
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

		public override IEnumerable<IDecalDraw> GetDrawsUncached()
		{
			foreach (var o in instances)
				o.obj = this;
			return base.GetDrawsUncached();
		}
	}
}
