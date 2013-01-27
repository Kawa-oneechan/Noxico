using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Drawing;

namespace Noxico
{
	public static class Mix
	{
		private class MixFileEntry
		{
			public string MixFile, Filename;
			public int Offset, Length;
			public bool NeedsPremultiply;
			public bool IsCompressed;
		}

		private static Dictionary<string, MixFileEntry> fileList;

		/// <summary>
		/// Populates the Mix database for usage
		/// </summary>
		public static void Initialize(string mainFile = "main")
		{
			Console.WriteLine("Mix.Initialize()");
			fileList = new Dictionary<string, MixFileEntry>();
			var mixfiles = new List<string>() { mainFile + ".mix" };
			mixfiles.AddRange(Directory.EnumerateFiles(".", "*.mix").Select(x => x.Substring(2)).Where(x => !x.Equals(mainFile + ".mix", StringComparison.InvariantCultureIgnoreCase)));
			Console.WriteLine("Mixfiles enumerated. Indexing contents...");
			foreach (var mixfile in mixfiles)
			{
				if (!File.Exists(mixfile))
				{
					Console.WriteLine("Mixfile \"{0}\" in list but nonexistant.", mixfile);
					continue;
				}
				using (var mStream = new BinaryReader(File.Open(mixfile, FileMode.Open)))
				{
					var header = mStream.ReadChars(8);
					var count = mStream.ReadInt32();
					var dummy = mStream.ReadInt32();
					for (var i = 0; i < count; i++)
					{
						var entry = new MixFileEntry();
						entry.Offset = mStream.ReadInt32();
						entry.Length = mStream.ReadInt32();
						var flags = mStream.ReadByte();
						entry.NeedsPremultiply = ((flags & 1) == 1);
						entry.IsCompressed = ((flags & 2) == 2);
						var fileNameC = mStream.ReadChars(55);
						var fileName = new string(fileNameC).Replace("\0", "");
						entry.MixFile = mixfile;
						entry.Filename = fileName;
						if (fileList.ContainsKey(fileName))
							fileList[fileName] = entry;
						else
							fileList.Add(fileName, entry);
					}
				}
			}
		}

