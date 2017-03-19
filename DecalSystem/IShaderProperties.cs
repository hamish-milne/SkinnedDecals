using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalSystem
{
	public interface IShaderProperties
	{
		void Add<T>(string name, T value);
	}

	public class ShaderPropertiesBase
	{
		protected static readonly HashSet<string> globalPropertyNames = new HashSet<string>();
	}

	public class CommandBufferWrapper : ShaderPropertiesBase, IShaderProperties
	{
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
		public CommandBuffer CmdBuf { get; set; }

		public void Add<T>(string name, T value)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(name);
			if (MethodRef<T>.Method == null)
				throw new Exception("Unsupported property type: " + typeof(T));

			// Workaround for SetBuffer not working for command buffers (what?)
			if (CmdBuf != null && (typeof(T) == typeof(ComputeBuffer) || globalPropertyNames.Contains(name)))
			{
				globalPropertyNames.Add(name);
				CmdBuf.SetGlobalBuffer(name, (ComputeBuffer) (object) value);
			}
			else
			{
				if(globalPropertyNames.Contains(name))
					Debug.LogWarning("The shader property " + name + " is being set globally, and will be overridden");
				MethodRef<T>.Method(PropertyBlock, name, value);
			}
		}
	}
}