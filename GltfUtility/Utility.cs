using glTFLoader.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using static glTFLoader.Schema.Accessor;

namespace DigitalRise
{
	internal static class Utility
	{
		public static string Version
		{
			get
			{
				var assembly = typeof(Utility).Assembly;
				var name = new AssemblyName(assembly.FullName);

				return name.Version.ToString();
			}
		}

		private static readonly int[] ComponentsCount = new[]
		{
			1,
			2,
			3,
			4,
			4,
			9,
			16
		};

		private static readonly int[] ComponentSizes = new[]
		{
			sizeof(sbyte),
			sizeof(byte),
			sizeof(short),
			sizeof(ushort),
			0,	// There's no such component
			sizeof(uint),
			sizeof(float)
		};

		public static int GetComponentCount(this TypeEnum type) => ComponentsCount[(int)type];
		public static int GetComponentSize(this ComponentTypeEnum type) => ComponentSizes[(int)type - 5120];

		public static bool HasAttribute(this MeshPrimitive primitive, string prefix)
		{
			return (from p in primitive.Attributes.Keys where p.StartsWith(prefix) select p).FirstOrDefault() != null;
		}

		public static int FindAttribute(this MeshPrimitive primitive, string prefix)
		{
			var key = (from p in primitive.Attributes.Keys where p.StartsWith(prefix) select p).FirstOrDefault();
			if (string.IsNullOrEmpty(key))
			{
				throw new Exception($"Couldn't find mandatory primitive attribute {prefix}.");
			}

			return primitive.Attributes[key];
		}

		public static byte[] ToBytes(this Stream input)
		{
			var ms = new MemoryStream();
			input.CopyTo(ms);

			return ms.ToArray();
		}

		private unsafe static int WriteData<T>(this Stream output, List<BufferView> bufferViews, List<Accessor> accessors, 
			void* ptr, int count, TypeEnum type)
		{
			var pos = (int)output.Position;

			// Write data to the binary buffer
			var bytes = new byte[count * Marshal.SizeOf(typeof(T))];
			Marshal.Copy(new IntPtr(ptr), bytes, 0, bytes.Length);

			output.Write(bytes);

			// Create new buffer view
			bufferViews.Add(new BufferView { ByteOffset = pos, ByteLength = bytes.Length });

			// Create new accessor
			accessors.Add(new Accessor
			{
				ComponentType = ComponentTypeEnum.FLOAT,
				Type = type,
				Count = count,
				BufferView = bufferViews.Count - 1,
			});

			return accessors.Count - 1;
		}

		public unsafe static int WriteData(this Stream output, List<BufferView> bufferViews, List<Accessor> accessors, Vector2[] data)
		{
			fixed (void* ptr = data)
			{
				return output.WriteData<Vector2>(bufferViews, accessors, ptr, data.Length, TypeEnum.VEC2);
			}
		}

		public unsafe static int WriteData(this Stream output, List<BufferView> bufferViews, List<Accessor> accessors, Vector3[] data)
		{
			fixed (void* ptr = data)
			{
				return output.WriteData<Vector3>(bufferViews, accessors, ptr, data.Length, TypeEnum.VEC3);
			}
		}

		public unsafe static int WriteData(this Stream output, List<BufferView> bufferViews, List<Accessor> accessors, Vector4[] data)
		{
			fixed (void* ptr = data)
			{
				return output.WriteData<Vector4>(bufferViews, accessors, ptr, data.Length, TypeEnum.VEC4);
			}
		}
	}
}