		public static bool FileExists(string filename)
		{
			if (File.Exists(Path.Combine("data", filename)))
				return true;
			return (fileList.ContainsKey(filename));
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns a <see cref="MemoryStream"/> for it.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="MemoryStream"/> if found, <see cref="null"/> otherwise.</returns>
		public static Stream GetStream(string filename)
		{
			Console.WriteLine("Mix.GetStream({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return new MemoryStream(File.ReadAllBytes(Path.Combine("data", filename)));
			if (!fileList.ContainsKey(filename))
				throw new FileNotFoundException("File " + filename + " was not found in the MIX files.");
			MemoryStream ret;
			var entry = fileList[filename];
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
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="string"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static string GetString(string filename)
		{
			Console.WriteLine("Mix.GetString({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return File.ReadAllText(Path.Combine("data", filename));
			if (!fileList.ContainsKey(filename))
				throw new FileNotFoundException("File " + filename + " was not found in the MIX files.");
			var bytes = GetBytes(filename);
			var ret = Encoding.UTF8.GetString(bytes);
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as an <see cref="XMLDocument"/>, merging in the nodes from same-named files in subdirectories to allow mod expansions.
		/// </summary>
		/// <param name="filename">The file to find. If this is in the root of the Mix system, mod expansions are merged in.</param>
		/// <returns>Returns an <see cref="XMLDocument"/> with the file(s)'s contents if found, <see cref="null"/> otherwise.</returns>
		public static XmlDocument GetXMLDocument(string filename, bool injectOnTop = false)
		{
			var x = new System.Xml.XmlDocument();
			if (filename.Contains('\\'))
			{
				var s = GetString(filename);
				if (s == null)
					throw new FileNotFoundException("File " + filename + " was not found in the MIX files.");
				x.LoadXml(s);
				return x;
			}

			var otherFiles = fileList.Keys.Where(e => e != filename && e.EndsWith(filename)).ToList();
			if (Directory.Exists("data"))
			{
				var externalOtherFiles = Directory.GetFiles("data", "*.xml", SearchOption.AllDirectories).Select(e => e.Substring(5)).Where(e => e != filename && e.EndsWith(filename));
				otherFiles.AddRange(externalOtherFiles);
			}
			if (otherFiles.Count() == 0)
			{
				x.LoadXml(GetString(filename));
				return x;
			}

			Console.WriteLine("{0} has mod expansions!", filename);
			x.LoadXml(GetString(filename));
			foreach (var f in otherFiles)
			{
				Console.WriteLine("Splicing in {0}...", f);
				var otherX = new System.Xml.XmlDocument();
				otherX.LoadXml(GetString(f));
				x.DocumentElement.InnerXml = otherX.DocumentElement.InnerXml + x.DocumentElement.InnerXml;
				//foreach (var e in otherX.DocumentElement.ChildNodes.OfType<XmlNode>())
				//	x.DocumentElement.InnerXml = (injectOnTop ? e.OuterXml + x.DocumentElement.InnerXml : x.DocumentElement.InnerXml + e.OuterXml);
			}

			//Prioritize nodes
			var priorityNodes = x.SelectNodes("//*[@priority='high']");
			if (priorityNodes.Count > 0)
			{
				var placeholder = x.CreateComment("Priority placeholder");
				x.DocumentElement.InsertBefore(placeholder, x.DocumentElement.FirstChild);
				foreach (var n in priorityNodes.OfType<XmlElement>())
				{
					var p = n.ParentNode;
					p.RemoveChild(n);
					p.InsertBefore(n, placeholder);
				}
			}

			return x;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="Bitmap"/>.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="Bitmap"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		//TODO: cache the returns.
		public static Bitmap GetBitmap(string filename)
		{
			Console.WriteLine("Mix.GetBytes({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return new Bitmap(Path.Combine("data", filename));
			if (!fileList.ContainsKey(filename))
				throw new FileNotFoundException("File " + filename + " was not found in the MIX files.");
			Bitmap ret;
			var entry = fileList[filename];
			using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
			{
				mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
				using (var str = new MemoryStream(mStream.ReadBytes(entry.Length)))
				{
					ret = (Bitmap)Bitmap.FromStream(str);
				}
			}
			return ret;
		}

		/// <summary>
		/// Looks up the given file in the Mix database and returns its contents as a <see cref="byte[]"/>.
		/// </summary>
		/// <param name="filename">The file to find.</param>
		/// <returns>Returns a <see cref="byte[]"/> with the file's contents if found, <see cref="null"/> otherwise.</returns>
		public static byte[] GetBytes(string filename)
		{
			Console.WriteLine("Mix.GetBytes({0})", filename);
			if (File.Exists(Path.Combine("data", filename)))
				return File.ReadAllBytes(Path.Combine("data", filename));
			if (!fileList.ContainsKey(filename))
				throw new FileNotFoundException("File " + filename + " was not found in the MIX files.");
			byte[] ret;
			var entry = fileList[filename];
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
					var decompressor = new System.IO.Compression.GZipStream(cStream, System.IO.Compression.CompressionMode.Decompress);
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
		
		public static void GetFileRange(string filename, out int offset, out int length, out string mixFile)
		{
			offset = -1;
			length = -1;
			mixFile = string.Empty;
			if (!fileList.ContainsKey(filename))
				return;
			var entry = fileList[filename];
			offset = entry.Offset;
			length = entry.Length;
			mixFile = entry.MixFile;
		}

		public static void SpreadEm()
		{
			Console.WriteLine("Spreadin' em...");
			if (fileList == null || fileList.Count == 0)
				Initialize();
			foreach (var entry in fileList.Values)
			{
				if (entry.Length == 0)
				{
					Console.WriteLine("* Bogus entry with zero length: \"{0}\", offset {1}", entry.Filename, entry.Offset);
					continue;
				}
				var targetPath = Path.Combine("data", entry.MixFile.Remove(entry.MixFile.Length - 4), entry.Filename);
				var targetDir = Path.GetDirectoryName(targetPath);
				if (!Directory.Exists(targetDir))
					Directory.CreateDirectory(targetDir);
				using (var mStream = new BinaryReader(File.Open(entry.MixFile, FileMode.Open)))
				{
					mStream.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
					File.WriteAllBytes(targetPath, mStream.ReadBytes(entry.Length));
				}
			}
			Console.WriteLine("All entries in mix files extracted. Happy Hacking.");
		}
	}
}