using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
//using System.Xml;

using Bitmap = System.Drawing.Bitmap;

namespace Noxico
{
	public static class Mix
	{
		private class MixFileEntry
		{
			public string MixFile, Filename;
			public int Offset, Length;
			public bool IsCompressed;
		}

		private static Dictionary<string, MixFileEntry> fileList;

		private static Dictionary<string, string> stringCache;

		/// <summary>
		/// Populates the Mix database for usage
		/// </summary>
		/// <param name="mainFile">The primary archive's base name.</param>
		public static void Initialize(string mainFile = "Noxico")
		{
			Program.WriteLine("Mix.Initialize()");
			fileList = new Dictionary<string, MixFileEntry>();
			stringCache = new Dictionary<string, string>();
			var mixfiles = new List<string>() { mainFile + ".nox" };
			mixfiles.AddRange(Directory.EnumerateFiles(".", "*.nox").Select(x => x.Substring(2)).Where(x => !x.Equals(mainFile + ".nox", StringComparison.OrdinalIgnoreCase)));
			Program.WriteLine("Mixfiles enumerated. Indexing contents...");
			foreach (var mixfile in mixfiles)
			{
				if (!File.Exists(mixfile))
				{
					Program.WriteLine("Mixfile \"{0}\" in list but nonexistant.", mixfile);
					continue;
				}
				using (var mStream = new BinaryReader(File.Open(mixfile, FileMode.Open)))
				{
					//This is not the "proper" way to do it. Fuck that.
					while (true)
					{
						var header = mStream.ReadBytes(4);
						if (header[0] != 'P' || header[1] != 'K' || header[2] != 3 || header[3] != 4)
						{
							if (header[2] == 1 && header[3] == 2) //reached the Central Directory
								break;
							throw new FileLoadException(string.Format("MIX file '{0}' has an incorrect header.", mixfile));
						}
						mStream.BaseStream.Seek(4, SeekOrigin.Current);
						var method = mStream.ReadInt16();
						mStream.BaseStream.Seek(8, SeekOrigin.Current);
						var moto = mStream.ReadBytes(4);
						var compressedSize = (moto[3] << 24) | (moto[2] << 16) | (moto[1] << 8) | moto[0]; //0x000000F8
						moto = mStream.ReadBytes(4);
						var uncompressedSize = (moto[3] << 24) | (moto[2] << 16) | (moto[1] << 8) | moto[0]; //0x00000197
						moto = mStream.ReadBytes(2);
						var filenameLength = (moto[1] << 8) | moto[0];
						mStream.BaseStream.Seek(2, SeekOrigin.Current);
						var filename = new string(mStream.ReadChars(filenameLength)).Replace('/', '\\');
						var offset = (int)mStream.BaseStream.Position;
						mStream.BaseStream.Seek(compressedSize, SeekOrigin.Current);
						if (filename.EndsWith("\\"))
							continue;
						var entry = new MixFileEntry()
						{
							Offset = offset,
							Length = compressedSize,
							IsCompressed = method == 8,
							Filename = filename,
							MixFile = mixfile,
						};
						fileList[filename] = entry;
					}
				}
			}
		}

