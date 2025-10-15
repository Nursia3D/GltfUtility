using System;
using System.IO;
using System.Reflection;
using DigitalRise;
using GltfUtility;

namespace EffectFarm
{
	class Program
	{
		// See system error codes: http://msdn.microsoft.com/en-us/library/windows/desktop/ms681382.aspx
		private const int ERROR_SUCCESS = 0;
		private const int ERROR_BAD_ARGUMENTS = 160;        // 0x0A0
		private const int ERROR_UNHANDLED_EXCEPTION = 574;  // 0x23E

		public static string Version
		{
			get
			{
				var assembly = typeof(Program).Assembly;
				var name = new AssemblyName(assembly.FullName);

				return name.Version.ToString();
			}
		}

		static void Log(string message)
		{
			Console.WriteLine(message);
		}

		static void ShowUsage()
		{
			Console.WriteLine($"Nursia GltfUtility {Utility.Version}");
			Console.WriteLine("Usage: nrs-gltf <inputFile> <outputFile> [-t] [-u]");
			Console.WriteLine("-t Generate tangent frames");
			Console.WriteLine("-u Unwind indices");
		}

		static int Process(string[] args)
		{
			var options = new Options();
			for (var i = 0; i < args.Length; ++i)
			{
				var a = args[i];
				if (a[0] == '-')
				{
					if (a.Length == 1)
					{
						Console.WriteLine("Invalid option -");
						ShowUsage();
						return ERROR_BAD_ARGUMENTS;
					}

					// Option
					switch (a[1])
					{
						case 't':
							options.Tangent = true;
							break;

						case 'u':
							options.Unwind = true;
							break;
					}
				}
				else
				{
					if (string.IsNullOrEmpty(options.InputFile))
					{
						options.InputFile = a;
					}
					else
					{
						options.OutputFile = a;
					}
				}
			}

			if (string.IsNullOrEmpty(options.InputFile))
			{
				Console.WriteLine("Input file isn't set");
				ShowUsage();
				return ERROR_BAD_ARGUMENTS;
			}

			if (!File.Exists(options.InputFile))
			{
				Console.WriteLine($"Input file {options.InputFile} doesn't exist");
				ShowUsage();
				return ERROR_BAD_ARGUMENTS;
			}

			if (string.IsNullOrEmpty(options.OutputFile))
			{
				Console.WriteLine("Output file isn't set");
				ShowUsage();
				return ERROR_BAD_ARGUMENTS;
			}

			var ext = Path.GetExtension(options.OutputFile).ToLower();
			if (ext != ".gltf" && ext != ".glb")
			{
				Console.WriteLine("Output file extension should be either gltf or glb");
				ShowUsage();
				return ERROR_BAD_ARGUMENTS;
			}

			var processor = new GltfProcessor();
			processor.Process(options);

			return ERROR_SUCCESS;
		}

		static int Main(string[] args)
		{
			try
			{
				return Process(args);
			}
			catch (Exception ex)
			{
				Log(ex.ToString());
				return ERROR_UNHANDLED_EXCEPTION;
			}
		}
	}
}