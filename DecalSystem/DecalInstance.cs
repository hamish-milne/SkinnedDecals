using System;
using UnityEngine;

namespace DecalSystem
{
	/// <summary>
	/// Represents a single decal instance
	/// </summary>
	[Serializable]
	public abstract class DecalInstance
	{
		[SerializeField, UseProperty]
		protected bool enabled = true;
		[SerializeField, UseProperty]
		protected DecalMaterial decalMaterial;

		/// <summary>
		/// Whether to draw this decal or not
		/// </summary>
		public virtual bool Enabled
		{
			get { return enabled; }
			set { enabled = value; }
		}

		/// <summary>
		/// The parent <c>DecalObject</c>
		/// </summary>
		public abstract DecalObject DecalObject { get; }

		/// <summary>
		/// The <c>DecalMaterial</c> to use
		/// </summary>
		public virtual DecalMaterial DecalMaterial
		{
			get { return decalMaterial; }
			set { decalMaterial = value; }
		}
	}

	/// <summary>
	/// A single decal draw command, for one or more decal instances
	/// </summary>
	public interface IDecalDraw
	{
		/// <summary>
		/// If <c>false</c>, this instance will be ignored
		/// </summary>
		bool Enabled { get; }

		/// <summary>
		/// The <c>DecalMaterial</c> being used
		/// </summary>
		DecalMaterial DecalMaterial { get; }

		/// <summary>
		/// Retrieves information necessary to execute the draw command
		/// </summary>
		/// <param name="dcam">The <c>DecalCamera</c> currently rendering</param>
		/// <param name="mesh">A <c>Mesh</c> to be drawn. Defaults to <c>null</c></param>
		/// <param name="renderer">A <c>Renderer</c> to be drawn. Defaults to <c>null</c></param>
		/// <param name="submesh">The submesh index to draw. Defaults to 0</param>
		/// <param name="material">The material to be used for the draw. Cannot be <c>null</c></param>
		/// <param name="matrix">The locak to world matrix of the mesh or renderer. Defaults to <c>Matrix4x4.identity</c></param>
		/// <remarks>
		/// This will be called once for each camera where the object is in view.
		/// <c>Mesh</c> and <c>Renderer</c> cannot both be set to non-null values.
		/// </remarks>
		void GetDrawCommand(DecalCamera dcam, ref Mesh mesh, ref Renderer renderer,
			ref int submesh, ref Material material, ref Matrix4x4 matrix);

		/// <summary>
		/// Sets the necessary shader/material properties using the provided interface
		/// </summary>
		/// <param name="properties"></param>
		void AddShaderProperties(IShaderProperties properties);
	}

	/// <summary>
	/// A common use-case of <c>DecalInstance</c>, where each one is a separate draw call
	/// </summary>
	[Serializable]
	public abstract class DecalInstanceBase : DecalInstance, IDecalDraw
	{
		/// <summary>
		/// The decal-to-object matrix, if any
		/// </summary>
		public abstract Matrix4x4? LocalMatrix { get; }

		/// <summary>
		/// The material mode keyword
		/// </summary>
		public abstract string ModeString { get; }

		/// <summary>
		/// Gets the default matrix from <c>LocalMatrix</c> and the object's transform
		/// </summary>
		/// <returns></returns>
		protected Matrix4x4 DefaultMatrix()
		{
			Matrix4x4 m;
			var transform = DecalObject?.transform;
			var matrix = LocalMatrix;
			if (transform != null && matrix != null)
				m = transform.localToWorldMatrix * matrix.Value;
			else if (transform != null)
				m = transform.localToWorldMatrix;
			else if (matrix != null)
				m = matrix.Value;
			else
				throw new Exception("No matrix or transform defined");
			return m;
		}

		/// <summary>
		/// Sets <c>matrix</c> and <c>material</c>
		/// </summary>
		/// <inheritdoc />
		public virtual void GetDrawCommand(DecalCamera dcam, ref Mesh mesh, ref Renderer renderer,
			ref int submesh, ref Material material, ref Matrix4x4 matrix)
		{
			matrix = DefaultMatrix();
			material = DecalMaterial?.GetMaterial(ModeString);
		}

		public abstract void AddShaderProperties(IShaderProperties properties);
	}
}