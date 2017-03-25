using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalSystem
{
	/// <summary>
	/// Provides a consistent interface for setting material override properties
	/// </summary>
	public interface IShaderProperties
	{
		/// <summary>
		/// Sets a material property to the given value
		/// </summary>
		/// <typeparam name="T">The property type</typeparam>
		/// <param name="name"></param>
		/// <param name="value"></param>
		void Add<T>(string name, T value);
	}

	public class ShaderPropertiesBase
	{
		/// <summary>
		/// Property names here have been set globally, and will therefore override all other values
		/// </summary>
		protected static readonly HashSet<string> globalPropertyNames = new HashSet<string>();
	}

	/// <summary>
	/// Shader properties wrapper for CommandBuffers. Uses the 'SetGlobal' functions
	/// </summary>
	public class CommandBufferWrapper : ShaderPropertiesBase, IShaderProperties
	{
		// Map types to methods
		private static readonly Dictionary<Type, string> methodNames
		= new Dictionary<Type, string>
		{
			{ typeof(Color), nameof(CommandBuffer.SetGlobalColor) },
			{ typeof(ComputeBuffer), nameof(CommandBuffer.SetGlobalBuffer) },
			{ typeof(float), nameof(CommandBuffer.SetGlobalFloat) },
			{ typeof(float[]), nameof(CommandBuffer.SetGlobalFloatArray) },
			{ typeof(List<float>), nameof(CommandBuffer.SetGlobalFloatArray) },
			{ typeof(Matrix4x4), nameof(CommandBuffer.SetGlobalMatrix) },
			{ typeof(Matrix4x4[]), nameof(CommandBuffer.SetGlobalMatrixArray) },
			{ typeof(List<Matrix4x4>), nameof(CommandBuffer.SetGlobalMatrixArray) },
			{ typeof(Texture), nameof(CommandBuffer.SetGlobalTexture) },
			{ typeof(Vector4), nameof(CommandBuffer.SetGlobalVector) },
			{ typeof(Vector4[]), nameof(CommandBuffer.SetGlobalVectorArray) },
			{ typeof(List<Vector4>), nameof(CommandBuffer.SetGlobalVectorArray) },
		};

		// Cache the setter method as a delegate
		private static class MethodRef<T>
		{
			public delegate void SetMethod(CommandBuffer buf, string name, T value);
			private static readonly Type[] arguments = {typeof(string), typeof(T)};
			public static readonly SetMethod Method;

			static MethodRef()
			{
				if (methodNames.TryGetValue(typeof(T), out string methodName))
					Method = (SetMethod)Delegate.CreateDelegate(typeof(SetMethod),
						typeof(CommandBuffer).GetMethod(methodName, arguments));
			}
		}

		/// <summary>
		/// The command buffer to add to.
		/// </summary>
		public CommandBuffer CmdBuf { get; set; }

		public void Add<T>(string name, T value)
		{
			if(string.IsNullOrEmpty(name)) throw new ArgumentNullException(name);
			if(MethodRef<T>.Method == null)
				throw new Exception("Unsupported property type: " + typeof(T));
			globalPropertyNames.Add(name);
			MethodRef<T>.Method(CmdBuf, name, value);
		}
	}

	/// <summary>
	/// Shader properties wrapper for a MaterialPropertyBlock
	/// </summary>
	public class PropertyBlockWrapper : ShaderPropertiesBase, IShaderProperties
	{
		private static readonly Dictionary<Type, string> methodNames
		= new Dictionary<Type, string>
		{
			{ typeof(Color), nameof(MaterialPropertyBlock.SetColor) },
			{ typeof(ComputeBuffer), nameof(MaterialPropertyBlock.SetBuffer) },
			{ typeof(float), nameof(MaterialPropertyBlock.SetFloat) },
			{ typeof(float[]), nameof(MaterialPropertyBlock.SetFloatArray) },
			{ typeof(List<float>), nameof(MaterialPropertyBlock.SetFloatArray) },
			{ typeof(Matrix4x4), nameof(MaterialPropertyBlock.SetMatrix) },
			{ typeof(Matrix4x4[]), nameof(MaterialPropertyBlock.SetMatrixArray) },
			{ typeof(List<Matrix4x4>), nameof(MaterialPropertyBlock.SetMatrixArray) },
			{ typeof(Texture), nameof(MaterialPropertyBlock.SetTexture) },
			{ typeof(Vector4), nameof(MaterialPropertyBlock.SetVector) },
			{ typeof(Vector4[]), nameof(MaterialPropertyBlock.SetVectorArray) },
			{ typeof(List<Vector4>), nameof(MaterialPropertyBlock.SetVectorArray) },
		};

		private static class MethodRef<T>
		{
			public delegate void SetMethod(MaterialPropertyBlock block, string name, T value);
			private static readonly Type[] arguments = { typeof(string), typeof(T) };
			public static readonly SetMethod Method;

			static MethodRef()
			{
				if (methodNames.TryGetValue(typeof(T), out string methodName))
					Method = (SetMethod)Delegate.CreateDelegate(typeof(SetMethod),
						typeof(MaterialPropertyBlock).GetMethod(methodName, arguments));
			}
		}

		public MaterialPropertyBlock PropertyBlock { get; set; }
		public IShaderProperties CmdBuf { get; set; }

		public void Add<T>(string name, T value)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(name);
			if (MethodRef<T>.Method == null)
				throw new Exception("Unsupported property type: " + typeof(T));

			// Any property set globally once needs to always be done the same way, or it'll just get ignored
			// When Unity introduces a 'clear global' function, we can change this
			// Also, *all* ComputeBuffer values need to be set globally if a CommandBuffer is being used - MaterialPropertyBlock.SetBuffer doesn't work
			if (CmdBuf != null &&
				CmdBuf != this && // Should never happen, but just in case to prevent recursion
				(typeof(T) == typeof(ComputeBuffer) || globalPropertyNames.Contains(name)))
			{
				globalPropertyNames.Add(name);
				CmdBuf.Add(name, value);
			}
			else
			{
				if(globalPropertyNames.Contains(name))
					Debug.LogWarning("The shader property " + name + " is being set globally, and will probably be overridden");
				MethodRef<T>.Method(PropertyBlock, name, value);
			}
		}
	}
}