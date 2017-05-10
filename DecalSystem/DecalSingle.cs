using System;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// A single screen-space decal instance
	/// </summary>
	public class DecalSingle : DecalObject
	{
		[SerializeField] protected DecalMaterial decalMaterial;

		/// <summary>
		/// The material to use
		/// </summary>
		public virtual DecalMaterial DecalMaterial
		{
			get => decalMaterial;
			set => decalMaterial = value;
		}

		public override bool ScreenSpace => true;
		public override Renderer Renderer => null;
		public override Mesh Mesh => DecalScreenSpaceObject.CubeMesh;
		public override Bounds Bounds => Util.UnitBounds(transform);
		public override Material[] Materials => null;
		public override bool CanAddDecals => false;
		public override bool ManualCulling => true;

		// As we only have one instance to draw, we can re-use the instance and array here
		private readonly IDecalDraw[] drawArray = new IDecalDraw[1];
		private readonly DecalInstanceSingle single;

		public DecalSingle()
		{
			single = new DecalInstanceSingle(this);
			drawArray[0] = single;
		}

		public override int Count => 1;
		public override DecalInstance GetDecal(int index)
		{
			if(index < 0 || index >= 1)
				throw new ArgumentNullException(nameof(index));
			return single;
		}

		public override bool RemoveDecal(DecalInstance instance)
		{
			throw new NotSupportedException();
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh, float maxNormal)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Draws the outline box to make it easier to position in the editor
		/// </summary>
		protected virtual void OnDrawGizmosSelected()
		{
			DecalProjector.DrawGizmo(transform);
		}

		/// <summary>
		/// The decal type for DecalSingle, bound to its parent object
		/// </summary>
		protected class DecalInstanceSingle : DecalInstanceBase
		{
			private readonly DecalSingle obj;

			public override DecalObject DecalObject => obj;

			public override Matrix4x4? LocalMatrix => null;

			public override string ModeString => ShaderKeywords.ScreenSpace;

			public override DecalMaterial DecalMaterial
			{
				get => obj.decalMaterial;
				set => obj.decalMaterial = value;
			}

			public override void GetDrawCommand(DecalCamera dcam,
				ref Mesh mesh, ref Renderer renderer, ref int submesh,
				ref Material material, ref Matrix4x4 matrix)
			{
				base.GetDrawCommand(dcam, ref mesh, ref renderer, ref submesh, ref material, ref matrix);
				mesh = DecalScreenSpaceObject.CubeMesh;
			}

			public override void AddShaderProperties(IShaderProperties properties)
			{
				// None
			}

			public override bool Enabled
			{
				get => obj.enabled;
				set => obj.enabled = value;
			}

			public DecalInstanceSingle(DecalSingle obj)
			{
				this.obj = obj;
			}
		}

		public override void ClearData()
		{
			// None
		}

		public override IDecalDraw[] GetDecalDraws()
		{
			return drawArray;
		}

		public override void UpdateBackRefs()
		{
			// None
		}
	}
}