		/// <summary>
		/// Checks whether a given file exists, be it in the data folder or in any loaded archives.
		/// </summary>
		/// <param name="fileName">The file to check for.</param>
		/// <returns>True if the file exists.</returns>
		public static bool FileExists(string fileName)
		{
			if (File.Exists(Path.Combine("data", fileName)))
				return true;
			return (fileList.ContainsKey(fileName));
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns a <see cref="MemoryStream"/> for it.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="MemoryStream"/> if found, <see cref="null"/> otherwise.</returns>
		public static Stream GetStream(string fileName)
		{
			Program.WriteLine("Mix.GetStream({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return new MemoryStream(File.ReadAllBytes(Path.Combine("data", fileName)));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			MemoryStream ret;
			var entry = fileList[fileName];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				ret = new MemoryStream(mStream.ReadBytes(entry.Length));
			}
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="string"/>.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="string"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static string GetString(string fileName, bool cache = true)
		{
			if (cache && stringCache.ContainsKey(fileName))
				return stringCache[fileName];
			Program.WriteLine("Mix.GetString({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return File.ReadAllText(Path.Combine("data", fileName));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			var bytes = GetBytes(fileName);
			var ret = Encoding.UTF8.GetString(bytes);
			if (cache)
				stringCache[fileName] = ret;
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a token tree.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <param name="cache">Should the original file contents be cached?</param>
		/// <returns>Returns a token tree representing the file's contents.</returns>
		public static List<Token> GetTokenTree(string fileName, bool cache = false)
		{
			var tml = GetString(fileName, cache);
			var carrier = new TokenCarrier();
			carrier.Tokenize(tml);
			var patches = fileList.Keys.Where(e => e.EndsWith(fileName + ".patch")).ToList();
			if (Directory.Exists("data"))
			{
				var externals = Directory.GetFiles("data", "*.tml.patch", SearchOption.AllDirectories).Select(e => e.Substring(5)).Where(e => e.EndsWith(fileName + ".patch"));
				patches.AddRange(externals);
			}
			if (patches.Count > 0)
			{
				Program.WriteLine("{0} has patches!", fileName);
				foreach (var p in patches)
				{
					var patch = GetString(p);
					carrier.Patch(patch);
				}
			}
			return carrier.Tokens;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="Bitmap"/>.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="Bitmap"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		//TODO: cache the returns.
		public static Bitmap GetBitmap(string fileName)
		{
			Program.WriteLine("Mix.GetBitmap({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return new Bitmap(Path.Combine("data", fileName));
			var raw = GetBytes(fileName);
			using (var str = new MemoryStream(raw))
			{
				return (Bitmap)Bitmap.FromStream(str);
			}
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="byte[]"/>.
		/// </summary>
		/// <param name="fileName">The file to find.</param>
		/// <returns>Returns a <see cref="byte[]"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static byte[] GetBytes(string fileName)
		{
			Program.WriteLine("Mix.GetBytes({0})", fileName);
			if (File.Exists(Path.Combine("data", fileName)))
				return File.ReadAllBytes(Path.Combine("data", fileName));
			if (!fileList.ContainsKey(fileName))
				throw new FileNotFoundException("File " + fileName + " was not found in the MIX files.");
			byte[] ret;
			var entry = fileList[fileName];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				if (!entry.IsCompressed)
				{
					ret = mStream.ReadBytes(entry.Length);
				}
				else
				{
					var cStream = new MemoryStream(mStream.ReadBytes(entry.Length));
					var decompressor = new System.IO.Compression.DeflateStream(cStream, System.IO.Compression.CompressionMode.Decompress);
					var outStream = new MemoryStream();
					var buffer = new byte[1024];
					var recieved = 1;
					while (recieved > 0)
					{
						recieved = decompressor.Read(buffer, 0, buffer.Length);
						outStream.Write(buffer, 0, recieved);
					}
					ret = new byte[outStream.Length];
					outStream.Seek(0, SeekOrigin.Begin);
					outStream.Read(ret, 0, ret.Length);
				}
			}
			return ret;
		}

		/// <summary>
		/// Returns a list of all the files in a given path.
		/// </summary>
		/// <param name="path">The path to look in.</param>
		/// <returns>A <see cref="string[]"/> with all the files found, which may be empty.</returns>
		public static string[] GetFilesInPath(string path)
		{
			var ret = new List<string>();
			foreach (var entry in fileList.Values.Where(x => x.Filename.StartsWith(path)))
				ret.Add(entry.Filename);
			if (Directory.Exists(Path.Combine("data", path)))
			{
				var getFiles = Directory.GetFiles(Path.Combine("data", path), "*", SearchOption.AllDirectories);
				ret.AddRange(getFiles.Select(x => x.Substring("data\\".Length)).Where(x => !ret.Contains(x)));
			}
			return ret.ToArray();
		}
		
		public static void GetFileRange(string fileName, out int offset, out int length, out string mixFile)
		{
			offset = -1;
			length = -1;
			mixFile = string.Empty;
			if (!fileList.ContainsKey(fileName))
				return;
			var entry = fileList[fileName];
			offset = entry.Offset;
			length = entry.Length;
			mixFile = entry.MixFile;
		}

		public static string[] GetFilesWithPattern(string pattern)
		{
			var ret = new List<string>();
			var regex = new System.Text.RegularExpressions.Regex(pattern.Replace("*", "(.*)").Replace("\\", "\\\\"));
			foreach (var entry in fileList.Values.Where(x => regex.IsMatch(x.Filename)))
				ret.Add(entry.Filename);
			if (Directory.Exists("data"))
			{
				if (pattern.Contains('\\'))
				{
					if (!Directory.Exists(Path.Combine("data", Path.GetDirectoryName(pattern))))
						return ret.ToArray();
				}
				var getFiles = Directory.GetFiles("data", pattern, SearchOption.AllDirectories);
				ret.AddRange(getFiles);
			}
			return ret.ToArray();
		}

		public static void SpreadEm()
		{
			Program.WriteLine("Spreadin' em...");
			if (fileList == null || fileList.Count == 0)
				Initialize();
			foreach (var entry in fileList.Values)
			{
				if (entry.Length == 0)
				{
					Program.WriteLine("* Bogus entry with zero length: \"{0}\", offset {1}", entry.Filename, entry.Offset);
					continue;
				}
				var targetPath = Path.Combine("data", /* entry.MixFile.Remove(entry.MixFile.Length - 4),*/ entry.Filename);
				var targetDir = Path.GetDirectoryName(targetPath);
				if (!Directory.Exists(targetDir))
					Directory.CreateDirectory(targetDir);
				using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
				{
					mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
					if (!entry.IsCompressed)
					{
						File.WriteAllBytes(targetPath, mStream.ReadBytes(entry.Length));
					}
					else
					{
						var cStream = new MemoryStream(mStream.ReadBytes(entry.Length));
						var decompressor = new System.IO.Compression.GZipStream(cStream, System.IO.Compression.CompressionMode.Decompress);
						var outStream = new MemoryStream();
						var buffer = new byte[1024];
						var recieved = 1;
						while (recieved > 0)
						{
							recieved = decompressor.Read(buffer, 0, buffer.Length);
							outStream.Write(buffer, 0, recieved);
						}
						var unpacked = new byte[outStream.Length];
						outStream.Seek(0, SeekOrigin.Begin);
						outStream.Read(unpacked, 0, unpacked.Length);
						File.WriteAllBytes(targetPath, unpacked);
					} 
				}
			}
			Program.WriteLine("All entries in mix files extracted. Happy Hacking.");
		}
	}
}